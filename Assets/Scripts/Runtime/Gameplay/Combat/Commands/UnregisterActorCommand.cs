namespace DarkFlare
{
    public class UnregisterActorCommand : AbstractCommand
    {
        readonly CombatActor _actor;

        public UnregisterActorCommand(CombatActor actor)
        {
            _actor = actor;
        }

        protected override void OnExecute()
        {
            this.GetSystem<CombatSystem>().UnregisterActor(_actor);
        }
    }
}
