using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DarkFlare
{
    public enum ResourceErrorCode
    {
        None,
        InvalidReference,
        OwnerClosed,
        Cancelled,
        TypeMismatch,
        LoadFailed,
        ServiceClosed,
    }

    public sealed class ResourceLoadResult<T> where T : UnityEngine.Object
    {
        ResourceLoadResult(
            ResourceErrorCode code,
            AssetLease<T> lease,
            Exception exception)
        {
            Code = code;
            Lease = lease;
            Exception = exception;
        }

        public ResourceErrorCode Code { get; }

        public AssetLease<T> Lease { get; }

        public Exception Exception { get; }

        public bool Succeeded => Code == ResourceErrorCode.None
            && Lease != null
            && Lease.Asset != null;

        internal static ResourceLoadResult<T> Success(AssetLease<T> lease)
        {
            return new ResourceLoadResult<T>(ResourceErrorCode.None, lease, null);
        }

        internal static ResourceLoadResult<T> Failure(
            ResourceErrorCode code,
            Exception exception = null)
        {
            return new ResourceLoadResult<T>(code, null, exception);
        }
    }

    public readonly struct ResourceDiagnosticsSnapshot
    {
        public ResourceDiagnosticsSnapshot(
            int activeOwners,
            int activeEntries,
            int activeLeases,
            int inFlightLoads)
        {
            ActiveOwners = activeOwners;
            ActiveEntries = activeEntries;
            ActiveLeases = activeLeases;
            InFlightLoads = inFlightLoads;
        }

        public int ActiveOwners { get; }

        public int ActiveEntries { get; }

        public int ActiveLeases { get; }

        public int InFlightLoads { get; }
    }

    internal interface IAddressableAssetLoadHandle<T> where T : UnityEngine.Object
    {
        UniTask<T> LoadAsync();

        void Release();
    }

    internal interface IAddressableAssetBackend
    {
        IAddressableAssetLoadHandle<T> StartLoad<T>(object runtimeKey)
            where T : UnityEngine.Object;
    }

    internal sealed class AddressableAssetBackend : IAddressableAssetBackend
    {
        public IAddressableAssetLoadHandle<T> StartLoad<T>(object runtimeKey)
            where T : UnityEngine.Object
        {
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(runtimeKey);
            return new AddressableAssetLoadHandle<T>(handle);
        }
    }

    internal sealed class AddressableAssetLoadHandle<T> : IAddressableAssetLoadHandle<T>
        where T : UnityEngine.Object
    {
        readonly AsyncOperationHandle<T> _handle;

        bool _released;

        public AddressableAssetLoadHandle(AsyncOperationHandle<T> handle)
        {
            _handle = handle;
        }

        public async UniTask<T> LoadAsync()
        {
            await _handle.ToUniTask();

            if (_released || !_handle.IsValid())
            {
                throw new OperationCanceledException("Addressables Asset Handle 已释放");
            }

            if (_handle.Status != AsyncOperationStatus.Succeeded || _handle.Result == null)
            {
                throw new InvalidOperationException(
                    "Addressables Asset 加载失败",
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

    public sealed class AssetOwnerScope : IDisposable
    {
        readonly object _gate = new object();
        readonly AddressableAssetService _service;
        readonly HashSet<IAssetLease> _leases = new HashSet<IAssetLease>();

        bool _closed;

        internal AssetOwnerScope(AddressableAssetService service, int id, string name)
        {
            _service = service;
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }

        public bool IsClosed
        {
            get
            {
                lock (_gate)
                {
                    return _closed;
                }
            }
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            IAssetLease[] leases;

            lock (_gate)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                leases = new IAssetLease[_leases.Count];
                _leases.CopyTo(leases);
                _leases.Clear();
            }

            for (int i = 0; i < leases.Length; i++)
            {
                leases[i].DisposeFromOwner();
            }

            _service.ReleaseOwner(this);
        }

        internal bool TryAdd(IAssetLease lease)
        {
            lock (_gate)
            {
                return !_closed && _leases.Add(lease);
            }
        }

        internal void Remove(IAssetLease lease)
        {
            lock (_gate)
            {
                _leases.Remove(lease);
            }
        }
    }

    internal interface IAssetLease
    {
        void DisposeFromOwner();
    }

    public sealed class AssetLease<T> : IDisposable, IAssetLease where T : UnityEngine.Object
    {
        readonly AddressableAssetService _service;
        readonly AssetOwnerScope _owner;
        readonly AddressableAssetService.ResourceEntry<T> _entry;

        bool _disposed;

        internal AssetLease(
            AddressableAssetService service,
            AssetOwnerScope owner,
            AddressableAssetService.ResourceEntry<T> entry,
            T asset)
        {
            _service = service;
            _owner = owner;
            _entry = entry;
            Asset = asset;
        }

        public T Asset { get; }

        public bool IsDisposed => _disposed;

        public void Dispose()
        {
            DisposeCore(true);
        }

        void IAssetLease.DisposeFromOwner()
        {
            DisposeCore(false);
        }

        void DisposeCore(bool removeFromOwner)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (removeFromOwner)
            {
                _owner.Remove(this);
            }

            _service.ReleaseLease(_entry);
        }
    }

    public sealed class AddressableAssetService : IDisposable
    {
        internal abstract class ResourceEntryBase
        {
            protected ResourceEntryBase(string key, Type assetType)
            {
                Key = key;
                AssetType = assetType;
            }

            public string Key { get; }

            public Type AssetType { get; }

            public int WaiterCount { get; set; }

            public int LeaseCount { get; set; }

            public bool Completed { get; set; }

            public bool Removed { get; set; }

            public abstract void ReleaseHandle();
        }

        internal sealed class ResourceEntry<T> : ResourceEntryBase where T : UnityEngine.Object
        {
            public ResourceEntry(
                string key,
                IAddressableAssetLoadHandle<T> handle)
                : base(key, typeof(T))
            {
                Handle = handle;
                Completion = new UniTaskCompletionSource<T>();
            }

            public IAddressableAssetLoadHandle<T> Handle { get; }

            public UniTaskCompletionSource<T> Completion { get; }

            public UniTask Runner { get; set; }

            public override void ReleaseHandle()
            {
                Handle.Release();
            }
        }

        readonly object _gate = new object();
        readonly Dictionary<string, ResourceEntryBase> _entries =
            new Dictionary<string, ResourceEntryBase>(StringComparer.Ordinal);
        readonly HashSet<AssetOwnerScope> _owners = new HashSet<AssetOwnerScope>();
        readonly IAddressableAssetBackend _backend;

        int _nextOwnerId;
        int _activeLeases;
        bool _closed;

        public AddressableAssetService()
            : this(new AddressableAssetBackend())
        {
        }

        internal AddressableAssetService(IAddressableAssetBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public AssetOwnerScope CreateOwner(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("资源 owner 名称不能为空", nameof(name));
            }

            lock (_gate)
            {
                if (_closed)
                {
                    throw new ObjectDisposedException(nameof(AddressableAssetService));
                }

                AssetOwnerScope owner = new AssetOwnerScope(this, ++_nextOwnerId, name.Trim());
                _owners.Add(owner);
                return owner;
            }
        }

        public async UniTask<ResourceLoadResult<T>> AcquireAsync<T>(
            AssetOwnerScope owner,
            object runtimeKey,
            string diagnosticKey,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (owner == null || runtimeKey == null || string.IsNullOrWhiteSpace(diagnosticKey))
            {
                return ResourceLoadResult<T>.Failure(ResourceErrorCode.InvalidReference);
            }

            cancellationToken.ThrowIfCancellationRequested();
            ResourceEntry<T> entry;

            lock (_gate)
            {
                if (_closed)
                {
                    return ResourceLoadResult<T>.Failure(ResourceErrorCode.ServiceClosed);
                }

                if (!_owners.Contains(owner) || owner.IsClosed)
                {
                    return ResourceLoadResult<T>.Failure(ResourceErrorCode.OwnerClosed);
                }

                if (_entries.TryGetValue(diagnosticKey, out ResourceEntryBase existing))
                {
                    if (!(existing is ResourceEntry<T> typed))
                    {
                        return ResourceLoadResult<T>.Failure(ResourceErrorCode.TypeMismatch);
                    }

                    entry = typed;
                }
                else
                {
                    IAddressableAssetLoadHandle<T> handle;

                    try
                    {
                        handle = _backend.StartLoad<T>(runtimeKey);
                    }
                    catch (Exception exception)
                    {
                        ApplicationLog.Exception(
                            LogEventIds.ResourceLoadFailed,
                            exception,
                            message: $"无法开始加载资源 {diagnosticKey}");
                        return ResourceLoadResult<T>.Failure(
                            ResourceErrorCode.LoadFailed,
                            exception);
                    }

                    entry = new ResourceEntry<T>(diagnosticKey, handle);
                    _entries.Add(diagnosticKey, entry);
                    entry.Runner = RunEntryAsync(entry);
                }

                entry.WaiterCount++;
            }

            T asset;

            try
            {
                asset = await entry.Completion.Task.AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                ReleaseWaiter(entry);
                return ResourceLoadResult<T>.Failure(ResourceErrorCode.Cancelled, exception);
            }
            catch (Exception exception)
            {
                ReleaseWaiter(entry);
                return ResourceLoadResult<T>.Failure(ResourceErrorCode.LoadFailed, exception);
            }

            lock (_gate)
            {
                entry.WaiterCount--;

                if (_closed
                    || entry.Removed
                    || !_owners.Contains(owner)
                    || owner.IsClosed)
                {
                    TryReleaseEntry(entry);
                    return ResourceLoadResult<T>.Failure(
                        _closed ? ResourceErrorCode.ServiceClosed : ResourceErrorCode.OwnerClosed);
                }

                AssetLease<T> lease = new AssetLease<T>(this, owner, entry, asset);

                if (!owner.TryAdd(lease))
                {
                    TryReleaseEntry(entry);
                    return ResourceLoadResult<T>.Failure(ResourceErrorCode.OwnerClosed);
                }

                entry.LeaseCount++;
                _activeLeases++;
                return ResourceLoadResult<T>.Success(lease);
            }
        }

        public ResourceDiagnosticsSnapshot GetDiagnostics()
        {
            lock (_gate)
            {
                int inFlight = 0;

                foreach (ResourceEntryBase entry in _entries.Values)
                {
                    if (!entry.Completed)
                    {
                        inFlight++;
                    }
                }

                return new ResourceDiagnosticsSnapshot(
                    _owners.Count,
                    _entries.Count,
                    _activeLeases,
                    inFlight);
            }
        }

        public void Dispose()
        {
            AssetOwnerScope[] owners;

            lock (_gate)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                owners = new AssetOwnerScope[_owners.Count];
                _owners.CopyTo(owners);
            }

            for (int i = 0; i < owners.Length; i++)
            {
                owners[i].Close();
            }

            lock (_gate)
            {
                ResourceEntryBase[] entries = new ResourceEntryBase[_entries.Count];
                _entries.Values.CopyTo(entries, 0);
                _entries.Clear();

                for (int i = 0; i < entries.Length; i++)
                {
                    entries[i].Removed = true;
                    entries[i].ReleaseHandle();
                }
            }
        }

        internal void ReleaseLease<T>(ResourceEntry<T> entry) where T : UnityEngine.Object
        {
            lock (_gate)
            {
                if (entry.LeaseCount > 0)
                {
                    entry.LeaseCount--;
                    _activeLeases--;
                }

                TryReleaseEntry(entry);
            }
        }

        internal void ReleaseOwner(AssetOwnerScope owner)
        {
            lock (_gate)
            {
                _owners.Remove(owner);
            }
        }

        async UniTask RunEntryAsync<T>(ResourceEntry<T> entry) where T : UnityEngine.Object
        {
            try
            {
                T asset = await entry.Handle.LoadAsync();

                lock (_gate)
                {
                    entry.Completed = true;

                    if (entry.Removed || _closed)
                    {
                        entry.ReleaseHandle();
                        entry.Completion.TrySetCanceled();
                        return;
                    }
                }

                entry.Completion.TrySetResult(asset);
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    entry.Completed = true;
                }

                ApplicationLog.Exception(
                    LogEventIds.ResourceLoadFailed,
                    exception,
                    message: $"资源加载失败 {entry.Key}");
                entry.Completion.TrySetException(exception);
            }
        }

        void ReleaseWaiter(ResourceEntryBase entry)
        {
            lock (_gate)
            {
                if (entry.WaiterCount > 0)
                {
                    entry.WaiterCount--;
                }

                TryReleaseEntry(entry);
            }
        }

        void TryReleaseEntry(ResourceEntryBase entry)
        {
            if (entry.Removed || entry.WaiterCount > 0 || entry.LeaseCount > 0)
            {
                return;
            }

            if (_entries.TryGetValue(entry.Key, out ResourceEntryBase current)
                && ReferenceEquals(current, entry))
            {
                _entries.Remove(entry.Key);
            }

            entry.Removed = true;
            entry.ReleaseHandle();
        }
    }
}
