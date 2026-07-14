namespace DarkFlare
{
    public class BuyItemCommand : AbstractCommand<bool>
    {
        readonly ItemInstance _item;

        public BuyItemCommand(ItemInstance item)
        {
            _item = item;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<TradingSystem>().BuyItem(_item);
        }
    }
}
