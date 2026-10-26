using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public enum SkillCastStatus
    {
        Success,
        InvalidOwner,
        InvalidDamageSource,
        InsufficientMana,
        SpawnFailed,
        ActionBlocked,
        InvalidSource,
        Cooldown,
        InvalidAim
    }

    public readonly struct SkillCastResult
    {
        public SkillCastStatus Status { get; }

        readonly IReadOnlyList<ProjectileController> _projectiles;
        public IReadOnlyList<ProjectileController> Projectiles => _projectiles ?? Array.Empty<ProjectileController>();
        public ProjectileController Projectile => Projectiles.Count > 0 ? Projectiles[0] : null;

        public bool IsSuccess => Status == SkillCastStatus.Success;

        public SkillCastResult(SkillCastStatus status, ProjectileController projectile = null)
        {
            Status = status;
            _projectiles = projectile != null ? Array.AsReadOnly(new[] { projectile }) : Array.Empty<ProjectileController>();
        }

        internal SkillCastResult(ProjectileController[] projectiles)
        {
            Status = SkillCastStatus.Success;
            _projectiles = Array.AsReadOnly(projectiles);
        }
    }

    public class FireProjectileCommand : AbstractCommand<SkillCastResult>
    {
        readonly CombatActor _owner;
        readonly ProjectileSkillDefinition _skill;
        readonly Vector3 _position;
        readonly Vector2 _direction;
        IReadOnlyList<ModifierInstance> _localModifiers;
        string _flameId;
        Action _commitCooldown;
        Func<bool> _validateSource;

        internal static SkillCastResult Perform(IArchitecture architecture, CombatActor owner, ProjectileSkillDefinition skill,
            Vector3 position, Vector2 direction, IReadOnlyList<ModifierInstance> modifiers, string flameId,
            Action commitCooldown, Func<bool> validateSource)
        {
            var command = new FireProjectileCommand(owner, skill, position, direction)
            { _localModifiers = modifiers, _flameId = flameId, _commitCooldown = commitCooldown, _validateSource = validateSource };
            return architecture.SendCommand(command);
        }

        public FireProjectileCommand(CombatActor owner, ProjectileSkillDefinition skill, Vector3 position, Vector2 direction)
        {
            _owner = owner;
            _skill = skill;
            _position = position;
            _direction = direction;
        }

        protected override SkillCastResult OnExecute()
        {
            if (_owner == null || !_owner.IsAlive || _skill == null)
            {
                return new SkillCastResult(SkillCastStatus.InvalidOwner);
            }

            if (_commitCooldown == null && _owner.GetComponent<PlayerController>() != null)
            {
                PlayerSkillStateRegistry skills = this.GetSystem<PlayerSkillStateRegistry>();
                PlayerSkillState normal = skills.Capture(_owner);
                return normal.Skill == _skill ? skills.Cast(_owner, -1, normal.SourceId, _position, _direction)
                    : new SkillCastResult(SkillCastStatus.InvalidSource);
            }

            EquipmentModel equipment = this.GetModel<EquipmentModel>();

            if (!AttackSnapshotFactory.CanCreateProjectile(_owner, _skill, equipment))
            {
                return Reject(
                    SkillCastStatus.InvalidDamageSource,
                    SkillCastRejectionReason.InvalidDamageSource);
            }

            ActorActionPermission permission = this.SendQuery(new GetActorActionsQuery(_owner));
            if (!permission.CanCast || _skill.DamageSource == ProjectileDamageSource.EquippedWeapon && !permission.CanAttack)
            {
                return Reject(SkillCastStatus.ActionBlocked, SkillCastRejectionReason.ActionBlocked);
            }

            if (!_owner.CanSpendMana(_skill.ManaCost))
            {
                return Reject(
                    SkillCastStatus.InsufficientMana,
                    SkillCastRejectionReason.InsufficientMana);
            }

            int seed = this.GetSystem<GameplayRandomSystem>().NextSeed(GameplayRandomChannel.PlayerAttack);
            var attacks = new AttackSnapshot[_skill.ProjectileCount];
            for (int i = 0; i < attacks.Length; i++)
            {
                attacks[i] = AttackSnapshotFactory.CreateProjectile(_owner, _skill, equipment,
                    AttackRandomRolls.ProjectileSeed(seed, i), _localModifiers, _flameId);
                if (attacks[i] == null)
                {
                    return Reject(SkillCastStatus.InvalidDamageSource, SkillCastRejectionReason.InvalidDamageSource);
                }
            }

            SpawnSystem spawner = this.GetSystem<SpawnSystem>();
            object mapOwner = this.GetUtility<SessionObjectRegistry>().MapOwner;
            ProjectileController[] projectiles = spawner.PrepareProjectiles(_skill, _position, _direction, attacks);

            if (projectiles != null)
            {
                if (_owner == null || !_owner.IsAlive || !_owner.isActiveAndEnabled
                    || !ReferenceEquals(mapOwner, this.GetUtility<SessionObjectRegistry>().MapOwner)
                    || !(_validateSource?.Invoke() ?? true))
                {
                    spawner.ReleaseProjectiles(projectiles);
                    return new SkillCastResult(SkillCastStatus.InvalidSource);
                }
                permission = this.SendQuery(new GetActorActionsQuery(_owner));
                if (!permission.CanCast || _skill.DamageSource == ProjectileDamageSource.EquippedWeapon && !permission.CanAttack)
                {
                    spawner.ReleaseProjectiles(projectiles);
                    return Reject(SkillCastStatus.ActionBlocked, SkillCastRejectionReason.ActionBlocked);
                }
                CombatResourceSnapshot before = _owner.Resources;
                if (!_owner.TrySpendMana(_skill.ManaCost))
                {
                    spawner.ReleaseProjectiles(projectiles);
                    return Reject(
                        SkillCastStatus.InsufficientMana,
                        SkillCastRejectionReason.InsufficientMana);
                }
                _commitCooldown?.Invoke();
                foreach (ProjectileController projectile in projectiles) { projectile.Launch(); }
                try { this.GetSystem<CombatSystem>().PublishResourceChanges(_owner, before, ActorResourceChangeReason.SkillCost); }
                finally
                {
                    try { this.SendEvent(new ActorAttackedEvent { Actor = _owner }); }
                    finally
                    {
                        if (_commitCooldown != null)
                        {
                            try { this.SendEvent(new PlayerSkillsChangedEvent()); }
                            finally { this.SendEvent(new PlayerSkillCastEvent(_owner, Array.AsReadOnly(attacks))); }
                        }
                    }
                }
                return new SkillCastResult(projectiles);
            }

            return Reject(SkillCastStatus.SpawnFailed, SkillCastRejectionReason.SpawnFailed);
        }

        SkillCastResult Reject(SkillCastStatus status, SkillCastRejectionReason reason)
        {
            this.SendEvent(new SkillCastRejectedEvent(
                _owner,
                _skill,
                reason,
                _owner != null ? _owner.CurrentMana : 0f,
                _skill != null ? _skill.ManaCost : 0f));
            return new SkillCastResult(status);
        }
    }
}
