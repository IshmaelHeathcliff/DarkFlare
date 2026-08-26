using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    internal interface IPrefabAssetLoadHandle
    {
        UniTask<GameObject> LoadAsync();

        void Release();
    }

    internal interface IPrefabAssetLoadBackend
    {
        IPrefabAssetLoadHandle StartLoad(AssetReferenceGameObject reference);
    }

    internal sealed class AddressablePrefabAssetLoadBackend : IPrefabAssetLoadBackend
    {
        readonly AddressableAssetService _service;
        readonly AssetOwnerScope _owner;

        public AddressablePrefabAssetLoadBackend(
            AddressableAssetService service,
            AssetOwnerScope owner)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public IPrefabAssetLoadHandle StartLoad(AssetReferenceGameObject reference)
        {
            return new AddressablePrefabAssetLoadHandle(_service, _owner, reference);
        }
    }

    internal sealed class AddressablePrefabAssetLoadHandle : IPrefabAssetLoadHandle
    {
        readonly AddressableAssetService _service;
        readonly AssetOwnerScope _owner;
        readonly AssetReferenceGameObject _reference;
        readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        AssetLease<GameObject> _lease;
        bool _released;

        public AddressablePrefabAssetLoadHandle(
            AddressableAssetService service,
            AssetOwnerScope owner,
            AssetReferenceGameObject reference)
        {
            _service = service;
            _owner = owner;
            _reference = reference;
        }

        public async UniTask<GameObject> LoadAsync()
        {
            ResourceLoadResult<GameObject> result = await _service.AcquireAsync<GameObject>(
                _owner,
                _reference.RuntimeKey,
                _reference.AssetGUID,
                _cancellation.Token);

            if (_released || result.Code == ResourceErrorCode.Cancelled)
            {
                result.Lease?.Dispose();
                throw new OperationCanceledException("Prefab Asset Lease 已释放");
            }

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Prefab 资源加载未成功：{result.Code}",
                    result.Exception);
            }

            _lease = result.Lease;
            return _lease.Asset;
        }

        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _cancellation.Cancel();
            _cancellation.Dispose();
            _lease?.Dispose();
            _lease = null;
        }
    }

    public class PrefabAssetLoader : IUtility
    {
        readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
        readonly Dictionary<string, IPrefabAssetLoadHandle> _handles =
            new Dictionary<string, IPrefabAssetLoadHandle>();
        readonly Dictionary<string, LoadOperation> _inFlight =
            new Dictionary<string, LoadOperation>();
        readonly IPrefabAssetLoadBackend _backend;
        readonly AddressableAssetService _ownedService;
        readonly AssetOwnerScope _owner;

        int _generation;

        public PrefabAssetLoader()
        {
            _ownedService = new AddressableAssetService();
            _owner = _ownedService.CreateOwner("standalone-prefab-loader");
            _backend = new AddressablePrefabAssetLoadBackend(_ownedService, _owner);
        }

        internal PrefabAssetLoader(
            AddressableAssetService service,
            AssetOwnerScope owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _backend = new AddressablePrefabAssetLoadBackend(
                service ?? throw new ArgumentNullException(nameof(service)),
                owner);
        }

        internal PrefabAssetLoader(IPrefabAssetLoadBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public void Dispose()
        {
            ReleaseAll();
            _owner?.Close();
            _ownedService?.Dispose();
        }

        public async UniTask PreloadAsync(
            IEnumerable<AssetReferenceGameObject> references,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (references == null)
            {
                return;
            }

            List<UniTask> loadTasks = new List<UniTask>();
            HashSet<string> requestedGuids = new HashSet<string>();

            foreach (AssetReferenceGameObject reference in references)
            {
                token.ThrowIfCancellationRequested();
                string guid = reference != null ? reference.AssetGUID : string.Empty;

                if (string.IsNullOrWhiteSpace(guid)
                    || _prefabCache.ContainsKey(guid)
                    || !requestedGuids.Add(guid))
                {
                    continue;
                }

                if (!_inFlight.TryGetValue(guid, out LoadOperation operation))
                {
                    operation = StartLoad(reference, guid);
                }

                loadTasks.Add(operation.Completion.Task.AttachExternalCancellation(token));
            }

            await UniTask.WhenAll(loadTasks);
        }

        public GameObject GetPrefab(AssetReferenceGameObject reference)
        {
            if (reference == null || string.IsNullOrEmpty(reference.AssetGUID))
            {
                return null;
            }

            if (_prefabCache.TryGetValue(reference.AssetGUID, out GameObject prefab))
            {
                return prefab;
            }

            ApplicationLog.Error(LogEventIds.ResourcePrefab, $"[PrefabAssetLoader] 未预热的 Addressable 引用: {reference.AssetGUID}");
            return null;
        }

        public void ReleaseAll()
        {
            _generation++;
            List<LoadOperation> operations = new List<LoadOperation>(_inFlight.Values);
            List<IPrefabAssetLoadHandle> handles =
                new List<IPrefabAssetLoadHandle>(_handles.Values);
            _inFlight.Clear();
            _handles.Clear();
            _prefabCache.Clear();

            for (int i = 0; i < operations.Count; i++)
            {
                LoadOperation operation = operations[i];
                operation.Invalidated = true;
                ReleaseOperationHandle(operation);
                operation.Completion.TrySetCanceled();
            }

            for (int i = 0; i < handles.Count; i++)
            {
                handles[i].Release();
            }
        }

        LoadOperation StartLoad(AssetReferenceGameObject reference, string guid)
        {
            IPrefabAssetLoadHandle handle;

            try
            {
                handle = _backend.StartLoad(reference);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"无法开始加载 Prefab Addressable: {guid}",
                    exception);
            }

            if (handle == null)
            {
                throw new InvalidOperationException(
                    $"Prefab Addressable 加载后端没有返回有效句柄: {guid}");
            }

            LoadOperation operation = new LoadOperation(
                guid,
                _generation,
                handle);
            _inFlight.Add(guid, operation);
            operation.Runner = LoadAndCompleteAsync(operation);
            return operation;
        }

        async UniTask LoadAndCompleteAsync(LoadOperation operation)
        {
            Exception failure = null;

            try
            {
                GameObject prefab = await operation.Handle.LoadAsync();

                if (!IsCurrentOperation(operation))
                {
                    ReleaseOperationHandle(operation);
                }
                else if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Prefab Addressable 加载结果为空: {operation.Guid}");
                }
                else
                {
                    _handles.Add(operation.Guid, operation.Handle);
                    _prefabCache.Add(operation.Guid, prefab);
                    operation.HandleTransferred = true;
                }
            }
            catch (Exception exception)
            {
                UndoPartialTransfer(operation);
                ReleaseOperationHandle(operation);
                failure = exception is OperationCanceledException
                    ? exception
                    : new InvalidOperationException(
                        $"Prefab Addressable 加载失败: {operation.Guid}",
                        exception);
            }
            finally
            {
                RemoveIfCurrent(operation);
            }

            if (operation.Invalidated || operation.Generation != _generation)
            {
                ReleaseOperationHandle(operation);
                operation.Completion.TrySetCanceled();
                return;
            }

            if (failure != null)
            {
                operation.Completion.TrySetException(failure);
                return;
            }

            if (!operation.HandleTransferred)
            {
                operation.Completion.TrySetCanceled();
                return;
            }

            ApplicationLog.Info(LogEventIds.ResourcePrefab, $"[PrefabAssetLoader] 加载完成: {operation.Guid} -> {_prefabCache[operation.Guid].name}");
            operation.Completion.TrySetResult();
        }

        bool IsCurrentOperation(LoadOperation operation)
        {
            return !operation.Invalidated
                   && operation.Generation == _generation
                   && _inFlight.TryGetValue(operation.Guid, out LoadOperation current)
                   && ReferenceEquals(current, operation);
        }

        void RemoveIfCurrent(LoadOperation operation)
        {
            if (_inFlight.TryGetValue(operation.Guid, out LoadOperation current)
                && ReferenceEquals(current, operation))
            {
                _inFlight.Remove(operation.Guid);
            }
        }

        void UndoPartialTransfer(LoadOperation operation)
        {
            if (_handles.TryGetValue(
                    operation.Guid,
                    out IPrefabAssetLoadHandle currentHandle)
                && ReferenceEquals(currentHandle, operation.Handle))
            {
                _handles.Remove(operation.Guid);
                _prefabCache.Remove(operation.Guid);
                operation.HandleTransferred = false;
            }
        }

        static void ReleaseOperationHandle(LoadOperation operation)
        {
            if (operation.HandleReleased || operation.HandleTransferred)
            {
                return;
            }

            operation.HandleReleased = true;
            operation.Handle.Release();
        }

        sealed class LoadOperation
        {
            public LoadOperation(
                string guid,
                int generation,
                IPrefabAssetLoadHandle handle)
            {
                Guid = guid;
                Generation = generation;
                Handle = handle;
                Completion = new UniTaskCompletionSource();
            }

            public string Guid { get; }

            public int Generation { get; }

            public IPrefabAssetLoadHandle Handle { get; }

            public UniTaskCompletionSource Completion { get; }

            public UniTask Runner { get; set; }

            public bool Invalidated { get; set; }

            public bool HandleReleased { get; set; }

            public bool HandleTransferred { get; set; }
        }
    }
}
