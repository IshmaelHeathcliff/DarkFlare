using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

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

    internal sealed class AddressableAudioClipLoader : IAudioClipLoader, IDisposable
    {
        readonly AddressableAssetService _service;
        readonly AddressableAssetService _ownedService;
        readonly AssetOwnerScope _owner;

        public AddressableAudioClipLoader()
        {
            _ownedService = new AddressableAssetService();
            _service = _ownedService;
            _owner = _service.CreateOwner("standalone-audio-loader");
        }

        public AddressableAudioClipLoader(
            AddressableAssetService service,
            AssetOwnerScope owner)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public IAudioClipLoadHandle StartLoad(AssetReferenceT<AudioClip> reference)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.AssetGUID))
            {
                throw new ArgumentException("AudioClip Addressable 引用无效", nameof(reference));
            }

            return new AddressableAudioClipLoadHandle(_service, _owner, reference);
        }

        public void Dispose()
        {
            _owner.Close();
            _ownedService?.Dispose();
        }
    }

    internal sealed class AddressableAudioClipLoadHandle : IAudioClipLoadHandle
    {
        readonly AddressableAssetService _service;
        readonly AssetOwnerScope _owner;
        readonly AssetReferenceT<AudioClip> _reference;

        AssetLease<AudioClip> _lease;
        bool _released;

        public AddressableAudioClipLoadHandle(
            AddressableAssetService service,
            AssetOwnerScope owner,
            AssetReferenceT<AudioClip> reference)
        {
            _service = service;
            _owner = owner;
            _reference = reference;
        }

        public async UniTask<AudioClip> LoadAsync(CancellationToken cancellationToken)
        {
            ResourceLoadResult<AudioClip> result = await _service.AcquireAsync<AudioClip>(
                _owner,
                _reference.RuntimeKey,
                _reference.AssetGUID,
                cancellationToken);

            if (_released || result.Code == ResourceErrorCode.Cancelled)
            {
                result.Lease?.Dispose();
                throw new OperationCanceledException(cancellationToken);
            }

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"AudioClip 资源加载失败：{result.Code}",
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
            _lease?.Dispose();
            _lease = null;
        }
    }
}
