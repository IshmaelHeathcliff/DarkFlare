using System;
using System.Collections.Generic;
using System.Threading;

namespace DarkFlare
{
    public enum TerrainEdge { North, East, South, West }

    // 仅表示技术显示归属；鞍点的单个采样归属不代表足迹连通性。
    public static class TerrainPartition
    {
        public static string Revision => "bilinear-ordinal-integer-v1";

        public static ContentId Sample(TerrainWangKey key, int u, int v, int denominator)
        {
            if (!key.IsValid) { throw new ArgumentException("需要有效的四角", nameof(key)); }
            if (denominator <= 0 || u < 0 || v < 0 || u > denominator || v > denominator)
            {
                throw new ArgumentOutOfRangeException(nameof(denominator), "采样坐标必须位于 0..denominator，分母必须为正");
            }
            return SampleUnchecked(key, u, v, denominator);
        }

        public static TerrainPartitionRaster Rasterize(TerrainWangKey key, int size, CancellationToken cancellationToken = default)
        {
            if (!key.IsValid) { throw new ArgumentException("需要有效的四角", nameof(key)); }
            if (size < 2 || size > 1024) { throw new ArgumentOutOfRangeException(nameof(size), "诊断栅格边长必须为 2..1024"); }
            cancellationToken.ThrowIfCancellationRequested();
            var cells = new ContentId[size * size];
            for (int y = 0; y < size; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < size; x++) { cells[x + y * size] = SampleUnchecked(key, x, y, size - 1); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new TerrainPartitionRaster(key, size, cells);
        }

        static ContentId SampleUnchecked(TerrainWangKey key, int u, int v, int denominator)
        {
            // 先转 Int64；四角权重之和为 denominator²，Int32 最大分母也不会溢出。
            long nw = (long)(denominator - u) * v;
            long ne = (long)u * v;
            long se = (long)u * (denominator - v);
            long sw = (long)(denominator - u) * (denominator - v);
            ContentId owner = key.NorthWest;
            long best = Weight(key, owner, nw, ne, se, sw);
            Consider(key, key.NorthEast, nw, ne, se, sw, ref owner, ref best);
            Consider(key, key.SouthEast, nw, ne, se, sw, ref owner, ref best);
            Consider(key, key.SouthWest, nw, ne, se, sw, ref owner, ref best);
            return owner;
        }

        static long Weight(TerrainWangKey key, ContentId terrain, long nw, long ne, long se, long sw)
        {
            return (key.NorthWest == terrain ? nw : 0) + (key.NorthEast == terrain ? ne : 0)
                + (key.SouthEast == terrain ? se : 0) + (key.SouthWest == terrain ? sw : 0);
        }

        static void Consider(TerrainWangKey key, ContentId candidate, long nw, long ne, long se, long sw, ref ContentId owner, ref long best)
        {
            long weight = Weight(key, candidate, nw, ne, se, sw);
            if (weight > best || (weight == best && candidate.CompareTo(owner) < 0))
            {
                owner = candidate;
                best = weight;
            }
        }
    }

    public sealed class TerrainPartitionRaster
    {
        readonly ContentId[] _cells;
        readonly IReadOnlyList<ContentId>[] _edges;

        public TerrainWangKey Key { get; }
        public int Size { get; }
        public string PartitionRevision => TerrainPartition.Revision;

        internal TerrainPartitionRaster(TerrainWangKey key, int size, ContentId[] cells)
        {
            Key = key;
            Size = size;
            _cells = cells;
            _edges = new IReadOnlyList<ContentId>[4];
            for (int edge = 0; edge < 4; edge++)
            {
                var samples = new ContentId[size];
                for (int i = 0; i < size; i++)
                {
                    samples[i] = (TerrainEdge)edge switch
                    {
                        TerrainEdge.North => GetTerrain(i, size - 1),
                        TerrainEdge.East => GetTerrain(size - 1, i),
                        TerrainEdge.South => GetTerrain(i, 0),
                        _ => GetTerrain(0, i)
                    };
                }
                _edges[edge] = Array.AsReadOnly(samples);
            }
        }

        public ContentId GetTerrain(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) { throw new ArgumentOutOfRangeException(nameof(x)); }
            return _cells[x + y * Size];
        }

        // 水平边左→右，垂直边下→上；不是绕四边顺时针排列。
        public IReadOnlyList<ContentId> GetEdge(TerrainEdge edge)
        {
            if ((int)edge < 0 || (int)edge >= _edges.Length) { throw new ArgumentOutOfRangeException(nameof(edge)); }
            return _edges[(int)edge];
        }
    }
}
