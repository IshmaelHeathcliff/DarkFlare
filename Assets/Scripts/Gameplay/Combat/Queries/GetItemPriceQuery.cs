namespace DarkFlare
{
    public class GetItemPriceQuery : AbstractQuery<ItemPrice>
    {
        readonly ItemInstance _item;

        public GetItemPriceQuery(ItemInstance item)
        {
            _item = item;
        }

        protected override ItemPrice OnDo()
        {
            TradingSystem trading = this.GetSystem<TradingSystem>();
            return new ItemPrice(trading.GetBuyPrice(_item), trading.GetSellPrice(_item));
        }
    }
}
