namespace DarkFlare
{
    public class GameplayPauseSystem : AbstractSystem
    {
        const string MenuPauseOwner = "gameplay-menu";

        GamePauseLease _menuPauseLease;

        public bool IsPaused => _menuPauseLease != null && !_menuPauseLease.IsReleased;

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused)
            {
                return;
            }

            if (paused)
            {
                _menuPauseLease = ResolveTimeService().AcquirePause(MenuPauseOwner);
            }
            else
            {
                _menuPauseLease.Dispose();
                _menuPauseLease = null;
            }

            this.SendEvent(new GameplayPauseChangedEvent(paused));
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            _menuPauseLease?.Dispose();
            _menuPauseLease = null;
        }

        static GameTimeService ResolveTimeService()
        {
            return ApplicationHost.TryGetCurrent(out ApplicationHost host)
                ? host.GameTime
                : GameTimeService.Shared;
        }
    }
}
