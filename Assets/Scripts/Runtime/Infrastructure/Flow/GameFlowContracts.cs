using System;

namespace DarkFlare
{
    public enum GameFlowState
    {
        Boot,
        FrontEnd,
        Loading,
        InGame,
        Paused,
        Recovering,
        FatalError
    }

    public enum SceneFlowOperation
    {
        None,
        EnterFrontEnd,
        StartGame,
        ReturnToFrontEnd
    }

    public enum GameStartIntent
    {
        None,
        NewGame,
        Continue
    }

    public enum SceneFlowPhase
    {
        None,
        Preparing,
        PreparingSave,
        LoadingScene,
        ActivatingScene,
        StartingSession,
        SavingBeforeExit,
        StoppingSession,
        UnloadingScene,
        Recovering,
        Completed
    }

    public enum SceneFlowErrorCode
    {
        None,
        InvalidState,
        Cancelled,
        Superseded,
        ConfigurationInvalid,
        SceneNotInBuild,
        LoadFailed,
        ActivationFailed,
        EntryMissing,
        EntryDuplicate,
        SavePrepareFailed,
        SaveBeforeExitFailed,
        SessionStartFailed,
        SessionStopFailed,
        UnloadFailed,
        Timeout,
        ApplicationUnavailable,
        FatalCleanupFailed
    }

    [Flags]
    public enum SceneFlowRecoveryAction
    {
        None = 0,
        Retry = 1 << 0,
        CancelTransition = 1 << 1,
        ReturnToFrontEnd = 1 << 2,
        RetrySafeBoot = 1 << 3,
        Quit = 1 << 4
    }

    public readonly struct SceneId : IEquatable<SceneId>, IComparable<SceneId>
    {
        static readonly SceneId BootstrapId = new SceneId("bootstrap");
        static readonly SceneId MainId = new SceneId("main");

        readonly string _value;

        public static SceneId Bootstrap => BootstrapId;

        public static SceneId Main => MainId;

        public string Value => _value ?? string.Empty;

        public bool IsValid => IsValidValue(_value);

        public SceneId(string value)
        {
            if (!IsValidValue(value))
            {
                throw new ArgumentException(
                    "SceneId 必须使用小写 snake_case，且必须以字母开头",
                    nameof(value));
            }

            _value = value;
        }

        public static bool TryCreate(string value, out SceneId sceneId)
        {
            if (!IsValidValue(value))
            {
                sceneId = default;
                return false;
            }

            sceneId = new SceneId(value);
            return true;
        }

        public bool Equals(SceneId other)
        {
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SceneId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        }

        public int CompareTo(SceneId other)
        {
            return string.Compare(_value, other._value, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(SceneId left, SceneId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SceneId left, SceneId right)
        {
            return !left.Equals(right);
        }

        static bool IsValidValue(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsLowerAsciiLetter(value[0]))
            {
                return false;
            }

            bool previousUnderscore = false;

            for (int i = 1; i < value.Length; i++)
            {
                char character = value[i];

                if (character == '_')
                {
                    if (previousUnderscore || i == value.Length - 1)
                    {
                        return false;
                    }

                    previousUnderscore = true;
                    continue;
                }

                if (!IsLowerAsciiLetter(character) && (character < '0' || character > '9'))
                {
                    return false;
                }

                previousUnderscore = false;
            }

            return true;
        }

        static bool IsLowerAsciiLetter(char character)
        {
            return character >= 'a' && character <= 'z';
        }
    }

    public readonly struct SceneFlowRequest
    {
        SceneFlowRequest(
            SceneFlowOperation operation,
            GameStartIntent startIntent,
            SceneId targetScene)
        {
            Operation = operation;
            StartIntent = startIntent;
            TargetScene = targetScene;
        }

        public SceneFlowOperation Operation { get; }

        public GameStartIntent StartIntent { get; }

        public SceneId TargetScene { get; }

        public bool IsValid
        {
            get
            {
                switch (Operation)
                {
                    case SceneFlowOperation.EnterFrontEnd:
                    case SceneFlowOperation.ReturnToFrontEnd:
                        return StartIntent == GameStartIntent.None
                            && TargetScene == SceneId.Bootstrap;
                    case SceneFlowOperation.StartGame:
                        return (StartIntent == GameStartIntent.NewGame
                                || StartIntent == GameStartIntent.Continue)
                            && TargetScene == SceneId.Main;
                    default:
                        return false;
                }
            }
        }

        public static SceneFlowRequest EnterFrontEnd()
        {
            return new SceneFlowRequest(
                SceneFlowOperation.EnterFrontEnd,
                GameStartIntent.None,
                SceneId.Bootstrap);
        }

        public static SceneFlowRequest StartGame(GameStartIntent intent)
        {
            if (intent != GameStartIntent.NewGame && intent != GameStartIntent.Continue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(intent),
                    intent,
                    "开始游戏请求必须明确 NewGame 或 Continue");
            }

            return new SceneFlowRequest(
                SceneFlowOperation.StartGame,
                intent,
                SceneId.Main);
        }

        public static SceneFlowRequest ReturnToFrontEnd()
        {
            return new SceneFlowRequest(
                SceneFlowOperation.ReturnToFrontEnd,
                GameStartIntent.None,
                SceneId.Bootstrap);
        }
    }

    public readonly struct SceneFlowProgress
    {
        public SceneFlowProgress(
            SceneFlowPhase phase,
            SceneId sceneId,
            float? progress01,
            bool canCancel)
        {
            if (phase == SceneFlowPhase.None)
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }

            if (!sceneId.IsValid)
            {
                throw new ArgumentException("进度必须关联有效 SceneId", nameof(sceneId));
            }

            if (progress01.HasValue
                && (float.IsNaN(progress01.Value)
                    || float.IsInfinity(progress01.Value)
                    || progress01.Value < 0f
                    || progress01.Value > 1f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(progress01),
                    progress01,
                    "可测量进度必须位于 [0, 1]");
            }

            Phase = phase;
            SceneId = sceneId;
            Progress01 = progress01;
            CanCancel = canCancel;
        }

        public SceneFlowPhase Phase { get; }

        public SceneId SceneId { get; }

        public float? Progress01 { get; }

        public bool HasMeasuredProgress => Progress01.HasValue;

        public bool CanCancel { get; }

        public static SceneFlowProgress Indeterminate(
            SceneFlowPhase phase,
            SceneId sceneId,
            bool canCancel)
        {
            return new SceneFlowProgress(phase, sceneId, null, canCancel);
        }

        public static SceneFlowProgress Measured(
            SceneFlowPhase phase,
            SceneId sceneId,
            float progress01,
            bool canCancel)
        {
            return new SceneFlowProgress(phase, sceneId, progress01, canCancel);
        }
    }

    public sealed class SceneFlowResult
    {
        SceneFlowResult(
            SceneFlowOperation operation,
            SceneFlowErrorCode errorCode,
            GameFlowState fromState,
            GameFlowState targetState,
            SceneFlowPhase phase,
            SceneId sceneId,
            SceneFlowRecoveryAction recoveryActions,
            LocalizedMessage playerMessage,
            Exception exception)
        {
            Operation = operation;
            ErrorCode = errorCode;
            FromState = fromState;
            TargetState = targetState;
            Phase = phase;
            SceneId = sceneId;
            RecoveryActions = recoveryActions;
            PlayerMessage = playerMessage;
            Exception = exception;
        }

        public SceneFlowOperation Operation { get; }

        public SceneFlowErrorCode ErrorCode { get; }

        public GameFlowState FromState { get; }

        public GameFlowState TargetState { get; }

        public SceneFlowPhase Phase { get; }

        public SceneId SceneId { get; }

        public SceneFlowRecoveryAction RecoveryActions { get; }

        public LocalizedMessage PlayerMessage { get; }

        public Exception Exception { get; }

        public bool Succeeded => ErrorCode == SceneFlowErrorCode.None;

        public bool CanRetry => HasRecoveryAction(SceneFlowRecoveryAction.Retry)
            || HasRecoveryAction(SceneFlowRecoveryAction.RetrySafeBoot);

        public bool HasRecoveryAction(SceneFlowRecoveryAction action)
        {
            return action != SceneFlowRecoveryAction.None
                && (RecoveryActions & action) == action;
        }

        public static SceneFlowResult Success(
            SceneFlowOperation operation,
            GameFlowState fromState,
            GameFlowState targetState,
            SceneId sceneId,
            LocalizedMessage playerMessage = default)
        {
            ValidateOperation(operation);
            ValidateScene(sceneId);
            return new SceneFlowResult(
                operation,
                SceneFlowErrorCode.None,
                fromState,
                targetState,
                SceneFlowPhase.Completed,
                sceneId,
                SceneFlowRecoveryAction.None,
                playerMessage,
                null);
        }

        public static SceneFlowResult Failure(
            SceneFlowOperation operation,
            SceneFlowErrorCode errorCode,
            GameFlowState fromState,
            GameFlowState targetState,
            SceneFlowPhase phase,
            SceneId sceneId,
            SceneFlowRecoveryAction recoveryActions,
            LocalizedMessage playerMessage,
            Exception exception = null)
        {
            ValidateOperation(operation);
            ValidateScene(sceneId);

            if (errorCode == SceneFlowErrorCode.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(errorCode),
                    errorCode,
                    "失败结果必须包含错误码");
            }

            if (phase == SceneFlowPhase.None || phase == SceneFlowPhase.Completed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(phase),
                    phase,
                    "失败结果必须保留实际失败阶段");
            }

            return new SceneFlowResult(
                operation,
                errorCode,
                fromState,
                targetState,
                phase,
                sceneId,
                recoveryActions,
                playerMessage,
                exception);
        }

        static void ValidateOperation(SceneFlowOperation operation)
        {
            if (operation == SceneFlowOperation.None)
            {
                throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        static void ValidateScene(SceneId sceneId)
        {
            if (!sceneId.IsValid)
            {
                throw new ArgumentException("结果必须关联有效 SceneId", nameof(sceneId));
            }
        }
    }

    public static class GameFlowTransitionRules
    {
        public static bool CanTransition(GameFlowState fromState, GameFlowState toState)
        {
            switch (fromState)
            {
                case GameFlowState.Boot:
                    return toState == GameFlowState.FrontEnd
                        || toState == GameFlowState.FatalError;
                case GameFlowState.FrontEnd:
                    return toState == GameFlowState.Loading;
                case GameFlowState.Loading:
                    return toState == GameFlowState.InGame
                        || toState == GameFlowState.FrontEnd
                        || toState == GameFlowState.Recovering
                        || toState == GameFlowState.FatalError;
                case GameFlowState.InGame:
                    return toState == GameFlowState.Paused
                        || toState == GameFlowState.Loading;
                case GameFlowState.Paused:
                    return toState == GameFlowState.InGame
                        || toState == GameFlowState.Loading;
                case GameFlowState.Recovering:
                    return toState == GameFlowState.FrontEnd
                        || toState == GameFlowState.InGame
                        || toState == GameFlowState.Paused
                        || toState == GameFlowState.FatalError;
                default:
                    return false;
            }
        }

        public static bool IsInteractiveStable(GameFlowState state)
        {
            return state == GameFlowState.FrontEnd
                || state == GameFlowState.InGame
                || state == GameFlowState.Paused;
        }

        public static bool IsTransactionState(GameFlowState state)
        {
            return state == GameFlowState.Loading
                || state == GameFlowState.Recovering;
        }

        public static bool IsTerminal(GameFlowState state)
        {
            return state == GameFlowState.FatalError;
        }
    }
}
