using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DarkFlare
{
    internal interface IAudioClipLoadHandle
    {
        UniTask<AudioClip> LoadAsync(CancellationToken cancellationToken);

        void Release();
    }

    internal interface IAudioClipLoader
    {
        IAudioClipLoadHandle StartLoad(AssetReferenceT<AudioClip> reference);
    }

    internal sealed class AddressableAudioClipLoader : IAudioClipLoader
    {
        public IAudioClipLoadHandle StartLoad(AssetReferenceT<AudioClip> reference)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.AssetGUID))
            {
                throw new ArgumentException("AudioClip Addressable 引用无效", nameof(reference));
            }

            AsyncOperationHandle<AudioClip> handle =
                Addressables.LoadAssetAsync<AudioClip>(reference.RuntimeKey);
            return new AddressableAudioClipLoadHandle(handle);
        }
    }

    internal sealed class AddressableAudioClipLoadHandle : IAudioClipLoadHandle
    {
        readonly AsyncOperationHandle<AudioClip> _handle;

        bool _released;

        public AddressableAudioClipLoadHandle(AsyncOperationHandle<AudioClip> handle)
        {
            _handle = handle;
        }

        public async UniTask<AudioClip> LoadAsync(CancellationToken cancellationToken)
        {
            await _handle.ToUniTask(cancellationToken: cancellationToken);

            if (_released || !_handle.IsValid())
            {
                throw new OperationCanceledException("AudioClip 加载句柄已释放");
            }

            if (_handle.Status != AsyncOperationStatus.Succeeded || _handle.Result == null)
            {
                throw new InvalidOperationException(
                    "AudioClip Addressable 加载失败",
                    _handle.OperationException);
            }

            return _handle.Result;
        }

        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            if (_handle.IsValid())
            {
                Addressables.Release(_handle);
            }
        }
    }
}
