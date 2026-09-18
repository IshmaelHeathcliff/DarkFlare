using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Newtonsoft.Json.Linq;

namespace DarkFlare.Tests
{
    public sealed class MapObjectPresentationTests
    {
        MapObjectPresentationCatalog Catalog => AssetDatabase.LoadAssetAtPath<MapObjectPresentationCatalog>(MapObjectPresentationSetup.CatalogPath);
        CompiledMapDocument Map => MapDocumentCompiler.Compile(MapDocumentImporter.ReadSource(MapObjectPresentationSetup.SamplePath), MapEditorDefinitionIndex.Capture());

        static UniTask<MapPresentationPrefabLease> Load(UnityEngine.AddressableAssets.AssetReferenceGameObject reference, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return UniTask.FromResult(new MapPresentationPrefabLease(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(reference.AssetGUID))));
        }

        [Test]
        public void PrepareDoesNotRegisterAndRepeatedActivationReleaseConverges()
        {
            var system = new WorldSortingSystem();
            CompiledMapDocument map = Map;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                MapObjectPresentationBatch batch = MapObjectPresentationBatch.PrepareAsync(map, Catalog, Load, default).GetAwaiter().GetResult();
                try
                {
                    Assert.IsFalse(batch.Root.activeSelf); Assert.AreEqual(0, system.ParticipantCount);
                    batch.Activate(system); batch.Activate(system);
                    Assert.AreEqual(batch.Participants.Count, system.ParticipantCount);
                    foreach (WorldSortParticipant p in batch.Participants)
                    {
                        Assert.IsTrue(p.IsRegistered); Assert.IsTrue(p.StableSortId.Contains(map.MapId));
                        Assert.AreEqual(WorldSortingSystem.QuantizeY(p.SortAnchor.position.y), p.SortKey.QuantizedY);
                    }
                    Assert.Greater(batch.Participants.Count, batch.Objects.Count);
                    Assert.AreEqual(0, batch.Root.GetComponentsInChildren<Collider2D>(true).Length);
                    Transform rotated = batch.Objects["wall_90"]; Assert.AreEqual(90, rotated.eulerAngles.z, 0.001f);
                    Assert.AreNotEqual(rotated.GetComponentsInChildren<WorldSortParticipant>()[0].SortAnchor.position.y,
                        rotated.GetComponentsInChildren<WorldSortParticipant>()[1].SortAnchor.position.y);
                }
                finally { batch.DisposeAsync().GetAwaiter().GetResult(); }
                Assert.AreEqual(0, system.ParticipantCount);
            }
        }

        [Test]
        public void ActivationCapacityFailureLeavesCandidateAndExistingRegistrationsUntouched()
        {
            var system = new WorldSortingSystem(1);
            MapObjectPresentationBatch batch = MapObjectPresentationBatch.PrepareAsync(Map, Catalog, Load, default).GetAwaiter().GetResult();
            try
            {
                Assert.Throws<InvalidOperationException>(() => batch.Activate(system));
                Assert.IsFalse(batch.Root.activeSelf); Assert.AreEqual(0, system.ParticipantCount);
            }
            finally { batch.DisposeAsync().GetAwaiter().GetResult(); }
        }

        [Test]
        public void LateLoadCancellationReleasesLeaseAndUnknownAppearanceDoesNotFallback()
        {
            using var cancellation = new CancellationTokenSource(); var lease = new CountedLease();
            Assert.Throws<OperationCanceledException>(() => MapObjectPresentationBatch.PrepareAsync(Map, Catalog, (reference, token) =>
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(reference.AssetGUID));
                cancellation.Cancel(); return UniTask.FromResult(new MapPresentationPrefabLease(prefab, lease));
            }, cancellation.Token).GetAwaiter().GetResult());
            Assert.AreEqual(1, lease.Count);
            Assert.Throws<InvalidOperationException>(() => Catalog.Resolve("map_object:editor_tree", "missing"));
        }

        [Test]
        public void InvalidVisualPrefabRejectsPhysicsNestedGroupsAndDuplicatePartIdentity()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(Catalog.Resolve("map_object:editor_long_barrier", null).AssetGUID));
            GameObject clone = UnityEngine.Object.Instantiate(prefab);
            try
            {
                MapObjectVisualPart.ValidatePrefab(clone);
                var collider = clone.AddComponent<BoxCollider2D>();
                Assert.Throws<InvalidOperationException>(() => MapObjectVisualPart.ValidatePrefab(clone));
                UnityEngine.Object.DestroyImmediate(collider);
                var group = clone.AddComponent<UnityEngine.Rendering.SortingGroup>();
                Assert.Throws<InvalidOperationException>(() => MapObjectVisualPart.ValidatePrefab(clone));
                UnityEngine.Object.DestroyImmediate(group);
                MapObjectVisualPart[] parts = clone.GetComponentsInChildren<MapObjectVisualPart>(true);
                var serialized = new SerializedObject(parts[1]); serialized.FindProperty("_partId").stringValue = parts[0].PartId; serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => MapObjectVisualPart.ValidatePrefab(clone));
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        [Test]
        public void PreparedCandidateCannotActivateAfterOwnerCancellation()
        {
            using var cancellation = new CancellationTokenSource();
            var system = new WorldSortingSystem();
            MapObjectPresentationBatch batch = MapObjectPresentationBatch.PrepareAsync(Map, Catalog, Load, cancellation.Token).GetAwaiter().GetResult();
            try
            {
                cancellation.Cancel(); Assert.Throws<OperationCanceledException>(() => batch.Activate(system));
                Assert.AreEqual(0, system.ParticipantCount); Assert.IsFalse(batch.Root.activeSelf);
            }
            finally { batch.DisposeAsync().GetAwaiter().GetResult(); }
        }

        [Test]
        public void PartialLoadFailureReleasesAllAcquiredLeasesAndCandidateObjects()
        {
            var first = new CountedLease(); var second = new CountedLease(); int loads = 0;
            var existing = new HashSet<MapObjectVisualPart>(UnityEngine.Resources.FindObjectsOfTypeAll<MapObjectVisualPart>());
            Assert.Throws<InvalidOperationException>(() => MapObjectPresentationBatch.PrepareAsync(Map, Catalog, (reference, token) =>
            {
                loads++; if (loads == 3) { throw new InvalidOperationException("Injected resource failure"); }
                return UniTask.FromResult(new MapPresentationPrefabLease(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(reference.AssetGUID)), loads == 1 ? first : second));
            }, default).GetAwaiter().GetResult());
            Assert.AreEqual(1, first.Count); Assert.AreEqual(1, second.Count);
            // Prefab assets may become resident during loading; only scene instances represent owned candidate objects.
            Assert.IsFalse(System.Linq.Enumerable.Any(UnityEngine.Resources.FindObjectsOfTypeAll<MapObjectVisualPart>(), p => p.gameObject.scene.IsValid() && !existing.Contains(p)));
        }

        [Test]
        public void CatalogResolvesExplicitVariantWithoutChangingTheDefinitionFootprint()
        {
            var catalog = UnityEngine.Object.Instantiate(Catalog);
            MapObjectDefinition definition = UnityEngine.Object.Instantiate(Catalog.Entries[0].Definition);
            try
            {
                Vector2 footprint = definition.Footprint;
                var settings = new SerializedObject(definition); settings.FindProperty("_appearanceVariants").arraySize = 1;
                settings.FindProperty("_appearanceVariants").GetArrayElementAtIndex(0).stringValue = "alternate"; settings.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(catalog); SerializedProperty entry = serialized.FindProperty("_entries").GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("_definition").objectReferenceValue = definition;
                SerializedProperty variants = entry.FindPropertyRelative("_appearances"); variants.arraySize = 2;
                variants.GetArrayElementAtIndex(1).FindPropertyRelative("_variantId").stringValue = "alternate";
                string alternate = Catalog.Entries[1].Appearances[0].Prefab.AssetGUID;
                variants.GetArrayElementAtIndex(1).FindPropertyRelative("_prefab").FindPropertyRelative("m_AssetGUID").stringValue = alternate;
                serialized.ApplyModifiedPropertiesWithoutUndo(); catalog.Validate();
                Assert.AreEqual(alternate, catalog.Resolve("map_object:" + definition.Id, "alternate").AssetGUID);
                Assert.AreNotEqual(alternate, catalog.Resolve("map_object:" + definition.Id, null).AssetGUID);
                Assert.AreEqual(footprint, definition.Footprint);
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); UnityEngine.Object.DestroyImmediate(definition); }
        }

        sealed class CountedLease : IDisposable
        {
            public int Count { get; private set; }
            public void Dispose() { Count++; }
        }

        [UnityTest]
        public IEnumerator ObjectPatchUndoAndRedoSynchronizeVisualsWithoutRebuildingTerrain()
        {
            string folder = "Assets/__MapVisual_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string path = folder + "/map.dfmap"; string recovery = null;
            var window = ScriptableObject.CreateInstance<MapEditorWindow>();
            try
            {
                MapDocument map = Map.CreateEditableCopy(); map.MapId = "map:visual_" + Guid.NewGuid().ToString("N");
                MapEditSession session = MapEditSessions.Create(path, map);
                recovery = Path.Combine(MapEditSessions.RecoveryRoot, MapEditService.Hash(session.SourceGuid) + ".json");
                window.LoadSource(path); window.Show(); window.EnterStage();
                for (int i = 0; i < 240 && (window.Preview?.IsValid != true || window.ObjectPreview == null); i++) { yield return null; }
                Assert.IsNotNull(window.ObjectPreview, window.ObjectPreviewStatus); Assert.IsTrue(window.Preview.IsValid);
                MapTerrainPreviewFrame terrain = window.Preview.Current;
                JObject record = (JObject)JObject.Parse(session.Current.CanonicalJson)["objects"][0];
                string id = (string)record["objectId"];
                Vector3 old = window.ObjectPreview.Objects[id].position;
                record["position"]["x"] = old.x + 1;
                var patch = MapEditService.Patch(session.Current, new JObject { ["type"] = "UpsertObject", ["record"] = record });
                Assert.AreEqual("Ok", (string)JObject.Parse(MapEditApi.ApplyPatch(path, patch.ToString()))["code"]);
                for (int i = 0; i < 240 && Mathf.Abs(window.ObjectPreview.Objects[id].position.x - old.x - 1) > .001f; i++) { yield return null; }
                Assert.AreSame(terrain, window.Preview.Current);
                Assert.AreEqual(old.x + 1, window.ObjectPreview.Objects[id].position.x, .001f);
                Undo.PerformUndo(); for (int i = 0; i < 240 && Mathf.Abs(window.ObjectPreview.Objects[id].position.x - old.x) > .001f; i++) { yield return null; }
                Assert.AreEqual(old.x, window.ObjectPreview.Objects[id].position.x, .001f);
                Undo.PerformRedo(); for (int i = 0; i < 240 && Mathf.Abs(window.ObjectPreview.Objects[id].position.x - old.x - 1) > .001f; i++) { yield return null; }
                Assert.AreEqual(old.x + 1, window.ObjectPreview.Objects[id].position.x, .001f);
                Assert.AreSame(terrain, window.Preview.Current);
            }
            finally
            {
                window.DiscardChanges(); window.Close(); MapEditSessions.Close(path); AssetDatabase.DeleteAsset(folder);
                if (recovery != null && File.Exists(recovery)) { File.Delete(recovery); }
                MapEditorDefinitionIndex.UpdateDependency();
            }
        }
    }
}
