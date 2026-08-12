namespace DarkFlare
{
    public class GetCraftingCostQuery : AbstractQuery<int>
    {
        readonly CraftOperation _operation;
        readonly CraftingAffixScope _scope;
        readonly ItemRarity _rarity;

        public GetCraftingCostQuery(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemRarity rarity)
        {
            _operation = operation;
            _scope = scope;
            _rarity = rarity;
        }

        protected override int OnDo()
        {
            return this.GetSystem<CraftingSystem>().GetCost(_operation, _scope, _rarity);
        }
    }
}
