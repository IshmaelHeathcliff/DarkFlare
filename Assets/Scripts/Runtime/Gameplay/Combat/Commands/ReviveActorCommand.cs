using UnityEngine;

namespace DarkFlare
{
    public class ReviveActorCommand : AbstractCommand
    {
        readonly CombatActor _actor;
        readonly Vector3 _position;

        public ReviveActorCommand(CombatActor actor, Vector3 position)
        {
            _actor = actor;
            _position = position;
        }

        protected override void OnExecute()
        {
            this.GetSystem<CombatSystem>().Revive(_actor, _position);
        }
    }
}
