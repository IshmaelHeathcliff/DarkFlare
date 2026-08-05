namespace DarkFlare
{
    public class UnequipItemCommand : AbstractCommand<bool>
    {
        readonly CombatActor _actor;
        readonly EquipmentSlot _slot;

        public UnequipItemCommand(CombatActor actor, EquipmentSlot slot)
        {
            _actor = actor;
            _slot = slot;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<EquipmentSystem>().Unequip(_actor, _slot);
        }
    }
}
