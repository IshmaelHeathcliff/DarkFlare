namespace DarkFlare
{
    public class EquipItemFromGridCommand : AbstractCommand<bool>
    {
        readonly CombatActor _actor;
        readonly ItemInstance _item;
        readonly EquipmentSlot _slot;

        public EquipItemFromGridCommand(CombatActor actor, ItemInstance item, EquipmentSlot slot)
        {
            _actor = actor;
            _item = item;
            _slot = slot;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<EquipmentSystem>().EquipAtSource(_actor, _item, _slot);
        }
    }
}
