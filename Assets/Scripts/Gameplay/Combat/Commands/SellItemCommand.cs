namespace DarkFlare
{
    public class SellItemCommand : AbstractCommand<bool>
    {
        readonly ItemInstance _item;

        public SellItemCommand(ItemInstance item)
        {
            _item = item;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<TradingSystem>().SellItem(_item);
        }
    }
}
