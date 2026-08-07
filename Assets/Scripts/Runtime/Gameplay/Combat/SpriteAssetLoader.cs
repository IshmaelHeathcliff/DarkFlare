using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DarkFlare
{
    public class SpriteAssetLoader : IUtility
    {
        readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        readonly Dictionary<string, AsyncOperationHandle<Sprite>> _handles = new Dictionary<string, AsyncOperationHandle<Sprite>>();
        readonly HashSet<string> _loadingGuids = new HashSet<string>();

        public async UniTask PreloadAsync(IEnumerable<AssetReferenceSprite> references, CancellationToken token)
        {
            if (references == null)
            {
                return;
            }

            HashSet<string> requestedGuids = new HashSet<string>();
            List<UniTask> loadTasks = new List<UniTask>();

            foreach (AssetReferenceSprite reference in references)
            {
                string guid = reference != null ? reference.AssetGUID : string.Empty;

                if (string.IsNullOrWhiteSpace(guid)
                    || !requestedGuids.Add(guid)
                    || _spriteCache.ContainsKey(guid)
                    || _loadingGuids.Contains(guid))
                {
                    continue;
                }

                _loadingGuids.Add(guid);
                loadTasks.Add(LoadOneAsync(reference, token));
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

            Debug.LogError($"[SpriteAssetLoader] 图标未预热或加载失败: {iconGuid}");
            return null;
        }

        public void ReleaseAll()
        {
            foreach (AsyncOperationHandle<Sprite> handle in _handles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _handles.Clear();
            _spriteCache.Clear();
            _loadingGuids.Clear();
        }

        async UniTask LoadOneAsync(AssetReferenceSprite reference, CancellationToken token)
        {
            string guid = reference.AssetGUID;
            AsyncOperationHandle<Sprite> handle = reference.LoadAssetAsync<Sprite>();

            try
            {
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: token);

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    Debug.LogError($"[SpriteAssetLoader] 图标加载失败: {guid}");
                    ReleaseFailedHandle(handle);
                    return;
                }

                _handles[guid] = handle;
                _spriteCache[guid] = handle.Result;
                Debug.Log($"[SpriteAssetLoader] 图标加载完成: {guid} -> {handle.Result.name}");
            }
            catch (OperationCanceledException)
            {
                ReleaseFailedHandle(handle);
                throw;
            }
            finally
            {
                _loadingGuids.Remove(guid);
            }
        }

        static void ReleaseFailedHandle(AsyncOperationHandle<Sprite> handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }
}
