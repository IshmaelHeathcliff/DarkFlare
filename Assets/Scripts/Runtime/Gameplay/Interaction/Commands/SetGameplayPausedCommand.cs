namespace DarkFlare
{
    public class SetGameplayPausedCommand : AbstractCommand
    {
        readonly bool _paused;

        public SetGameplayPausedCommand(bool paused)
        {
            _paused = paused;
        }

        protected override void OnExecute()
        {
            this.GetSystem<GameplayPauseSystem>().SetPaused(_paused);
        }
    }
}
