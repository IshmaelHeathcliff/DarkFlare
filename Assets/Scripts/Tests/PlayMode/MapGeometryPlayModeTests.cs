using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class MapGeometryPlayModeTests
    {
        Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded) { yield return SceneManager.UnloadSceneAsync(_scene); }
        }

        [UnityTest]
        public IEnumerator PhysicalActor_StopsAtWaterAndBoundsAndRepeatedProjectionReleases()
        {
            var dirt = ScriptableObject.CreateInstance<TerrainDefinition>();
            var water = ScriptableObject.CreateInstance<TerrainDefinition>();
            var set = ScriptableObject.CreateInstance<TerrainSetDefinition>();
            Set(dirt, "_id", "physics_dirt"); Set(water, "_id", "physics_water"); Set(water, "_walkable", false);
            Set(set, "_id", "physics_set"); Set(set, "_terrains", new[] { dirt, water });
            var lookup = new MapDefinitionLookup(new ScriptableObject[] { dirt, water, set });
            var chunk = new MapChunk { Cells = new int[1024] };
            for (int y = 0; y < 6; y++) { chunk.Cells[y * 32] = 1; chunk.Cells[y * 32 + 1] = 1; }
            var doc = new MapDocument { MapId = "map:physics", Width = 6, Height = 6, TerrainSetId = "terrain_set:physics_set",
                TerrainPalette = new[] { "terrain:physics_dirt", "terrain:physics_water" }, DefaultTerrainId = "terrain:physics_dirt", Chunks = new[] { chunk } };
            Scene scene = SceneManager.CreateScene("Geometry lifecycle " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            _scene = scene;
            try
            {
                MapGeometrySnapshot geometry = MapGeometryCompiler.Compile(MapGeometryInput.Capture(MapDocumentCompiler.Compile(doc, lookup), lookup));
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    using var projection = new MapGeometryProjection(geometry, scene);
                    yield return projection.PopulateAsync().ToCoroutine();
                    var actor = new GameObject("Geometry test actor");
                    SceneManager.MoveGameObjectToScene(actor, scene);
                    actor.layer = LayerMask.NameToLayer(GameplayPhysicsLayers.PlayerActor);
                    var circle = actor.AddComponent<CircleCollider2D>(); circle.radius = 0.32f;
                    var body = actor.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true;
                    body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    try
                    {
                        body.position = new Vector2(4, 3);
                        Physics2D.SyncTransforms();
                        PhysicsScene2D physics = scene.GetPhysicsScene2D();
                        for (int step = 0; step < 100; step++) { body.linearVelocity = Vector2.left * 2; physics.Simulate(0.02f); }
                        Assert.That(body.position.x, Is.InRange(2.31f, 2.36f), "真实圆 Collider 应在同源水岸停止");
                        for (int step = 0; step < 100; step++) { body.linearVelocity = Vector2.right * 2; physics.Simulate(0.02f); }
                        Assert.That(body.position.x, Is.InRange(5.64f, 5.69f), "逻辑边界应阻止进入外侧视觉半格");
                    }
                    finally { UnityEngine.Object.Destroy(actor); projection.Dispose(); }
                    yield return null;
                    Assert.IsTrue(projection.Root == null);
                    Assert.AreEqual(0, scene.GetRootGameObjects().Length);
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(dirt); UnityEngine.Object.Destroy(water); UnityEngine.Object.Destroy(set);
            }
            yield return null;
        }

        static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
