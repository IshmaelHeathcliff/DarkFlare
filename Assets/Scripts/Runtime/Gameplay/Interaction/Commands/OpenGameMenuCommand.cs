namespace DarkFlare
{
    public class OpenGameMenuCommand : AbstractCommand<bool>
    {
        readonly WorldInteractionTarget _target;

        public OpenGameMenuCommand(WorldInteractionTarget target)
        {
            _target = target;
        }

        protected override bool OnExecute()
        {
            if (_target == null || !_target.CanInteract)
            {
                return false;
            }

            if (_target.MapBinding?.Kind == MapInteractionKind.Portal)
            {
                ApplicationHost host = ApplicationHost.Current;
                WorldMapRuntime maps = this.GetUtility<WorldMapRuntime>();
                if (maps.Active == null || maps.IsTransitioning || maps.Active.Generation != _target.MapGeneration) { return false; }
                string mapId = _target.MapBinding.TargetMapId, entryId = _target.MapBinding.TargetEntryId;
                host.Input.WaitForInteractRelease();
                if (maps.CanReset(mapId) && host.ApplicationShell != null)
                {
                    host.ApplicationShell.ShowMapPortalChoice(mapId, entryId);
                    return true;
                }
                host.CurrentSession.SessionScope.Tasks.Run("map-exit", async token =>
                {
                    await host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap(mapId, entryId), token);
                });
                return true;
            }

            this.SendEvent(new GameMenuOpenRequestedEvent(
                _target.MenuPage,
                _target.AvailablePages,
                _target));
            return true;
        }
    }
}
