using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkFlare.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class MapEditTests
    {
        MapTestDefinitions _definitions;
        string _folder;
        string _path;
        MapEditSession _session;
        IMapDefinitionLookup Lookup => new MapDefinitionLookup(_definitions.Assets);

        [SetUp]
        public void SetUp()
        {
            _definitions = new MapTestDefinitions();
            _folder = "Temp/MapEditTests_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(_folder); _path = _folder + "/test.dfmap";
            CompiledMapDocument map = MapDocumentCompiler.Compile(_definitions.Document(), Lookup);
            File.WriteAllText(_path, map.CanonicalJson);
            _session = new MapEditSession(map, _path, () => Lookup);
        }

        [TearDown]
        public void TearDown()
        {
            _session?.Dispose(); _definitions?.Dispose();
            string recovery = Path.Combine(MapEditSessions.RecoveryRoot, MapEditService.Hash(_path) + ".json");
            if (File.Exists(recovery)) { File.Delete(recovery); }
            if (Directory.Exists(MapEditSessions.RecoveryRoot))
            {
                foreach (string conflict in Directory.GetFiles(MapEditSessions.RecoveryRoot, Path.GetFileName(recovery) + ".conflict-*.json")) { File.Delete(conflict); }
            }
            if (Directory.Exists(_folder)) { Directory.Delete(_folder, true); }
        }

        static JObject Fill(string terrain, int x = 0, int y = 0, int width = 1, int height = 1)
        {
            return new JObject { ["type"] = "FillRect", ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height, ["terrainId"] = "terrain:test_" + terrain };
        }

        [Test]
        public void EntryPatch_PreviewUndoRedoAndRemoveShareTheAtomicDocument()
        {
            var operation = new JObject { ["type"] = "UpsertEntry", ["record"] = new JObject
            { ["entryId"] = "arrival", ["kind"] = "Arrival", ["position"] = new JObject { ["x"] = 1, ["y"] = 2 } } };
            JObject patch = MapEditService.Patch(_session.Current, operation);
            Assert.AreEqual("arrival", _session.Preview(patch).CreateEditableCopy().Entries.Single().EntryId);
            Assert.IsEmpty(_session.Current.CreateEditableCopy().Entries);
            _session.Apply(patch);
            Undo.PerformUndo(); Assert.IsEmpty(_session.Current.CreateEditableCopy().Entries);
            Undo.PerformRedo(); Assert.AreEqual(2, _session.Current.CreateEditableCopy().Entries.Single().Position.Y);
            _session.Apply(MapEditService.Patch(_session.Current, new JObject { ["type"] = "RemoveEntry", ["id"] = "arrival" }));
            Assert.IsEmpty(_session.Current.CreateEditableCopy().Entries);
        }

        [Test]
        public void AtomicBatchRejectsLateFailureAndCancellation()
        {
            string before = _session.Current.SourceHash;
            JObject patch = MapEditService.Patch(_session.Current, Fill("grass"), Fill("water", 4));
            Assert.Throws<MapEditException>(() => _session.Apply(patch));
            Assert.AreEqual(before, _session.Current.SourceHash);
            var cancellation = new CancellationToken(true);
            Assert.Throws<OperationCanceledException>(() => MapEditService.Preview(_session.Current, MapEditService.Patch(_session.Current, Fill("grass")), Lookup, cancellation));
            Assert.IsFalse(_session.IsDirty);
        }

        [Test]
        public void NoOpSparseChunkAndEraseRestoreSemanticDirtyState()
        {
            _session.Apply(MapEditService.Patch(_session.Current, Fill("dirt")));
            Assert.AreEqual(0, _session.Current.Revision);
            _session.Apply(MapEditService.Patch(_session.Current, Fill("grass")));
            Assert.IsTrue(_session.IsDirty);
            _session.Apply(MapEditService.Patch(_session.Current, Fill("dirt")));
            Assert.IsFalse(_session.IsDirty);
            Assert.AreEqual(2, _session.Current.Revision);
            Assert.IsEmpty(_session.Current.CreateEditableCopy().Chunks);
        }

        [Test]
        public void PreviewAndApplyAreIdenticalButOnlyApplyMutates()
        {
            JObject patch = MapEditService.Patch(_session.Current, Fill("road", 1, 1, 2, 2));
            CompiledMapDocument preview = _session.Preview(patch);
            Assert.AreEqual(0, _session.Current.Revision);
            _session.Apply(patch);
            Assert.AreEqual(preview.CanonicalJson, _session.Current.CanonicalJson);
            Assert.AreEqual("terrain:test_dirt", MapDocumentCompiler.Compile(MapDocumentImporter.ReadSource(_path), Lookup).GetTerrain(1, 1), "Apply 不写磁盘");
        }

        [Test]
        public void UndoRedoIsMonotonicAndRetryDoesNotReapplyUndoneRequest()
        {
            JObject patch = MapEditService.Patch(_session.Current, Fill("grass"));
            _session.Apply(patch);
            Undo.PerformUndo();
            Assert.AreEqual("terrain:test_dirt", _session.Current.GetTerrain(0, 0));
            Assert.AreEqual(2, _session.Current.Revision);
            JObject receipt = _session.Apply(patch);
            Assert.IsTrue((bool)receipt["replayed"]);
            Assert.AreEqual(2, _session.Current.Revision);
            Undo.PerformRedo();
            Assert.AreEqual("terrain:test_grass", _session.Current.GetTerrain(0, 0));
            Assert.AreEqual(3, _session.Current.Revision);
            patch["operations"][0]["terrainId"] = "terrain:test_water";
            Assert.AreEqual("Conflict", Assert.Throws<MapEditException>(() => _session.Apply(patch)).Code);
        }

        [Test]
        public void UndoBudgetKeepsOtherEditorObjectsHistory()
        {
            var other = ScriptableObject.CreateInstance<MapEditUndoState>();
            other.Json = "before";
            try
            {
                Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(other, "其他工具编辑"); other.Json = "after"; Undo.FlushUndoRecordObjects();
                for (int i = 0; i < 70; i++) { _session.Apply(MapEditService.Patch(_session.Current, Fill(i % 2 == 0 ? "grass" : "road"))); }
                Assert.LessOrEqual(_session.UndoTransactionCount, 64);
                Assert.LessOrEqual(_session.EstimatedUndoBytes, 64 * 1048576);
                for (int i = 0; i < 6; i++) { Undo.PerformUndo(); }
                Assert.AreEqual("after", other.Json);
                // ClearUndo(载体) 可能保留空组；越过本测试创建的组，不触碰更早的历史。
                for (int i = 0; i < 65 && other.Json == "after"; i++) { Undo.PerformUndo(); }
                Assert.AreEqual("before", other.Json, "不能为了裁剪地图历史清除全局 Undo");
            }
            finally { Undo.ClearUndo(other); UnityEngine.Object.DestroyImmediate(other); }
        }

        [Test]
        public void DefinitionsAreRevalidatedBetweenPreviewAndApply()
        {
            JObject patch = MapEditService.Patch(_session.Current, Fill("grass"));
            _session.Preview(patch);
            MapTestDefinitions.Set(_definitions.Dirt, "_id", "renamed");
            Assert.Throws<MapEditException>(() => _session.Apply(patch));
            Assert.AreEqual(0, _session.Current.Revision);
        }

        [Test]
        public void OverrideConflictRequiresExplicitClearAndCanBeClearedWithinBatch()
        {
            _session.Apply(MapEditService.Patch(_session.Current, new JObject { ["type"] = "SetVariant", ["x"] = -1, ["y"] = -1, ["variantId"] = "dirt_a" }));
            JObject patch = MapEditService.Patch(_session.Current, Fill("grass"));
            Assert.Throws<MapEditException>(() => _session.Apply(patch));
            patch["clearAffectedOverrides"] = true;
            _session.Apply(patch);
            Assert.IsEmpty(_session.Current.CreateEditableCopy().VariantOverrides);
        }

        [Test]
        public void FloodDoesNotCrossDifferentTerrainAndLineHasNoGaps()
        {
            _session.Apply(MapEditService.Patch(_session.Current, Fill("water", 2, 0, 1, 4)));
            _session.Apply(MapEditService.Patch(_session.Current, new JObject { ["type"] = "FillRegion", ["x"] = 0, ["y"] = 0, ["terrainId"] = "terrain:test_grass" }));
            Assert.AreEqual("terrain:test_grass", _session.Current.GetTerrain(1, 3));
            Assert.AreEqual("terrain:test_dirt", _session.Current.GetTerrain(3, 3));
            var line = MapEditService.Line(0, 0, 100, 31).ToArray();
            Assert.AreEqual(101, line.Length);
            for (int i = 1; i < line.Length; i++) { Assert.LessOrEqual(Math.Abs(line[i].X - line[i - 1].X) + Math.Abs(line[i].Y - line[i - 1].Y), 2); }
        }

        [TestCase("unknown")]
        [TestCase("duplicate")]
        [TestCase("integer")]
        [TestCase("oversize")]
        [TestCase("wrongrecord")]
        public void StrictPatchRejectsAmbiguityAndBudgets(string mutation)
        {
            JObject patch = MapEditService.Patch(_session.Current, Fill("grass"));
            if (mutation == "unknown") { patch["extra"] = true; }
            if (mutation == "integer") { patch["baseRevision"] = "0"; }
            if (mutation == "oversize") { patch["operations"][0]["width"] = int.MaxValue; }
            if (mutation == "wrongrecord") { patch["operations"] = new JArray(new JObject { ["type"] = "UpsertObject", ["record"] = new JObject { ["$type"] = "Forbidden" } }); }
            string json = patch.ToString();
            if (mutation == "duplicate") { json = json.Replace("\"patchVersion\": 1", "\"patchVersion\": 1, \"patchVersion\": 1"); }
            Assert.Throws<MapEditException>(() => _session.Apply(MapEditService.ReadPatch(json)));
            Assert.AreEqual(0, _session.Current.Revision);
        }

        [Test]
        public void ObjectRegionBatchRetainsBindingsAndRejectsInvalidPolygonAtomically()
        {
            MapDocument document = _definitions.Document();
            document.Objects = new[] { _definitions.Decoration("tree") };
            document.Regions = new[] { new MapRegionRecord { RegionId = "safe", DefinitionId = "map_region:test_safe", Points = new[] { new MapPoint(), new MapPoint { X = 4, Y = 4 } } } };
            JObject raw = JObject.Parse(MapDocumentJson.Write(document));
            _session.Apply(MapEditService.Patch(_session.Current, new JObject { ["type"] = "UpsertObject", ["record"] = raw["objects"][0] }, new JObject { ["type"] = "UpsertRegion", ["record"] = raw["regions"][0] }));
            Assert.AreEqual("tree", _session.Current.CreateEditableCopy().Objects[0].ObjectId);
            string before = _session.Current.SourceHash;
            raw["regions"][0]["points"] = new JArray(new JObject { ["x"] = 3, ["y"] = 3 }, new JObject { ["x"] = 1, ["y"] = 1 });
            Assert.Throws<MapEditException>(() => _session.Apply(MapEditService.Patch(_session.Current, Fill("road"), new JObject { ["type"] = "UpsertRegion", ["record"] = raw["regions"][0] })));
            Assert.AreEqual(before, _session.Current.SourceHash);
        }

        [Test]
        public void AtomicFileWriteDetectsChangesAndKeepsWholeFileOnFailure()
        {
            string baseline = MapEditFiles.Hash(_path);
            File.WriteAllText(_path, "external");
            Assert.AreEqual("Conflict", Assert.Throws<MapEditException>(() => MapEditFiles.Write(_path, "new", baseline)).Code);
            Assert.AreEqual("external", File.ReadAllText(_path));
            using (FileStream locked = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.None))
            { Assert.Throws<IOException>(() => MapEditFiles.Write(_path, "new", baseline)); }
            Assert.IsEmpty(Directory.GetFiles(_folder, "*~"));
        }

        [Test]
        public void RecoveryPreservesDraftAndRejectsOldRequestEvenAfterUndoToClean()
        {
            JObject old = MapEditService.Patch(_session.Current, Fill("grass"));
            _session.Apply(old); Undo.PerformUndo();
            Assert.IsFalse(_session.IsDirty);
            _session.Dispose();
            _session = new MapEditSession(MapDocumentCompiler.Compile(MapDocumentImporter.ReadSource(_path), Lookup), _path, () => Lookup);
            Assert.IsTrue(_session.TryRestore());
            Assert.Greater(_session.Current.Revision, 2);
            Assert.AreEqual("Conflict", Assert.Throws<MapEditException>(() => _session.Apply(old)).Code);
            Assert.IsFalse(_session.IsDirty);
            _session.Apply(MapEditService.Patch(_session.Current, Fill("road")));
            _session.Dispose();
            _session = new MapEditSession(MapDocumentCompiler.Compile(MapDocumentImporter.ReadSource(_path), Lookup), _path, () => Lookup);
            Assert.IsTrue(_session.TryRestore()); Assert.IsTrue(_session.IsDirty);
            Assert.AreEqual("terrain:test_road", _session.Current.GetTerrain(0, 0));
        }

        [Test]
        public void RecoveryDoesNotOverwriteExternalFile()
        {
            _session.Apply(MapEditService.Patch(_session.Current, Fill("grass"))); _session.Dispose();
            MapDocument replacement = _definitions.Document(); replacement.Seed = 42;
            File.WriteAllText(_path, MapDocumentJson.Write(replacement));
            _session = new MapEditSession(MapDocumentCompiler.Compile(replacement, Lookup), _path, () => Lookup);
            Assert.IsFalse(_session.TryRestore());
            Assert.AreEqual(42, _session.Current.Seed);
            _session.Dispose();
            string[] conflicts = Directory.GetFiles(MapEditSessions.RecoveryRoot, MapEditService.Hash(_path) + ".json.conflict-*.json");
            Assert.AreEqual(1, conflicts.Length);
            JObject preserved = JObject.Parse(File.ReadAllText(conflicts[0]));
            Assert.AreEqual("terrain:test_grass", MapDocumentCompiler.Compile(MapDocumentJson.Read(preserved["document"].ToString()), Lookup).GetTerrain(0, 0), "关闭新会话也不能覆盖冲突草稿");
        }

        [Test]
        public void PickingRespectsNegativeOriginHalfCellAndBorders()
        {
            MapDocument document = _definitions.Document(); document.Origin = new MapPoint { X = -10, Y = -20 }; document.CellSize = 0.5;
            CompiledMapDocument map = MapDocumentCompiler.Compile(document, Lookup);
            Assert.AreEqual(new Vector2Int(0, 0), MapEditorWindow.LogicalCell(map, new Vector2(-9.75f, -19.75f)));
            Assert.AreEqual(new Vector2Int(-1, 0), MapEditorWindow.LogicalCell(map, new Vector2(-10.001f, -19.75f)));
            Assert.AreEqual(new Vector2Int(4, 4), MapEditorWindow.LogicalCell(map, new Vector2(-8, -18)));
        }

        [Test]
        public void ConnectivityDistinguishesDisconnectedEndpointAndBudget()
        {
            MapDocument document = _definitions.Document(); document.Width = 8; document.Height = 8;
            CompiledMapDocument map = MapDocumentCompiler.Compile(document, Lookup);
            MapGeometrySnapshot open = MapGeometryCompiler.Compile(MapGeometryInput.Capture(map, Lookup));
            var profile = new MapGroundProfile(0.1);
            Assert.AreEqual("Connected", MapEditConnectivity.Check(open, new MapGeometryPoint(1, 1), new MapGeometryPoint(6, 6), profile));
            Assert.AreEqual("BudgetExceeded", MapEditConnectivity.Check(open, new MapGeometryPoint(1, 1), new MapGeometryPoint(6, 6), profile, 1));
            Assert.AreEqual("NoLegalEndpoint", MapEditConnectivity.Check(open, new MapGeometryPoint(0, 0), new MapGeometryPoint(6, 6), profile));
            map = MapEditService.Preview(map, MapEditService.Patch(map, Fill("water", 3, 0, 2, 8)), Lookup);
            MapGeometrySnapshot wall = MapGeometryCompiler.Compile(MapGeometryInput.Capture(map, Lookup));
            Assert.AreEqual("Disconnected", MapEditConnectivity.Check(wall, new MapGeometryPoint(1, 1), new MapGeometryPoint(6, 6), profile));
        }

        [UnityTest]
        public IEnumerator SceneViewStrokeEscapeAndSavedUndoUseTheSameSession()
        {
            string root = "Assets/__MapEdit_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool wasDirty = scene.isDirty;
            var window = ScriptableObject.CreateInstance<MapEditorWindow>();
            string path = root + "/map.dfmap";
            string recovery = null;
            try
            {
                foreach (ScriptableObject asset in _definitions.Assets) { AssetDatabase.CreateAsset(asset, root + "/" + asset.GetType().Name + "_" + ((IContentDefinition)asset).Id + ".asset"); }
                MapEditorDefinitionIndex.UpdateDependency();
                MapEditSession session = MapEditSessions.Create(path, _definitions.Document());
                recovery = Path.Combine(MapEditSessions.RecoveryRoot, MapEditService.Hash(session.SourceGuid) + ".json");
                window.LoadSource(path); window.Show(); window.EnterStage();
                Assert.AreSame(session, MapEditSessions.Open(path));
                MapTestDefinitions.Set(window, "_terrain", "terrain:test_grass");
                MapTestDefinitions.Set(window, "_materials", null);
                var view = SceneView.lastActiveSceneView;
                for (int i = 0; i < 120 && window.Preview?.IsValid != true; i++) { yield return null; }
                Assert.IsTrue(window.Preview?.IsValid, window.Preview?.Status);
                view.Focus(); view.Repaint(); yield return null;
                Vector2 Gui(Vector3 world)
                {
                    Vector3 screen = view.camera.WorldToScreenPoint(world);
                    // 读取实际视口：编辑舞台与普通场景的工具栏高度不同。
                    Rect viewport = UnityEngine.UIElements.UQueryExtensions.Q(view.rootVisualElement, "unity-scene-view-camera-rect", (string)null).worldBound;
                    return new Vector2(viewport.x + screen.x / EditorGUIUtility.pixelsPerPoint, viewport.y + (view.camera.pixelHeight - screen.y) / EditorGUIUtility.pixelsPerPoint);
                }
                Vector2 start = Gui(new Vector3(0.5f, 0.5f)), end = Gui(new Vector3(3.5f, 0.5f));
                view.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                view.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                view.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = end });
                yield return null;
                for (int x = 0; x < 4; x++) { Assert.AreEqual("terrain:test_grass", session.Current.GetTerrain(x, 0), "真实 SceneView 拖拽格 " + x + " start=" + start + " end=" + end + " revision=" + session.Current.Revision + " status=" + typeof(MapEditorWindow).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(window)); }
                Assert.AreEqual(1, session.Current.Revision, "整笔仅一次修订");
                view.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.S, control = true }); Assert.IsFalse(session.IsDirty);
                view.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Z, control = true }); Assert.IsTrue(session.IsDirty);
                Assert.AreEqual("terrain:test_dirt", session.Current.GetTerrain(0, 0));
                view.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Z, control = true, shift = true }); Assert.IsFalse(session.IsDirty);
                string before = session.Current.SourceHash;
                MapTestDefinitions.Set(window, "_terrain", "terrain:test_water");
                view.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                view.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                view.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = start });
                Assert.AreEqual(before, session.Current.SourceHash, "Esc 不提交未完成笔画");
                JObject apiPatch = MapEditService.Patch(session.Current, Fill("road", 0, 1, 4, 1));
                Assert.AreEqual("Ok", (string)JObject.Parse(MapEditApi.PreviewPatch(path, apiPatch.ToString()))["code"]);
                Assert.AreEqual(before, session.Current.SourceHash);
                Assert.AreEqual("Ok", (string)JObject.Parse(MapEditApi.ApplyPatch(path, apiPatch.ToString()))["code"]);
                Assert.AreEqual("terrain:test_road", window.Session.Current.GetTerrain(0, 1), "AI 与手工共享工作副本");
                Assert.AreEqual("Ok", (string)JObject.Parse(MapEditApi.Save(path))["code"]);
                JObject read = JObject.Parse(MapEditApi.ReadRegion(path, 0, 1, 4, 1));
                Assert.IsTrue(read["cells"].All(item => (string)item["terrainId"] == "terrain:test_road"));
                Assert.AreEqual(wasDirty, scene.isDirty);
                MapEditSessions.Close(path);
            }
            finally
            {
                window.DiscardChanges(); window.Close();
                MapEditSessions.Close(path); AssetDatabase.DeleteAsset(root);
                if (recovery != null && File.Exists(recovery)) { File.Delete(recovery); }
                MapEditorDefinitionIndex.UpdateDependency();
            }
        }

        [UnityTest]
        public IEnumerator PreviewStageOwnsPublishedTilesAndRestoresOriginalScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            var stage = MapEditingStage.Enter();
            var preview = new MapTerrainPreviewSession();
            var map = _session.Current;
            var task = preview.RebuildAsync(() => (map, TerrainRuleSnapshot.Compile(_definitions.TerrainSet, Lookup)));
            yield return task.ToCoroutine();
            try
            {
                Assert.IsTrue(preview.IsValid, preview.Status);
                preview.Current.MoveToScene(stage.scene);
                Assert.AreEqual(stage.scene, preview.Current.Tilemap.gameObject.scene);
                Assert.AreEqual(dirty, active.isDirty);
            }
            finally { preview.Dispose(); StageUtility.GoBackToPreviousStage(); }
            Assert.AreEqual(active, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Assert.AreEqual(dirty, active.isDirty);
        }
    }
}
