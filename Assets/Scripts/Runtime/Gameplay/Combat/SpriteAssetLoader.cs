using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public class SpriteAssetLoader : IUtility
    {
        readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        readonly Dictionary<string, AssetLease<Sprite>> _leases =
            new Dictionary<string, AssetLease<Sprite>>();
        readonly Dictionary<string, LoadOperation> _inFlight =
            new Dictionary<string, LoadOperation>();
        readonly AddressableAssetService _service;
        readonly AddressableAssetService _ownedService;
        readonly AssetOwnerScope _owner;

        int _generation;

        public SpriteAssetLoader()
        {
            _ownedService = new AddressableAssetService();
            _service = _ownedService;
            _owner = _service.CreateOwner("standalone-sprite-loader");
        }

        internal SpriteAssetLoader(AddressableAssetService service, AssetOwnerScope owner)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public async UniTask PreloadAsync(
            IEnumerable<AssetReferenceSprite> references,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (references == null)
            {
                return;
            }

            HashSet<string> requestedGuids = new HashSet<string>();
            List<UniTask> loadTasks = new List<UniTask>();

            foreach (AssetReferenceSprite reference in references)
            {
                token.ThrowIfCancellationRequested();
                string guid = reference != null ? reference.AssetGUID : string.Empty;

                if (string.IsNullOrWhiteSpace(guid)
                    || !requestedGuids.Add(guid)
                    || _spriteCache.ContainsKey(guid))
                {
                    continue;
                }

                if (!_inFlight.TryGetValue(guid, out LoadOperation operation))
                {
                    operation = new LoadOperation(guid, _generation);
                    _inFlight.Add(guid, operation);
                    operation.Runner = LoadOneAsync(reference, operation);
                }

                loadTasks.Add(operation.Completion.Task.AttachExternalCancellation(token));
            }

            await UniTask.WhenAll(loadTasks);
        }

        public Sprite GetSprite(string iconGuid)
        {
            if (string.IsNullOrWhiteSpace(iconGuid))
            {
                return null;
            }

            if (_spriteCache.TryGetValue(iconGuid, out Sprite sprite))
            {
                return sprite;
            }

            ApplicationLog.Warning(
                LogEventIds.ResourceSprite,
                $"[SpriteAssetLoader] 图标未预热或加载失败: {iconGuid}");
            return null;
        }

        public void ReleaseAll()
        {
            _generation++;
            LoadOperation[] operations = new LoadOperation[_inFlight.Count];
            _inFlight.Values.CopyTo(operations, 0);
            _inFlight.Clear();

            for (int i = 0; i < operations.Length; i++)
            {
                operations[i].Invalidated = true;
                operations[i].Completion.TrySetCanceled();
            }

            foreach (AssetLease<Sprite> lease in _leases.Values)
            {
                lease.Dispose();
            }

            _leases.Clear();
            _spriteCache.Clear();
        }

        public void Dispose()
        {
            ReleaseAll();
            _owner.Close();
            _ownedService?.Dispose();
        }

        async UniTask LoadOneAsync(AssetReferenceSprite reference, LoadOperation operation)
        {
            ResourceLoadResult<Sprite> result = await _service.AcquireAsync<Sprite>(
                _owner,
                reference.RuntimeKey,
                operation.Guid);

            if (operation.Invalidated || operation.Generation != _generation)
            {
                result.Lease?.Dispose();
                operation.Completion.TrySetCanceled();
                return;
            }

            if (!result.Succeeded)
            {
                ApplicationLog.Error(
                    LogEventIds.ResourceSprite,
                    $"[SpriteAssetLoader] 图标加载失败: {operation.Guid} ({result.Code})");
                RemoveIfCurrent(operation);
                operation.Completion.TrySetResult();
                return;
            }

            _leases.Add(operation.Guid, result.Lease);
            _spriteCache.Add(operation.Guid, result.Lease.Asset);
            RemoveIfCurrent(operation);
            ApplicationLog.Info(
                LogEventIds.ResourceSprite,
                $"[SpriteAssetLoader] 图标加载完成: {operation.Guid} -> {result.Lease.Asset.name}");
            operation.Completion.TrySetResult();
        }

        void RemoveIfCurrent(LoadOperation operation)
        {
            if (_inFlight.TryGetValue(operation.Guid, out LoadOperation current)
                && ReferenceEquals(current, operation))
            {
                _inFlight.Remove(operation.Guid);
            }
        }

        sealed class LoadOperation
        {
            public LoadOperation(string guid, int generation)
            {
                Guid = guid;
                Generation = generation;
                Completion = new UniTaskCompletionSource();
            }

            public string Guid { get; }

            public int Generation { get; }

            public UniTaskCompletionSource Completion { get; }

            public UniTask Runner { get; set; }

            public bool Invalidated { get; set; }
        }
    }
}
