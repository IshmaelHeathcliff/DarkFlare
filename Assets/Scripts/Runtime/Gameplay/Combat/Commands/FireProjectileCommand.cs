using UnityEngine;

namespace DarkFlare
{
    public enum SkillCastStatus
    {
        Success,
        InvalidOwner,
        InvalidDamageSource,
        InsufficientMana,
        SpawnFailed
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

            EquipmentModel equipment = this.GetModel<EquipmentModel>();

            if (!AttackSnapshotFactory.CanCreateProjectile(_owner, _skill, equipment))
            {
                return Reject(
                    SkillCastStatus.InvalidDamageSource,
                    SkillCastRejectionReason.InvalidDamageSource);
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
                seed);

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
                if (!this.GetSystem<CombatSystem>().TrySpendMana(_owner, _skill.ManaCost))
                {
                    Object.Destroy(projectile.gameObject);
                    return Reject(
                        SkillCastStatus.InsufficientMana,
                        SkillCastRejectionReason.InsufficientMana);
                }

                this.SendEvent(new ActorAttackedEvent { Actor = _owner });
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
