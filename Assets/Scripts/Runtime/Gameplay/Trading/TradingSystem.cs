using System;
using System.Linq;
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
        bool _refreshing;
        bool _disposed;

        protected override void OnDeinit() { _disposed = true; }

        public bool RefreshMerchant()
        {
            if (_disposed || _refreshing) { return false; }
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host) && host.CurrentSession != null
                && (host.CurrentSession.Architecture.GetSystem<TradingSystem>() != this
                    || host.CurrentSession.State != GameSessionState.Running
                    || !host.CurrentSession.SceneScope.CanAcceptWork || host.Input.IsSuspended
                    || host.ApplicationShell.BlocksGameplay
                    || host.SceneFlow.State != GameFlowState.InGame && host.SceneFlow.State != GameFlowState.Paused))
            { return false; }
            EconomyModel economy = this.GetModel<EconomyModel>();
            if (economy.Trader == null) { return false; }
            _refreshing = true;
            try
            {
                ItemInstance[] stock;
                int seed = GameplayRandomSystem.CreateRootSeed();
                try
                {
                    stock = economy.Trader.CreateStock(new System.Random(seed), this.GetUtility<IItemInstanceIdGenerator>()).ToArray();
                }
                catch (Exception exception)
                {
                    ApplicationLog.Warning(LogEventIds.GameplayTrading, $"[TradingSystem] 刷新失败，保留原库存：{exception.Message}");
                    return false;
                }
                economy.RestoreState(economy.Trader, stock, economy.BuyMultiplier, economy.SellMultiplier);
                ApplicationLog.Info(LogEventIds.GameplayTrading, $"[TradingSystem] 库存刷新：seed={seed}，共 {stock.Length} 件");
                this.SendEvent(new MerchantStockRefreshedEvent());
                return true;
            }
            finally { _refreshing = false; }
        }
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
            if (item == null || item.BaseDefinition == null || item.BaseDefinition.ItemType == ItemType.Currency)
            {
                return false;
            }

            InventoryModel inventory = this.GetModel<InventoryModel>();

            int price = GetSellPrice(item);
            if (!inventory.Grid.Placements.TryGetValue(item, out RectInt placement)) { return false; }
            if ((long)inventory.Gold + price > int.MaxValue || !inventory.RemoveItemWithoutEvents(item))
            {
                ApplicationLog.Info(LogEventIds.GameplayTrading, $"[TradingSystem] 出售失败：{DescribeItem(item)} 不在背包中");
                return false;
            }

            int previousGold = inventory.Gold;
            if (!inventory.TryChangeGoldWithoutEvents(price))
            {
                inventory.TryAddItemAtWithoutEvents(item, placement.position);
                return false;
            }
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
                && item.BaseDefinition != null && item.BaseDefinition.ItemType != ItemType.Currency
                && inventory.Gold >= GetBuyPrice(item)
                && (origin.HasValue ? inventory.CanAddItemAt(item, origin.Value) : inventory.CanAddItem(item));
        }

        public bool BuyItem(ItemInstance item, Vector2Int? origin = null)
        {
            if (item == null || item.BaseDefinition == null || item.BaseDefinition.ItemType == ItemType.Currency)
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
            if (trader == null)
            {
                economy.ClearStock();
                return;
            }
            ItemInstance[] stock = trader.CreateStock(new System.Random(GameplayRandomSystem.CreateRootSeed()),
                this.GetUtility<IItemInstanceIdGenerator>()).ToArray();
            economy.RestoreState(
                trader,
                stock,
                trader.BuyMultiplier,
                trader.SellMultiplier);

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

    public readonly struct MerchantStockRefreshedEvent { }

    public sealed class RefreshMerchantCommand : AbstractCommand<bool>
    {
        protected override bool OnExecute()
        {
            return this.GetSystem<TradingSystem>().RefreshMerchant();
        }
    }
}
