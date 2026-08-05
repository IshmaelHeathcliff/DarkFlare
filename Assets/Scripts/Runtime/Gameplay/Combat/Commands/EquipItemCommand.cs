namespace DarkFlare
{
    public class EquipItemCommand : AbstractCommand<bool>
    {
        readonly CombatActor _actor;
        readonly ItemInstance _item;
        readonly EquipmentSlot _slot;

        public EquipItemCommand(CombatActor actor, ItemInstance item)
            : this(actor, item, EquipmentSlot.Weapon)
        {
        }

        public EquipItemCommand(CombatActor actor, ItemInstance item, EquipmentSlot slot)
        {
            _actor = actor;
            _item = item;
            _slot = slot;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<EquipmentSystem>().Equip(_actor, _item, _slot);
        }
    }
}
