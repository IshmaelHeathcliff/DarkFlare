namespace DarkFlare
{
    public class EquipItemCommand : AbstractCommand<bool>
    {
        readonly CombatActor _actor;
        readonly ItemInstance _item;

        public EquipItemCommand(CombatActor actor, ItemInstance item)
        {
            _actor = actor;
            _item = item;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<CombatSystem>().EquipWeapon(_actor, _item);
        }
    }
}
