using System;
using System.Text;

namespace DarkFlare
{
    public enum PlayerErrorSeverity
    {
        Warning,
        Error,
        Fatal,
    }

    [Flags]
    public enum PlayerErrorAction
    {
        None = 0,
        Dismiss = 1 << 0,
        Retry = 1 << 1,
        ReturnFrontEnd = 1 << 2,
        Quit = 1 << 3,
    }

    public readonly struct PlayerErrorPresentation
    {
        public PlayerErrorPresentation(
            string technicalCode,
            LocalizedMessage message,
            PlayerErrorSeverity severity,
            PlayerErrorAction actions)
        {
            TechnicalCode = technicalCode ?? string.Empty;
            Message = message;
            Severity = severity;
            Actions = actions;
        }

        public string TechnicalCode { get; }

        public LocalizedMessage Message { get; }

        public PlayerErrorSeverity Severity { get; }

        public PlayerErrorAction Actions { get; }

        public bool HasAction(PlayerErrorAction action)
        {
            return action != PlayerErrorAction.None
                && (Actions & action) == action;
        }
    }

    public static class PlayerErrorCatalog
    {
        public static PlayerErrorPresentation From(SceneFlowResult result)
        {
            if (result == null)
            {
                return Unhandled();
            }

            PlayerErrorAction actions = PlayerErrorAction.Dismiss;

            if (result.CanRetry)
            {
                actions |= PlayerErrorAction.Retry;
            }

            if (result.HasRecoveryAction(SceneFlowRecoveryAction.ReturnToFrontEnd))
            {
                actions |= PlayerErrorAction.ReturnFrontEnd;
            }

            if (result.HasRecoveryAction(SceneFlowRecoveryAction.Quit))
            {
                actions |= PlayerErrorAction.Quit;
            }

            PlayerErrorSeverity severity = result.ErrorCode == SceneFlowErrorCode.FatalCleanupFailed
                ? PlayerErrorSeverity.Fatal
                : PlayerErrorSeverity.Error;
            LocalizedMessage message = result.PlayerMessage.IsEmpty
                ? LocalizedMessage.Ui($"flow.error.{ToSnakeCase(result.ErrorCode.ToString())}")
                : result.PlayerMessage;
            return new PlayerErrorPresentation(
                $"scene-flow.{ToSnakeCase(result.ErrorCode.ToString())}",
                message,
                severity,
                actions);
        }

        public static PlayerErrorPresentation From(SaveOperationResult result)
        {
            if (result == null)
            {
                return Unhandled();
            }

            PlayerErrorAction actions = PlayerErrorAction.Dismiss;

            if (result.CanRetry)
            {
                actions |= PlayerErrorAction.Retry;
            }

            string suffix = ToSnakeCase(result.ErrorCode.ToString());
            return new PlayerErrorPresentation(
                $"save.{suffix}",
                LocalizedMessage.Ui($"save.error.{ToSaveMessageSuffix(result.ErrorCode)}"),
                PlayerErrorSeverity.Error,
                actions);
        }

        public static PlayerErrorPresentation From(
            ResourceErrorCode errorCode,
            bool applicationRequired,
            bool retrySupported = false)
        {
            string suffix = ToSnakeCase(errorCode.ToString());
            PlayerErrorAction actions = applicationRequired
                ? PlayerErrorAction.Quit
                : PlayerErrorAction.Dismiss;

            if (!applicationRequired && retrySupported)
            {
                actions |= PlayerErrorAction.Retry;
            }

            return new PlayerErrorPresentation(
                $"resource.{suffix}",
                LocalizedMessage.Ui($"resource.error.{suffix}"),
                applicationRequired ? PlayerErrorSeverity.Fatal : PlayerErrorSeverity.Error,
                actions);
        }

        public static PlayerErrorPresentation Unhandled()
        {
            return new PlayerErrorPresentation(
                "application.unhandled-exception",
                LocalizedMessage.Ui("flow.error.unhandled_exception"),
                PlayerErrorSeverity.Fatal,
                PlayerErrorAction.Quit);
        }

        static string ToSaveMessageSuffix(SaveErrorCode errorCode)
        {
            switch (errorCode)
            {
                case SaveErrorCode.SlotNotFound:
                case SaveErrorCode.NoValidGeneration:
                case SaveErrorCode.OperationInProgress:
                case SaveErrorCode.SessionUnavailable:
                case SaveErrorCode.SnapshotUnavailable:
                case SaveErrorCode.Cancelled:
                case SaveErrorCode.ContentVersionMismatch:
                case SaveErrorCode.ContentMissing:
                case SaveErrorCode.FutureSchemaUnsupported:
                case SaveErrorCode.ChecksumMismatch:
                case SaveErrorCode.PermissionDenied:
                case SaveErrorCode.StorageFull:
                case SaveErrorCode.FlushTimedOut:
                    return ToSnakeCase(errorCode.ToString());
                default:
                    return "unknown";
            }
        }

        static string ToSnakeCase(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length + 8);

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (i > 0 && char.IsUpper(character))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }
    }
}
