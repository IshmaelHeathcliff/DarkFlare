using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed partial class MapEditorWindow
    {
        [SerializeField, TabGroup("对象与区域"), LabelText("显示正式对象"), OnValueChanged(nameof(SchedulePreview))] bool _showFormalObjects = true;
        [SerializeField, TabGroup("对象与区域"), LabelText("对象表现目录"), OnValueChanged(nameof(SchedulePreview))] MapObjectPresentationCatalog _presentationCatalog;
        MapObjectPresentationBatch _objectPreview;
        CancellationTokenSource _objectCancellation;
        int _objectGeneration;
        string _objectStatus = "对象表现尚未构建";
        string _terrainPreviewKey;
        public MapObjectPresentationBatch ObjectPreview => _objectPreview;
        public string ObjectPreviewStatus => _objectStatus;

        void TerrainMaterialsChanged()
        {
            SchedulePreview();
        }

        void ReleaseObjectPreview()
        {
            _objectGeneration++; _objectCancellation?.Cancel(); _objectCancellation?.Dispose(); _objectCancellation = null;
            MapObjectPresentationBatch previous = _objectPreview; _objectPreview = null;
            if (previous != null) { previous.DisposeAsync().Forget(); }
            _terrainPreviewKey = null;
        }

        async UniTask RebuildObjectsAsync()
        {
            int generation = ++_objectGeneration;
            _objectCancellation?.Cancel(); _objectCancellation?.Dispose(); _objectCancellation = new CancellationTokenSource();
            CancellationToken token = _objectCancellation.Token;
            if (!_showFormalObjects)
            {
                MapObjectPresentationBatch previous = _objectPreview; _objectPreview = null;
                if (previous != null) { await previous.DisposeAsync(); }
                _objectStatus = "仅显示诊断轮廓"; return;
            }
            _presentationCatalog ??= AssetDatabase.LoadAssetAtPath<MapObjectPresentationCatalog>(MapObjectPresentationSetup.CatalogPath);
            _objectStatus = "正在构建对象，旧对象画面已过期";
            MapObjectPresentationBatch candidate = null;
            try
            {
                candidate = await MapObjectPresentationBatch.PrepareAsync(_session.Current, _presentationCatalog, (reference, cancellation) =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    return UniTask.FromResult(new MapPresentationPrefabLease(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(reference.AssetGUID))));
                }, token);
                token.ThrowIfCancellationRequested();
                if (this == null || generation != _objectGeneration || _stage == null || StageUtility.GetCurrentStage() != _stage) { return; }
                candidate.MoveToScene(_stage.scene); candidate.Root.hideFlags = HideFlags.HideAndDontSave;
                candidate.Activate(new WorldSortingSystem());
                MapObjectPresentationBatch old = _objectPreview; _objectPreview = candidate; candidate = null;
                if (old != null) { await old.DisposeAsync(); }
                _objectStatus = "正式对象已同步：" + _objectPreview.Objects.Count;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (generation == _objectGeneration) { _objectStatus = "正式对象不可用，旧画面已过期：" + error.Message; } }
            finally { if (candidate != null) { await candidate.DisposeAsync(); } }
            SceneView.RepaintAll(); Repaint();
        }

        string TerrainPreviewKey()
        {
            MapDocument map = _session.Current.CreateEditableCopy();
            map.Objects = Array.Empty<MapObjectRecord>(); map.Regions = Array.Empty<MapRegionRecord>();
            map.Entries = Array.Empty<MapEntryRecord>(); map.BossSpawns = Array.Empty<MapBossSpawnRecord>(); map.Revision = 0;
            string setPath = AssetDatabase.GetAssetPath(_set);
            string materialsPath = AssetDatabase.GetAssetPath(_materials);
            return MapDocumentJson.Write(map) + "|" + (string.IsNullOrEmpty(setPath) ? "" : AssetDatabase.GetAssetDependencyHash(setPath).ToString())
                + "|" + (string.IsNullOrEmpty(materialsPath) ? "" : AssetDatabase.GetAssetDependencyHash(materialsPath).ToString());
        }
    }
}
