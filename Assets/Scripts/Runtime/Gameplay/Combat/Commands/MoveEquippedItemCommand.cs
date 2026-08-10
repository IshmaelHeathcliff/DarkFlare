namespace DarkFlare
{
    public class MoveEquippedItemCommand : AbstractCommand<bool>
    {
        readonly CombatActor _actor;
        readonly EquipmentSlot _sourceSlot;
        readonly EquipmentSlot _targetSlot;

        public MoveEquippedItemCommand(
            CombatActor actor,
            EquipmentSlot sourceSlot,
            EquipmentSlot targetSlot)
        {
            _actor = actor;
            _sourceSlot = sourceSlot;
            _targetSlot = targetSlot;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<EquipmentSystem>().MoveEquippedItem(_actor, _sourceSlot, _targetSlot);
        }
    }
}
