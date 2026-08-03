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
            ProjectileController projectile = this.GetSystem<SpawnSystem>().SpawnProjectile(_skill, _owner, _position, _direction);

            if (projectile != null)
            {
                this.SendEvent(new ActorAttackedEvent { Actor = _owner });
            }
        }
    }
}
