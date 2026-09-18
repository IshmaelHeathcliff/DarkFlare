using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace DarkFlare.Tests
{
    public sealed class MapTerrainPreviewTests
    {
        MapTestDefinitions _definitions;
        MapTerrainPreviewSession _session;

        [SetUp]
        public void SetUp()
        {
            _definitions = new MapTestDefinitions();
            _session = new MapTerrainPreviewSession();
        }

        [TearDown]
        public void TearDown()
        {
            _session.Dispose();
            _definitions.Dispose();
        }

        [Test]
        public void Tiles_ShareResourcesAndReleaseOnlyAfterLastOwner()
        {
            ContentId terrain = ContentId.Parse("terrain:test_dirt");
            var key = new TerrainWangKey(terrain, terrain, terrain, terrain);
            using var library = new DiagnosticTileLibrary();
            DiagnosticTileLibrary.Lease first = library.Acquire("rule_a", key, "a");
            DiagnosticTileLibrary.Lease second = library.Acquire("rule_a", key, "a");
            using DiagnosticTileLibrary.Lease changedRule = library.Acquire("rule_b", key, "a");
            Tile tile = first.Tile;
            Sprite sprite = tile.sprite;
            Texture2D texture = sprite.texture;
            Assert.AreSame(tile, second.Tile);
            Assert.AreNotSame(tile, changedRule.Tile);
            Assert.AreEqual(FilterMode.Point, texture.filterMode);
            Assert.AreEqual(1, texture.mipmapCount);
            Assert.AreEqual(Tile.ColliderType.None, tile.colliderType);
            Assert.AreEqual(Vector2.one * 32, sprite.pivot);
            Assert.AreEqual(64, sprite.pixelsPerUnit);
            first.Dispose();
            Assert.IsTrue(tile != null);
            second.Dispose();
            second.Dispose();
            Assert.IsTrue(tile == null && sprite == null && texture == null);
            library.Dispose();
            Assert.IsTrue(changedRule.Tile == null);
        }

        [Test]
        public void DiagnosticVariants_ChangeOnlyInteriorAndHaveOpaqueMatchingEdges()
        {
            using var library = new DiagnosticTileLibrary();
            foreach (ContentId terrain in MapTerrainPreviewFrame.GalleryTerrains)
            {
                var key = new TerrainWangKey(terrain, terrain, terrain, terrain);
                using var a = library.Acquire("gallery", key, "diagnostic_a");
                using var b = library.Acquire("gallery", key, "diagnostic_b");
                Color32[] first = a.Tile.sprite.texture.GetPixels32();
                Color32[] second = b.Tile.sprite.texture.GetPixels32();
                Assert.IsTrue(first.Where((color, i) => !color.Equals(second[i])).Any());
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        int i = x + y * 64;
                        Assert.AreEqual(255, first[i].a);
                        Assert.AreEqual(255, second[i].a);
                        if (x < 2 || x >= 62 || y < 2 || y >= 62)
                        {
                            Assert.AreEqual(DiagnosticTileLibrary.TerrainColor(terrain), first[i]);
                            Assert.AreEqual(first[i], second[i]);
                        }
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator MapFrame_UsesIsolatedPreviewSceneAndExactScaledBounds()
        {
            Scene active = SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            int roots = active.rootCount;
            int previews = EditorSceneManager.previewSceneCount;
            MapDocument source = _definitions.Document();
            source.Width = 35;
            source.Height = 33;
            source.CellSize = 0.5;
            source.Origin = new MapPoint { X = -13.25, Y = 7.5 };
            yield return _session.RebuildAsync(() => Capture(source)).ToCoroutine();
            Assert.IsTrue(_session.IsValid, _session.Status);
            MapTerrainPreviewFrame frame = _session.Current;
            Assert.IsTrue(EditorSceneManager.IsPreviewScene(frame.Camera.scene));
            Assert.AreEqual(frame.Camera.scene, frame.Tilemap.gameObject.scene);
            Assert.AreEqual(TilemapRenderer.Mode.Chunk, frame.Renderer.mode);
            Assert.AreEqual("Ground", frame.Renderer.sortingLayerName);
            Assert.That(frame.Renderer.bounds.min.x, Is.EqualTo(-13.5f).Within(0.0001));
            Assert.That(frame.Renderer.bounds.min.y, Is.EqualTo(7.25f).Within(0.0001));
            Assert.That(frame.Renderer.bounds.size.x, Is.EqualTo(18).Within(0.0001));
            Assert.That(frame.Renderer.bounds.size.y, Is.EqualTo(17).Within(0.0001));
            Assert.IsTrue(frame.TryInspect(new Vector2(-13.25f, 7.5f), out Vector2Int cell, out _, out _));
            Assert.AreEqual(new Vector2Int(-1, -1), cell);
            Assert.AreEqual(active, SceneManager.GetActiveScene());
            Assert.AreEqual(dirty, active.isDirty);
            Assert.AreEqual(roots, active.rootCount);
            Assert.AreEqual(1, frame.ResourceCount, "相同 Key / 变体不能按格分配纹理");
            _session.Dispose();
            Assert.AreEqual(previews, EditorSceneManager.previewSceneCount);
            Assert.IsTrue(frame.Tilemap == null && frame.Camera == null);
        }

        [UnityTest]
        public IEnumerator SourceFailure_KeepsStaleFrameAndRecoveryPublishesWholeSnapshot()
        {
            MapDocument source = _definitions.Document();
            yield return _session.RebuildAsync(() => Capture(source)).ToCoroutine();
            MapTerrainPreviewFrame before = _session.Current;
            yield return _session.RebuildAsync(() => throw new MapDocumentException("$", "损坏源")).ToCoroutine();
            Assert.IsFalse(_session.IsValid);
            Assert.AreSame(before, _session.Current);
            StringAssert.Contains("损坏源", _session.Status);
            source.DefaultTerrainId = "terrain:test_water";
            yield return _session.RebuildAsync(() => Capture(source)).ToCoroutine();
            Assert.IsTrue(_session.IsValid, _session.Status);
            Assert.IsTrue(before.Tilemap == null);
            Assert.AreEqual(ContentId.Parse("terrain:test_water"), _session.Current.Resolution.Map.DefaultTerrainId);
            Assert.AreEqual(1, _session.ResourceCount);
        }

        [UnityTest]
        public IEnumerator NewRequestAndDispose_CancelPendingFramesWithoutLatePublication()
        {
            int previews = EditorSceneManager.previewSceneCount;
            UniTask first = _session.RebuildAsync();
            Assert.IsTrue(_session.IsBuilding);
            MapDocument source = _definitions.Document();
            UniTask second = _session.RebuildAsync(() => Capture(source));
            yield return UniTask.WhenAll(first, second).ToCoroutine();
            Assert.IsTrue(_session.IsValid, _session.Status);
            Assert.IsFalse(_session.Current.IsGallery);
            Assert.AreEqual(1, _session.ResourceCount);
            UniTask pending = _session.RebuildAsync();
            _session.Dispose();
            yield return pending.ToCoroutine();
            Assert.IsNull(_session.Current);
            Assert.AreEqual(0, _session.ResourceCount);
            Assert.AreEqual(previews, EditorSceneManager.previewSceneCount);
        }

        [UnityTest]
        public IEnumerator UnchangedMap_ReusesTilesAcrossAtomicFrameReplacement()
        {
            MapDocument source = _definitions.Document();
            yield return _session.RebuildAsync(() => Capture(source)).ToCoroutine();
            TileBase tile = _session.Current.Tilemap.GetTile(Vector3Int.zero);
            source.Revision++;
            yield return _session.RebuildAsync(() => Capture(source)).ToCoroutine();
            Assert.IsTrue(_session.IsValid, _session.Status);
            Assert.IsEmpty(_session.Current.Resolution.RebuiltCells);
            Assert.AreSame(tile, _session.Current.Tilemap.GetTile(Vector3Int.zero));
            Assert.AreEqual(source.Revision, _session.Current.Resolution.Map.Revision);
        }

        (CompiledMapDocument, TerrainRuleSnapshot) Capture(MapDocument source)
        {
            var lookup = new MapDefinitionLookup(_definitions.Assets);
            return (MapDocumentCompiler.Compile(source, lookup), TerrainRuleSnapshot.Compile(_definitions.TerrainSet, lookup));
        }
    }
}
