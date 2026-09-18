using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct TerrainResolvedCell
    {
        public Vector2Int Coordinates { get; }
        public TerrainVariantSelection Selection { get; }

        internal TerrainResolvedCell(Vector2Int coordinates, TerrainVariantSelection selection)
        {
            Coordinates = coordinates;
            Selection = selection;
        }
    }

    public sealed class TerrainResolution
    {
        readonly TerrainVariantSelection[] _cells;

        public CompiledMapDocument Map { get; }
        public string RuleHash { get; }
        public string PartitionRevision { get; }
        public int CellCount => _cells.Length;
        public bool IsFullRebuild { get; }
        public IReadOnlyList<Vector2Int> RebuiltCells { get; }
        public double ComparisonMilliseconds { get; }
        public double ResolutionMilliseconds { get; }

        internal TerrainResolution(CompiledMapDocument map, string ruleHash, TerrainVariantSelection[] cells,
            bool fullRebuild, Vector2Int[] rebuiltCells, double comparisonMilliseconds, double resolutionMilliseconds)
        {
            Map = map;
            RuleHash = ruleHash;
            PartitionRevision = TerrainPartition.Revision;
            _cells = cells;
            IsFullRebuild = fullRebuild;
            RebuiltCells = Array.AsReadOnly(rebuiltCells);
            ComparisonMilliseconds = comparisonMilliseconds;
            ResolutionMilliseconds = resolutionMilliseconds;
        }

        public TerrainResolvedCell GetCell(int x, int y)
        {
            if (!DualGridCoordinates.ContainsDisplay(Map, x, y)) { throw new ArgumentOutOfRangeException(nameof(x), "显示格越界"); }
            return new TerrainResolvedCell(new Vector2Int(x, y), _cells[x + 1 + (y + 1) * (Map.Width + 1)]);
        }

        internal TerrainVariantSelection[] PrepareCells(bool changed)
        {
            // 零变化可共享不可变数组；有变化先复制，取消 / 异常不会污染已发布结果。
            return changed ? (TerrainVariantSelection[])_cells.Clone() : _cells;
        }
    }

    public static class TerrainResolver
    {
        public static TerrainResolution Resolve(CompiledMapDocument map, TerrainRuleSnapshot rules,
            TerrainResolution previous = null, CancellationToken cancellationToken = default)
        {
            if (map == null) { throw new ArgumentNullException(nameof(map)); }
            if (rules == null) { throw new ArgumentNullException(nameof(rules)); }
            if (map.TerrainSetId != rules.TerrainSetId) { throw new MapDocumentException("$.terrainSetId", "地图与规则快照的地表集不匹配"); }
            cancellationToken.ThrowIfCancellationRequested();
            var timer = Stopwatch.StartNew();
            bool fullRebuild = RequiresFullRebuild(map, rules, previous);
            Vector2Int[] dirty = FindChanges(map, previous?.Map, fullRebuild, cancellationToken);
            double comparisonMilliseconds = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            TerrainVariantSelection[] cells = fullRebuild
                ? new TerrainVariantSelection[(map.Width + 1) * (map.Height + 1)]
                : previous.PrepareCells(dirty.Length != 0);
            for (int i = 0; i < dirty.Length; i++)
            {
                if ((i & 127) == 0) { cancellationToken.ThrowIfCancellationRequested(); }
                Vector2Int coordinate = dirty[i];
                cells[coordinate.x + 1 + (coordinate.y + 1) * (map.Width + 1)] = rules.Resolve(map, coordinate.x, coordinate.y);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new TerrainResolution(map, rules.RuleHash, cells, fullRebuild, dirty,
                comparisonMilliseconds, timer.Elapsed.TotalMilliseconds);
        }

        static bool RequiresFullRebuild(CompiledMapDocument map, TerrainRuleSnapshot rules, TerrainResolution previous)
        {
            if (previous == null || previous.RuleHash != rules.RuleHash || previous.PartitionRevision != TerrainPartition.Revision) { return true; }
            CompiledMapDocument before = previous.Map;
            return before.MapId != map.MapId || before.Width != map.Width || before.Height != map.Height
                || before.OriginX != map.OriginX || before.OriginY != map.OriginY || before.CellSize != map.CellSize
                || before.Seed != map.Seed || before.TerrainSetId != map.TerrainSetId || before.DefaultTerrainId != map.DefaultTerrainId
                || !before.TerrainPalette.SequenceEqual(map.TerrainPalette);
        }

        static Vector2Int[] FindChanges(CompiledMapDocument map, CompiledMapDocument before, bool fullRebuild, CancellationToken cancellationToken)
        {
            if (!fullRebuild && before.SourceHash == map.SourceHash) { return Array.Empty<Vector2Int>(); }
            int stride = map.Width + 1;
            var affected = new HashSet<int>();
            if (!fullRebuild)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (before.GetTerrainId(x, y) == map.GetTerrainId(x, y)) { continue; }
                        // 四邻均在合法显示范围内，包括 -1；索引采用 +1 平移。
                        int index = x + y * stride;
                        affected.Add(index);
                        affected.Add(index + 1);
                        affected.Add(index + stride);
                        affected.Add(index + stride + 1);
                    }
                }
            }
            var dirty = new List<Vector2Int>(fullRebuild ? stride * (map.Height + 1) : affected.Count);
            for (int y = -1; y < map.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = -1; x < map.Width; x++)
                {
                    bool changed = fullRebuild || affected.Contains(x + 1 + (y + 1) * stride);
                    if (!changed)
                    {
                        before.TryGetVariantOverride(x, y, out string oldVariant);
                        map.TryGetVariantOverride(x, y, out string newVariant);
                        changed = !string.Equals(oldVariant, newVariant, StringComparison.Ordinal);
                    }
                    if (changed) { dirty.Add(new Vector2Int(x, y)); }
                }
            }
            return dirty.ToArray();
        }
    }
}
