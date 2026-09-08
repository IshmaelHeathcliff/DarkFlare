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

            this.SendEvent(new GameMenuOpenRequestedEvent(
                _target.MenuPage,
                _target.AvailablePages,
                _target));
            return true;
        }
    }
}
