using System;

namespace DarkFlare
{
    public enum ApplicationLifecycleState
    {
        None,
        Booting,
        Ready,
        ShuttingDown,
        Shutdown,
        Failed
    }

    public enum GameSessionState
    {
        None,
        Created,
        Initializing,
        Running,
        RollingBack,
        Stopping,
        Abandoned
    }

    public enum LifecycleScopeState
    {
        Active,
        Stopping,
        Stopped,
        Abandoned
    }

    public enum LifecycleResultCode
    {
        Succeeded,
        AlreadyCompleted,
        OperationInProgress,
        InvalidState,
        ApplicationShuttingDown,
        ValidationFailed,
        Cancelled,
        Failed
    }

    public enum LifecycleTaskFailurePolicy
    {
        Report,
        ReportAndStopScope,
        Propagate
    }

    public readonly struct LifecycleResult
    {
        public LifecycleResult(
            LifecycleResultCode code,
            string message = null,
            Exception exception = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public LifecycleResultCode Code { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public bool IsSuccess => Code == LifecycleResultCode.Succeeded
            || Code == LifecycleResultCode.AlreadyCompleted;

        public static LifecycleResult Success(string message = null)
        {
            return new LifecycleResult(LifecycleResultCode.Succeeded, message);
        }

        public static LifecycleResult AlreadyCompleted(string message = null)
        {
            return new LifecycleResult(LifecycleResultCode.AlreadyCompleted, message);
        }

        public static LifecycleResult Failure(
            LifecycleResultCode code,
            string message,
            Exception exception = null)
        {
            return new LifecycleResult(code, message, exception);
        }
    }

    public readonly struct LifecycleTaskFailure
    {
        public LifecycleTaskFailure(string scopeName, string operationName, Exception exception)
        {
            ScopeName = scopeName;
            OperationName = operationName;
            Exception = exception;
        }

        public string ScopeName { get; }

        public string OperationName { get; }

        public Exception Exception { get; }
    }
}
