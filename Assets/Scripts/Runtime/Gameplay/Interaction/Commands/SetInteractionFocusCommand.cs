namespace DarkFlare
{
    public class SetInteractionFocusCommand : AbstractCommand
    {
        readonly WorldInteractionTarget _target;

        public SetInteractionFocusCommand(WorldInteractionTarget target)
        {
            _target = target;
        }

        protected override void OnExecute()
        {
            this.SendEvent(new InteractionFocusChangedEvent(_target));
        }
    }
}
