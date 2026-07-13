namespace DarkFlare
{
    public class EquipItemCommand : AbstractCommand
    {
        readonly CombatActor _actor;
        readonly ItemInstance _item;

        public EquipItemCommand(CombatActor actor, ItemInstance item)
        {
            _actor = actor;
            _item = item;
        }

        protected override void OnExecute()
        {
            this.GetSystem<CombatSystem>().EquipWeapon(_actor, _item);
        }
    }
}
