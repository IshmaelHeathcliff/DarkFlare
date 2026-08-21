using System;

namespace DarkFlare
{
    public enum PlatformLifecycleState
    {
        Active,
        Suspended,
        Resuming,
        ShuttingDown,
        Shutdown,
    }

    [Flags]
    public enum PlatformSuspensionReason
    {
        None = 0,
        FocusLost = 1 << 0,
        PlatformSuspended = 1 << 1,
    }

    public enum PlatformCheckpointUrgency
    {
        Regular,
        Urgent,
        Final,
    }

    public enum PlatformLifecycleResultCode
    {
        Success,
        AlreadyApplied,
        Deferred,
        SessionUnavailable,
        Cancelled,
        TimedOut,
        SaveFailed,
        Closed,
        Failed,
    }

    public sealed class PlatformLifecycleResult
    {
        public PlatformLifecycleResultCode Code { get; }

        public PlatformLifecycleState PreviousState { get; }

        public PlatformLifecycleState State { get; }

        public PlatformSuspensionReason SuspensionReasons { get; }

        public bool CheckpointRequested { get; }

        public Exception Exception { get; }

        public bool Succeeded => Code == PlatformLifecycleResultCode.Success
            || Code == PlatformLifecycleResultCode.AlreadyApplied
            || Code == PlatformLifecycleResultCode.SessionUnavailable;

        PlatformLifecycleResult(
            PlatformLifecycleResultCode code,
            PlatformLifecycleState previousState,
            PlatformLifecycleState state,
            PlatformSuspensionReason suspensionReasons,
            bool checkpointRequested,
            Exception exception)
        {
            Code = code;
            PreviousState = previousState;
            State = state;
            SuspensionReasons = suspensionReasons;
            CheckpointRequested = checkpointRequested;
            Exception = exception;
        }

        public static PlatformLifecycleResult Success(
            PlatformLifecycleState previousState,
            PlatformLifecycleState state,
            PlatformSuspensionReason suspensionReasons,
            bool checkpointRequested)
        {
            return new PlatformLifecycleResult(
                PlatformLifecycleResultCode.Success,
                previousState,
                state,
                suspensionReasons,
                checkpointRequested,
                null);
        }

        public static PlatformLifecycleResult AlreadyApplied(
            PlatformLifecycleState state,
            PlatformSuspensionReason suspensionReasons)
        {
            return new PlatformLifecycleResult(
                PlatformLifecycleResultCode.AlreadyApplied,
                state,
                state,
                suspensionReasons,
                false,
                null);
        }

        public static PlatformLifecycleResult SessionUnavailable(
            PlatformLifecycleState previousState,
            PlatformLifecycleState state,
            PlatformSuspensionReason suspensionReasons,
            bool checkpointRequested)
        {
            return new PlatformLifecycleResult(
                PlatformLifecycleResultCode.SessionUnavailable,
                previousState,
                state,
                suspensionReasons,
                checkpointRequested,
                null);
        }

        public static PlatformLifecycleResult Failure(
            PlatformLifecycleResultCode code,
            PlatformLifecycleState previousState,
            PlatformLifecycleState state,
            PlatformSuspensionReason suspensionReasons,
            bool checkpointRequested,
            Exception exception = null)
        {
            if (code == PlatformLifecycleResultCode.Success
                || code == PlatformLifecycleResultCode.AlreadyApplied
                || code == PlatformLifecycleResultCode.SessionUnavailable)
            {
                throw new ArgumentOutOfRangeException(nameof(code), code, "失败结果代码非法");
            }

            return new PlatformLifecycleResult(
                code,
                previousState,
                state,
                suspensionReasons,
                checkpointRequested,
                exception);
        }
    }
}
