namespace DarkFlare
{
    public class InventoryModel : AbstractModel
    {
        // 背包尺寸暂硬编码，配置化留给背包 UI/角色成长那一步
        const int DefaultWidth = 10;
        const int DefaultHeight = 6;

        public InventoryGrid Grid { get; private set; }

        public int Gold { get; private set; }

        protected override void OnInit()
        {
            Grid = new InventoryGrid(DefaultWidth, DefaultHeight);
        }

        public bool TryAddItem(ItemInstance item)
        {
            bool added = Grid.TryAdd(item);

            if (added)
            {
                this.SendEvent(new InventoryChangedEvent(item, InventoryChangeType.Added));
            }

            return added;
        }

        public bool RemoveItem(ItemInstance item)
        {
            bool removed = Grid.Remove(item);

            if (removed)
            {
                this.SendEvent(new InventoryChangedEvent(item, InventoryChangeType.Removed));
            }

            return removed;
        }

        public bool TryExchangeItem(ItemInstance itemToRemove, ItemInstance itemToAdd)
        {
            bool exchanged = Grid.TryExchange(itemToRemove, itemToAdd);

            if (!exchanged)
            {
                return false;
            }

            this.SendEvent(new InventoryChangedEvent(itemToRemove, InventoryChangeType.Removed));

            if (itemToAdd != null)
            {
                this.SendEvent(new InventoryChangedEvent(itemToAdd, InventoryChangeType.Added));
            }

            return true;
        }

        public void AddGold(int amount)
        {
            if (amount > 0)
            {
                int previousGold = Gold;
                Gold += amount;
                this.SendEvent(new GoldChangedEvent(previousGold, Gold));
            }
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount)
            {
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            int previousGold = Gold;
            Gold -= amount;
            this.SendEvent(new GoldChangedEvent(previousGold, Gold));
            return true;
        }
    }
}
