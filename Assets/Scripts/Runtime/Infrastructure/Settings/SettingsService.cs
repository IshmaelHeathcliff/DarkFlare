using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DarkFlare
{
    public enum SettingsOperationCode
    {
        Success,
        DefaultLoaded,
        RecoveredFromBackup,
        InvalidSettings,
        Busy,
        Closed,
        Cancelled,
        StorageFailure,
    }

    public sealed class SettingsOperationResult
    {
        public SettingsOperationCode Code { get; }

        public UserSettingsSnapshot Settings { get; }

        public SettingsRecoverySource RecoverySource { get; }

        public Exception Exception { get; }

        public bool Succeeded => Code == SettingsOperationCode.Success
            || Code == SettingsOperationCode.DefaultLoaded
            || Code == SettingsOperationCode.RecoveredFromBackup;

        internal SettingsOperationResult(
            SettingsOperationCode code,
            UserSettingsSnapshot settings,
            SettingsRecoverySource recoverySource,
            Exception exception)
        {
            Code = code;
            Settings = settings;
            RecoverySource = recoverySource;
            Exception = exception;
        }
    }

    public sealed class SettingsService
    {
        readonly ILocalSettingsStorage _storage;
        int _operationInProgress;
        bool _closed;

        public SettingsService(ILocalSettingsStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Current = UserSettingsSnapshot.Default;
        }

        public UserSettingsSnapshot Current { get; private set; }

        public bool IsBusy => Volatile.Read(ref _operationInProgress) != 0;

        public event Action<UserSettingsSnapshot> Changed;

        public SettingsOperationResult Initialize(CancellationToken cancellationToken = default)
        {
            if (_closed)
            {
                return Failure(SettingsOperationCode.Closed);
            }

            LocalSettingsLoadResult loaded = _storage.Load(cancellationToken);

            if (loaded.Succeeded)
            {
                Current = UserSettingsSnapshot.FromDto(loaded.Document.Payload);
                SettingsOperationCode code = loaded.RecoverySource == SettingsRecoverySource.Backup
                    ? SettingsOperationCode.RecoveredFromBackup
                    : SettingsOperationCode.Success;
                return new SettingsOperationResult(
                    code,
                    Current,
                    loaded.RecoverySource,
                    loaded.Exception);
            }

            if (loaded.Code == LocalSettingsStorageCode.Cancelled)
            {
                return Failure(SettingsOperationCode.Cancelled, loaded.Exception);
            }

            Current = UserSettingsSnapshot.Default;
            SettingsRecoverySource source = loaded.Code == LocalSettingsStorageCode.NotFound
                ? SettingsRecoverySource.DefaultMissing
                : SettingsRecoverySource.DefaultInvalid;
            return new SettingsOperationResult(
                SettingsOperationCode.DefaultLoaded,
                Current,
                source,
                loaded.Exception);
        }

        public async UniTask<SettingsOperationResult> UpdateAsync(
            UserSettingsSnapshot candidate,
            CancellationToken cancellationToken = default)
        {
            if (_closed)
            {
                return Failure(SettingsOperationCode.Closed);
            }

            SettingsDataValidationResult validation = SettingsDataValidator.ValidateSnapshot(candidate);

            if (!validation.Succeeded)
            {
                return Failure(SettingsOperationCode.InvalidSettings);
            }

            if (Interlocked.CompareExchange(ref _operationInProgress, 1, 0) != 0)
            {
                return Failure(SettingsOperationCode.Busy);
            }

            try
            {
                UserSettingsDocumentDto document = new UserSettingsDocumentDto
                {
                    UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                    Payload = candidate.ToDto(),
                };
                await UniTask.SwitchToThreadPool();
                LocalSettingsCommitResult committed = _storage.Commit(document, cancellationToken);
                await UniTask.SwitchToMainThread();

                if (!committed.Succeeded)
                {
                    SettingsOperationCode code = committed.Code == LocalSettingsStorageCode.Cancelled
                        ? SettingsOperationCode.Cancelled
                        : SettingsOperationCode.StorageFailure;
                    return Failure(code, committed.Exception);
                }

                Current = UserSettingsSnapshot.FromDto(committed.Document.Payload);
                NotifyChanged(Current);
                return new SettingsOperationResult(
                    SettingsOperationCode.Success,
                    Current,
                    SettingsRecoverySource.Current,
                    null);
            }
            catch (OperationCanceledException exception)
            {
                await UniTask.SwitchToMainThread();
                return Failure(SettingsOperationCode.Cancelled, exception);
            }
            catch (Exception exception)
            {
                await UniTask.SwitchToMainThread();
                return Failure(SettingsOperationCode.StorageFailure, exception);
            }
            finally
            {
                Volatile.Write(ref _operationInProgress, 0);
            }
        }

        public void Close()
        {
            _closed = true;
            Changed = null;
        }

        void NotifyChanged(UserSettingsSnapshot settings)
        {
            Action<UserSettingsSnapshot> changed = Changed;

            if (changed == null)
            {
                return;
            }

            Delegate[] handlers = changed.GetInvocationList();

            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<UserSettingsSnapshot>)handlers[i]).Invoke(settings);
                }
                catch (Exception exception)
                {
                    ApplicationLog.Exception(LogEventIds.InfrastructureSettings, exception);
                }
            }
        }

        static SettingsOperationResult Failure(
            SettingsOperationCode code,
            Exception exception = null)
        {
            return new SettingsOperationResult(
                code,
                null,
                SettingsRecoverySource.DefaultInvalid,
                exception);
        }
    }
}
