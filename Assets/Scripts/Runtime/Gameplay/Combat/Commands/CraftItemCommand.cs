namespace DarkFlare
{
    public class CraftItemCommand : AbstractCommand<bool>
    {
        readonly CraftOperation _operation;
        readonly ItemInstance _item;
        readonly AffixInstance _targetAffix;

        public CraftItemCommand(CraftOperation operation, ItemInstance item, AffixInstance targetAffix = null)
        {
            _operation = operation;
            _item = item;
            _targetAffix = targetAffix;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<CraftingSystem>().Craft(_operation, _item, _targetAffix);
        }
    }
}
