using UnityEngine;

namespace DarkFlare
{
    public readonly struct ItemPrice
    {
        public int Buy { get; }

        public int Sell { get; }

        public ItemPrice(int buy, int sell)
        {
            Buy = buy;
            Sell = sell;
        }
    }

    public class TradingSystem : AbstractSystem
    {
        protected override void OnInit()
        {
        }

        public int GetBuyPrice(ItemInstance item)
        {
            return ItemValueCalculator.GetBuyPrice(item, this.GetModel<EconomyModel>().BuyMultiplier);
        }

        public int GetSellPrice(ItemInstance item)
        {
            return ItemValueCalculator.GetSellPrice(item, this.GetModel<EconomyModel>().SellMultiplier);
        }

        public bool SellItem(ItemInstance item)
        {
            if (item == null)
            {
                return false;
            }

            InventoryModel inventory = this.GetModel<InventoryModel>();

            int price = GetSellPrice(item);
            if ((long)inventory.Gold + price > int.MaxValue || !inventory.RemoveItemWithoutEvents(item))
            {
                ApplicationLog.Info(LogEventIds.GameplayTrading, $"[TradingSystem] 出售失败：{DescribeItem(item)} 不在背包中");
                return false;
            }

            int previousGold = inventory.Gold;
            inventory.TryChangeGoldWithoutEvents(price);
            inventory.NotifyItemChanged(item, InventoryChangeType.Removed);
            inventory.NotifyGoldChanged(previousGold);
            this.SendEvent(new TradeCompletedEvent(TradeOperation.Sell, item, price));
            ApplicationLog.Info(LogEventIds.GameplayTrading, $"[TradingSystem] 出售 {DescribeItem(item)} 获得 {price} 金币，当前金币 {inventory.Gold}");
            return true;
        }

        public bool CanBuyItem(ItemInstance item, Vector2Int? origin = null)
        {
            InventoryModel inventory = this.GetModel<InventoryModel>();
            return item != null && this.GetModel<EconomyModel>().HasStock(item)
                && inventory.Gold >= GetBuyPrice(item)
                && (origin.HasValue ? inventory.CanAddItemAt(item, origin.Value) : inventory.CanAddItem(item));
        }

        public bool BuyItem(ItemInstance item, Vector2Int? origin = null)
        {
            if (item == null)
            {
                return false;
            }

            EconomyModel economy = this.GetModel<EconomyModel>();
            InventoryModel inventory = this.GetModel<InventoryModel>();

            if (!economy.HasStock(item))
            {
                ApplicationLog.Info(LogEventIds.GameplayTrading, $"[TradingSystem] 购买失败：商人没有 {DescribeItem(item)}");
                return false;
            }

            int price = GetBuyPrice(item);

            if (inventory.Gold < price)
            {
                ApplicationLog.Info(LogEventIds.GameplayTrading, $"[TradingSystem] 购买失败：金币不足（需要 {price}，持有 {inventory.Gold}）");
                return false;
            }

            bool added = origin.HasValue
                ? inventory.TryAddItemAtWithoutEvents(item, origin.Value)
                : inventory.TryAddItemWithoutEvents(item);
            if (!added)
            {
                ApplicationLog.Info(LogEventIds.GameplayTrading, $"[TradingSystem] 购买失败：背包放不下 {DescribeItem(item)}");
                return false;
            }

            int previousGold = inventory.Gold;
            inventory.TryChangeGoldWithoutEvents(-price);
            economy.RemoveStock(item);
            inventory.NotifyItemChanged(item, InventoryChangeType.Added);
            inventory.NotifyGoldChanged(previousGold);
            this.SendEvent(new TradeCompletedEvent(TradeOperation.Buy, item, price));
            ApplicationLog.Info(LogEventIds.GameplayTrading, $"[TradingSystem] 购买 {DescribeItem(item)} 花费 {price} 金币，当前金币 {inventory.Gold}");
            return true;
        }

        public void SetupMerchant(TraderDefinition trader)
        {
            EconomyModel economy = this.GetModel<EconomyModel>();
            economy.ClearStock();

            if (trader == null)
            {
                return;
            }

            economy.RestoreState(
                trader,
                System.Array.Empty<ItemInstance>(),
                trader.BuyMultiplier,
                trader.SellMultiplier);

            foreach (ItemInstance item in trader.CreateStock(
                         new System.Random(),
                         this.GetUtility<IItemInstanceIdGenerator>()))
            {
                economy.AddStock(item);
            }

            ApplicationLog.Info(LogEventIds.GameplayTrading, $"[TradingSystem] 商人库存初始化完成，共 {economy.MerchantStock.Count} 件");
        }

        public void GrantGold(int amount)
        {
            this.GetModel<InventoryModel>().AddGold(amount);
            ApplicationLog.Info(LogEventIds.GameplayTrading, $"[TradingSystem] 发放初始金币 {amount}，当前金币 {this.GetModel<InventoryModel>().Gold}");
        }

        static string DescribeItem(ItemInstance item)
        {
            return item.BaseDefinition != null ? $"{item.BaseDefinition.DisplayName}[{item.Rarity}]" : item.InstanceId;
        }
    }
}
