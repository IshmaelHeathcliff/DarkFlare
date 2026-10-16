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

        public ProjectileController Projectile { get; }

        public bool IsSuccess => Status == SkillCastStatus.Success;

        public SkillCastResult(SkillCastStatus status, ProjectileController projectile = null)
        {
            Status = status;
            Projectile = projectile;
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

        internal static SkillCastResult Perform(IArchitecture architecture, CombatActor owner, ProjectileSkillDefinition skill,
            Vector3 position, Vector2 direction, IReadOnlyList<ModifierInstance> modifiers, string flameId, Action commitCooldown)
        {
            var command = new FireProjectileCommand(owner, skill, position, direction)
            { _localModifiers = modifiers, _flameId = flameId, _commitCooldown = commitCooldown };
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
            AttackSnapshot attack = AttackSnapshotFactory.CreateProjectile(
                _owner,
                _skill,
                equipment,
                seed, _localModifiers, _flameId);

            if (attack == null)
            {
                return Reject(
                    SkillCastStatus.InvalidDamageSource,
                    SkillCastRejectionReason.InvalidDamageSource);
            }

            ProjectileController projectile = this.GetSystem<SpawnSystem>().SpawnProjectile(
                _skill,
                _position,
                _direction,
                attack);

            if (projectile != null)
            {
                CombatResourceSnapshot before = _owner.Resources;
                if (!_owner.TrySpendMana(_skill.ManaCost))
                {
                    UnityEngine.Object.Destroy(projectile.gameObject);
                    return Reject(
                        SkillCastStatus.InsufficientMana,
                        SkillCastRejectionReason.InsufficientMana);
                }
                _commitCooldown?.Invoke();
                try { this.GetSystem<CombatSystem>().PublishResourceChanges(_owner, before, ActorResourceChangeReason.SkillCost); }
                finally
                {
                    try { this.SendEvent(new ActorAttackedEvent { Actor = _owner }); }
                    finally
                    {
                        if (_commitCooldown != null)
                        {
                            try { this.SendEvent(new PlayerSkillsChangedEvent()); }
                            finally { this.SendEvent(new PlayerSkillCastEvent(_owner, attack)); }
                        }
                    }
                }
                return new SkillCastResult(SkillCastStatus.Success, projectile);
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
