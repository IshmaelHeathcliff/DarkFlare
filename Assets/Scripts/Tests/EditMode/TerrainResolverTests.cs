using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class TerrainResolverTests
    {
        MapTestDefinitions _definitions;

        [SetUp]
        public void SetUp() { _definitions = new MapTestDefinitions(); }

        [TearDown]
        public void TearDown() { _definitions.Dispose(); }

        [Test]
        public void Coordinates_ApplyOriginAndHalfCellOnceAndUseFloorOutsideMap()
        {
            MapDocument source = _definitions.Document();
            source.Origin = new MapPoint { X = -13.25, Y = -9.5 };
            source.CellSize = 0.5;
            CompiledMapDocument map = Compile(source);
            Assert.AreEqual((-13.0, -9.25), DualGridCoordinates.GetLogicalCenter(map, 0, 0));
            Assert.AreEqual((-13.25, -9.5), DualGridCoordinates.GetDisplayCenter(map, -1, -1));
            Assert.AreEqual((-11.25, -7.5), DualGridCoordinates.GetDisplayCenter(map, 3, 3));
            Assert.AreEqual(new Vector2Int(-1, -1), DualGridCoordinates.WorldToLogical(map, -13.2501, -9.5001));
            Assert.AreEqual(new Vector2Int(4, 4), DualGridCoordinates.WorldToLogical(map, -11.25, -7.5));
            Assert.IsFalse(DualGridCoordinates.ContainsLogical(map, 4, 4));
            Assert.IsTrue(DualGridCoordinates.ContainsDisplay(map, -1, -1));
            Assert.IsFalse(DualGridCoordinates.ContainsDisplay(map, -2, 0));
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    (double worldX, double worldY) = DualGridCoordinates.GetLogicalCenter(map, x, y);
                    Assert.AreEqual(new Vector2Int(x, y), DualGridCoordinates.WorldToLogical(map, worldX, worldY));
                }
            }
        }

        [TestCase(-33, -2)]
        [TestCase(-32, -1)]
        [TestCase(-1, -1)]
        [TestCase(0, 0)]
        [TestCase(31, 0)]
        [TestCase(32, 1)]
        [TestCase(int.MinValue, -67108864)]
        public void DisplayChunk_UsesFloorDivision(int coordinate, int expected)
        {
            Assert.AreEqual(new Vector2Int(expected, expected), DualGridCoordinates.GetDisplayChunk(coordinate, coordinate));
        }

        [Test]
        public void Coordinates_RejectNonFiniteOverflowAndInvalidDisplayCoordinates()
        {
            CompiledMapDocument map = Compile(_definitions.Document());
            Assert.Throws<ArgumentOutOfRangeException>(() => DualGridCoordinates.WorldToLogical(map, double.NaN, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DualGridCoordinates.WorldToLogical(map, double.PositiveInfinity, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DualGridCoordinates.WorldToLogical(map, (double)int.MaxValue + 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DualGridCoordinates.Sample(map, map.Width, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DualGridCoordinates.GetLogicalCenter(map, -1, 0));
        }

        [Test]
        public void ChangedLogicalCell_IdentifiesFourAdjacentDisplayCellsIncludingNegativeEdge()
        {
            CompiledMapDocument map = Compile(_definitions.Document());
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 0) },
                DualGridCoordinates.GetAffectedDisplayCells(map, 0, 0));
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(2, 3), new Vector2Int(3, 3) },
                DualGridCoordinates.GetAffectedDisplayCells(map, 3, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => DualGridCoordinates.GetAffectedDisplayCells(map, 4, 0));
        }

        [Test]
        public void FourMaterials_AllCombinationsPreserveClockwiseCornersAndDistinctIdentity()
        {
            MapDocument source = _definitions.Document();
            source.Width = 2;
            source.Height = 2;
            source.Chunks = new[] { new MapChunk { Cells = new int[1024] } };
            ContentId[] ids = source.TerrainPalette.Select(ContentId.Parse).ToArray();
            var keys = new HashSet<TerrainWangKey>();
            for (int encoded = 0; encoded < 256; encoded++)
            {
                int nw = encoded & 3;
                int ne = (encoded >> 2) & 3;
                int se = (encoded >> 4) & 3;
                int sw = (encoded >> 6) & 3;
                source.Chunks[0].Cells[32] = nw;
                source.Chunks[0].Cells[33] = ne;
                source.Chunks[0].Cells[1] = se;
                source.Chunks[0].Cells[0] = sw;
                TerrainWangKey key = DualGridCoordinates.Sample(Compile(source), 0, 0);
                Assert.AreEqual(ids[nw], key.NorthWest);
                Assert.AreEqual(ids[ne], key.NorthEast);
                Assert.AreEqual(ids[se], key.SouthEast);
                Assert.AreEqual(ids[sw], key.SouthWest);
                Assert.IsTrue(keys.Add(key), "不同组合不能合并为同一 Key");
            }
        }

        [Test]
        public void BinaryMasks_CoverAllSixteenPatternsInNwNeSeSwOrder()
        {
            ContentId dirt = ContentId.Parse("terrain:test_dirt");
            ContentId water = ContentId.Parse("terrain:test_water");
            for (int mask = 0; mask < 16; mask++)
            {
                var key = new TerrainWangKey((mask & 1) != 0 ? dirt : water, (mask & 2) != 0 ? dirt : water,
                    (mask & 4) != 0 ? dirt : water, (mask & 8) != 0 ? dirt : water);
                Assert.AreEqual(mask, key.GetMask(dirt));
                Assert.AreEqual(15 ^ mask, key.GetMask(water));
                Assert.AreEqual(0, key.GetMask(ContentId.Parse("terrain:other")));
            }
        }

        [Test]
        public void Sampling_CrossesSourceChunksAndUsesDefaultOnlyOutsideMap()
        {
            MapDocument source = _definitions.Document();
            source.Width = 35;
            source.Height = 33;
            source.Chunks = Enumerable.Range(0, 4).Select(i => new MapChunk { X = i % 2, Y = i / 2, Cells = new int[1024] }).ToArray();
            source.Chunks[0].Cells[31 + 31 * 32] = 1;
            source.Chunks[1].Cells[31 * 32] = 2;
            source.Chunks[2].Cells[31] = 3;
            CompiledMapDocument map = Compile(source);
            TerrainWangKey center = DualGridCoordinates.Sample(map, 31, 31);
            Assert.AreEqual(ContentId.Parse("terrain:test_road"), center.NorthWest);
            Assert.AreEqual(ContentId.Parse("terrain:test_dirt"), center.NorthEast);
            Assert.AreEqual(ContentId.Parse("terrain:test_water"), center.SouthEast);
            Assert.AreEqual(ContentId.Parse("terrain:test_grass"), center.SouthWest);
            Assert.AreEqual(15, DualGridCoordinates.Sample(map, 34, 32).GetMask(map.DefaultTerrainId));
            source.Chunks[0].Cells[0] = 1;
            TerrainWangKey border = DualGridCoordinates.Sample(Compile(source), -1, -1);
            Assert.AreEqual(2, border.GetMask(ContentId.Parse("terrain:test_grass")));
        }

        [Test]
        public void MapSnapshot_ExposesDetachedHeaderAndOverrideData()
        {
            MapDocument source = _definitions.Document();
            source.Seed = -9;
            source.Revision = 7;
            source.Origin.X = -10;
            source.VariantOverrides = new[] { new MapVariantOverride { X = -1, Y = -1, VariantId = "dirt_a" } };
            CompiledMapDocument map = Compile(source);
            source.Origin.X = 99;
            source.Seed = 99;
            source.VariantOverrides[0].VariantId = "changed";
            map.CreateEditableCopy().VariantOverrides[0].X = 2;
            Assert.AreEqual(-9, map.Seed);
            Assert.AreEqual(7, map.Revision);
            Assert.AreEqual(-10, map.OriginX);
            Assert.IsTrue(map.TryGetVariantOverride(-1, -1, out string id));
            Assert.AreEqual("dirt_a", id);
            Assert.IsFalse(map.TryGetVariantOverride(2, -1, out _));
        }

        [Test]
        public void StableHash_MatchesIndependentUtf8LittleEndianVector()
        {
            var key = new TerrainWangKey(ContentId.Parse("terrain:test_dirt"), ContentId.Parse("terrain:test_grass"),
                ContentId.Parse("terrain:test_water"), ContentId.Parse("terrain:test_road"));
            ContentId set = ContentId.Parse("terrain_set:test_set");
            // 外部 BigInt FNV-1a 参考：153 字节，长度前缀 / Int32 小端，包含负 seed 和坐标。
            Assert.AreEqual(0x03A5643BDC5A01E8UL, TerrainVariantHash.Compute(-123, -1, 32, set, key));
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.AreEqual(0x03A5643BDC5A01E8UL, TerrainVariantHash.Compute(-123, -1, 32, set, key));
            }
            finally { CultureInfo.CurrentCulture = previous; }
            Assert.AreNotEqual(TerrainVariantHash.Compute(-123, -1, 32, set, key), TerrainVariantHash.Compute(-123, 1, 32, set, key));
        }

        [Test]
        public void Selection_IsStableAcrossCandidateOrderAndUnrelatedMapEdits()
        {
            TerrainTileVariant first = _definitions.TerrainSet.Variants[0];
            TerrainTileVariant second = Variant("dirt_b", _definitions.Dirt);
            MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { second, first });
            TerrainRuleSnapshot before = Rules();
            MapDocument source = _definitions.Document();
            source.Seed = 121;
            CompiledMapDocument original = Compile(source);
            string[] choices = Enumerable.Range(0, 16).Select(i => before.Resolve(original, i % 4, i / 4).VariantId).ToArray();
            CollectionAssert.AreEquivalent(new[] { "dirt_a", "dirt_b" }, choices.Distinct().ToArray());
            MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { first, second });
            TerrainRuleSnapshot after = Rules();
            Assert.AreEqual(before.RuleHash, after.RuleHash);
            for (int i = 0; i < 16; i++) { Assert.AreEqual(choices[i], after.Resolve(original, i % 4, i / 4).VariantId); }
            source.Revision++;
            source.Objects = new[] { _definitions.Decoration("tree") };
            source.Chunks = new[] { new MapChunk { Cells = new int[1024] } };
            source.Chunks[0].Cells[3 + 3 * 32] = 1;
            CompiledMapDocument edited = Compile(source);
            Assert.AreNotEqual(original.SourceHash, edited.SourceHash);
            Assert.AreEqual(before.Resolve(original, 0, 0).VariantId, after.Resolve(edited, 0, 0).VariantId);
        }

        [Test]
        public void Override_WinsSelectionAndIsRejectedAgainstChangedRules()
        {
            MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { Variant("dirt_a", _definitions.Dirt), Variant("dirt_b", _definitions.Dirt) });
            MapDocument source = _definitions.Document();
            source.VariantOverrides = new[] { new MapVariantOverride { X = 0, Y = 0, VariantId = "dirt_b" } };
            CompiledMapDocument map = Compile(source);
            TerrainRuleSnapshot before = Rules();
            Assert.AreEqual("dirt_b", before.Resolve(map, 0, 0).VariantId);
            Assert.IsTrue(before.Resolve(map, 0, 0).IsOverride);
            MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { Variant("dirt_a", _definitions.Dirt) });
            TerrainRuleSnapshot after = Rules();
            Assert.AreNotEqual(before.RuleHash, after.RuleHash);
            Assert.Throws<MapDocumentException>(() => after.Resolve(map, 0, 0));
        }

        [Test]
        public void MissingCandidates_PreserveTheKeyWithoutInventingAVariant()
        {
            MapDocument source = _definitions.Document();
            source.DefaultTerrainId = "terrain:test_grass";
            CompiledMapDocument map = Compile(source);
            TerrainVariantSelection selection = Rules().Resolve(map, 0, 0);
            Assert.IsFalse(selection.HasVariant);
            Assert.IsFalse(selection.IsOverride);
            Assert.IsNull(selection.VariantId);
            Assert.AreEqual(15, selection.Key.GetMask(ContentId.Parse("terrain:test_grass")));
            Assert.IsEmpty(map.CreateEditableCopy().VariantOverrides);
        }

        [Test]
        public void RulesAndKeys_SupportSixtyFourMaterialsAndRejectInvalidNamespaces()
        {
            var terrains = _definitions.TerrainSet.Terrains.ToList();
            for (int i = terrains.Count; i < 64; i++) { terrains.Add(_definitions.Create<TerrainDefinition>("material_" + i)); }
            MapTestDefinitions.Set(_definitions.TerrainSet, "_terrains", terrains.ToArray());
            TerrainDefinition last = terrains[63];
            MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { Variant("last_a", last) });
            MapDocument source = _definitions.Document();
            source.TerrainPalette = terrains.Select(terrain => "terrain:" + terrain.Id).ToArray();
            source.DefaultTerrainId = "terrain:material_63";
            CompiledMapDocument map = Compile(source);
            TerrainRuleSnapshot rules = Rules();
            Assert.AreEqual("last_a", rules.Resolve(map, 0, 0).VariantId);
            ContentId id = ContentId.Parse("terrain:material_63");
            Assert.Throws<ArgumentException>(() => new TerrainWangKey(ContentId.Parse("item:material_63"), id, id, id));
            Assert.Throws<MapDocumentException>(() => rules.GetCandidates(default));
            var unknown = ContentId.Parse("terrain:not_in_set");
            Assert.Throws<MapDocumentException>(() => rules.GetCandidates(new TerrainWangKey(unknown, id, id, id)));
            source.TerrainSetId = "terrain_set:different";
            TerrainSetDefinition different = _definitions.Create<TerrainSetDefinition>("different");
            MapTestDefinitions.Set(different, "_terrains", terrains.ToArray());
            CompiledMapDocument wrongMap = Compile(source);
            Assert.Throws<MapDocumentException>(() => rules.Resolve(wrongMap, 0, 0));
        }

        [Test]
        public void RuleSnapshot_IsImmutableAndHashTracksCandidateBindingChanges()
        {
            CompiledMapDocument map = Compile(_definitions.Document());
            TerrainRuleSnapshot original = Rules();
            TerrainWangKey dirt = DualGridCoordinates.Sample(map, 0, 0);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)original.GetCandidates(dirt))[0] = "changed");
            TerrainDefinition grass = _definitions.TerrainSet.Terrains[1];
            MapTestDefinitions.Set(_definitions.TerrainSet, "_variants", new[] { Variant("dirt_a", grass) });
            TerrainRuleSnapshot changed = Rules();
            Assert.AreNotEqual(original.RuleHash, changed.RuleHash);
            Assert.IsFalse(changed.Resolve(map, 0, 0).HasVariant);
            _definitions.Dispose();
            Assert.AreEqual("dirt_a", original.Resolve(map, 0, 0).VariantId, "快照不能依赖已销毁或已修改的 SO");
        }

        CompiledMapDocument Compile(MapDocument source)
        {
            return MapDocumentCompiler.Compile(source, new MapDefinitionLookup(_definitions.Assets));
        }

        TerrainRuleSnapshot Rules()
        {
            return TerrainRuleSnapshot.Compile(_definitions.TerrainSet, new MapDefinitionLookup(_definitions.Assets));
        }

        static TerrainTileVariant Variant(string id, TerrainDefinition terrain)
        {
            var variant = new TerrainTileVariant();
            MapTestDefinitions.Set(variant, "_id", id);
            MapTestDefinitions.Set(variant, "_northWest", terrain);
            MapTestDefinitions.Set(variant, "_northEast", terrain);
            MapTestDefinitions.Set(variant, "_southEast", terrain);
            MapTestDefinitions.Set(variant, "_southWest", terrain);
            return variant;
        }
    }
}
