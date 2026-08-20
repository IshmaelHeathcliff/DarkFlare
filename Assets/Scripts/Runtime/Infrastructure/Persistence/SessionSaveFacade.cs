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
        bool _operationInProgress;

        public SessionSaveFacade(
            ApplicationHost host,
            GameSessionHost session,
            SaveCoordinator coordinator,
            Scene scene)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _scene = scene;
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

    }
}
