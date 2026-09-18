using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace DarkFlare
{
    public sealed class MapPresentationPrefabLease : IDisposable
    {
        readonly IDisposable _lease;
        public GameObject Prefab { get; }
        public MapPresentationPrefabLease(GameObject prefab, IDisposable lease = null) { Prefab = prefab; _lease = lease; }
        public void Dispose() { _lease?.Dispose(); }
    }

    // 候选仅持有表现。创建 / 激活 / 释放在 Unity 主线程调用；调用者用 generation 防止过期候选替换当前图。
    public sealed class MapObjectPresentationBatch
    {
        readonly List<MapPresentationPrefabLease> _leases = new List<MapPresentationPrefabLease>();
        readonly List<WorldSortParticipant> _participants = new List<WorldSortParticipant>();
        readonly Dictionary<string, Transform> _objects = new Dictionary<string, Transform>(StringComparer.Ordinal);
        readonly AssetOwnerScope _owner;
        readonly CancellationToken _activationToken;
        WorldSortRegistration[] _registrations = Array.Empty<WorldSortRegistration>();
        bool _disposed;
        UniTask? _disposeTask;
        public GameObject Root { get; }
        public bool IsActive { get; private set; }
        public IReadOnlyDictionary<string, Transform> Objects => _objects;
        public IReadOnlyList<WorldSortParticipant> Participants => _participants;

        MapObjectPresentationBatch(AssetOwnerScope owner, CancellationToken token)
        {
            _owner = owner; _activationToken = token; Root = new GameObject("Map object presentation"); Root.SetActive(false);
        }

        public static async UniTask<MapObjectPresentationBatch> PrepareAsync(CompiledMapDocument map, MapObjectPresentationCatalog catalog,
            AddressableAssetService resources, CancellationToken token)
        {
            AssetOwnerScope owner = resources.CreateOwner("map-visual:" + map.MapId);
            return await PrepareCoreAsync(map, catalog, async (reference, cancellation) =>
            {
                ResourceLoadResult<GameObject> loaded = await resources.AcquireAsync<GameObject>(owner, reference.RuntimeKey, "map-visual:" + reference.AssetGUID, cancellation);
                if (!loaded.Succeeded) { throw new InvalidOperationException("地图表现加载失败：" + loaded.Code, loaded.Exception); }
                return new MapPresentationPrefabLease(loaded.Lease.Asset, loaded.Lease);
            }, owner, token);
        }

        public static UniTask<MapObjectPresentationBatch> PrepareAsync(CompiledMapDocument map, MapObjectPresentationCatalog catalog,
            Func<AssetReferenceGameObject, CancellationToken, UniTask<MapPresentationPrefabLease>> loader, CancellationToken token)
        { return PrepareCoreAsync(map, catalog, loader, null, token); }

        static async UniTask<MapObjectPresentationBatch> PrepareCoreAsync(CompiledMapDocument map, MapObjectPresentationCatalog catalog,
            Func<AssetReferenceGameObject, CancellationToken, UniTask<MapPresentationPrefabLease>> loader, AssetOwnerScope owner, CancellationToken token)
        {
            var batch = new MapObjectPresentationBatch(owner, token);
            try
            {
                token.ThrowIfCancellationRequested();
                if (catalog == null) { throw new InvalidOperationException("请选择对象表现目录"); }
                catalog.Validate();
                var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                foreach (MapObjectRecord record in map.CreateEditableCopy().Objects)
                {
                    token.ThrowIfCancellationRequested();
                    AssetReferenceGameObject reference = catalog.Resolve(record.DefinitionId, record.AppearanceVariant);
                    if (!prefabs.TryGetValue(reference.AssetGUID, out GameObject prefab))
                    {
                        MapPresentationPrefabLease lease = await loader(reference, token);
                        if (lease == null) { throw new InvalidOperationException("地图表现加载器返回空租约"); }
                        batch._leases.Add(lease); token.ThrowIfCancellationRequested();
                        prefab = lease.Prefab; MapObjectVisualPart.ValidatePrefab(prefab); prefabs.Add(reference.AssetGUID, prefab);
                    }
                    GameObject instance = UnityEngine.Object.Instantiate(prefab, batch.Root.transform, false);
                    instance.name = record.ObjectId;
                    instance.transform.SetPositionAndRotation(new Vector3((float)(map.OriginX + record.Position.X), (float)(map.OriginY + record.Position.Y), 0), Quaternion.Euler(0, 0, record.Rotation));
                    instance.SetActive(true); batch._objects.Add(record.ObjectId, instance.transform);
                    foreach (MapObjectVisualPart part in instance.GetComponentsInChildren<MapObjectVisualPart>(true))
                    {
                        WorldSortParticipant participant = part.GetComponent<WorldSortParticipant>();
                        participant.ConfigureExplicit(participant.Category, WorldSortingSystem.ComposeIdentity(map.MapId, record.ObjectId, part.PartId), participant.SortAnchor);
                        batch._participants.Add(participant);
                    }
                }
                token.ThrowIfCancellationRequested(); return batch;
            }
            catch { await batch.DisposeAsync(); throw; }
        }

        public void MoveToScene(Scene scene)
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(MapObjectPresentationBatch)); }
            SceneManager.MoveGameObjectToScene(Root, scene);
        }

        public void Activate(WorldSortingSystem sorting)
        {
            _activationToken.ThrowIfCancellationRequested();
            if (_disposed || (_owner != null && _owner.IsClosed)) { throw new ObjectDisposedException(nameof(MapObjectPresentationBatch)); }
            if (IsActive) { return; }
            if (!sorting.TryRegisterBatch(_participants, out _registrations, out string error)) { throw new InvalidOperationException(error); }
            try { Root.SetActive(true); sorting.Tick(); IsActive = true; }
            catch { foreach (WorldSortRegistration registration in _registrations) { registration.Dispose(); } Root.SetActive(false); throw; }
        }

        public UniTask DisposeAsync()
        {
            _disposeTask ??= DisposeCoreAsync().Preserve();
            return _disposeTask.Value;
        }

        public void Deactivate()
        {
            if (_disposed || !IsActive) { return; }
            foreach (WorldSortRegistration registration in _registrations) { registration.Dispose(); }
            _registrations = Array.Empty<WorldSortRegistration>();
            if (Root != null) { Root.SetActive(false); }
            IsActive = false;
        }

        async UniTask DisposeCoreAsync()
        {
            if (_disposed) { return; }
            _disposed = true; IsActive = false;
            foreach (WorldSortRegistration registration in _registrations) { registration.Dispose(); }
            if (Root != null)
            {
                Root.SetActive(false);
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(Root);
                    // 清理不可取消：必须等实例销毁完成后释放底层资源，调用方应等待此任务。
                    await UniTask.NextFrame(CancellationToken.None);
                }
                else { UnityEngine.Object.DestroyImmediate(Root); }
            }
            foreach (MapPresentationPrefabLease lease in _leases) { lease.Dispose(); }
            _leases.Clear(); _owner?.Dispose(); _objects.Clear(); _participants.Clear();
        }
    }
}
