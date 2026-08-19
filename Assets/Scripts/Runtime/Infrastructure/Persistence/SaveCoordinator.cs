using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DarkFlare
{
    public enum SaveOperation
    {
        Save,
        PrepareContinue,
        Continue,
        NewGame,
        Flush,
    }

    public enum SaveErrorCode
    {
        None,
        InvalidRequest,
        OperationInProgress,
        SessionUnavailable,
        SnapshotUnavailable,
        Cancelled,
        RootUnavailable,
        PermissionDenied,
        StorageFull,
        IoFailure,
        CommitFailed,
        SlotNotFound,
        FormatUnsupported,
        HeaderInvalid,
        ChecksumMismatch,
        NoValidGeneration,
        FutureSchemaUnsupported,
        MigrationFailed,
        ContentVersionMismatch,
        ContentMissing,
        PayloadInvalid,
        RestorePreparationFailed,
        RestoreCommitFailed,
        FlushTimedOut,
    }

    public enum SaveRecoverySource
    {
        None,
        Current,
        Backup,
    }

    public sealed class SaveOperationResult
    {
        public SaveOperation Operation { get; }

        public SaveSlotId SlotId { get; }

        public SaveErrorCode ErrorCode { get; }

        public SaveRecoverySource RecoverySource { get; }

        public bool CanRetry { get; }

        public Exception Exception { get; }

        public long CommitSequence { get; }

        public bool Succeeded => ErrorCode == SaveErrorCode.None;

        internal SaveOperationResult(
            SaveOperation operation,
            SaveSlotId slotId,
            SaveErrorCode errorCode,
            SaveRecoverySource recoverySource,
            bool canRetry,
            Exception exception,
            long commitSequence)
        {
            Operation = operation;
            SlotId = slotId;
            ErrorCode = errorCode;
            RecoverySource = recoverySource;
            CanRetry = canRetry;
            Exception = exception;
            CommitSequence = commitSequence;
        }

        internal static SaveOperationResult Success(
            SaveOperation operation,
            SaveSlotId slotId,
            long commitSequence = 0,
            SaveRecoverySource recoverySource = SaveRecoverySource.None)
        {
            return new SaveOperationResult(
                operation,
                slotId,
                SaveErrorCode.None,
                recoverySource,
                false,
                null,
                commitSequence);
        }

        internal static SaveOperationResult Failure(
            SaveOperation operation,
            SaveSlotId slotId,
            SaveErrorCode errorCode,
            bool canRetry = false,
            Exception exception = null)
        {
            return new SaveOperationResult(
                operation,
                slotId,
                errorCode,
                SaveRecoverySource.None,
                canRetry,
                exception,
                0);
        }
    }

    public sealed class PrepareContinueResult
    {
        public SaveOperationResult OperationResult { get; }

        public PreparedRestore PreparedRestore { get; }

        public bool Succeeded => OperationResult.Succeeded && PreparedRestore != null;

        internal PrepareContinueResult(
            SaveOperationResult operationResult,
            PreparedRestore preparedRestore)
        {
            OperationResult = operationResult;
            PreparedRestore = preparedRestore;
        }
    }

    public sealed class SaveCoordinator
    {
        public static readonly SaveSlotId AutoSlot = new SaveSlotId("auto");

        readonly LifecycleScope _profileScope;
        readonly ILocalSaveStorage _storage;
        readonly ContentCatalog _catalog;
        readonly string _gameVersion;
        readonly List<PendingSaveRequest> _pendingSaves = new List<PendingSaveRequest>();
        readonly List<IUnRegister> _dirtyRegistrations = new List<IUnRegister>();

        ISessionSnapshotSource _snapshotSource;
        GameSessionHost _boundSession;
        UniTaskCompletionSource<bool> _drainCompletion;
        UniTaskCompletionSource<bool> _loadCompletion;
        bool _accepting = true;
        bool _workerRunning;
        bool _loadInProgress;
        long _dirtyRevision;
        long _persistedRevision;

        public SaveCoordinator(
            LifecycleScope profileScope,
            ILocalSaveStorage storage,
            ContentCatalog catalog,
            string gameVersion)
        {
            _profileScope = profileScope ?? throw new ArgumentNullException(nameof(profileScope));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _gameVersion = string.IsNullOrWhiteSpace(gameVersion) ? "unknown" : gameVersion;
        }

        public bool IsAccepting => _accepting;

        public bool IsBusy => _workerRunning || _loadInProgress;

        public bool HasDirtyChanges => _dirtyRevision != _persistedRevision;

        public long DirtyRevision => _dirtyRevision;

        public int BoundArchitectureGeneration => _snapshotSource?.ArchitectureGeneration ?? 0;

        public void BindSession(GameSessionHost session, ISessionSnapshotSource snapshotSource)
        {
            if (!_accepting)
            {
                throw new InvalidOperationException("SaveCoordinator 已关闭请求入口");
            }

            if (session == null || snapshotSource == null || !snapshotSource.IsAvailable)
            {
                throw new ArgumentException("只能绑定可捕获的 Running Session");
            }

            UnbindSession(_boundSession);
            _boundSession = session;
            _snapshotSource = snapshotSource;
            RegisterDirtyEvents(session.Architecture);
            MarkDirty();
        }

        public void UnbindSession(GameSessionHost session)
        {
            if (_boundSession == null || (session != null && !ReferenceEquals(_boundSession, session)))
            {
                return;
            }

            _snapshotSource?.Invalidate();
            _snapshotSource = null;
            _boundSession = null;
            UnregisterDirtyEvents();
        }

        public void MarkDirty()
        {
            if (_accepting && _dirtyRevision < long.MaxValue)
            {
                _dirtyRevision++;
            }
        }

        public UniTask<SaveOperationResult> SaveAsync(
            SaveSlotId slotId,
            CancellationToken cancellationToken = default)
        {
            return EnqueueSave(slotId, cancellationToken, true);
        }

        public async UniTask<PrepareContinueResult> PrepareContinueAsync(
            SaveSlotId slotId,
            CancellationToken cancellationToken = default)
        {
            if (!_accepting)
            {
                return ContinueFailure(slotId, SaveErrorCode.InvalidRequest);
            }

            if (_loadInProgress)
            {
                return ContinueFailure(slotId, SaveErrorCode.OperationInProgress);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ContinueFailure(slotId, SaveErrorCode.Cancelled);
            }

            _loadInProgress = true;
            _loadCompletion = new UniTaskCompletionSource<bool>();

            try
            {
                await WaitForDrainAsync();
                cancellationToken.ThrowIfCancellationRequested();
                LocalSaveLoadResult loadResult;
                await UniTask.SwitchToThreadPool();

                try
                {
                    loadResult = _storage.LoadLatest(slotId, cancellationToken);
                }
                finally
                {
                    await UniTask.SwitchToMainThread();
                }

                if (!loadResult.Succeeded)
                {
                    return new PrepareContinueResult(
                        MapLoadFailure(slotId, loadResult),
                        null);
                }

                PreparedRestoreResult prepared = SaveRestorePreparer.Prepare(
                    loadResult.Document,
                    _catalog);

                if (!prepared.Succeeded)
                {
                    return ContinueFailure(
                        slotId,
                        ClassifyPreparationFailure(prepared.Issues));
                }

                SaveRecoverySource recoverySource = loadResult.RecoveredFromBackup
                    ? SaveRecoverySource.Backup
                    : SaveRecoverySource.Current;
                return new PrepareContinueResult(
                    SaveOperationResult.Success(
                        SaveOperation.PrepareContinue,
                        slotId,
                        loadResult.Document.Header.CommitSequence,
                        recoverySource),
                    prepared.Value);
            }
            catch (OperationCanceledException)
            {
                return ContinueFailure(slotId, SaveErrorCode.Cancelled);
            }
            finally
            {
                _loadInProgress = false;
                _loadCompletion.TrySetResult(true);
            }
        }

        public async UniTask<SaveOperationResult> CloseAndFlushAsync(
            SaveSlotId slotId,
            TimeSpan timeout)
        {
            _accepting = false;
            UniTask<SaveOperationResult> finalSave = ExecuteCloseAndFlushAsync(slotId).Preserve();
            UniTask timeoutTask = UniTask.Delay(timeout, DelayType.Realtime);
            int completed = await UniTask.WhenAny(finalSave.AsUniTask(), timeoutTask);

            if (completed != 0)
            {
                return SaveOperationResult.Failure(
                    SaveOperation.Flush,
                    slotId,
                    SaveErrorCode.FlushTimedOut,
                    true,
                    new TimeoutException($"存档 Flush 超过 {timeout.TotalSeconds:0.###} 秒"));
            }

            SaveOperationResult result = await finalSave;
            return result.Succeeded
                ? SaveOperationResult.Success(
                    SaveOperation.Flush,
                    slotId,
                    result.CommitSequence)
                : new SaveOperationResult(
                    SaveOperation.Flush,
                    slotId,
                    result.ErrorCode,
                    result.RecoverySource,
                    result.CanRetry,
                    result.Exception,
                    result.CommitSequence);
        }

        async UniTask<SaveOperationResult> ExecuteCloseAndFlushAsync(SaveSlotId slotId)
        {
            if (_loadInProgress && _loadCompletion != null)
            {
                await _loadCompletion.Task;
            }

            if (_snapshotSource != null && _snapshotSource.IsAvailable)
            {
                return await EnqueueSave(slotId, default, false);
            }

            await WaitForDrainAsync();
            return SaveOperationResult.Success(SaveOperation.Flush, slotId);
        }

        public void EmergencyClose()
        {
            _accepting = false;
            UnbindSession(_boundSession);
        }

        UniTask<SaveOperationResult> EnqueueSave(
            SaveSlotId slotId,
            CancellationToken cancellationToken,
            bool requireAccepting)
        {
            if ((requireAccepting && !_accepting) || _loadInProgress)
            {
                SaveErrorCode errorCode = _loadInProgress
                    ? SaveErrorCode.OperationInProgress
                    : SaveErrorCode.InvalidRequest;
                return UniTask.FromResult(SaveOperationResult.Failure(
                    SaveOperation.Save,
                    slotId,
                    errorCode));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromResult(SaveOperationResult.Failure(
                    SaveOperation.Save,
                    slotId,
                    SaveErrorCode.Cancelled));
            }

            if (_snapshotSource == null || !_snapshotSource.IsAvailable)
            {
                return UniTask.FromResult(SaveOperationResult.Failure(
                    SaveOperation.Save,
                    slotId,
                    SaveErrorCode.SessionUnavailable));
            }

            PendingSaveRequest request = new PendingSaveRequest(slotId);
            _pendingSaves.Add(request);

            if (!_workerRunning)
            {
                StartWorker();
            }

            return request.Completion.Task;
        }

        void StartWorker()
        {
            _workerRunning = true;
            _drainCompletion = new UniTaskCompletionSource<bool>();

            try
            {
                _profileScope.Tasks.Run(
                    "save-coordinator-writer",
                    ExecuteWriterAsync,
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                _workerRunning = false;
                CompleteAllPending(SaveOperationResult.Failure(
                    SaveOperation.Save,
                    AutoSlot,
                    SaveErrorCode.SessionUnavailable,
                    exception: exception));
                _drainCompletion.TrySetResult(false);
            }
        }

        async UniTask ExecuteWriterAsync(CancellationToken cancellationToken)
        {
            bool retired = false;
            List<PendingSaveRequest> activeBatch = null;

            try
            {
                while (_pendingSaves.Count > 0)
                {
                    SaveSlotId requestedSlot = _pendingSaves[0].SlotId;
                    List<PendingSaveRequest> batch = _pendingSaves
                        .Where(request => request.SlotId == requestedSlot)
                        .ToList();
                    activeBatch = batch;
                    _pendingSaves.RemoveAll(request => request.SlotId == requestedSlot);
                    ISessionSnapshotSource source = _snapshotSource;
                    int generation = source?.ArchitectureGeneration ?? 0;
                    SessionSnapshotResult snapshot = source != null && source.IsAvailable
                        ? source.Capture()
                        : null;

                    if (snapshot == null || !snapshot.Succeeded)
                    {
                        SaveOperationResult failure = SaveOperationResult.Failure(
                            SaveOperation.Save,
                            batch[0].SlotId,
                            source == null || !source.IsAvailable
                                ? SaveErrorCode.SessionUnavailable
                                : SaveErrorCode.SnapshotUnavailable);

                        if (_pendingSaves.Count == 0)
                        {
                            retired = true;
                            _workerRunning = false;
                            _drainCompletion?.TrySetResult(true);
                        }

                        CompleteBatch(batch, failure);
                        activeBatch = null;

                        if (retired)
                        {
                            return;
                        }

                        continue;
                    }

                    long capturedRevision = _dirtyRevision;
                    string catalogId = _catalog.CatalogId;
                    int contentVersion = _catalog.ContentVersion;
                    SaveSlotId slotId = batch[0].SlotId;
                    SaveOperationResult result;
                    await UniTask.SwitchToThreadPool();

                    try
                    {
                        result = CommitSnapshot(
                            slotId,
                            snapshot.Payload,
                            catalogId,
                            contentVersion,
                            cancellationToken);
                    }
                    finally
                    {
                        await UniTask.SwitchToMainThread();
                    }

                    if (result.Succeeded
                        && _snapshotSource != null
                        && _snapshotSource.ArchitectureGeneration == generation)
                    {
                        _persistedRevision = Math.Max(_persistedRevision, capturedRevision);
                    }

                    if (_pendingSaves.Count == 0)
                    {
                        retired = true;
                        _workerRunning = false;
                        _drainCompletion?.TrySetResult(true);
                    }

                    CompleteBatch(batch, result);
                    activeBatch = null;

                    if (retired)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                retired = true;
                _workerRunning = false;
                _drainCompletion?.TrySetResult(true);
                SaveOperationResult failure = SaveOperationResult.Failure(
                    SaveOperation.Save,
                    AutoSlot,
                    SaveErrorCode.Cancelled);
                CompleteActiveAndPending(activeBatch, failure);
            }
            catch (Exception exception)
            {
                retired = true;
                _workerRunning = false;
                _drainCompletion?.TrySetResult(true);
                SaveOperationResult failure = SaveOperationResult.Failure(
                    SaveOperation.Save,
                    AutoSlot,
                    SaveErrorCode.CommitFailed,
                    true,
                    exception);
                CompleteActiveAndPending(activeBatch, failure);
                throw;
            }
            finally
            {
                if (!retired)
                {
                    _workerRunning = false;
                    _drainCompletion?.TrySetResult(true);
                }
            }
        }

        void CompleteActiveAndPending(
            IReadOnlyList<PendingSaveRequest> activeBatch,
            SaveOperationResult result)
        {
            if (activeBatch != null)
            {
                CompleteBatch(activeBatch, result);
            }

            CompleteAllPending(result);
        }

        SaveOperationResult CommitSnapshot(
            SaveSlotId slotId,
            SavePayloadDto payload,
            string catalogId,
            int contentVersion,
            CancellationToken cancellationToken)
        {
            LocalSaveLoadResult previous = _storage.LoadLatest(slotId, cancellationToken);

            if (!previous.Succeeded
                && previous.Code != LocalSaveStorageCode.SlotNotFound
                && previous.Code != LocalSaveStorageCode.NoValidGeneration)
            {
                return MapLoadFailure(slotId, previous, SaveOperation.Save);
            }

            long nextSequence = GetNextCommitSequence(previous);
            string now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string created = previous.Succeeded
                ? previous.Document.Header.CreatedUtc
                : now;
            SaveDocumentDto document = new SaveDocumentDto
            {
                Header = new SaveHeaderDto
                {
                    FormatId = LocalSaveFormat.FormatId,
                    FormatVersion = LocalSaveFormat.FormatVersion,
                    SaveSchemaVersion = SaveSchemaVersion.Current.Value,
                    GameVersion = _gameVersion,
                    CatalogId = catalogId,
                    ContentVersion = contentVersion,
                    SlotId = slotId.Value,
                    CommitSequence = nextSequence,
                    CreatedUtc = created,
                    UpdatedUtc = now,
                    Summary = CreateSummary(payload),
                },
                Payload = payload,
            };
            LocalSaveCommitResult commit = _storage.Commit(slotId, document, cancellationToken);
            return commit.Succeeded
                ? SaveOperationResult.Success(SaveOperation.Save, slotId, nextSequence)
                : MapCommitFailure(slotId, commit);
        }

        async UniTask WaitForDrainAsync()
        {
            if (_workerRunning && _drainCompletion != null)
            {
                await _drainCompletion.Task;
            }
        }

        void RegisterDirtyEvents(IArchitecture architecture)
        {
            _dirtyRegistrations.Add(architecture.RegisterEvent<InventoryChangedEvent>(_ => MarkDirty()));
            _dirtyRegistrations.Add(architecture.RegisterEvent<GoldChangedEvent>(_ => MarkDirty()));
            _dirtyRegistrations.Add(architecture.RegisterEvent<EquipmentChangedEvent>(_ => MarkDirty()));
            _dirtyRegistrations.Add(architecture.RegisterEvent<TradeCompletedEvent>(_ => MarkDirty()));
            _dirtyRegistrations.Add(architecture.RegisterEvent<ItemCraftedEvent>(_ => MarkDirty()));
            _dirtyRegistrations.Add(architecture.RegisterEvent<ActorResourceChangedEvent>(_ => MarkDirty()));
            _dirtyRegistrations.Add(architecture.RegisterEvent<ActorRegisteredEvent>(_ => MarkDirty()));
            _dirtyRegistrations.Add(architecture.RegisterEvent<ActorUnregisteredEvent>(_ => MarkDirty()));
        }

        void UnregisterDirtyEvents()
        {
            for (int i = 0; i < _dirtyRegistrations.Count; i++)
            {
                _dirtyRegistrations[i].UnRegister();
            }

            _dirtyRegistrations.Clear();
        }

        void CompleteBatch(
            IReadOnlyList<PendingSaveRequest> batch,
            SaveOperationResult result)
        {
            for (int i = 0; i < batch.Count; i++)
            {
                SaveOperationResult requestResult = batch[i].SlotId == result.SlotId
                    ? result
                    : SaveOperationResult.Failure(
                        SaveOperation.Save,
                        batch[i].SlotId,
                        SaveErrorCode.InvalidRequest);
                batch[i].Completion.TrySetResult(requestResult);
            }
        }

        void CompleteAllPending(SaveOperationResult result)
        {
            List<PendingSaveRequest> pending = _pendingSaves.ToList();
            _pendingSaves.Clear();

            for (int i = 0; i < pending.Count; i++)
            {
                pending[i].Completion.TrySetResult(new SaveOperationResult(
                    result.Operation,
                    pending[i].SlotId,
                    result.ErrorCode,
                    result.RecoverySource,
                    result.CanRetry,
                    result.Exception,
                    result.CommitSequence));
            }
        }

        static SaveSummaryDto CreateSummary(SavePayloadDto payload)
        {
            return new SaveSummaryDto
            {
                RunId = payload.Run.InstanceIds.RunId,
                Gold = payload.Profile.Gold,
                ItemCount = payload.Items.Count,
                CurrentHealth = payload.Run.Player.Resources.CurrentHealth,
                MaxHealth = payload.Run.Player.Resources.MaxHealth,
            };
        }

        static long GetNextCommitSequence(LocalSaveLoadResult result)
        {
            long maximum = 0;

            if (result.Metadata?.Current != null)
            {
                maximum = Math.Max(maximum, result.Metadata.Current.CommitSequence);
            }

            if (result.Metadata?.Backup != null)
            {
                maximum = Math.Max(maximum, result.Metadata.Backup.CommitSequence);
            }

            IReadOnlyList<SaveGenerationFailure> invalid = result.Metadata?.InvalidGenerations;

            if (invalid != null)
            {
                for (int i = 0; i < invalid.Count; i++)
                {
                    maximum = Math.Max(maximum, invalid[i].CommitSequence);
                }
            }

            if (maximum == long.MaxValue)
            {
                throw new InvalidOperationException("存档提交序号已耗尽");
            }

            return maximum + 1;
        }

        static SaveErrorCode ClassifyPreparationFailure(IReadOnlyList<DtoMapIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Code == DtoMapIssueCode.MissingContent)
                {
                    return SaveErrorCode.ContentMissing;
                }
            }

            return SaveErrorCode.RestorePreparationFailed;
        }

        static SaveOperationResult MapCommitFailure(
            SaveSlotId slotId,
            LocalSaveCommitResult result)
        {
            SaveErrorCode code = result.Code switch
            {
                LocalSaveStorageCode.Busy => SaveErrorCode.OperationInProgress,
                LocalSaveStorageCode.Cancelled => SaveErrorCode.Cancelled,
                LocalSaveStorageCode.SerializationFailed => MapSerializationCode(result.SerializationCode),
                LocalSaveStorageCode.VerificationFailed => SaveErrorCode.CommitFailed,
                LocalSaveStorageCode.CommitConflict => SaveErrorCode.CommitFailed,
                LocalSaveStorageCode.IoFailure => MapIoException(result.Exception),
                _ => SaveErrorCode.CommitFailed,
            };
            Exception exception = result.Exception;

            if (exception == null && result.ValidationIssues.Count > 0)
            {
                exception = new InvalidDataException(string.Join(
                    "; ",
                    result.ValidationIssues.Select(issue => $"{issue.Path}: {issue.Message}")));
            }

            return SaveOperationResult.Failure(
                SaveOperation.Save,
                slotId,
                code,
                IsRetryable(code),
                exception);
        }

        static SaveOperationResult MapLoadFailure(
            SaveSlotId slotId,
            LocalSaveLoadResult result,
            SaveOperation operation = SaveOperation.PrepareContinue)
        {
            SaveErrorCode code = result.Code switch
            {
                LocalSaveStorageCode.Busy => SaveErrorCode.OperationInProgress,
                LocalSaveStorageCode.Cancelled => SaveErrorCode.Cancelled,
                LocalSaveStorageCode.SlotNotFound => SaveErrorCode.SlotNotFound,
                LocalSaveStorageCode.NoValidGeneration => SaveErrorCode.NoValidGeneration,
                LocalSaveStorageCode.IoFailure => MapIoException(result.Exception),
                _ => SaveErrorCode.IoFailure,
            };
            return SaveOperationResult.Failure(
                operation,
                slotId,
                code,
                IsRetryable(code),
                result.Exception);
        }

        static SaveErrorCode MapSerializationCode(SaveSerializationCode code)
        {
            return code switch
            {
                SaveSerializationCode.FormatUnsupported => SaveErrorCode.FormatUnsupported,
                SaveSerializationCode.HeaderInvalid => SaveErrorCode.HeaderInvalid,
                SaveSerializationCode.ChecksumMismatch => SaveErrorCode.ChecksumMismatch,
                SaveSerializationCode.SchemaUnsupported => SaveErrorCode.FutureSchemaUnsupported,
                SaveSerializationCode.MigrationFailed => SaveErrorCode.MigrationFailed,
                SaveSerializationCode.ValidationFailed => SaveErrorCode.PayloadInvalid,
                _ => SaveErrorCode.CommitFailed,
            };
        }

        static SaveErrorCode MapIoException(Exception exception)
        {
            if (exception is UnauthorizedAccessException || exception is SecurityException)
            {
                return SaveErrorCode.PermissionDenied;
            }

            if (exception is DriveNotFoundException || exception is DirectoryNotFoundException)
            {
                return SaveErrorCode.RootUnavailable;
            }

            return exception is IOException ioException
                && (ioException.HResult & 0xffff) == 0x70
                ? SaveErrorCode.StorageFull
                : SaveErrorCode.IoFailure;
        }

        static bool IsRetryable(SaveErrorCode code)
        {
            return code == SaveErrorCode.OperationInProgress
                || code == SaveErrorCode.RootUnavailable
                || code == SaveErrorCode.StorageFull
                || code == SaveErrorCode.IoFailure
                || code == SaveErrorCode.CommitFailed
                || code == SaveErrorCode.FlushTimedOut;
        }

        static PrepareContinueResult ContinueFailure(
            SaveSlotId slotId,
            SaveErrorCode errorCode)
        {
            return new PrepareContinueResult(
                SaveOperationResult.Failure(
                    SaveOperation.PrepareContinue,
                    slotId,
                    errorCode,
                    IsRetryable(errorCode)),
                null);
        }

        sealed class PendingSaveRequest
        {
            public PendingSaveRequest(SaveSlotId slotId)
            {
                SlotId = slotId;
            }

            public SaveSlotId SlotId { get; }

            public UniTaskCompletionSource<SaveOperationResult> Completion { get; } =
                new UniTaskCompletionSource<SaveOperationResult>();
        }
    }
}
