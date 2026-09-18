using System;
using UnityEngine;

namespace DarkFlare
{
    public static class DualGridCoordinates
    {
        public static bool ContainsLogical(CompiledMapDocument map, int x, int y)
        {
            if (map == null) { throw new ArgumentNullException(nameof(map)); }
            return x >= 0 && y >= 0 && x < map.Width && y < map.Height;
        }

        public static bool ContainsDisplay(CompiledMapDocument map, int x, int y)
        {
            if (map == null) { throw new ArgumentNullException(nameof(map)); }
            return x >= -1 && y >= -1 && x < map.Width && y < map.Height;
        }

        public static (double X, double Y) GetLogicalCenter(CompiledMapDocument map, int x, int y)
        {
            if (!ContainsLogical(map, x, y)) { throw new ArgumentOutOfRangeException(nameof(x), "逻辑格越界"); }
            return (map.OriginX + (x + 0.5) * map.CellSize, map.OriginY + (y + 0.5) * map.CellSize);
        }

        public static (double X, double Y) GetDisplayCenter(CompiledMapDocument map, int x, int y)
        {
            if (!ContainsDisplay(map, x, y)) { throw new ArgumentOutOfRangeException(nameof(x), "显示格越界"); }
            return (map.OriginX + (x + 1.0) * map.CellSize, map.OriginY + (y + 1.0) * map.CellSize);
        }

        // 返回格坐标而不夹到地图边缘；是否可用由 ContainsLogical 单独判断。
        public static Vector2Int WorldToLogical(CompiledMapDocument map, double worldX, double worldY)
        {
            if (map == null) { throw new ArgumentNullException(nameof(map)); }
            return new Vector2Int(ToCell((worldX - map.OriginX) / map.CellSize), ToCell((worldY - map.OriginY) / map.CellSize));
        }

        public static Vector2Int GetDisplayChunk(int x, int y)
        {
            return new Vector2Int(FloorDivide(x, MapDocumentLimits.ChunkSide), FloorDivide(y, MapDocumentLimits.ChunkSide));
        }

        public static Vector2Int[] GetAffectedDisplayCells(CompiledMapDocument map, int logicalX, int logicalY)
        {
            if (!ContainsLogical(map, logicalX, logicalY)) { throw new ArgumentOutOfRangeException(nameof(logicalX), "逻辑格越界"); }
            // 合法逻辑格的四个相邻显示格（含边界 -1）均在显示范围内。
            return new[]
            {
                new Vector2Int(logicalX - 1, logicalY - 1), new Vector2Int(logicalX, logicalY - 1),
                new Vector2Int(logicalX - 1, logicalY), new Vector2Int(logicalX, logicalY)
            };
        }

        public static TerrainWangKey Sample(CompiledMapDocument map, int x, int y)
        {
            if (!ContainsDisplay(map, x, y)) { throw new ArgumentOutOfRangeException(nameof(x), "显示格越界"); }
            return new TerrainWangKey(map.GetTerrainId(x, y + 1), map.GetTerrainId(x + 1, y + 1),
                map.GetTerrainId(x + 1, y), map.GetTerrainId(x, y));
        }

        static int FloorDivide(int value, int divisor)
        {
            int quotient = value / divisor;
            return value % divisor < 0 ? quotient - 1 : quotient;
        }

        static int ToCell(double value)
        {
            double cell = Math.Floor(value);
            if (double.IsNaN(cell) || cell < int.MinValue || cell > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "坐标非有限或超出整数格范围");
            }
            return (int)cell;
        }
    }
}
