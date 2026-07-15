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

            if (!inventory.RemoveItem(item))
            {
                Debug.Log($"[TradingSystem] 出售失败：{DescribeItem(item)} 不在背包中");
                return false;
            }

            int price = GetSellPrice(item);
            inventory.AddGold(price);
            Debug.Log($"[TradingSystem] 出售 {DescribeItem(item)} 获得 {price} 金币，当前金币 {inventory.Gold}");
            return true;
        }

        public bool BuyItem(ItemInstance item)
        {
            if (item == null)
            {
                return false;
            }

            EconomyModel economy = this.GetModel<EconomyModel>();
            InventoryModel inventory = this.GetModel<InventoryModel>();

            if (!economy.HasStock(item))
            {
                Debug.Log($"[TradingSystem] 购买失败：商人没有 {DescribeItem(item)}");
                return false;
            }

            int price = GetBuyPrice(item);

            if (inventory.Gold < price)
            {
                Debug.Log($"[TradingSystem] 购买失败：金币不足（需要 {price}，持有 {inventory.Gold}）");
                return false;
            }

            if (!inventory.TryAddItem(item))
            {
                Debug.Log($"[TradingSystem] 购买失败：背包放不下 {DescribeItem(item)}");
                return false;
            }

            inventory.TrySpendGold(price);
            economy.RemoveStock(item);
            Debug.Log($"[TradingSystem] 购买 {DescribeItem(item)} 花费 {price} 金币，当前金币 {inventory.Gold}");
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

            economy.SetMultipliers(trader.BuyMultiplier, trader.SellMultiplier);

            foreach (ItemInstance item in trader.CreateStock(new System.Random()))
            {
                economy.AddStock(item);
            }

            Debug.Log($"[TradingSystem] 商人库存初始化完成，共 {economy.MerchantStock.Count} 件");
        }

        public void GrantGold(int amount)
        {
            this.GetModel<InventoryModel>().AddGold(amount);
            Debug.Log($"[TradingSystem] 发放初始金币 {amount}，当前金币 {this.GetModel<InventoryModel>().Gold}");
        }

        static string DescribeItem(ItemInstance item)
        {
            return item.BaseDefinition != null ? $"{item.BaseDefinition.DisplayName}[{item.Rarity}]" : item.InstanceId;
        }
    }
}
