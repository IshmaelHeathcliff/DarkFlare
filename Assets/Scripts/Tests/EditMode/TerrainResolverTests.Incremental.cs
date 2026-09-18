using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed partial class TerrainResolverTests
    {
        [Test]
        public void Resolution_FullBuildCoversBorderAndKeepsMissingVariantsExplicit()
        {
            MapDocument source = _definitions.Document();
            source.DefaultTerrainId = "terrain:test_water";
            CompiledMapDocument map = Compile(source);
            TerrainResolution result = TerrainResolver.Resolve(map, Rules());
            Assert.IsTrue(result.IsFullRebuild);
            Assert.AreEqual((map.Width + 1) * (map.Height + 1), result.CellCount);
            Assert.AreEqual(result.CellCount, result.RebuiltCells.Distinct().Count());
            foreach (Vector2Int coordinate in result.RebuiltCells)
            {
                TerrainResolvedCell cell = result.GetCell(coordinate.x, coordinate.y);
                Assert.AreEqual(coordinate, cell.Coordinates);
                Assert.AreEqual(DualGridCoordinates.Sample(map, coordinate.x, coordinate.y), cell.Selection.Key);
                Assert.IsFalse(cell.Selection.HasVariant);
                Assert.AreEqual(map.DefaultTerrainId, TerrainPartition.Sample(cell.Selection.Key, 1, 1, 2));
            }
            Assert.AreEqual(Rules().RuleHash, result.RuleHash);
            Assert.Throws<ArgumentOutOfRangeException>(() => result.GetCell(-2, 0));
            Assert.Throws<NotSupportedException>(() => ((IList<Vector2Int>)result.RebuiltCells)[0] = default);
            Assert.Throws<NotSupportedException>(() => ((IList<ContentId>)map.TerrainPalette)[0] = default);
        }

        [Test]
        public void Resolution_LocalChangesAcrossChunksAndBorderMatchFullBuildAndPreserveOldSnapshot()
        {
            MapDocument source = LargeMap();
            TerrainRuleSnapshot rules = Rules();
            TerrainResolution original = TerrainResolver.Resolve(Compile(source), rules);
            source.Chunks[0].Cells[31 + 31 * 32] = 1;
            source.Chunks[1].Cells[31 * 32] = 2;
            source.Chunks[2].Cells[31] = 3;
            source.Chunks[0].Cells[0] = 2;
            CompiledMapDocument changed = Compile(source);
            TerrainResolution local = TerrainResolver.Resolve(changed, rules, original);
            var expected = new HashSet<Vector2Int>();
            foreach (Vector2Int logical in new[] { new Vector2Int(31, 31), new Vector2Int(32, 31), new Vector2Int(31, 32), Vector2Int.zero })
            {
                expected.UnionWith(DualGridCoordinates.GetAffectedDisplayCells(changed, logical.x, logical.y));
            }
            Assert.IsFalse(local.IsFullRebuild);
            CollectionAssert.AreEquivalent(expected, local.RebuiltCells);
            AssertEquivalent(TerrainResolver.Resolve(changed, rules), local);
            Assert.AreEqual(15, original.GetCell(31, 31).Selection.Key.GetMask(ContentId.Parse("terrain:test_dirt")));
            Assert.AreNotEqual(original.GetCell(31, 31).Selection.Key, local.GetCell(31, 31).Selection.Key);
            // 连续版本继续从局部快照比较，删除块后恢复缺省地表。
            source.Chunks = Array.Empty<MapChunk>();
            TerrainResolution restored = TerrainResolver.Resolve(Compile(source), rules, local);
            AssertEquivalent(original, restored);
        }

        [Test]
        public void Resolution_OverrideAddChangeRemoveOnlyRebuildsItsDisplayCell()
        {
            MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { Variant("dirt_a", _definitions.Dirt), Variant("dirt_b", _definitions.Dirt) });
            TerrainRuleSnapshot rules = Rules();
            MapDocument source = _definitions.Document();
            TerrainResolution previous = TerrainResolver.Resolve(Compile(source), rules);
            foreach (string id in new[] { "dirt_a", "dirt_b", null })
            {
                source.VariantOverrides = id == null ? Array.Empty<MapVariantOverride>()
                    : new[] { new MapVariantOverride { X = -1, Y = -1, VariantId = id } };
                CompiledMapDocument map = Compile(source);
                TerrainResolution result = TerrainResolver.Resolve(map, rules, previous);
                Assert.IsFalse(result.IsFullRebuild);
                CollectionAssert.AreEqual(new[] { new Vector2Int(-1, -1) }, result.RebuiltCells);
                Assert.AreEqual(id != null, result.GetCell(-1, -1).Selection.IsOverride);
                if (id != null) { Assert.AreEqual(id, result.GetCell(-1, -1).Selection.VariantId); }
                AssertEquivalent(TerrainResolver.Resolve(map, rules), result);
                previous = result;
            }
        }

        [Test]
        public void Resolution_UnrelatedEditsAndReorderedCandidatesDoNotRebuildTerrain()
        {
            TerrainTileVariant a = Variant("dirt_a", _definitions.Dirt);
            TerrainTileVariant b = Variant("dirt_b", _definitions.Dirt);
            MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { a, b });
            MapDocument source = _definitions.Document();
            TerrainResolution original = TerrainResolver.Resolve(Compile(source), Rules());
            MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { b, a });
            source.Revision++;
            source.Objects = new[] { _definitions.Decoration("tree") };
            source.Regions = new[] { new MapRegionRecord { RegionId = "safe", DefinitionId = "map_region:test_safe",
                Points = new[] { new MapPoint(), new MapPoint { X = 4, Y = 4 } } } };
            CompiledMapDocument edited = Compile(source);
            TerrainResolution result = TerrainResolver.Resolve(edited, Rules(), original);
            Assert.IsFalse(result.IsFullRebuild);
            Assert.IsEmpty(result.RebuiltCells);
            Assert.AreSame(edited, result.Map, "零地表变化也要发布最新源身份");
            Assert.AreNotEqual(original.Map.SourceHash, result.Map.SourceHash);
            AssertEquivalent(original, result);
            TerrainResolution repeated = TerrainResolver.Resolve(edited, Rules(), result);
            Assert.IsEmpty(repeated.RebuiltCells);
        }

        [TestCase("width")]
        [TestCase("height")]
        [TestCase("origin_x")]
        [TestCase("origin_y")]
        [TestCase("cell_size")]
        [TestCase("seed")]
        [TestCase("default")]
        [TestCase("palette")]
        [TestCase("map_id")]
        [TestCase("terrain_set")]
        [TestCase("rules")]
        public void Resolution_DependentHeaderOrRuleChangesInvalidateAllCells(string change)
        {
            MapDocument source = _definitions.Document();
            TerrainResolution original = TerrainResolver.Resolve(Compile(source), Rules());
            TerrainRuleSnapshot rules = Rules();
            switch (change)
            {
                case "width": source.Width++; break;
                case "height": source.Height++; break;
                case "origin_x": source.Origin.X = -2; break;
                case "origin_y": source.Origin.Y = 3; break;
                case "cell_size": source.CellSize = 0.5; break;
                case "seed": source.Seed++; break;
                case "default": source.DefaultTerrainId = "terrain:test_grass"; break;
                case "palette": Array.Reverse(source.TerrainPalette); break;
                case "map_id": source.MapId = "map:other"; break;
                case "terrain_set":
                    TerrainSetDefinition other = _definitions.Create<TerrainSetDefinition>("other");
                    MapTestDefinitions.Set(other, "_terrains", _definitions.TerrainSet.Terrains.ToArray());
                    source.TerrainSetId = "terrain_set:other";
                    rules = TerrainRuleSnapshot.Compile(other, new MapDefinitionLookup(_definitions.Assets));
                    break;
                case "rules":
                    MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { Variant("dirt_b", _definitions.Dirt) });
                    rules = Rules();
                    break;
            }
            CompiledMapDocument map = Compile(source);
            TerrainResolution result = TerrainResolver.Resolve(map, rules, original);
            Assert.IsTrue(result.IsFullRebuild);
            Assert.AreEqual(result.CellCount, result.RebuiltCells.Count);
            AssertEquivalent(TerrainResolver.Resolve(map, rules), result);
        }

        [Test]
        public void Resolution_CancellationAndLateInvalidOverrideDoNotMutatePublishedResult()
        {
            MapDocument source = _definitions.Document();
            source.VariantOverrides = new[] { new MapVariantOverride { X = 3, Y = 3, VariantId = "dirt_a" } };
            CompiledMapDocument map = Compile(source);
            TerrainResolution original = TerrainResolver.Resolve(map, Rules());
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => TerrainResolver.Resolve(map, Rules(), original, cancelled.Token));
            MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { Variant("dirt_b", _definitions.Dirt) });
            TerrainRuleSnapshot changedRules = Rules();
            Assert.Throws<MapDocumentException>(() => TerrainResolver.Resolve(map, changedRules, original));
            foreach (Vector2Int coordinate in original.RebuiltCells)
            {
                Assert.AreEqual("dirt_a", original.GetCell(coordinate.x, coordinate.y).Selection.VariantId);
            }
            Assert.IsTrue(original.GetCell(3, 3).Selection.IsOverride);
            Assert.AreEqual(map.SourceHash, original.Map.SourceHash);
        }

        MapDocument LargeMap()
        {
            MapDocument source = _definitions.Document();
            source.Width = 35;
            source.Height = 33;
            source.Origin = new MapPoint { X = -12.5, Y = 7 };
            source.Chunks = Enumerable.Range(0, 4).Select(i => new MapChunk { X = i % 2, Y = i / 2, Cells = new int[1024] }).ToArray();
            return source;
        }

        static void AssertEquivalent(TerrainResolution expected, TerrainResolution actual)
        {
            Assert.AreEqual(expected.CellCount, actual.CellCount);
            for (int y = -1; y < expected.Map.Height; y++)
            {
                for (int x = -1; x < expected.Map.Width; x++)
                {
                    TerrainResolvedCell a = expected.GetCell(x, y);
                    TerrainResolvedCell b = actual.GetCell(x, y);
                    Assert.AreEqual(a.Coordinates, b.Coordinates);
                    Assert.AreEqual(a.Selection.Key, b.Selection.Key);
                    Assert.AreEqual(a.Selection.VariantId, b.Selection.VariantId);
                    Assert.AreEqual(a.Selection.IsOverride, b.Selection.IsOverride);
                }
            }
        }
    }
}
