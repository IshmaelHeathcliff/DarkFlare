using UnityEngine;

namespace DarkFlare
{
    public class SpawnPlayerCommand : AbstractCommand<CombatActor>
    {
        readonly CharacterDefinition _definition;
        readonly ProjectileSkillDefinition _skill;
        readonly Vector3 _position;

        public SpawnPlayerCommand(CharacterDefinition definition, ProjectileSkillDefinition skill, Vector3 position)
        {
            _definition = definition;
            _skill = skill;
            _position = position;
        }

        protected override CombatActor OnExecute()
        {
            return this.GetSystem<SpawnSystem>().SpawnPlayer(_definition, _skill, _position);
        }
    }
}
