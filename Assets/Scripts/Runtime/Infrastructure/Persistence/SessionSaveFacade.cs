using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace DarkFlare
{
    public sealed class SessionSaveFacade
    {
        readonly ApplicationHost _host;
        readonly GameSessionHost _session;
        readonly SaveCoordinator _coordinator;
        readonly Scene _scene;
        readonly GameplaySceneConfiguration _configuration;
        bool _operationInProgress;

        public SessionSaveFacade(
            ApplicationHost host,
            GameSessionHost session,
            SaveCoordinator coordinator,
            Scene scene,
            GameplaySceneConfiguration configuration)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _scene = scene;
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public int ArchitectureGeneration => _session.ArchitectureGeneration;

        public bool IsBusy => _operationInProgress || _coordinator.IsBusy;

        public bool IsAvailable => !_operationInProgress
            && ReferenceEquals(_host.CurrentSession, _session)
            && _session.State == GameSessionState.Running
            && _session.IsCurrentArchitectureLease
            && _scene.IsValid()
            && _scene.isLoaded;

        public async UniTask<SaveOperationResult> SaveAutoAsync(
            CancellationToken cancellationToken = default)
        {
            if (!TryBeginOperation(cancellationToken, out SaveOperationResult failure))
            {
                return failure;
            }

            try
            {
                return await _coordinator.SaveAsync(
                    SaveCoordinator.AutoSlot,
                    cancellationToken);
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public async UniTask<SaveOperationResult> ProbeAutoSaveAsync(
            CancellationToken cancellationToken = default)
        {
            if (!TryBeginOperation(cancellationToken, out SaveOperationResult failure))
            {
                return ChangeOperation(failure, SaveOperation.PrepareContinue);
            }

            try
            {
                PrepareContinueResult prepared = await _coordinator.PrepareContinueAsync(
                    SaveCoordinator.AutoSlot,
                    cancellationToken);
                return prepared.OperationResult;
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public async UniTask<SaveOperationResult> ContinueAutoAsync(
            CancellationToken cancellationToken = default)
        {
            if (!TryBeginOperation(cancellationToken, out SaveOperationResult failure))
            {
                return ChangeOperation(failure, SaveOperation.Continue);
            }

            try
            {
                PrepareContinueResult prepared = await _coordinator.PrepareContinueAsync(
                    SaveCoordinator.AutoSlot,
                    cancellationToken);

                if (!prepared.Succeeded)
                {
                    return ChangeOperation(
                        prepared.OperationResult,
                        SaveOperation.Continue);
                }

                if (!IsCurrentSession())
                {
                    return SaveOperationResult.Failure(
                        SaveOperation.Continue,
                        SaveCoordinator.AutoSlot,
                        SaveErrorCode.SessionUnavailable);
                }

                SaveOperationResult transition = await BeginTransitionAsync(
                    new RestoreGameSessionInitializer(
                        _configuration,
                        prepared.PreparedRestore),
                    SaveOperation.Continue);

                if (!transition.Succeeded)
                {
                    return transition;
                }

                return SaveOperationResult.Success(
                    SaveOperation.Continue,
                    SaveCoordinator.AutoSlot,
                    prepared.OperationResult.CommitSequence,
                    prepared.OperationResult.RecoverySource);
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public async UniTask<SaveOperationResult> StartNewGameAsync(
            CancellationToken cancellationToken = default)
        {
            if (!TryBeginOperation(cancellationToken, out SaveOperationResult failure))
            {
                return ChangeOperation(failure, SaveOperation.NewGame);
            }

            try
            {
                return await BeginTransitionAsync(
                    new NewGameSessionInitializer(_configuration),
                    SaveOperation.NewGame);
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        bool TryBeginOperation(
            CancellationToken cancellationToken,
            out SaveOperationResult failure)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                failure = SaveOperationResult.Failure(
                    SaveOperation.Save,
                    SaveCoordinator.AutoSlot,
                    SaveErrorCode.Cancelled);
                return false;
            }

            if (_operationInProgress || _coordinator.IsBusy)
            {
                failure = SaveOperationResult.Failure(
                    SaveOperation.Save,
                    SaveCoordinator.AutoSlot,
                    SaveErrorCode.OperationInProgress,
                    true);
                return false;
            }

            if (!IsCurrentSession())
            {
                failure = SaveOperationResult.Failure(
                    SaveOperation.Save,
                    SaveCoordinator.AutoSlot,
                    SaveErrorCode.SessionUnavailable);
                return false;
            }

            _operationInProgress = true;
            failure = null;
            return true;
        }

        bool IsCurrentSession()
        {
            return ReferenceEquals(_host.CurrentSession, _session)
                && _host.State == ApplicationLifecycleState.Ready
                && _session.State == GameSessionState.Running
                && _session.IsCurrentArchitectureLease
                && _scene.IsValid()
                && _scene.isLoaded;
        }

        async UniTask<SaveOperationResult> BeginTransitionAsync(
            IGameSessionInitializer initializer,
            SaveOperation operation)
        {
            UniTaskCompletionSource<LifecycleResult> completion =
                new UniTaskCompletionSource<LifecycleResult>();
            LifecycleResult submitted = _host.BeginSceneSessionInitialization(
                _scene,
                initializer,
                onCompleted: result => completion.TrySetResult(result));

            if (!submitted.IsSuccess)
            {
                return SaveOperationResult.Failure(
                    operation,
                    SaveCoordinator.AutoSlot,
                    submitted.Code == LifecycleResultCode.OperationInProgress
                        ? SaveErrorCode.OperationInProgress
                        : SaveErrorCode.RestoreCommitFailed,
                    submitted.Code == LifecycleResultCode.OperationInProgress,
                    submitted.Exception);
            }

            LifecycleResult completed = await completion.Task;
            return completed.IsSuccess
                ? SaveOperationResult.Success(operation, SaveCoordinator.AutoSlot)
                : SaveOperationResult.Failure(
                    operation,
                    SaveCoordinator.AutoSlot,
                    completed.Code == LifecycleResultCode.Cancelled
                        ? SaveErrorCode.Cancelled
                        : SaveErrorCode.RestoreCommitFailed,
                    false,
                    completed.Exception);
        }

        static SaveOperationResult ChangeOperation(
            SaveOperationResult result,
            SaveOperation operation)
        {
            return new SaveOperationResult(
                operation,
                result.SlotId,
                result.ErrorCode,
                result.RecoverySource,
                result.CanRetry,
                result.Exception,
                result.CommitSequence);
        }
    }
}
