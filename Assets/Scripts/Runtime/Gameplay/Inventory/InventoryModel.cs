using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public class InventoryModel : AbstractModel
    {
        // 背包尺寸暂硬编码，配置化留给背包 UI/角色成长那一步
        const int DefaultWidth = 10;
        const int DefaultHeight = 6;

        public InventoryGrid Grid { get; private set; }

        ItemBaseDefinition _goldDefinition;

        public int Gold => CountItems(_goldDefinition);

        public void ConfigureCurrency(ItemBaseDefinition definition)
        {
            if (definition == null || definition.ItemType != ItemType.Currency || !definition.IsStackable || !definition.IsConsumable)
            {
                throw new ArgumentException("金币必须是可堆叠、可消耗的通货", nameof(definition));
            }
            _goldDefinition = definition;
        }

        public void ConfigureCurrency(ContentCatalog catalog)
        {
            foreach (ItemBaseDefinition definition in catalog.GetAll<ItemBaseDefinition>())
            {
                if (definition.ItemType == ItemType.Currency)
                {
                    ConfigureCurrency(definition);
                    return;
                }
            }
            throw new InvalidOperationException("内容目录缺少金币配置");
        }

        public int CountItems(ItemBaseDefinition definition)
        {
            if (definition == null) { return 0; }
            long count = 0;
            foreach (ItemInstance item in Grid.Placements.Keys)
            {
                if (item.BaseDefinition == definition) { count += item.Quantity; }
            }
            return (int)Math.Min(int.MaxValue, count);
        }

        protected override void OnInit()
        {
            Grid = new InventoryGrid(DefaultWidth, DefaultHeight);
        }

        public void RestoreState(InventoryGrid grid, int gold)
        {
            if (grid == null)
            {
                throw new System.ArgumentNullException(nameof(grid));
            }

            if (gold < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(gold));
            }

            Grid = grid;
            if (Gold != gold)
            {
                throw new InvalidOperationException("背包金币与存档摘要不一致");
            }
        }

        public bool TryAddItem(ItemInstance item)
        {
            int previousGold = Gold;
            bool added = TryAddItemWithoutEvents(item);

            if (added)
            {
                this.SendEvent(new InventoryChangedEvent(item, InventoryChangeType.Added));
                NotifyGoldChanged(previousGold);
            }

            return added;
        }

        public bool TryAddItemAt(ItemInstance item, Vector2Int origin)
        {
            int previousGold = Gold;
            bool added = TryAddItemAtWithoutEvents(item, origin);

            if (added)
            {
                this.SendEvent(new InventoryChangedEvent(
                    item,
                    InventoryChangeType.Added,
                    default,
                    Grid.Placements.TryGetValue(item, out RectInt placement) ? placement : default));
                NotifyGoldChanged(previousGold);
            }

            return added;
        }

        public bool TryMoveItem(ItemInstance item, Vector2Int origin)
        {
            if (item == null || !Grid.Placements.TryGetValue(item, out RectInt previousPlacement))
            {
                return false;
            }

            ItemInstance stack = FindMergeTarget(item, origin);
            if (stack != null)
            {
                int moved = Math.Min(item.Quantity, stack.BaseDefinition.MaxStackSize - stack.Quantity);
                stack.TrySetQuantity(stack.Quantity + moved);
                if (moved == item.Quantity) { Grid.Remove(item); }
                else { item.TrySetQuantity(item.Quantity - moved); }
                NotifyItemChanged(item, Grid.Placements.ContainsKey(item) ? InventoryChangeType.Moved : InventoryChangeType.Removed);
                NotifyItemChanged(stack, InventoryChangeType.Added);
                return true;
            }

            if (!Grid.CanMove(item, origin, out ItemInstance exchangedItem))
            {
                return false;
            }

            RectInt exchangedPreviousPlacement = exchangedItem != null
                ? Grid.Placements[exchangedItem]
                : default;

            if (!Grid.TryMove(item, origin, out exchangedItem))
            {
                return false;
            }

            NotifyItemMoved(item, previousPlacement, Grid.Placements[item]);

            if (exchangedItem != null)
            {
                NotifyItemMoved(
                    exchangedItem,
                    exchangedPreviousPlacement,
                    Grid.Placements[exchangedItem]);
            }

            return true;
        }

        public bool RemoveItem(ItemInstance item)
        {
            int previousGold = Gold;
            bool removed = Grid.Remove(item);

            if (removed)
            {
                this.SendEvent(new InventoryChangedEvent(item, InventoryChangeType.Removed));
                NotifyGoldChanged(previousGold);
            }

            return removed;
        }

        public bool TryExchangeItem(ItemInstance itemToRemove, ItemInstance itemToAdd)
        {
            bool exchanged = TryExchangeItemWithoutEvents(itemToRemove, itemToAdd);

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

        public bool CanExchangeItem(ItemInstance itemToRemove, ItemInstance itemToAdd)
        {
            return Grid.CanExchange(itemToRemove, itemToAdd);
        }

        public bool CanExchangeItemAt(ItemInstance itemToRemove, ItemInstance itemToAdd, Vector2Int origin)
        {
            return Grid.CanExchangeAt(itemToRemove, itemToAdd, origin);
        }

        public bool TryExchangeItemWithoutEvents(ItemInstance itemToRemove, ItemInstance itemToAdd)
        {
            return Grid.TryExchange(itemToRemove, itemToAdd);
        }

        public bool TryExchangeItemAtWithoutEvents(
            ItemInstance itemToRemove,
            ItemInstance itemToAdd,
            Vector2Int origin)
        {
            return Grid.TryExchangeAt(itemToRemove, itemToAdd, origin);
        }

        public bool CanAddItem(ItemInstance item)
        {
            return CanReceive(item, null);
        }

        public bool CanAddItemAt(ItemInstance item, Vector2Int origin)
        {
            return CanReceive(item, origin);
        }

        public bool TryAddItemWithoutEvents(ItemInstance item)
        {
            return TryReceive(item, null);
        }

        public bool TryAddItemAtWithoutEvents(ItemInstance item, Vector2Int origin)
        {
            return TryReceive(item, origin);
        }

        public bool RemoveItemWithoutEvents(ItemInstance item)
        {
            return Grid.Remove(item);
        }

        public void NotifyItemChanged(ItemInstance item, InventoryChangeType changeType)
        {
            this.SendEvent(new InventoryChangedEvent(item, changeType));
        }

        public void NotifyItemMoved(ItemInstance item, RectInt previousPlacement, RectInt currentPlacement)
        {
            this.SendEvent(new InventoryChangedEvent(
                item,
                InventoryChangeType.Moved,
                previousPlacement,
                currentPlacement));
        }

        public void AddGold(int amount)
        {
            if (amount > 0)
            {
                int previousGold = Gold;
                if (!TryChangeGoldWithoutEvents(amount))
                {
                    throw new InvalidOperationException("金币发放失败：配置缺失、背包空间不足或数量溢出");
                }
                NotifyGoldChanged(previousGold);
            }
        }

        internal bool TryChangeGoldWithoutEvents(int change)
        {
            long next = (long)Gold + change;
            if (next < 0 || next > int.MaxValue)
            {
                return false;
            }

            if (change == 0) { return true; }
            if (_goldDefinition == null) { return false; }
            if (change < 0) { return TryConsumeWithoutEvents(_goldDefinition, -change); }
            int remaining = change;
            foreach (ItemInstance stack in Grid.Placements.Keys)
            {
                if (stack.BaseDefinition == _goldDefinition)
                {
                    remaining = Math.Max(0, remaining - (_goldDefinition.MaxStackSize - stack.Quantity));
                }
            }
            InventoryGrid planned = new InventoryGrid(Grid.Width, Grid.Height);
            foreach (KeyValuePair<ItemInstance, RectInt> pair in Grid.Placements)
            {
                planned.TryAddAt(pair.Key, pair.Value.position);
            }
            List<ItemInstance> newStacks = new List<ItemInstance>();
            while (remaining > 0)
            {
                ItemInstance coins = _goldDefinition.CreateInstance(
                    this.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 0, ItemRarity.Normal);
                int quantity = Math.Min(remaining, _goldDefinition.MaxStackSize);
                coins.TrySetQuantity(quantity);
                if (!planned.TryAdd(coins)) { return false; }
                newStacks.Add(coins);
                remaining -= quantity;
            }
            remaining = change;
            foreach (ItemInstance stack in Grid.Placements.Keys)
            {
                if (stack.BaseDefinition != _goldDefinition) { continue; }
                int moved = Math.Min(remaining, _goldDefinition.MaxStackSize - stack.Quantity);
                stack.TrySetQuantity(stack.Quantity + moved);
                remaining -= moved;
            }
            foreach (ItemInstance stack in newStacks)
            {
                Grid.TryAddAt(stack, planned.Placements[stack].position);
            }
            return true;
        }

        internal void NotifyGoldChanged(int previousGold)
        {
            if (previousGold != Gold)
            {
                this.SendEvent(new InventoryChangedEvent(null, InventoryChangeType.Added));
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
            if (!TryChangeGoldWithoutEvents(-amount)) { return false; }
            NotifyGoldChanged(previousGold);
            return true;
        }

        internal bool TryConsumeWithoutEvents(ItemBaseDefinition definition, int quantity)
        {
            if (definition == null || !definition.IsConsumable || quantity < 0 || CountItems(definition) < quantity)
            {
                return false;
            }
            List<ItemInstance> items = new List<ItemInstance>(Grid.Placements.Keys);
            foreach (ItemInstance item in items)
            {
                if (quantity == 0) { break; }
                if (item.BaseDefinition != definition) { continue; }
                int consumed = Math.Min(quantity, item.Quantity);
                if (consumed == item.Quantity) { Grid.Remove(item); }
                else { item.TrySetQuantity(item.Quantity - consumed); }
                quantity -= consumed;
            }
            return true;
        }

        bool CanReceive(ItemInstance item, Vector2Int? origin)
        {
            if (item == null || item.BaseDefinition == null || Grid.Placements.ContainsKey(item)) { return false; }
            if (item.BaseDefinition == _goldDefinition && (long)Gold + item.Quantity > int.MaxValue) { return false; }
            long remaining = item.Quantity;
            foreach (KeyValuePair<ItemInstance, RectInt> pair in Grid.Placements)
            {
                if (origin.HasValue && !pair.Value.Contains(origin.Value)) { continue; }
                if (pair.Key.CanStackWith(item)) { remaining -= pair.Key.BaseDefinition.MaxStackSize - pair.Key.Quantity; }
            }
            return remaining <= 0 || (origin.HasValue ? Grid.CanAddAt(item, origin.Value) : Grid.CanAdd(item));
        }

        public bool CanMoveItem(ItemInstance item, Vector2Int origin)
        {
            return item != null && Grid.Placements.ContainsKey(item)
                && (FindMergeTarget(item, origin) != null || Grid.CanMove(item, origin, out _));
        }

        ItemInstance FindMergeTarget(ItemInstance item, Vector2Int origin)
        {
            foreach (KeyValuePair<ItemInstance, RectInt> pair in Grid.Placements)
            {
                if (pair.Value.Contains(origin) && pair.Key.CanStackWith(item) && pair.Key.Quantity < pair.Key.BaseDefinition.MaxStackSize)
                {
                    return pair.Key;
                }
            }
            return null;
        }

        bool TryReceive(ItemInstance item, Vector2Int? origin)
        {
            if (!CanReceive(item, origin)) { return false; }
            int remaining = item.Quantity;
            foreach (KeyValuePair<ItemInstance, RectInt> pair in Grid.Placements)
            {
                if (origin.HasValue && !pair.Value.Contains(origin.Value)) { continue; }
                if (!pair.Key.CanStackWith(item)) { continue; }
                int moved = Math.Min(remaining, pair.Key.BaseDefinition.MaxStackSize - pair.Key.Quantity);
                pair.Key.TrySetQuantity(pair.Key.Quantity + moved);
                remaining -= moved;
                if (remaining == 0) { return true; }
            }
            item.TrySetQuantity(remaining);
            return origin.HasValue ? Grid.TryAddAt(item, origin.Value) : Grid.TryAdd(item);
        }
    }
}
