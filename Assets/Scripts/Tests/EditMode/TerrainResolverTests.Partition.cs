using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed partial class TerrainResolverTests
    {
        [Test]
        public void Partition_AllKeysMatchIndependentAggregatedWeightsWithoutHoles()
        {
            foreach (TerrainWangKey key in AllKeys())
            {
                TerrainPartitionRaster raster = TerrainPartition.Rasterize(key, 9);
                for (int y = 0; y < 9; y++)
                {
                    for (int x = 0; x < 9; x++)
                    {
                        // 独立 decimal 参考，按材质分组而不是逐角竞争。
                        decimal u = x / 8m;
                        decimal v = y / 8m;
                        ContentId[] ids = { key.NorthWest, key.NorthEast, key.SouthEast, key.SouthWest };
                        decimal[] weights = { (1 - u) * v, u * v, u * (1 - v), (1 - u) * (1 - v) };
                        ContentId expected = Enumerable.Range(0, 4).GroupBy(i => ids[i])
                            .OrderByDescending(group => group.Sum(i => weights[i])).ThenBy(group => group.Key).First().Key;
                        Assert.AreEqual(expected, raster.GetTerrain(x, y), $"Key={key.GetHashCode()} ({x},{y})");
                    }
                }
                Assert.AreEqual(key.NorthWest, raster.GetTerrain(0, 8));
                Assert.AreEqual(key.NorthEast, raster.GetTerrain(8, 8));
                Assert.AreEqual(key.SouthEast, raster.GetTerrain(8, 0));
                Assert.AreEqual(key.SouthWest, raster.GetTerrain(0, 0));
            }
        }

        [Test]
        public void Partition_AllCompatibleEdgesMatchIndependentGlobalDirection()
        {
            TerrainPartitionRaster[] rasters = AllKeys().Select(key => TerrainPartition.Rasterize(key, 9)).ToArray();
            foreach (TerrainPartitionRaster a in rasters)
            {
                CheckEdge(a.GetEdge(TerrainEdge.North), a.Key.NorthWest, a.Key.NorthEast);
                CheckEdge(a.GetEdge(TerrainEdge.South), a.Key.SouthWest, a.Key.SouthEast);
                CheckEdge(a.GetEdge(TerrainEdge.West), a.Key.SouthWest, a.Key.NorthWest);
                CheckEdge(a.GetEdge(TerrainEdge.East), a.Key.SouthEast, a.Key.NorthEast);
                foreach (TerrainPartitionRaster b in rasters)
                {
                    if (a.Key.NorthEast == b.Key.NorthWest && a.Key.SouthEast == b.Key.SouthWest)
                    {
                        CollectionAssert.AreEqual(a.GetEdge(TerrainEdge.East), b.GetEdge(TerrainEdge.West));
                    }
                    if (a.Key.NorthWest == b.Key.SouthWest && a.Key.NorthEast == b.Key.SouthEast)
                    {
                        CollectionAssert.AreEqual(a.GetEdge(TerrainEdge.North), b.GetEdge(TerrainEdge.South));
                    }
                }
            }
        }

        [Test]
        public void Partition_TiesAreOrdinalAndLargeIntegerWeightsDoNotOverflow()
        {
            ContentId a = ContentId.Parse("terrain:a");
            ContentId z = ContentId.Parse("terrain:z");
            var saddle = new TerrainWangKey(z, a, z, a);
            Assert.AreEqual(a, TerrainPartition.Sample(saddle, 1, 1, 2));
            Assert.AreEqual(z, TerrainPartition.Sample(saddle, 0, int.MaxValue, int.MaxValue));
            var repeated = new TerrainWangKey(z, a, z, z);
            Assert.AreEqual(z, TerrainPartition.Sample(repeated, 1, 1, 2), "重复材质必须合并权重");
            Assert.AreEqual(a, TerrainPartition.Sample(repeated, int.MaxValue, int.MaxValue, int.MaxValue));
        }

        [Test]
        public void Partition_RejectsInvalidInputsAndExposesReadOnlyEdges()
        {
            TerrainWangKey key = AllKeys()[0];
            Assert.Throws<ArgumentException>(() => TerrainPartition.Sample(default, 0, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainPartition.Sample(key, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainPartition.Sample(key, -1, 0, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainPartition.Sample(key, 0, 9, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainPartition.Rasterize(key, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainPartition.Rasterize(key, 1025));
            TerrainPartitionRaster raster = TerrainPartition.Rasterize(key, 2);
            Assert.Throws<NotSupportedException>(() => ((IList<ContentId>)raster.GetEdge(TerrainEdge.East))[0] = default);
            Assert.Throws<ArgumentOutOfRangeException>(() => raster.GetEdge((TerrainEdge)4));
            Assert.Throws<ArgumentOutOfRangeException>(() => raster.GetTerrain(2, 0));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => TerrainPartition.Rasterize(key, 64, cancelled.Token));
        }

        static void CheckEdge(IReadOnlyList<ContentId> samples, ContentId start, ContentId end)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                ContentId expected = i * 2 < samples.Count - 1 ? start : end;
                if (i * 2 == samples.Count - 1) { expected = start.CompareTo(end) < 0 ? start : end; }
                Assert.AreEqual(expected, samples[i], "边只依赖两端材质和全局方向");
            }
        }

        static TerrainWangKey[] AllKeys()
        {
            ContentId[] ids = { ContentId.Parse("terrain:test_dirt"), ContentId.Parse("terrain:test_grass"),
                ContentId.Parse("terrain:test_water"), ContentId.Parse("terrain:test_road") };
            return Enumerable.Range(0, 256).Select(i => new TerrainWangKey(ids[i & 3], ids[(i >> 2) & 3], ids[(i >> 4) & 3], ids[(i >> 6) & 3])).ToArray();
        }
    }
}
