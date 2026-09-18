using System;
using System.Collections;
using System.IO;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class MapDocumentImporterTests
    {
        string _root;
        string _path;
        MapTestDefinitions _definitions;
        bool _previousIgnoreLogs;

        [SetUp]
        public void SetUp()
        {
            _previousIgnoreLogs = LogAssert.ignoreFailingMessages;
            _root = "Assets/__MapImportTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_root));
            _path = _root + "/sample.dfmap";
            _definitions = new MapTestDefinitions();
            foreach (ScriptableObject asset in _definitions.Assets)
            {
                AssetDatabase.CreateAsset(asset, _root + "/" + ((IContentDefinition)asset).Id + ".asset");
            }
            AssetDatabase.SaveAssets();
            MapEditorDefinitionIndex.UpdateDependency();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(_root);
            _definitions.Dispose();
            MapEditorDefinitionIndex.UpdateDependency();
            LogAssert.ignoreFailingMessages = _previousIgnoreLogs;
        }

        [Test]
        public void Reimport_PreservesGuidAndLocalIdWithOneReadOnlyMapArtifact()
        {
            MapDocument document = _definitions.Document();
            MapDocumentPage.SaveSource(_path, MapDocumentJson.Write(document));
            MapDefinition before = AssetDatabase.LoadAssetAtPath<MapDefinition>(_path);
            Assert.IsNotNull(before);
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(before, out string guid, out long localId));
            document.Revision++;
            MapDocumentPage.SaveSource(_path, MapDocumentJson.Write(document));
            MapDefinition after = AssetDatabase.LoadAssetAtPath<MapDefinition>(_path);
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(after, out string afterGuid, out long afterLocalId));
            Assert.AreEqual(guid, afterGuid);
            Assert.AreEqual(localId, afterLocalId);
            Assert.AreEqual(1, MapDocumentJson.Read(after.CanonicalJson).Revision);
            Assert.AreEqual(1, Array.FindAll(AssetDatabase.LoadAllAssetsAtPath(_path), asset => asset is MapDefinition).Length);
            Assert.IsFalse(File.Exists(Path.ChangeExtension(_path, ".asset")), "不得另存可编辑布局资产");
        }

        [Test]
        public void FailedImport_InvalidatesOldMapAndBlocksBuildValidation()
        {
            MapDocumentPage.SaveSource(_path, MapDocumentJson.Write(_definitions.Document()));
            LogAssert.ignoreFailingMessages = true;
            File.WriteAllText(_path, "{ broken");
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<MapDefinition>(_path));
            Assert.IsInstanceOf<TextAsset>(AssetDatabase.LoadMainAssetAtPath(_path));
            Assert.IsTrue(MapEditorValidation.Scan().Exists(issue => issue.AssetPath == _path));
            Assert.Throws<UnityEditor.Build.BuildFailedException>(() => new MapBuildValidation().OnPreprocessBuild(null));
        }

        [Test]
        public void InvalidExchange_DoesNotOverwriteExistingSourceOrArtifact()
        {
            string original = MapDocumentJson.Write(_definitions.Document());
            MapDocumentPage.SaveSource(_path, original);
            string hash = AssetDatabase.LoadAssetAtPath<MapDefinition>(_path).SourceHash;
            Assert.Throws<MapDocumentException>(() => MapDocumentPage.SaveSource(_path, "{}"));
            Assert.AreEqual(original, File.ReadAllText(_path));
            Assert.AreEqual(hash, AssetDatabase.LoadAssetAtPath<MapDefinition>(_path).SourceHash);
            Assert.IsFalse(File.Exists(_path + "~"));
        }

        [UnityTest]
        public IEnumerator DefinitionChange_AutomaticallyInvalidatesAndRecoversMap()
        {
            MapDocumentPage.SaveSource(_path, MapDocumentJson.Write(_definitions.Document()));
            LogAssert.ignoreFailingMessages = true;
            MapTestDefinitions.Set(_definitions.Dirt, "_movementCost", 0);
            EditorUtility.SetDirty(_definitions.Dirt);
            AssetDatabase.SaveAssetIfDirty(_definitions.Dirt);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            yield return Await(() => AssetDatabase.LoadAssetAtPath<MapDefinition>(_path) == null, "定义改变后地图未失效");
            MapTestDefinitions.Set(_definitions.Dirt, "_movementCost", 2);
            EditorUtility.SetDirty(_definitions.Dirt);
            AssetDatabase.SaveAssetIfDirty(_definitions.Dirt);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            yield return Await(() => AssetDatabase.LoadAssetAtPath<MapDefinition>(_path) != null, "修复定义后地图未恢复");
        }

        [UnityTest]
        public IEnumerator MissingReference_AddRenameConflictAndDeletionRefreshIndex()
        {
            MapDocument document = _definitions.Document();
            document.Objects = new[] { _definitions.Decoration("tree") };
            document.Objects[0].DefinitionId = "map_object:test_missing";
            LogAssert.ignoreFailingMessages = true;
            File.WriteAllText(_path, MapDocumentJson.Write(document));
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceSynchronousImport);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<MapDefinition>(_path));

            MapObjectDefinition added = _definitions.Create<MapObjectDefinition>("test_missing");
            string addedPath = _root + "/added.asset";
            AssetDatabase.CreateAsset(added, addedPath);
            AssetDatabase.SaveAssets();
            yield return Await(() => AssetDatabase.LoadAssetAtPath<MapDefinition>(_path) != null, "新增缺失定义后未恢复");

            MapTestDefinitions.Set(added, "_id", "test_renamed");
            EditorUtility.SetDirty(added);
            AssetDatabase.SaveAssets();
            yield return Await(() => AssetDatabase.LoadAssetAtPath<MapDefinition>(_path) == null, "ID 改名后未失效");
            MapTestDefinitions.Set(added, "_id", "test_missing");
            EditorUtility.SetDirty(added);
            AssetDatabase.SaveAssets();
            yield return Await(() => AssetDatabase.LoadAssetAtPath<MapDefinition>(_path) != null, "ID 恢复后未重导入");

            MapObjectDefinition duplicate = _definitions.Create<MapObjectDefinition>("test_missing");
            string duplicatePath = _root + "/duplicate.asset";
            AssetDatabase.CreateAsset(duplicate, duplicatePath);
            AssetDatabase.SaveAssets();
            yield return Await(() => AssetDatabase.LoadAssetAtPath<MapDefinition>(_path) == null, "冲突 ID 未使地图失效");
            AssetDatabase.DeleteAsset(duplicatePath);
            yield return Await(() => AssetDatabase.LoadAssetAtPath<MapDefinition>(_path) != null, "删除冲突定义后未恢复");
        }

        [UnityTest]
        public IEnumerator PreviewWindow_ReimportInvalidatesAndRecoversWithoutKeepingResourcesAfterClose()
        {
            MapDocument document = _definitions.Document();
            MapDocumentPage.SaveSource(_path, MapDocumentJson.Write(document));
            var window = MapTerrainPreviewWindow.OpenSource(_path);
            MapTerrainPreviewSession session = window.Session;
            try
            {
                yield return Await(() => session.IsValid, "初次地图预览未完成：" + session.Status);
                document.Revision++;
                document.DefaultTerrainId = "terrain:test_water";
                MapDocumentPage.SaveSource(_path, MapDocumentJson.Write(document));
                yield return Await(() => session.IsValid && session.Current.Resolution.Map.Revision == document.Revision, "重新导入后预览未刷新");
                MapTerrainPreviewFrame lastGood = session.Current;
                LogAssert.ignoreFailingMessages = true;
                File.WriteAllText(_path, "{ broken");
                AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                yield return Await(() => !session.IsBuilding && !session.IsValid && session.Status.Contains("失败"), "非法源未标记过期");
                Assert.AreSame(lastGood, session.Current);
                MapDocumentPage.SaveSource(_path, MapDocumentJson.Write(document));
                yield return Await(() => session.IsValid, "修复源后预览未恢复");
                Assert.IsTrue(lastGood.Tilemap == null);
            }
            finally { window.Close(); }
            Assert.IsNull(session.Current);
            Assert.AreEqual(0, session.ResourceCount);
        }

        static IEnumerator Await(Func<bool> predicate, string message)
        {
            double deadline = EditorApplication.timeSinceStartup + 15;
            while (!predicate() && EditorApplication.timeSinceStartup < deadline) { yield return null; }
            Assert.IsTrue(predicate(), message);
        }
    }
}
