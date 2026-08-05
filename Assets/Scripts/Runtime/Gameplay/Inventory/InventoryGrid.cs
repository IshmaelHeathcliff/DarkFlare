using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public sealed class InventoryGrid
    {
        readonly ItemInstance[,] _cells;
        readonly Dictionary<ItemInstance, RectInt> _placements = new Dictionary<ItemInstance, RectInt>();

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyDictionary<ItemInstance, RectInt> Placements => _placements;

        public InventoryGrid(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            _cells = new ItemInstance[Width, Height];
        }

        public bool IsOccupied(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return true;
            }

            return _cells[x, y] != null;
        }

        public bool TryAdd(ItemInstance item)
        {
            if (!TryFindPlacement(item, null, out RectInt placement))
            {
                return false;
            }

            Occupy(item, placement);
            return true;
        }

        public bool CanAdd(ItemInstance item)
        {
            return TryFindPlacement(item, null, out _);
        }

        public bool TryExchange(ItemInstance itemToRemove, ItemInstance itemToAdd)
        {
            if (!CanExchange(itemToRemove, itemToAdd))
            {
                return false;
            }

            if (itemToAdd == null)
            {
                return Remove(itemToRemove);
            }

            if (!TryFindPlacement(itemToAdd, itemToRemove, out RectInt placement))
            {
                return false;
            }

            Remove(itemToRemove);
            Occupy(itemToAdd, placement);
            return true;
        }

        public bool CanExchange(ItemInstance itemToRemove, ItemInstance itemToAdd)
        {
            if (itemToRemove == null || !_placements.ContainsKey(itemToRemove))
            {
                return false;
            }

            return itemToAdd == null || TryFindPlacement(itemToAdd, itemToRemove, out _);
        }

        public bool Remove(ItemInstance item)
        {
            if (item == null || !_placements.TryGetValue(item, out RectInt rect))
            {
                return false;
            }

            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    _cells[x, y] = null;
                }
            }

            _placements.Remove(item);
            return true;
        }

        bool TryFindPlacement(ItemInstance item, ItemInstance ignoredItem, out RectInt placement)
        {
            placement = default;

            if (item == null || item.BaseDefinition == null || _placements.ContainsKey(item))
            {
                return false;
            }

            Vector2Int size = item.BaseDefinition.GridSize;
            int itemWidth = Mathf.Max(1, size.x);
            int itemHeight = Mathf.Max(1, size.y);

            for (int originY = 0; originY <= Height - itemHeight; originY++)
            {
                for (int originX = 0; originX <= Width - itemWidth; originX++)
                {
                    if (CanPlace(originX, originY, itemWidth, itemHeight, ignoredItem))
                    {
                        placement = new RectInt(originX, originY, itemWidth, itemHeight);
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanPlace(int originX, int originY, int itemWidth, int itemHeight, ItemInstance ignoredItem)
        {
            for (int y = originY; y < originY + itemHeight; y++)
            {
                for (int x = originX; x < originX + itemWidth; x++)
                {
                    ItemInstance occupiedItem = _cells[x, y];

                    if (occupiedItem != null && occupiedItem != ignoredItem)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        void Occupy(ItemInstance item, RectInt rect)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    _cells[x, y] = item;
                }
            }

            _placements[item] = rect;
        }
    }
}
