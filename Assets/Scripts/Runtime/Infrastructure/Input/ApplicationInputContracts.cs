using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DarkFlare
{
    public enum InputContext
    {
        Gameplay,
        UI,
    }

    [Flags]
    public enum InputSuspensionReason
    {
        None = 0,
        FocusLost = 1 << 0,
        PlatformSuspended = 1 << 1,
        SceneTransition = 1 << 2,
        Rebinding = 1 << 3,
        Shutdown = 1 << 4,
    }

    public enum InputDeviceFamily
    {
        Unknown,
        KeyboardMouse,
        Gamepad,
    }

    public enum RebindableInputAction
    {
        PlayerMove,
        PlayerInteract,
        PlayerToggleMenu,
        UiNavigate,
        UiSubmit,
        UiCancel,
        UiRearrange,
        PlayerPause,
        UiPause,
        UiPreviousWindow,
        UiNextWindow,
    }

    public enum InputBindingPart
    {
        Primary,
        Up,
        Down,
        Left,
        Right,
    }

    public enum InputRebindResultCode
    {
        Success,
        AlreadyApplied,
        Cancelled,
        TimedOut,
        DeviceUnavailable,
        InvalidTarget,
        InvalidControl,
        Conflict,
        RequiredBindingMissing,
        InvalidOverrides,
        SettingsCommitFailed,
        Closed,
    }

    public readonly struct InputBindingTarget : IEquatable<InputBindingTarget>
    {
        public RebindableInputAction Action { get; }

        public InputBindingPart Part { get; }

        public InputDeviceFamily DeviceFamily { get; }

        public InputBindingTarget(
            RebindableInputAction action,
            InputBindingPart part,
            InputDeviceFamily deviceFamily)
        {
            if (deviceFamily == InputDeviceFamily.Unknown)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deviceFamily),
                    deviceFamily,
                    "重绑定目标必须指定设备族");
            }

            Action = action;
            Part = part;
            DeviceFamily = deviceFamily;
        }

        public bool Equals(InputBindingTarget other)
        {
            return Action == other.Action
                && Part == other.Part
                && DeviceFamily == other.DeviceFamily;
        }

        public override bool Equals(object obj)
        {
            return obj is InputBindingTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Action;
                hash = (hash * 397) ^ (int)Part;
                hash = (hash * 397) ^ (int)DeviceFamily;
                return hash;
            }
        }

        public static bool operator ==(InputBindingTarget left, InputBindingTarget right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InputBindingTarget left, InputBindingTarget right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class InputBindingConflict
    {
        public InputBindingTarget Target { get; }

        public InputBindingTarget ConflictingTarget { get; }

        public string ControlPath { get; }

        public InputBindingConflict(
            InputBindingTarget target,
            InputBindingTarget conflictingTarget,
            string controlPath)
        {
            Target = target;
            ConflictingTarget = conflictingTarget;
            ControlPath = controlPath ?? string.Empty;
        }
    }

    public sealed class InputRebindResult
    {
        public InputRebindResultCode Code { get; }

        public InputBindingTarget Target { get; }

        public string ControlPath { get; }

        public InputBindingConflict Conflict { get; }

        public Exception Exception { get; }

        public bool Succeeded => Code == InputRebindResultCode.Success
            || Code == InputRebindResultCode.AlreadyApplied;

        InputRebindResult(
            InputRebindResultCode code,
            InputBindingTarget target,
            string controlPath,
            InputBindingConflict conflict,
            Exception exception)
        {
            Code = code;
            Target = target;
            ControlPath = controlPath ?? string.Empty;
            Conflict = conflict;
            Exception = exception;
        }

        public static InputRebindResult Success(
            InputBindingTarget target,
            string controlPath)
        {
            return new InputRebindResult(
                InputRebindResultCode.Success,
                target,
                controlPath,
                null,
                null);
        }

        public static InputRebindResult AlreadyApplied(
            InputBindingTarget target,
            string controlPath)
        {
            return new InputRebindResult(
                InputRebindResultCode.AlreadyApplied,
                target,
                controlPath,
                null,
                null);
        }

        public static InputRebindResult Failure(
            InputRebindResultCode code,
            InputBindingTarget target,
            Exception exception = null)
        {
            if (code == InputRebindResultCode.Success
                || code == InputRebindResultCode.AlreadyApplied
                || code == InputRebindResultCode.Conflict)
            {
                throw new ArgumentOutOfRangeException(nameof(code), code, "失败结果代码非法");
            }

            return new InputRebindResult(code, target, string.Empty, null, exception);
        }

        public static InputRebindResult ConflictFailure(InputBindingConflict conflict)
        {
            if (conflict == null)
            {
                throw new ArgumentNullException(nameof(conflict));
            }

            return new InputRebindResult(
                InputRebindResultCode.Conflict,
                conflict.Target,
                conflict.ControlPath,
                conflict,
                null);
        }
    }

    public readonly struct InputGlyphToken : IEquatable<InputGlyphToken>
    {
        public InputDeviceFamily DeviceFamily { get; }

        public string ControlPath { get; }

        public string GlyphId { get; }

        public string FallbackText { get; }

        public bool UsesTextFallback => string.IsNullOrWhiteSpace(GlyphId);

        public InputGlyphToken(
            InputDeviceFamily deviceFamily,
            string controlPath,
            string glyphId,
            string fallbackText)
        {
            DeviceFamily = deviceFamily;
            ControlPath = controlPath ?? string.Empty;
            GlyphId = glyphId ?? string.Empty;
            FallbackText = fallbackText ?? string.Empty;
        }

        public bool Equals(InputGlyphToken other)
        {
            return DeviceFamily == other.DeviceFamily
                && string.Equals(ControlPath, other.ControlPath, StringComparison.Ordinal)
                && string.Equals(GlyphId, other.GlyphId, StringComparison.Ordinal)
                && string.Equals(FallbackText, other.FallbackText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is InputGlyphToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)DeviceFamily;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ControlPath);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(GlyphId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(FallbackText);
                return hash;
            }
        }
    }

    public static class RebindableInputCatalog
    {
        static readonly ReadOnlyCollection<RebindableInputAction> ActionsValue =
            Array.AsReadOnly(new[]
            {
                RebindableInputAction.PlayerMove,
                RebindableInputAction.PlayerInteract,
                RebindableInputAction.PlayerToggleMenu,
                RebindableInputAction.UiNavigate,
                RebindableInputAction.UiSubmit,
                RebindableInputAction.UiCancel,
                RebindableInputAction.UiRearrange,
                RebindableInputAction.PlayerPause,
                RebindableInputAction.UiPause,
                RebindableInputAction.UiPreviousWindow,
                RebindableInputAction.UiNextWindow,
            });

        public static IReadOnlyList<RebindableInputAction> Actions => ActionsValue;

        public static string GetMapName(RebindableInputAction action)
        {
            switch (action)
            {
                case RebindableInputAction.PlayerMove:
                case RebindableInputAction.PlayerInteract:
                case RebindableInputAction.PlayerToggleMenu:
                case RebindableInputAction.PlayerPause:
                    return "Player";
                case RebindableInputAction.UiNavigate:
                case RebindableInputAction.UiSubmit:
                case RebindableInputAction.UiCancel:
                case RebindableInputAction.UiRearrange:
                case RebindableInputAction.UiPause:
                case RebindableInputAction.UiPreviousWindow:
                case RebindableInputAction.UiNextWindow:
                    return "UI";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        public static string GetActionName(RebindableInputAction action)
        {
            switch (action)
            {
                case RebindableInputAction.PlayerMove:
                    return "Move";
                case RebindableInputAction.PlayerInteract:
                    return "Interact";
                case RebindableInputAction.PlayerToggleMenu:
                    return "ToggleMenu";
                case RebindableInputAction.UiNavigate:
                    return "Navigate";
                case RebindableInputAction.UiSubmit:
                    return "Submit";
                case RebindableInputAction.UiCancel:
                    return "Cancel";
                case RebindableInputAction.UiRearrange:
                    return "Rearrange";
                case RebindableInputAction.PlayerPause:
                case RebindableInputAction.UiPause:
                    return "Pause";
                case RebindableInputAction.UiPreviousWindow:
                    return "PreviousWindow";
                case RebindableInputAction.UiNextWindow:
                    return "NextWindow";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        public static bool UsesDirectionalParts(RebindableInputAction action)
        {
            return action == RebindableInputAction.PlayerMove
                || action == RebindableInputAction.UiNavigate;
        }
    }
}
