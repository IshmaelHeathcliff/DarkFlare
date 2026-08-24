using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace DarkFlare
{
    public sealed class InputGlyphResolver
    {
        static readonly IReadOnlyDictionary<string, string> GamepadGlyphs =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "buttonSouth", "gamepad.button_south" },
                { "buttonEast", "gamepad.button_east" },
                { "buttonWest", "gamepad.button_west" },
                { "buttonNorth", "gamepad.button_north" },
                { "start", "gamepad.start" },
                { "select", "gamepad.select" },
                { "dpad", "gamepad.dpad" },
                { "dpad/up", "gamepad.dpad_up" },
                { "dpad/down", "gamepad.dpad_down" },
                { "dpad/left", "gamepad.dpad_left" },
                { "dpad/right", "gamepad.dpad_right" },
                { "leftStick", "gamepad.left_stick" },
                { "leftStick/up", "gamepad.left_stick_up" },
                { "leftStick/down", "gamepad.left_stick_down" },
                { "leftStick/left", "gamepad.left_stick_left" },
                { "leftStick/right", "gamepad.left_stick_right" },
            };

        public InputGlyphToken Resolve(
            string controlPath,
            InputDeviceFamily deviceFamily,
            string displayName = null)
        {
            string normalizedPath = controlPath ?? string.Empty;
            string fallback = CreateFallbackText(normalizedPath, displayName);

            if (deviceFamily == InputDeviceFamily.KeyboardMouse
                && normalizedPath.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
            {
                return new InputGlyphToken(
                    deviceFamily,
                    normalizedPath,
                    "keyboard.keycap",
                    fallback);
            }

            if (deviceFamily == InputDeviceFamily.KeyboardMouse
                && normalizedPath.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase))
            {
                string mouseControl = GetControlName(normalizedPath);
                string glyphId = string.Equals(mouseControl, "leftButton", StringComparison.OrdinalIgnoreCase)
                    ? "mouse.left_button"
                    : string.Equals(mouseControl, "rightButton", StringComparison.OrdinalIgnoreCase)
                        ? "mouse.right_button"
                        : string.Equals(mouseControl, "middleButton", StringComparison.OrdinalIgnoreCase)
                            ? "mouse.middle_button"
                            : string.Empty;
                return new InputGlyphToken(deviceFamily, normalizedPath, glyphId, fallback);
            }

            if (deviceFamily == InputDeviceFamily.Gamepad
                && normalizedPath.StartsWith("<Gamepad>/", StringComparison.OrdinalIgnoreCase))
            {
                string controlName = GetControlName(normalizedPath);
                GamepadGlyphs.TryGetValue(controlName, out string glyphId);
                return new InputGlyphToken(
                    deviceFamily,
                    normalizedPath,
                    glyphId ?? string.Empty,
                    fallback);
            }

            return new InputGlyphToken(deviceFamily, normalizedPath, string.Empty, fallback);
        }

        static string CreateFallbackText(string controlPath, string displayName)
        {
            if (!string.IsNullOrWhiteSpace(displayName)
                && displayName.IndexOf('<') < 0)
            {
                return displayName.Trim();
            }

            string shortName = InputControlPath.ToHumanReadableString(
                controlPath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
            return string.IsNullOrWhiteSpace(shortName) || shortName.IndexOf('<') >= 0
                ? "?"
                : shortName.Trim();
        }

        static string GetControlName(string controlPath)
        {
            int separator = controlPath.IndexOf("/", StringComparison.Ordinal);
            return separator < 0 || separator == controlPath.Length - 1
                ? string.Empty
                : controlPath.Substring(separator + 1);
        }
    }
}
