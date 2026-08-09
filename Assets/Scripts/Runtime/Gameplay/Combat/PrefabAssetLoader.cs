using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DarkFlare
{
    public class PrefabAssetLoader : IUtility
    {
        readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
        readonly Dictionary<string, AssetReferenceGameObject> _references = new Dictionary<string, AssetReferenceGameObject>();

        public async UniTask PreloadAsync(IEnumerable<AssetReferenceGameObject> references, CancellationToken token)
        {
            List<UniTask> loadTasks = new List<UniTask>();

            foreach (AssetReferenceGameObject reference in references)
            {
                if (reference == null || string.IsNullOrEmpty(reference.AssetGUID) || _prefabCache.ContainsKey(reference.AssetGUID))
                {
                    continue;
                }

                loadTasks.Add(LoadOneAsync(reference, token));
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

            Debug.LogError($"[PrefabAssetLoader] 未预热的 Addressable 引用: {reference.AssetGUID}");
            return null;
        }

        public void ReleaseAll()
        {
            foreach (AssetReferenceGameObject reference in _references.Values)
            {
                if (reference.OperationHandle.IsValid())
                {
                    reference.ReleaseAsset();
                }
            }

            _references.Clear();
            _prefabCache.Clear();
        }

        async UniTask LoadOneAsync(AssetReferenceGameObject reference, CancellationToken token)
        {
            Debug.Log($"[PrefabAssetLoader] 开始加载: {reference.AssetGUID}");
            AsyncOperationHandle<GameObject> handle = reference.LoadAssetAsync();
            try
            {
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: token);

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    Debug.LogError($"[PrefabAssetLoader] 加载失败: {reference.AssetGUID}");
                    ReleaseFailedReference(reference);
                    return;
                }

                Debug.Log($"[PrefabAssetLoader] 加载完成: {reference.AssetGUID} -> {handle.Result.name}");
                _references[reference.AssetGUID] = reference;
                _prefabCache[reference.AssetGUID] = handle.Result;
            }
            catch (OperationCanceledException)
            {
                ReleaseFailedReference(reference);
                throw;
            }
        }

        static void ReleaseFailedReference(AssetReferenceGameObject reference)
        {
            if (reference.OperationHandle.IsValid())
            {
                reference.ReleaseAsset();
            }
        }
    }
}
