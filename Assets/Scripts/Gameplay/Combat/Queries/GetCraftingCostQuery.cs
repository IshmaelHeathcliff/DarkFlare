namespace DarkFlare
{
    public class GetCraftingCostQuery : AbstractQuery<int>
    {
        readonly CraftOperation _operation;

        public GetCraftingCostQuery(CraftOperation operation)
        {
            _operation = operation;
        }

        protected override int OnDo()
        {
            return this.GetSystem<CraftingSystem>().GetCost(_operation);
        }
    }
}
