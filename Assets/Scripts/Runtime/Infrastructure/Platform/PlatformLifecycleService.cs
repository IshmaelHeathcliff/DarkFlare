using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DarkFlare
{
    public sealed class PlatformLifecycleService
    {
        const string PauseOwner = "platform-lifecycle";

        readonly ApplicationInputService _input;
        readonly AudioService _audio;
        readonly GameTimeService _time;
        readonly Func<PlatformCheckpointUrgency, CancellationToken, UniTask<SaveOperationResult>>
            _checkpoint;
        readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        IDisposable _focusInputLease;
        IDisposable _suspendInputLease;
        GamePauseLease _pauseLease;
        bool _checkpointRequestedForEpisode;

        public PlatformLifecycleService(
            ApplicationInputService input,
            AudioService audio,
            GameTimeService time,
            Func<PlatformCheckpointUrgency, CancellationToken, UniTask<SaveOperationResult>>
                checkpoint)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            State = PlatformLifecycleState.Active;
        }

        public PlatformLifecycleState State { get; private set; }

        public PlatformSuspensionReason SuspensionReasons { get; private set; }

        public bool IsClosed => State == PlatformLifecycleState.Shutdown;

        public event Action<PlatformLifecycleState> StateChanged;

        public UniTask<PlatformLifecycleResult> HandleFocusChangedAsync(
            bool hasFocus,
            CancellationToken cancellationToken = default)
        {
            return hasFocus
                ? ResumeAsync(PlatformSuspensionReason.FocusLost, cancellationToken)
                : SuspendAsync(
                    PlatformSuspensionReason.FocusLost,
                    PlatformCheckpointUrgency.Regular,
                    cancellationToken);
        }

        public UniTask<PlatformLifecycleResult> HandlePauseChangedAsync(
            bool paused,
            CancellationToken cancellationToken = default)
        {
            return paused
                ? SuspendAsync(
                    PlatformSuspensionReason.PlatformSuspended,
                    PlatformCheckpointUrgency.Urgent,
                    cancellationToken)
                : ResumeAsync(
                    PlatformSuspensionReason.PlatformSuspended,
                    cancellationToken);
        }

        public void BeginShutdown()
        {
            if (State == PlatformLifecycleState.ShuttingDown
                || State == PlatformLifecycleState.Shutdown)
            {
                return;
            }

            SetState(PlatformLifecycleState.ShuttingDown);
            _audio.Suspend();
        }

        public void Close()
        {
            if (State == PlatformLifecycleState.Shutdown)
            {
                return;
            }

            SetState(PlatformLifecycleState.ShuttingDown);
            _focusInputLease?.Dispose();
            _focusInputLease = null;
            _suspendInputLease?.Dispose();
            _suspendInputLease = null;
            _pauseLease?.Dispose();
            _pauseLease = null;
            SuspensionReasons = PlatformSuspensionReason.None;
            _checkpointRequestedForEpisode = false;
            StateChanged = null;
            State = PlatformLifecycleState.Shutdown;
        }

        async UniTask<PlatformLifecycleResult> SuspendAsync(
            PlatformSuspensionReason reason,
            PlatformCheckpointUrgency urgency,
            CancellationToken cancellationToken)
        {
            try
            {
                await _gate.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                return PlatformLifecycleResult.Failure(
                    PlatformLifecycleResultCode.Cancelled,
                    State,
                    State,
                    SuspensionReasons,
                    false,
                    exception);
            }

            try
            {
                if (State == PlatformLifecycleState.ShuttingDown
                    || State == PlatformLifecycleState.Shutdown)
                {
                    return PlatformLifecycleResult.Failure(
                        PlatformLifecycleResultCode.Closed,
                        State,
                        State,
                        SuspensionReasons,
                        false);
                }

                if ((SuspensionReasons & reason) != 0)
                {
                    return PlatformLifecycleResult.AlreadyApplied(
                        State,
                        SuspensionReasons);
                }

                PlatformLifecycleState previous = State;
                bool firstReason = SuspensionReasons == PlatformSuspensionReason.None;
                SuspensionReasons |= reason;
                AcquireInputLease(reason);

                if (firstReason)
                {
                    _pauseLease = _time.AcquirePause(PauseOwner);
                    _audio.Suspend();
                }

                SetState(PlatformLifecycleState.Suspended);
                bool requestCheckpoint = !_checkpointRequestedForEpisode;

                if (!requestCheckpoint)
                {
                    return PlatformLifecycleResult.Success(
                        previous,
                        State,
                        SuspensionReasons,
                        false);
                }

                _checkpointRequestedForEpisode = true;
                SaveOperationResult checkpointResult;

                try
                {
                    checkpointResult = await _checkpoint(urgency, cancellationToken);
                }
                catch (OperationCanceledException exception)
                {
                    return PlatformLifecycleResult.Failure(
                        PlatformLifecycleResultCode.Cancelled,
                        previous,
                        State,
                        SuspensionReasons,
                        true,
                        exception);
                }
                catch (TimeoutException exception)
                {
                    return PlatformLifecycleResult.Failure(
                        PlatformLifecycleResultCode.TimedOut,
                        previous,
                        State,
                        SuspensionReasons,
                        true,
                        exception);
                }
                catch (Exception exception)
                {
                    return PlatformLifecycleResult.Failure(
                        PlatformLifecycleResultCode.Failed,
                        previous,
                        State,
                        SuspensionReasons,
                        true,
                        exception);
                }

                return MapCheckpointResult(previous, checkpointResult);
            }
            finally
            {
                _gate.Release();
            }
        }

        async UniTask<PlatformLifecycleResult> ResumeAsync(
            PlatformSuspensionReason reason,
            CancellationToken cancellationToken)
        {
            try
            {
                await _gate.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                return PlatformLifecycleResult.Failure(
                    PlatformLifecycleResultCode.Cancelled,
                    State,
                    State,
                    SuspensionReasons,
                    false,
                    exception);
            }

            try
            {
                if (State == PlatformLifecycleState.ShuttingDown
                    || State == PlatformLifecycleState.Shutdown)
                {
                    return PlatformLifecycleResult.Failure(
                        PlatformLifecycleResultCode.Closed,
                        State,
                        State,
                        SuspensionReasons,
                        false);
                }

                if ((SuspensionReasons & reason) == 0)
                {
                    return PlatformLifecycleResult.AlreadyApplied(
                        State,
                        SuspensionReasons);
                }

                PlatformLifecycleState previous = State;
                SetState(PlatformLifecycleState.Resuming);
                SuspensionReasons &= ~reason;

                if (SuspensionReasons == PlatformSuspensionReason.None)
                {
                    _input.RefreshDevices();
                }

                ReleaseInputLease(reason);

                if (SuspensionReasons != PlatformSuspensionReason.None)
                {
                    SetState(PlatformLifecycleState.Suspended);
                    return PlatformLifecycleResult.Success(
                        previous,
                        State,
                        SuspensionReasons,
                        false);
                }

                _audio.Resume();
                _pauseLease?.Dispose();
                _pauseLease = null;
                _checkpointRequestedForEpisode = false;
                SetState(PlatformLifecycleState.Active);
                return PlatformLifecycleResult.Success(
                    previous,
                    State,
                    SuspensionReasons,
                    false);
            }
            finally
            {
                _gate.Release();
            }
        }

        PlatformLifecycleResult MapCheckpointResult(
            PlatformLifecycleState previous,
            SaveOperationResult checkpointResult)
        {
            if (checkpointResult == null)
            {
                return PlatformLifecycleResult.Failure(
                    PlatformLifecycleResultCode.SaveFailed,
                    previous,
                    State,
                    SuspensionReasons,
                    true);
            }

            if (checkpointResult.Succeeded)
            {
                return PlatformLifecycleResult.Success(
                    previous,
                    State,
                    SuspensionReasons,
                    true);
            }

            switch (checkpointResult.ErrorCode)
            {
                case SaveErrorCode.SessionUnavailable:
                    return PlatformLifecycleResult.SessionUnavailable(
                        previous,
                        State,
                        SuspensionReasons,
                        true);
                case SaveErrorCode.OperationInProgress:
                    return PlatformLifecycleResult.Failure(
                        PlatformLifecycleResultCode.Deferred,
                        previous,
                        State,
                        SuspensionReasons,
                        true,
                        checkpointResult.Exception);
                case SaveErrorCode.Cancelled:
                    return PlatformLifecycleResult.Failure(
                        PlatformLifecycleResultCode.Cancelled,
                        previous,
                        State,
                        SuspensionReasons,
                        true,
                        checkpointResult.Exception);
                case SaveErrorCode.FlushTimedOut:
                    return PlatformLifecycleResult.Failure(
                        PlatformLifecycleResultCode.TimedOut,
                        previous,
                        State,
                        SuspensionReasons,
                        true,
                        checkpointResult.Exception);
                default:
                    return PlatformLifecycleResult.Failure(
                        PlatformLifecycleResultCode.SaveFailed,
                        previous,
                        State,
                        SuspensionReasons,
                        true,
                        checkpointResult.Exception);
            }
        }

        void AcquireInputLease(PlatformSuspensionReason reason)
        {
            if (reason == PlatformSuspensionReason.FocusLost)
            {
                _focusInputLease = _input.AcquireSuspension(
                    InputSuspensionReason.FocusLost);
                return;
            }

            _suspendInputLease = _input.AcquireSuspension(
                InputSuspensionReason.PlatformSuspended);
        }

        void ReleaseInputLease(PlatformSuspensionReason reason)
        {
            if (reason == PlatformSuspensionReason.FocusLost)
            {
                _focusInputLease?.Dispose();
                _focusInputLease = null;
                return;
            }

            _suspendInputLease?.Dispose();
            _suspendInputLease = null;
        }

        void SetState(PlatformLifecycleState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
