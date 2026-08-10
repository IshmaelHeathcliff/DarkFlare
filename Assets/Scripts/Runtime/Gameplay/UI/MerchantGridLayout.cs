using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public static class MerchantGridLayout
    {
        public static IReadOnlyList<RectInt> Pack(
            int width,
            IReadOnlyList<Vector2Int> itemSizes,
            out int height)
        {
            int safeWidth = Mathf.Max(1, width);
            List<RectInt> placements = new List<RectInt>(itemSizes?.Count ?? 0);
            List<bool[]> rows = new List<bool[]>();

            if (itemSizes == null)
            {
                height = 0;
                return placements;
            }

            for (int i = 0; i < itemSizes.Count; i++)
            {
                Vector2Int requestedSize = itemSizes[i];
                Vector2Int size = new Vector2Int(
                    Mathf.Clamp(requestedSize.x, 1, safeWidth),
                    Mathf.Max(1, requestedSize.y));
                RectInt placement = FindPlacement(rows, safeWidth, size);
                Occupy(rows, safeWidth, placement);
                placements.Add(placement);
            }

            height = rows.Count;
            return placements;
        }

        static RectInt FindPlacement(List<bool[]> rows, int width, Vector2Int size)
        {
            for (int y = 0; ; y++)
            {
                EnsureRows(rows, width, y + size.y);

                for (int x = 0; x <= width - size.x; x++)
                {
                    RectInt candidate = new RectInt(x, y, size.x, size.y);

                    if (CanPlace(rows, candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        static bool CanPlace(List<bool[]> rows, RectInt placement)
        {
            for (int y = placement.yMin; y < placement.yMax; y++)
            {
                for (int x = placement.xMin; x < placement.xMax; x++)
                {
                    if (rows[y][x])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static void Occupy(List<bool[]> rows, int width, RectInt placement)
        {
            EnsureRows(rows, width, placement.yMax);

            for (int y = placement.yMin; y < placement.yMax; y++)
            {
                for (int x = placement.xMin; x < placement.xMax; x++)
                {
                    rows[y][x] = true;
                }
            }
        }

        static void EnsureRows(List<bool[]> rows, int width, int count)
        {
            while (rows.Count < count)
            {
                rows.Add(new bool[width]);
            }
        }
    }
}
