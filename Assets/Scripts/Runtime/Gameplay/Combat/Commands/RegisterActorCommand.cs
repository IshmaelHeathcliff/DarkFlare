namespace DarkFlare
{
    public class RegisterActorCommand : AbstractCommand
    {
        readonly CombatActor _actor;

        public RegisterActorCommand(CombatActor actor)
        {
            _actor = actor;
        }

        protected override void OnExecute()
        {
            this.GetSystem<CombatSystem>().RegisterActor(_actor);
        }
    }
}
