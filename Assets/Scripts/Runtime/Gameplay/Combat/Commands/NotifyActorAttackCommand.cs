namespace DarkFlare
{
    public class NotifyActorAttackCommand : AbstractCommand
    {
        readonly CombatActor _actor;

        public NotifyActorAttackCommand(CombatActor actor)
        {
            _actor = actor;
        }

        protected override void OnExecute()
        {
            if (_actor != null && _actor.IsAlive)
            {
                this.SendEvent(new ActorAttackedEvent { Actor = _actor });
            }
        }
    }
}
