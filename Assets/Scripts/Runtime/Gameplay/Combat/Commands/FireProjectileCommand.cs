using UnityEngine;

namespace DarkFlare
{
    public class FireProjectileCommand : AbstractCommand
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

        protected override void OnExecute()
        {
            if (_owner == null || _skill == null)
            {
                return;
            }

            int seed = this.GetSystem<GameplayRandomSystem>().NextSeed(GameplayRandomChannel.PlayerAttack);
            AttackSnapshot attack = AttackSnapshotFactory.CreateProjectile(
                _owner,
                _skill,
                this.GetModel<EquipmentModel>(),
                seed);
            ProjectileController projectile = this.GetSystem<SpawnSystem>().SpawnProjectile(
                _skill,
                _position,
                _direction,
                attack);

            if (projectile != null)
            {
                this.SendEvent(new ActorAttackedEvent { Actor = _owner });
            }
        }
    }
}
