using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class MapDocumentTests
    {
        MapTestDefinitions _definitions;

        [SetUp]
        public void SetUp() { _definitions = new MapTestDefinitions(); }

        [TearDown]
        public void TearDown() { _definitions.Dispose(); }

        [Test]
        public void RoundTrip_PreservesCombinedObjectsRegionsAndDetachedSnapshot()
        {
            MapDocument source = _definitions.Document();
            source.Origin = new MapPoint { X = -13, Y = -20 };
            source.Objects = new[]
            {
                new MapObjectRecord
                {
                    ObjectId = "merchant", DefinitionId = "map_object:test_merchant", Position = new MapPoint { X = 2, Y = 3 }, Rotation = 90,
                    Interaction = new MapInteractionBinding { Kind = MapInteractionKind.Trader, ContentId = "trader:test_trader" }
                }
            };
            source.Regions = new[]
            {
                new MapRegionRecord
                {
                    RegionId = "square", DefinitionId = "map_region:test_safe", Priority = 5, Shape = MapRegionShape.Rectangle,
                    Points = new[] { new MapPoint { X = 0.5, Y = 0.5 }, new MapPoint { X = 4, Y = 4 } }
                }
            };
            CompiledMapDocument first = Compile(source);
            MapDocument copy = first.CreateEditableCopy();
            Assert.AreEqual(2, copy.Objects[0].Position.X, "对象仍使用局部世界坐标，origin 不得提前叠加");
            Assert.AreEqual(MapInteractionKind.Trader, copy.Objects[0].Interaction.Kind);
            Assert.AreEqual(5, copy.Regions[0].Priority);
            Assert.AreEqual(first.CanonicalJson, Compile(copy).CanonicalJson);
            source.TerrainPalette[0] = "terrain:corrupted";
            copy.Objects[0].ObjectId = "changed";
            Assert.AreEqual("terrain:test_dirt", first.GetTerrain(0, 0));
            Assert.AreEqual("merchant", first.CreateEditableCopy().Objects[0].ObjectId);
            StringAssert.DoesNotContain("\r", first.CanonicalJson);
        }

        [Test]
        public void CanonicalOrder_IsIndependentOfObjectInputOrder()
        {
            MapDocument document = _definitions.Document();
            MapObjectRecord first = _definitions.Decoration("first");
            MapObjectRecord second = _definitions.Decoration("second");
            document.Objects = new[] { second, first };
            CompiledMapDocument a = Compile(document);
            document.Objects = new[] { first, second };
            CompiledMapDocument b = Compile(document);
            Assert.AreEqual(a.CanonicalJson, b.CanonicalJson);
            Assert.AreEqual(a.SourceHash, b.SourceHash);
        }

        [Test]
        public void Chunks_UseRowMajorIndicesAndDefaultForMissingAndVirtualCells()
        {
            MapDocument document = _definitions.Document();
            document.Width = 35;
            document.Height = 33;
            document.DefaultTerrainId = "terrain:test_grass";
            int[] cells = Enumerable.Repeat(1, 1024).ToArray();
            cells[2] = 3;
            document.Chunks = new[] { new MapChunk { X = 1, Y = 1, Cells = cells } };
            CompiledMapDocument map = Compile(document);
            Assert.AreEqual("terrain:test_road", map.GetTerrain(34, 32));
            Assert.AreEqual("terrain:test_grass", map.GetTerrain(0, 0));
            Assert.AreEqual("terrain:test_grass", map.GetTerrain(-1, 32));
            Assert.AreEqual("terrain:test_grass", map.GetTerrain(35, 32));
            cells[3] = 0;
            Reject(document, ".cells[3]");
        }

        [Test]
        public void VariantOverride_MustMatchAllFourCornerMaterials()
        {
            MapDocument document = _definitions.Document();
            document.VariantOverrides = new[] { new MapVariantOverride { X = -1, Y = -1, VariantId = "dirt_a" } };
            Assert.DoesNotThrow(() => Compile(document));
            int[] cells = new int[1024];
            cells[0] = 1;
            document.Chunks = new[] { new MapChunk { Cells = cells } };
            Reject(document, "$.variantOverrides[0]");
        }

        [Test]
        public void Capacity_AcceptsDensePressureMapAndRejectsOverflowBeforeGridAllocation()
        {
            MapDocument document = _definitions.Document();
            document.Width = 256;
            document.Height = 256;
            document.Chunks = Enumerable.Range(0, 64).Select(i => new MapChunk
            {
                X = i % 8, Y = i / 8, Cells = Enumerable.Repeat(i % 4, 1024).ToArray()
            }).ToArray();
            CompiledMapDocument map = Compile(document);
            Assert.AreEqual("terrain:test_road", map.GetTerrain(255, 255));
            document.Width = int.MaxValue;
            document.Height = int.MaxValue;
            Reject(document, "$.width");
        }

        [TestCase("version", "$.formatVersion")]
        [TestCase("revision", "$.revision")]
        [TestCase("namespace", "$.mapId")]
        [TestCase("missing", "$.terrainSetId")]
        [TestCase("palette", "$.terrainPalette[1]")]
        [TestCase("default", "$.defaultTerrainId")]
        [TestCase("chunk", "$.chunks[1]")]
        [TestCase("index", "$.chunks[0].cells[0]")]
        [TestCase("object", "$.objects[1].objectId")]
        [TestCase("rotation", "$.objects[0].rotation")]
        [TestCase("binding", "$.objects[0].interaction")]
        [TestCase("coordinate", "$.origin.x")]
        public void SemanticErrors_ReportActionablePath(string mutation, string path)
        {
            MapDocument document = _definitions.Document();
            switch (mutation)
            {
                case "version": document.FormatVersion = 2; break;
                case "revision": document.Revision = -1; break;
                case "namespace": document.MapId = "item:test_map"; break;
                case "missing": document.TerrainSetId = "terrain_set:missing"; break;
                case "palette": document.TerrainPalette[1] = document.TerrainPalette[0]; break;
                case "default": document.DefaultTerrainId = "terrain:missing"; break;
                case "chunk": document.Chunks = new[] { new MapChunk { Cells = new int[1024] }, new MapChunk { Cells = new int[1024] } }; break;
                case "index": document.Chunks = new[] { new MapChunk { Cells = new int[1024] } }; document.Chunks[0].Cells[0] = 4; break;
                case "object": document.Objects = new[] { _definitions.Decoration("same"), _definitions.Decoration("same") }; break;
                case "rotation": document.Objects = new[] { _definitions.Decoration("one") }; document.Objects[0].Rotation = 45; break;
                case "binding": document.Objects = new[] { _definitions.Decoration("one") }; document.Objects[0].DefinitionId = "map_object:test_merchant"; break;
                case "coordinate": document.Origin.X = double.NaN; break;
            }
            Reject(document, path);
        }

        [TestCase("unknown")]
        [TestCase("duplicate")]
        [TestCase("missing")]
        [TestCase("null")]
        [TestCase("coercion")]
        [TestCase("nonfinite")]
        [TestCase("trailing")]
        [TestCase("depth")]
        public void Reader_RejectsAmbiguousOrMalformedExchange(string mutation)
        {
            string json = MapDocumentJson.Write(_definitions.Document());
            switch (mutation)
            {
                case "unknown": json = json.Replace("{\n", "{\n  \"$type\": \"Anything\",\n"); break;
                case "duplicate": json = json.Replace("\"revision\": 0", "\"revision\": 0, \"revision\": 1"); break;
                case "missing": json = json.Replace("  \"objects\": [],\n", ""); break;
                case "null": json = json.Replace("\"objects\": []", "\"objects\": null"); break;
                case "coercion": json = json.Replace("\"width\": 4", "\"width\": \"4\""); break;
                case "nonfinite": json = json.Replace("\"x\": 0.0", "\"x\": NaN"); break;
                case "trailing": json += "{}"; break;
                case "depth": json = new string('[', 20) + "0" + new string(']', 20); break;
            }
            Assert.Throws<MapDocumentException>(() => MapDocumentJson.Read(json), mutation);
        }

        [Test]
        public void Reader_RejectsBytesBeforeDecodeAndInvalidUtf8()
        {
            Assert.Throws<MapDocumentException>(() => MapDocumentJson.Read(new byte[MapDocumentLimits.MaxBytes + 1]));
            Assert.Throws<MapDocumentException>(() => MapDocumentJson.Read(new byte[] { 0xC3, 0x28 }));
        }

        [Test]
        public void Exchange_AcceptsUtf8BomAndProducesIdenticalOutputAcrossCultures()
        {
            MapDocument source = _definitions.Document();
            source.Origin.X = -1.25;
            string baseline = Compile(source).CanonicalJson;
            byte[] bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(MapDocumentJson.Encode(baseline)).ToArray();
            Assert.AreEqual(baseline, Compile(MapDocumentJson.Read(bytes)).CanonicalJson);
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.AreEqual(baseline, Compile(MapDocumentJson.Read(baseline)).CanonicalJson);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void Region_RejectsSelfIntersectionAndWrongTypedParameters()
        {
            MapDocument document = _definitions.Document();
            var region = new MapRegionRecord
            {
                RegionId = "zone", DefinitionId = "map_region:test_safe", Shape = MapRegionShape.Polygon,
                Points = new[] { new MapPoint(), new MapPoint { X = 4 }, new MapPoint { X = 4, Y = 4 }, new MapPoint { Y = 4 } }
            };
            document.Regions = new[] { region };
            Assert.DoesNotThrow(() => Compile(document));
            region.Points = new[] { region.Points[0], region.Points[2], region.Points[1], region.Points[3] };
            Reject(document, ".points");
            region.SpawnDefinitionId = "spawn:anything";
            Reject(document, ".spawnDefinitionId");
        }

        [Test]
        public void Lookup_RejectsDuplicateDefinitionInsteadOfSelectingFirst()
        {
            _definitions.Create<TerrainDefinition>("test_dirt");
            Reject(_definitions.Document(), "$.terrainSetId");
        }

        [Test]
        public void NewDefinitions_ResolveThroughExistingContentCatalog()
        {
            var importedMap = _definitions.Create<MapDefinition>("test_map");
            importedMap.Initialize(Compile(_definitions.Document()));
            ContentCatalogDefinition catalog = _definitions.Create<ContentCatalogDefinition>(null);
            MapTestDefinitions.Set(catalog, "_entries", _definitions.Assets.Where(asset => asset != catalog).ToList());
            ContentCatalogBuildResult result = ContentCatalog.Build(catalog);
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Issues));
            Assert.IsTrue(result.Catalog.TryResolve(ContentId.Parse("map:test_map"), out MapDefinition resolved));
            Assert.AreSame(importedMap, resolved);
        }

        CompiledMapDocument Compile(MapDocument document)
        {
            return MapDocumentCompiler.Compile(document, new MapDefinitionLookup(_definitions.Assets));
        }

        void Reject(MapDocument document, string path)
        {
            MapDocumentException error = Assert.Throws<MapDocumentException>(() => Compile(document));
            StringAssert.Contains(path, error.Message);
        }
    }

    // 仅在内存或测试隔离目录创建定义，不向正式 core 添加资产。
    sealed class MapTestDefinitions : IDisposable
    {
        public List<ScriptableObject> Assets { get; } = new List<ScriptableObject>();
        public TerrainDefinition Dirt { get; }
        public TerrainSetDefinition TerrainSet { get; }

        public MapTestDefinitions()
        {
            Dirt = Create<TerrainDefinition>("test_dirt");
            TerrainDefinition grass = Create<TerrainDefinition>("test_grass");
            TerrainDefinition water = Create<TerrainDefinition>("test_water");
            TerrainDefinition road = Create<TerrainDefinition>("test_road");
            Set(water, "_walkable", false);
            TerrainSet = Create<TerrainSetDefinition>("test_set");
            Set(TerrainSet, "_terrains", new[] { Dirt, grass, water, road });
            var variant = new TerrainTileVariant();
            Set(variant, "_id", "dirt_a");
            Set(variant, "_southWest", Dirt);
            Set(variant, "_southEast", Dirt);
            Set(variant, "_northWest", Dirt);
            Set(variant, "_northEast", Dirt);
            Set(TerrainSet, "_variants", new[] { variant });
            MapObjectDefinition merchant = Create<MapObjectDefinition>("test_merchant");
            Set(merchant, "_capabilities", MapObjectCapabilities.Obstacle | MapObjectCapabilities.Decoration | MapObjectCapabilities.Interaction);
            Set(merchant, "_interactionKind", MapInteractionKind.Trader);
            Create<MapObjectDefinition>("test_decoration");
            Create<TraderDefinition>("test_trader");
            MapRegionDefinition region = Create<MapRegionDefinition>("test_safe");
            Set(region, "_kind", MapRegionKind.Safe);
        }

        public MapDocument Document()
        {
            return new MapDocument
            {
                MapId = "map:test_map", Width = 4, Height = 4, TerrainSetId = "terrain_set:test_set",
                TerrainPalette = new[] { "terrain:test_dirt", "terrain:test_grass", "terrain:test_water", "terrain:test_road" },
                DefaultTerrainId = "terrain:test_dirt"
            };
        }

        public MapObjectRecord Decoration(string id)
        {
            return new MapObjectRecord { ObjectId = id, DefinitionId = "map_object:test_decoration", Position = new MapPoint { X = 2, Y = 2 } };
        }

        public T Create<T>(string id) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            if (id != null) { Set(asset, "_id", id); }
            Assets.Add(asset);
            return asset;
        }

        public static void Set(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, field);
            info.SetValue(target, value);
        }

        public void Dispose()
        {
            foreach (ScriptableObject asset in Assets)
            {
                if (asset != null && !UnityEditor.EditorUtility.IsPersistent(asset)) { UnityEngine.Object.DestroyImmediate(asset); }
            }
            Assets.Clear();
        }
    }
}
