using UnityEngine;

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
            Gold = gold;
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

        public bool TryAddItemAt(ItemInstance item, Vector2Int origin)
        {
            bool added = Grid.TryAddAt(item, origin);

            if (added)
            {
                this.SendEvent(new InventoryChangedEvent(
                    item,
                    InventoryChangeType.Added,
                    default,
                    Grid.Placements[item]));
            }

            return added;
        }

        public bool TryMoveItem(ItemInstance item, Vector2Int origin)
        {
            if (item == null || !Grid.Placements.TryGetValue(item, out RectInt previousPlacement))
            {
                return false;
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
            bool removed = Grid.Remove(item);

            if (removed)
            {
                this.SendEvent(new InventoryChangedEvent(item, InventoryChangeType.Removed));
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
            return Grid.CanAdd(item);
        }

        public bool CanAddItemAt(ItemInstance item, Vector2Int origin)
        {
            return Grid.CanAddAt(item, origin);
        }

        public bool TryAddItemWithoutEvents(ItemInstance item)
        {
            return Grid.TryAdd(item);
        }

        public bool TryAddItemAtWithoutEvents(ItemInstance item, Vector2Int origin)
        {
            return Grid.TryAddAt(item, origin);
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
