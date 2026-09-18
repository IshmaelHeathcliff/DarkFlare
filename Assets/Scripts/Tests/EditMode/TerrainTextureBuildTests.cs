using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DarkFlare.Tests
{
    public sealed class TerrainTextureBuildTests
    {
        string _root;
        string _output;
        TerrainDefinition _terrain;
        TerrainSetDefinition _set;
        TerrainTextureSources _sources;
        AddressableAssetSettings _addressableSettings;

        [SetUp]
        public void SetUp()
        {
            string id = "texture_test_" + Guid.NewGuid().ToString("N");
            _root = "Assets/__" + id;
            AssetDatabase.CreateFolder("Assets", "__" + id);
            _terrain = ScriptableObject.CreateInstance<TerrainDefinition>();
            MapTestDefinitions.Set(_terrain, "_id", id);
            AssetDatabase.CreateAsset(_terrain, _root + "/terrain.asset");
            _set = ScriptableObject.CreateInstance<TerrainSetDefinition>();
            MapTestDefinitions.Set(_set, "_id", id);
            MapTestDefinitions.Set(_set, "_terrains", new[] { _terrain });
            AssetDatabase.CreateAsset(_set, _root + "/set.asset");
            WriteSource(40);
            var binding = new TerrainMaterialSource();
            MapTestDefinitions.Set(binding, "_terrain", _terrain);
            MapTestDefinitions.Set(binding, "_source", AssetDatabase.LoadAssetAtPath<Texture2D>(_root + "/source.png"));
            _sources = ScriptableObject.CreateInstance<TerrainTextureSources>();
            MapTestDefinitions.Set(_sources, "_terrainSet", _set);
            MapTestDefinitions.Set(_sources, "_materials", new[] { binding });
            AssetDatabase.CreateAsset(_sources, _root + "/sources.asset");
            _output = "Assets/Art/Tiles/Environment/WangCompiled/" + AssetDatabase.AssetPathToGUID(_root + "/sources.asset");
        }

        [TearDown]
        public void TearDown()
        {
            if (_addressableSettings != null)
            {
                foreach (AddressableAssetGroup group in _addressableSettings.groups.ToArray()) { Object.DestroyImmediate(group); }
                Object.DestroyImmediate(_addressableSettings);
            }
            AssetDatabase.DeleteAsset(_output);
            AssetDatabase.DeleteAsset(_root);
        }

        [Test]
        public void Compile_ReusesRevisionAndSourceEditInvalidatesWithoutReplacingOldOutput()
        {
            TerrainTileCatalog first = Build();
            Assert.AreSame(first, _sources.RequireCurrent());
            Assert.AreSame(first, Build());
            string oldPath = AssetDatabase.GetAssetPath(first);
            WriteSource(90);
            Assert.Throws<InvalidOperationException>(() => _sources.RequireCurrent());
            TerrainTileCatalog second = Build();
            Assert.AreNotEqual(first.Revision, second.Revision);
            Assert.AreSame(second, _sources.RequireCurrent());
            Assert.AreSame(first, AssetDatabase.LoadAssetAtPath<TerrainTileCatalog>(oldPath));
            Assert.IsTrue(first.Entries[0].Tile.sprite != null);
        }

        [Test]
        public void CancelAfterTileWrite_RemovesOnlyNewRevisionAndPreservesPublishedAssets()
        {
            TerrainTileCatalog first = Build();
            WriteSource(110);
            string pending = _output + "/" + TerrainTextureBuild.Capture(_sources).Revision;
            using var cancellation = new CancellationTokenSource();
            var progress = new InlineProgress(_ => cancellation.Cancel());
            Assert.Throws<OperationCanceledException>(() => TerrainTextureBuild.Compile(_sources, cancellation.Token, false, progress));
            Assert.AreEqual(1, progress.Reports);
            Assert.IsFalse(Directory.Exists(pending));
            Assert.AreSame(first, _sources.Compiled);
            Assert.IsTrue(first.Entries[0].Tile.sprite != null);
            Assert.IsTrue(File.Exists(AssetDatabase.GetAssetPath(first)));
            Assert.AreNotSame(first, Build());
        }

        [Test]
        public void BuildFailureAndCorruptExistingRevision_NeverOverwritePublishedOutput()
        {
            TerrainTileCatalog first = Build();
            WriteSource(70);
            string pending = _output + "/" + TerrainTextureBuild.Capture(_sources).Revision;
            Assert.Throws<InvalidOperationException>(() => TerrainTextureBuild.Compile(_sources, showProgress: false,
                progress: new InlineProgress(_ => throw new InvalidOperationException("consumer failure"))));
            Assert.IsFalse(Directory.Exists(pending));
            Assert.AreSame(first, _sources.Compiled);
            WriteSource(40);
            string png = AssetDatabase.GetAssetPath(first.Entries[0].Tile.sprite);
            byte[] corrupted = { 1, 2, 3 };
            File.WriteAllBytes(png, corrupted);
            Assert.Throws<InvalidOperationException>(() => Build());
            CollectionAssert.AreEqual(corrupted, File.ReadAllBytes(png));
            Assert.AreSame(first, _sources.Compiled);
        }

        [Test]
        public void InvalidCanvasOrMissingVariantBinding_RejectsBeforePublication()
        {
            TerrainTileCatalog first = Build();
            WriteSource(60, 64);
            Assert.Throws<ArgumentException>(() => Build());
            Assert.AreSame(first, _sources.Compiled);
            var variant = new TerrainTileVariant();
            MapTestDefinitions.Set(variant, "_id", "alternate");
            foreach (string corner in new[] { "_northWest", "_northEast", "_southEast", "_southWest" }) { MapTestDefinitions.Set(variant, corner, _terrain); }
            MapTestDefinitions.Set(_set, "_variants", new[] { variant });
            Assert.Throws<ArgumentException>(() => TerrainTextureBuild.Capture(_sources));
            var binding = new TerrainMaterialSource();
            MapTestDefinitions.Set(binding, "_terrain", _terrain);
            MapTestDefinitions.Set(binding, "_source", AssetDatabase.LoadAssetAtPath<Texture2D>(_root + "/source.png"));
            var alternate = new TerrainVariantSource();
            MapTestDefinitions.Set(alternate, "_variantId", "alternate");
            MapTestDefinitions.Set(alternate, "_materials", new[] { binding });
            MapTestDefinitions.Set(_sources, "_variants", new[] { alternate });
            WriteSource(60);
            TerrainTileCatalog compiled = Build();
            var id = new ContentId(ContentNamespaces.Terrain, _terrain.Id);
            var key = new TerrainWangKey(id, id, id, id);
            Assert.IsNotNull(compiled.GetTile(key, "alternate"));
            Assert.Throws<InvalidOperationException>(() => compiled.GetTile(key, "unknown"));
        }

        [UnityTest]
        public IEnumerator FormalPreview_RejectsStaleSourceAndReleasesSceneWithoutDestroyingAssets()
        {
            TerrainTileCatalog catalog = Build();
            int previews = EditorSceneManager.previewSceneCount;
            var document = new MapDocument
            {
                MapId = "map:texture_preview", Width = 4, Height = 4,
                TerrainSetId = "terrain_set:" + _set.Id, DefaultTerrainId = "terrain:" + _terrain.Id,
                TerrainPalette = new[] { "terrain:" + _terrain.Id }
            };
            IMapDefinitionLookup definitions = MapEditorDefinitionIndex.Capture();
            CompiledMapDocument map = MapDocumentCompiler.Compile(document, definitions);
            TerrainRuleSnapshot rules = TerrainRuleSnapshot.Compile(_set, definitions);
            using var session = new MapTerrainPreviewSession();
            yield return session.RebuildAsync(() => (map, rules), _sources.RequireCurrent).ToCoroutine();
            Assert.IsTrue(session.IsValid, session.Status);
            MapTerrainPreviewFrame frame = session.Current;
            Assert.AreSame(catalog.Entries[0].Tile, frame.Tilemap.GetTile(Vector3Int.zero));
            Assert.AreEqual(catalog.Revision, frame.DisplayRevision);
            Assert.AreEqual(0, session.ResourceCount, "正式资产不进入诊断纹理租约");
            WriteSource(90);
            yield return session.RebuildAsync(() => (map, rules), _sources.RequireCurrent).ToCoroutine();
            Assert.IsFalse(session.IsValid);
            Assert.AreSame(frame, session.Current);
            TerrainTileCatalog updated = Build();
            yield return session.RebuildAsync(() => (map, rules), _sources.RequireCurrent).ToCoroutine();
            Assert.IsTrue(session.IsValid, session.Status);
            Assert.AreEqual(updated.Revision, session.Current.DisplayRevision);
            Assert.IsTrue(frame.Tilemap == null);
            session.Dispose();
            Assert.AreEqual(previews, EditorSceneManager.previewSceneCount);
            Assert.IsTrue(catalog.Entries[0].Tile.sprite.texture != null);
            Assert.IsTrue(updated.Entries[0].Tile.sprite.texture != null);
        }

        [Test]
        public void Publication_RequiresCurrentSourceAndExplicitlyAdvancesStableTarget()
        {
            AddressableAssetSettings settings = CreatePresentation(out TerrainPresentationDefinition target);
            TerrainTileCatalog first = Build();
            TerrainPresentationPublisher.Publish(_sources, settings);
            Assert.AreSame(first, target.Tiles);
            Assert.DoesNotThrow(target.Validate);
            WriteSource(100);
            Assert.Throws<InvalidOperationException>(() => TerrainPresentationPublisher.Publish(_sources, settings));
            Assert.AreSame(first, target.Tiles);
            TerrainTileCatalog second = Build();
            Assert.Throws<InvalidOperationException>(() => TerrainPresentationPublisher.ValidatePublished(_sources, settings));
            Assert.AreSame(first, target.Tiles);
            TerrainPresentationPublisher.Publish(_sources, settings);
            Assert.AreSame(second, target.Tiles);
            Assert.DoesNotThrow(() => TerrainPresentationPublisher.ValidatePublished(_sources, settings));
            Assert.AreEqual("maps/terrain/" + _set.Id, target.Address);
            Assert.Throws<ArgumentException>(() => TerrainPresentationDefinition.AddressFor(ContentId.Parse("terrain:wrong")));
        }

        [UnityTest]
        public IEnumerator FormalPopulation_CancelAndDisposeReleaseFramesButKeepPersistentTiles()
        {
            TerrainTileCatalog catalog = Build();
            var document = new MapDocument
            {
                MapId = "map:formal_lifetime", Width = 2, Height = 2,
                TerrainSetId = "terrain_set:" + _set.Id, DefaultTerrainId = "terrain:" + _terrain.Id,
                TerrainPalette = new[] { "terrain:" + _terrain.Id }
            };
            IMapDefinitionLookup lookup = MapEditorDefinitionIndex.Capture();
            TerrainRuleSnapshot rules = TerrainRuleSnapshot.Compile(_set, lookup);
            CompiledMapDocument small = MapDocumentCompiler.Compile(document, lookup);
            document.Width = 256;
            document.Height = 256;
            CompiledMapDocument large = MapDocumentCompiler.Compile(document, lookup);
            using var session = new MapTerrainPreviewSession();
            yield return session.RebuildAsync(() => (small, rules), _sources.RequireCurrent).ToCoroutine();
            Assert.IsTrue(session.IsValid, session.Status);
            MapTerrainPreviewFrame previous = session.Current;
            var buildingField = typeof(MapTerrainPreviewSession).GetField("_building", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                UniTask pending = session.RebuildAsync(() => (large, rules), _sources.RequireCurrent);
                // 只观察本会话；其他已打开预览窗口可能因测试资产导入而重建。
                for (int frame = 0; frame < 300 && buildingField.GetValue(session) == null && session.IsBuilding; frame++) { yield return UniTask.Delay(10, DelayType.Realtime).ToCoroutine(); }
                var building = (MapTerrainPreviewFrame)buildingField.GetValue(session);
                Assert.IsNotNull(building, session.Status);
                Assert.IsNotNull(building.Tilemap.GetTile(new Vector3Int(-1, -1, 0)));
                var buildingScene = building.Camera.scene;
                Assert.IsTrue(session.IsBuilding);
                if (attempt == 0) { session.Cancel(); }
                else { session.Dispose(); }
                yield return pending.ToCoroutine();
                Assert.IsFalse(session.IsBuilding);
                Assert.IsFalse(session.IsValid);
                Assert.AreEqual(0, session.ResourceCount);
                Assert.IsTrue(catalog.Entries[0].Tile.sprite.texture != null);
                Assert.IsTrue(building.Tilemap == null && building.Camera == null);
                Assert.IsFalse(buildingScene.IsValid());
                if (attempt == 0)
                {
                    Assert.AreSame(previous, session.Current);
                    Assert.IsTrue(previous.Camera.scene.IsValid());
                }
                else
                {
                    Assert.IsNull(session.Current);
                    Assert.IsTrue(previous.Tilemap == null);
                }
            }
        }

        [Test]
        public void Publication_RejectsDuplicateAddressAndRollsBackForbiddenRuntimeDependency()
        {
            AddressableAssetSettings settings = CreatePresentation(out TerrainPresentationDefinition target);
            TerrainTileCatalog first = Build();
            TerrainPresentationPublisher.Publish(_sources, settings);
            AddressableAssetEntry duplicate = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(_root + "/source.png"), settings.FindGroup(TerrainPresentationPublisher.GroupName));
            duplicate.SetAddress(target.Address);
            Assert.Throws<InvalidOperationException>(() => TerrainPresentationPublisher.Publish(_sources, settings));
            Assert.AreSame(first, target.Tiles);
            settings.RemoveAssetEntry(duplicate.guid);
            WriteSource(110);
            TerrainTileCatalog second = Build();
            // 目录若被移动到开发样例目录，不得发布给 Runtime；失败需恢复旧指针。
            AssetDatabase.CreateFolder(_root, "Development");
            string oldPath = AssetDatabase.GetAssetPath(second);
            Assert.IsEmpty(AssetDatabase.MoveAsset(oldPath, _root + "/Development/catalog.asset"));
            Assert.Throws<InvalidOperationException>(() => TerrainPresentationPublisher.Publish(_sources, settings));
            Assert.AreSame(first, target.Tiles);
            Assert.DoesNotThrow(target.Validate);
        }

        AddressableAssetSettings CreatePresentation(out TerrainPresentationDefinition target)
        {
            target = ScriptableObject.CreateInstance<TerrainPresentationDefinition>();
            MapTestDefinitions.Set(target, "_terrainSet", _set);
            AssetDatabase.CreateAsset(target, _root + "/presentation.asset");
            MapTestDefinitions.Set(_sources, "_presentation", target);
            // 独立设置，不改项目默认 Addressables 分组或玩家数据。
            AddressableAssetSettings settings = AddressableAssetSettings.Create(_root + "/Addressables", "TestSettings", false, false);
            _addressableSettings = settings;
            AddressableAssetGroup group = settings.CreateGroup(TerrainPresentationPublisher.GroupName, false, false, false, null);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(_root + "/presentation.asset"), group);
            entry.SetAddress(target.Address);
            entry.SetLabel("df.session", true, true);
            entry.SetLabel("df.terrain", true, true);
            return settings;
        }

        TerrainTileCatalog Build() { return TerrainTextureBuild.Compile(_sources, showProgress: false); }

        void WriteSource(byte color, int size = 1254)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(Enumerable.Repeat(new Color32(color, 30, 20, 255), size * size).ToArray());
                texture.Apply();
                File.WriteAllBytes(_root + "/source.png", texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(_root + "/source.png", ImportAssetOptions.ForceSynchronousImport);
        }

        sealed class InlineProgress : IProgress<int>
        {
            readonly Action<int> _report;
            public int Reports { get; private set; }
            public InlineProgress(Action<int> report) { _report = report; }
            public void Report(int value) { Reports++; _report(value); }
        }
    }
}
