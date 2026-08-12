namespace DarkFlare
{
    public class CraftItemCommand : AbstractCommand<CraftingResult>
    {
        readonly CraftOperation _operation;
        readonly CraftingAffixScope _scope;
        readonly ItemInstance _item;

        public CraftItemCommand(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemInstance item)
        {
            _operation = operation;
            _scope = scope;
            _item = item;
        }

        protected override CraftingResult OnExecute()
        {
            return this.GetSystem<CraftingSystem>().Craft(_operation, _scope, _item);
        }
    }
}
