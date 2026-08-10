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

        public bool TryAddAt(ItemInstance item, Vector2Int origin)
        {
            if (!CanAddAt(item, origin))
            {
                return false;
            }

            Occupy(item, CreatePlacement(item, origin));
            return true;
        }

        public bool CanAdd(ItemInstance item)
        {
            return TryFindPlacement(item, null, out _);
        }

        public bool CanAddAt(ItemInstance item, Vector2Int origin)
        {
            return item != null
                && item.BaseDefinition != null
                && !_placements.ContainsKey(item)
                && CanPlace(CreatePlacement(item, origin), null, null);
        }

        public bool TryMove(ItemInstance item, Vector2Int origin, out ItemInstance exchangedItem)
        {
            if (!CanMove(item, origin, out exchangedItem))
            {
                return false;
            }

            RectInt source = _placements[item];
            RectInt target = CreatePlacement(item, origin);

            if (exchangedItem == null)
            {
                Vacate(item, source);
                Occupy(item, target);
                return true;
            }

            RectInt exchangedSource = _placements[exchangedItem];
            RectInt exchangedTarget = CreatePlacement(exchangedItem, source.position);
            Vacate(item, source);
            Vacate(exchangedItem, exchangedSource);
            Occupy(item, target);
            Occupy(exchangedItem, exchangedTarget);
            return true;
        }

        public bool CanMove(ItemInstance item, Vector2Int origin, out ItemInstance exchangedItem)
        {
            exchangedItem = null;

            if (item == null
                || item.BaseDefinition == null
                || !_placements.TryGetValue(item, out RectInt source))
            {
                return false;
            }

            RectInt target = CreatePlacement(item, origin);

            if (target == source || !IsInside(target))
            {
                return false;
            }

            List<ItemInstance> occupants = GetDistinctOccupants(target, item);

            if (occupants.Count == 0)
            {
                return CanPlace(target, item, null);
            }

            if (occupants.Count != 1)
            {
                return false;
            }

            ItemInstance occupant = occupants[0];
            RectInt exchangedTarget = CreatePlacement(occupant, source.position);

            if (!IsInside(exchangedTarget)
                || target.Overlaps(exchangedTarget)
                || !CanPlace(target, item, occupant)
                || !CanPlace(exchangedTarget, item, occupant))
            {
                return false;
            }

            exchangedItem = occupant;
            return true;
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

        public bool TryExchangeAt(ItemInstance itemToRemove, ItemInstance itemToAdd, Vector2Int origin)
        {
            if (!CanExchangeAt(itemToRemove, itemToAdd, origin))
            {
                return false;
            }

            RectInt source = _placements[itemToRemove];
            Vacate(itemToRemove, source);

            if (itemToAdd != null)
            {
                Occupy(itemToAdd, CreatePlacement(itemToAdd, origin));
            }

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

        public bool CanExchangeAt(ItemInstance itemToRemove, ItemInstance itemToAdd, Vector2Int origin)
        {
            if (itemToRemove == null || !_placements.ContainsKey(itemToRemove))
            {
                return false;
            }

            if (itemToAdd == null)
            {
                return true;
            }

            if (itemToAdd.BaseDefinition == null || _placements.ContainsKey(itemToAdd))
            {
                return false;
            }

            return CanPlace(CreatePlacement(itemToAdd, origin), itemToRemove, null);
        }

        public bool Remove(ItemInstance item)
        {
            if (item == null || !_placements.TryGetValue(item, out RectInt rect))
            {
                return false;
            }

            Vacate(item, rect);
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
                    RectInt candidate = new RectInt(originX, originY, itemWidth, itemHeight);

                    if (CanPlace(candidate, ignoredItem, null))
                    {
                        placement = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanPlace(RectInt rect, ItemInstance firstIgnoredItem, ItemInstance secondIgnoredItem)
        {
            if (!IsInside(rect))
            {
                return false;
            }

            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    ItemInstance occupiedItem = _cells[x, y];

                    if (occupiedItem != null
                        && occupiedItem != firstIgnoredItem
                        && occupiedItem != secondIgnoredItem)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        List<ItemInstance> GetDistinctOccupants(RectInt rect, ItemInstance ignoredItem)
        {
            List<ItemInstance> occupants = new List<ItemInstance>();

            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    ItemInstance occupant = _cells[x, y];

                    if (occupant != null && occupant != ignoredItem && !occupants.Contains(occupant))
                    {
                        occupants.Add(occupant);
                    }
                }
            }

            return occupants;
        }

        RectInt CreatePlacement(ItemInstance item, Vector2Int origin)
        {
            Vector2Int size = item.BaseDefinition.GridSize;
            return new RectInt(
                origin.x,
                origin.y,
                Mathf.Max(1, size.x),
                Mathf.Max(1, size.y));
        }

        bool IsInside(RectInt rect)
        {
            return rect.xMin >= 0
                && rect.yMin >= 0
                && rect.xMax <= Width
                && rect.yMax <= Height;
        }

        void Vacate(ItemInstance item, RectInt rect)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (_cells[x, y] == item)
                    {
                        _cells[x, y] = null;
                    }
                }
            }

            _placements.Remove(item);
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
