using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class MapGeometryTests
    {
        MapTestDefinitions _definitions;
        [SetUp] public void SetUp() { _definitions = new MapTestDefinitions(); }
        [TearDown] public void TearDown() { _definitions.Dispose(); }
        MapGeometrySnapshot Build(MapDocument doc, int subdivisions = 4)
        {
            var lookup = new MapDefinitionLookup(_definitions.Assets);
            return MapGeometryCompiler.Compile(MapGeometryInput.Capture(MapDocumentCompiler.Compile(doc, lookup), lookup, new MapGeometrySettings(subdivisions)));
        }
        MapObjectRecord Obstacle(string id, double x, double y, float width, float height, int rotation = 0)
        {
            MapObjectDefinition definition = _definitions.Create<MapObjectDefinition>(id);
            MapTestDefinitions.Set(definition, "_capabilities", MapObjectCapabilities.Obstacle);
            MapTestDefinitions.Set(definition, "_footprint", new Vector2(width, height));
            return new MapObjectRecord { ObjectId = id, DefinitionId = "map_object:" + id, Position = new MapPoint { X = x, Y = y }, Rotation = rotation };
        }

        [TestCase(0.30)] [TestCase(0.32)] [TestCase(0.36)]
        public void StraightChannel_OneUnitSupportsRealRadiiAndNarrowChannelRejects(double radius)
        {
            MapDocument doc = _definitions.Document();
            doc.Objects = new[] { Obstacle("left", 0.75, 2, 1.5f, 4), Obstacle("right", 3.25, 2, 1.5f, 4) };
            MapGeometrySnapshot geometry = Build(doc);
            var profile = new MapGroundProfile(radius);
            Assert.AreEqual(MapGeometryResult.Valid, geometry.CheckSegment(new MapGeometryPoint(2, 0.5), new MapGeometryPoint(2, 3.5), profile));
            Assert.AreEqual(MapGeometryResult.Valid, geometry.CheckEdge(7, 6, 7, 7, profile, out int cost));
            Assert.AreEqual(10, cost);
            Assert.AreEqual(MapGeometryResult.Blocked, geometry.CheckPoint(new MapGeometryPoint(1.5 + radius - 0.001, 2), profile));
            Assert.AreEqual(MapGeometryResult.Blocked, geometry.CheckSegment(new MapGeometryPoint(2, 2), new MapGeometryPoint(3.5, 2), profile));
        }

        [Test]
        public void SweptCircle_RejectsThinWallEvenWhenBothEndpointsAreLegal()
        {
            MapDocument doc = _definitions.Document();
            doc.Objects = new[] { Obstacle("wall", 2, 2, 0.02f, 2) };
            MapGeometrySnapshot geometry = Build(doc);
            var a = new MapGeometryPoint(1, 2); var b = new MapGeometryPoint(3, 2); var profile = new MapGroundProfile(0.3);
            Assert.AreEqual(MapGeometryResult.Valid, geometry.CheckPoint(a, profile));
            Assert.AreEqual(MapGeometryResult.Valid, geometry.CheckPoint(b, profile));
            Assert.AreEqual(MapGeometryResult.Blocked, geometry.CheckSegment(a, b, profile));
            Assert.AreEqual(MapGeometryResult.Valid, geometry.CheckSegment(new MapGeometryPoint(1, 0.5), new MapGeometryPoint(3, 0.5), profile));
        }

        [Test]
        public void Diagonal_RequiresOrthogonalNodesAndSweptClearance()
        {
            MapDocument doc = _definitions.Document();
            doc.Objects = new[] { Obstacle("corner", 1.375, 1.125, 0.04f, 0.04f) };
            MapGeometrySnapshot geometry = Build(doc);
            Assert.AreEqual(MapGeometryResult.Valid, geometry.CheckSegment(geometry.GetNode(4, 4), geometry.GetNode(5, 5), new MapGroundProfile(0.02, 0)));
            Assert.AreEqual(MapGeometryResult.Blocked, geometry.CheckEdge(4, 4, 5, 5, new MapGroundProfile(0.02, 0), out _));
        }

        [Test]
        public void Geometry_UsesPixelCoverageAcrossAllWangKeysIncludingSaddles()
        {
            var profile = new MapGroundProfile(0.001, 0);
            for (int encoded = 0; encoded < 256; encoded++)
            {
                MapDocument doc = _definitions.Document();
                var chunk = new MapChunk { Cells = new int[1024] };
                chunk.Cells[32] = encoded & 3; chunk.Cells[33] = encoded >> 2 & 3;
                chunk.Cells[1] = encoded >> 4 & 3; chunk.Cells[0] = encoded >> 6 & 3;
                doc.Chunks = new[] { chunk };
                MapGeometrySnapshot geometry = Build(doc);
                TerrainWangKey key = DualGridCoordinates.Sample(MapDocumentCompiler.Compile(doc, new MapDefinitionLookup(_definitions.Assets)), 0, 0);
                for (int py = 0; py < 64; py += 7)
                {
                    for (int px = 0; px < 64; px += 7)
                    {
                        bool water = TerrainPartition.Sample(key, px, py, 63).LocalId == "test_water";
                        var p = new MapGeometryPoint(0.5 + (px + 0.5) / 64, 0.5 + (py + 0.5) / 64);
                        Assert.AreEqual(water ? MapGeometryResult.Blocked : MapGeometryResult.Valid, geometry.CheckPoint(p, profile), $"key={encoded},pixel={px},{py}");
                    }
                }
            }
        }

        [Test]
        public void Transform_UsesWorldFootprintAndLogicalRegionsExactlyOnce()
        {
            MapDocument doc = _definitions.Document();
            doc.CellSize = 0.5; doc.Origin = new MapPoint { X = -13.25, Y = 7.5 };
            doc.Objects = new[] { Obstacle("rotated", 1, 1, 0.2f, 1, 90) };
            doc.Regions = new[] { new MapRegionRecord { RegionId = "safe", DefinitionId = "map_region:test_safe",
                Points = new[] { new MapPoint { X = 1, Y = 1 }, new MapPoint { X = 3, Y = 3 } } } };
            MapGeometrySnapshot geometry = Build(doc);
            Assert.AreEqual(-12.75, geometry.Rectangles[0].MinX, 1e-7);
            Assert.AreEqual(8.4, geometry.Rectangles[0].MinY, 1e-7);
            Assert.AreEqual(-12.75, geometry.Regions[0].Points[0].X, 1e-7);
            Assert.AreEqual(MapGeometryResult.Valid, geometry.CheckPoint(new MapGeometryPoint(-12.25, 8.1), new MapGroundProfile(0.05)));
            Assert.AreEqual(MapGeometryResult.Outside, geometry.CheckPoint(new MapGeometryPoint(-13.3, 8), new MapGroundProfile(0.05)));
        }

        [Test]
        public void Hash_DependsOnRulesAndGeometryButNotVisualSeedOrRevision()
        {
            MapDocument doc = _definitions.Document();
            MapGeometrySnapshot first = Build(doc);
            doc.Seed = 123; doc.Revision = 12;
            doc.VariantOverrides = new[] { new MapVariantOverride { X = 1, Y = 1, VariantId = "dirt_a" } };
            Assert.AreEqual(first.GeometryHash, Build(doc).GeometryHash);
            MapTestDefinitions.Set(_definitions.Dirt, "_movementCost", 7);
            MapGeometrySnapshot cost = Build(doc);
            Assert.AreNotEqual(first.GeometryHash, cost.GeometryHash);
            Assert.AreEqual(MapGeometryResult.Valid, cost.CheckEdge(5, 5, 6, 6, new MapGroundProfile(0.3), out int edgeCost));
            Assert.AreEqual(98, edgeCost);
            MapTestDefinitions.Set(_definitions.Dirt, "_walkable", false);
            MapGeometrySnapshot blocked = Build(doc);
            Assert.AreNotEqual(cost.GeometryHash, blocked.GeometryHash);
            Assert.AreEqual(MapGeometryResult.Blocked, blocked.CheckPoint(new MapGeometryPoint(2, 2), new MapGroundProfile(0.3)));
            Assert.AreEqual(MapGeometryResult.Valid, first.CheckPoint(new MapGeometryPoint(2, 2), new MapGroundProfile(0.3)));
        }

        [Test]
        public void Landing_IsBoundedDeterministicAndDoesNotPromiseConnectivity()
        {
            MapDocument doc = _definitions.Document();
            doc.Objects = new[] { Obstacle("wall", 2, 2, 0.2f, 4) };
            MapGeometrySnapshot geometry = Build(doc);
            var profile = new MapGroundProfile(0.3);
            var requested = new MapGeometryPoint(2, 2);
            Assert.AreEqual(MapGeometryResult.BudgetExceeded, geometry.FindPoint(requested, profile, 2, 1, out _));
            Assert.AreEqual(MapGeometryResult.NoCandidate, geometry.FindPoint(requested, profile, 0.05, 10, out _));
            Assert.AreEqual(MapGeometryResult.Valid, geometry.FindPoint(requested, profile, 1, 100, out MapGeometryPoint point));
            Assert.Less(point.X, 2);
            Assert.AreEqual(MapGeometryResult.Valid, geometry.CheckPoint(point, profile));
            Assert.AreEqual(MapGeometryResult.Blocked, geometry.CheckSegment(point, new MapGeometryPoint(3, 2), profile));
        }

        [Test]
        public void Spawn_RequiresWholeFootprintAndSafeRegionsOverridePriorityWithoutBlockingWalking()
        {
            MapRegionDefinition spawn = _definitions.Create<MapRegionDefinition>("test_spawn_region");
            MapTestDefinitions.Set(spawn, "_kind", MapRegionKind.Spawn);
            _definitions.Create<MonsterSpawnDefinition>("test_spawn");
            MapDocument doc = _definitions.Document();
            doc.Regions = new[]
            {
                new MapRegionRecord { RegionId = "spawn", DefinitionId = "map_region:test_spawn_region", Priority = 100,
                    SpawnDefinitionId = "spawn:test_spawn", Points = new[] { new MapPoint(), new MapPoint { X = 4, Y = 4 } } },
                new MapRegionRecord { RegionId = "safe", DefinitionId = "map_region:test_safe", Priority = -1,
                    Points = new[] { new MapPoint { X = 1, Y = 1 }, new MapPoint { X = 2, Y = 2 } } }
            };
            MapGeometrySnapshot geometry = Build(doc);
            var p = new MapGeometryPoint(2.2, 1.5); var profile = new MapGroundProfile(0.3);
            Assert.AreEqual(MapGeometryResult.Valid, geometry.CheckPoint(p, profile));
            Assert.AreEqual(MapGeometryResult.RegionRejected, geometry.CheckPoint(p, profile, "spawn"));
            Assert.AreEqual(MapGeometryResult.Valid, geometry.CheckPoint(new MapGeometryPoint(3, 3), profile, "spawn"));
            Assert.AreEqual(MapGeometryResult.RegionRejected, geometry.CheckPoint(new MapGeometryPoint(3, 3), profile, "missing"));
            Assert.AreEqual("spawn", geometry.Regions[0].Id);
        }

        [Test]
        public void LimitsInvalidInputsAndCancellation_RejectWithoutPublishingPartialData()
        {
            MapDocument doc = _definitions.Document();
            var lookup = new MapDefinitionLookup(_definitions.Assets);
            CompiledMapDocument map = MapDocumentCompiler.Compile(doc, lookup);
            Assert.Throws<MapGeometryCapacityException>(() => MapGeometryInput.Capture(map, lookup, new MapGeometrySettings(maxNodes: 1)));
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            Assert.Throws<OperationCanceledException>(() => MapGeometryCompiler.Compile(MapGeometryInput.Capture(map, lookup), cancel.Token));
            MapGeometrySnapshot geometry = Build(doc);
            Assert.AreEqual(MapGeometryResult.InvalidInput, geometry.CheckPoint(new MapGeometryPoint(double.NaN, 1), new MapGroundProfile(0.3)));
            Assert.AreEqual(MapGeometryResult.InvalidInput, geometry.CheckPoint(new MapGeometryPoint(1, 1), default));
            Assert.AreEqual(MapGeometryResult.BudgetExceeded, geometry.CheckSegment(new MapGeometryPoint(1, 1), new MapGeometryPoint(3, 3), new MapGroundProfile(0.3), 1));
            Assert.Throws<OperationCanceledException>(() => geometry.FindPoint(new MapGeometryPoint(1, 1), new MapGroundProfile(0.3), 1, 100, out _, token: cancel.Token));
        }

        [UnityTest]
        public IEnumerator PhysicsProjection_WaterAndBoundarySkinNeverAdmitOverlappingFootprints()
        {
            MapDocument doc = _definitions.Document();
            doc.Width = 8; doc.Height = 8; doc.CellSize = 0.5; doc.Origin = new MapPoint { X = -4.25, Y = 7.5 };
            var chunk = new MapChunk { Cells = new int[1024] };
            for (int y = 0; y < 8; y++) { for (int x = 0; x < 8; x++) { chunk.Cells[x + y * 32] = x < 4 && y > 2 ? 2 : 0; } }
            doc.Chunks = new[] { chunk };
            MapGeometrySnapshot geometry = Build(doc);
            Scene scene = EditorSceneManager.NewPreviewScene();
            using var projection = new MapGeometryProjection(geometry, scene);
            try
            {
                yield return projection.PopulateAsync().ToCoroutine();
                Physics2D.SyncTransforms();
                PhysicsScene2D physics = scene.GetPhysicsScene2D();
                var profile = new MapGroundProfile(0.36);
                int valid = 0;
                for (int y = 1; y < 100; y++)
                {
                    for (int x = 1; x < 100; x++)
                    {
                        var p = new MapGeometryPoint(-4.25 + x * 0.04, 7.5 + y * 0.04);
                        if (geometry.CheckPoint(p, profile) != MapGeometryResult.Valid) { continue; }
                        valid++;
                        Assert.IsNull(physics.OverlapCircle(new Vector2((float)p.X, (float)p.Y), 0.36f), $"footprint {x},{y}");
                    }
                }
                Assert.Greater(valid, 100);
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                {
                    doc.Origin.X = 100000;
                    using var invalid = new MapGeometryProjection(Build(doc), scene);
                });
            }
            finally { projection.Dispose(); EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator PreviewGeometry_CancelFailureAndDisposeRetainOnlyCompleteFrames()
        {
            MapDocument doc = _definitions.Document();
            var lookup = new MapDefinitionLookup(_definitions.Assets);
            CompiledMapDocument map = MapDocumentCompiler.Compile(doc, lookup);
            TerrainRuleSnapshot rules = TerrainRuleSnapshot.Compile(_definitions.TerrainSet, lookup);
            using var session = new MapTerrainPreviewSession();
            yield return session.RebuildAsync(() => (map, rules), captureGeometry: m => MapGeometryInput.Capture(m, lookup)).ToCoroutine();
            Assert.IsTrue(session.IsValid, session.Status);
            MapTerrainPreviewFrame old = session.Current;
            Assert.IsNotNull(old.Projection);
            Assert.AreEqual(map.SourceHash, old.Geometry.SourceHash);
            UniTask pending = session.RebuildAsync(() => (map, rules), captureGeometry: m => MapGeometryInput.Capture(m, lookup));
            session.Cancel();
            yield return pending.ToCoroutine();
            Assert.AreSame(old, session.Current);
            Assert.IsFalse(session.IsValid);
            yield return session.RebuildAsync(() => (map, rules), captureGeometry: _ => throw new MapGeometryCapacityException("capacity")).ToCoroutine();
            Assert.AreSame(old, session.Current);
            Assert.IsFalse(session.IsValid);
            session.Dispose();
            Assert.IsTrue(old.Projection.Root == null);
            Assert.IsNull(session.Current);
        }

        [UnityTest]
        public IEnumerator PhysicsProjection_PreservesHoleUnionSweptQueriesAndReleasesObjects()
        {
            MapDocument doc = _definitions.Document();
            doc.Objects = new[] { Obstacle("west", 1, 2, 0.25f, 2), Obstacle("east", 3, 2, 0.25f, 2),
                Obstacle("north", 2, 3, 2.25f, 0.25f), Obstacle("south", 2, 1, 2.25f, 0.25f) };
            MapGeometrySnapshot geometry = Build(doc);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var projection = new MapGeometryProjection(geometry, scene);
            try
            {
                yield return projection.PopulateAsync().ToCoroutine();
                Physics2D.SyncTransforms();
                PhysicsScene2D physics = scene.GetPhysicsScene2D();
                Assert.IsTrue(physics.IsValid());
                Assert.IsNull(physics.OverlapCircle(new Vector2(2, 2), 0.3f));
                Assert.IsNotNull(physics.OverlapCircle(new Vector2(1, 2), 0.3f));
                Assert.IsNotNull(physics.OverlapCircle(new Vector2(0, 2), 0.3f));
                var random = new System.Random(41);
                for (int i = 0; i < 200; i++)
                {
                    var a = new MapGeometryPoint(0.4 + random.NextDouble() * 3.2, 0.4 + random.NextDouble() * 3.2);
                    var b = new MapGeometryPoint(0.4 + random.NextDouble() * 3.2, 0.4 + random.NextDouble() * 3.2);
                    if (geometry.CheckSegment(a, b, new MapGroundProfile(0.3)) != MapGeometryResult.Valid) { continue; }
                    var start = new Vector2((float)a.X, (float)a.Y); var delta = new Vector2((float)(b.X - a.X), (float)(b.Y - a.Y));
                    Assert.IsNull(physics.CircleCast(start, 0.3f, delta.normalized, delta.magnitude).collider, $"sweep {i}");
                }
                projection.Dispose();
                Assert.IsTrue(projection.Root == null);
            }
            finally { projection.Dispose(); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
