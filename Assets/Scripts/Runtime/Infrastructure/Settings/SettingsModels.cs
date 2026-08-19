using System;
using System.Collections.Generic;
using System.Globalization;

namespace DarkFlare
{
    public static class LocalSettingsFormat
    {
        public const string FormatId = "darkflare-settings";

        public const int FormatVersion = 1;

        public const int MaximumDocumentBytes = 1024 * 1024;

        public const int MaximumJsonDepth = 32;

        public const int MaximumBindingOverridesLength = 256 * 1024;
    }

    public enum UserLanguagePreference
    {
        Auto,
        SimplifiedChinese,
        English,
    }

    public enum InputGlyphPreference
    {
        Auto,
        KeyboardMouse,
        Gamepad,
    }

    public enum PreferredDisplayMode
    {
        Windowed,
        FullscreenWindow,
        ExclusiveFullscreen,
    }

    public sealed class UserSettingsDocumentDto : IPersistenceDto
    {
        public string FormatId { get; set; } = LocalSettingsFormat.FormatId;

        public int FormatVersion { get; set; } = LocalSettingsFormat.FormatVersion;

        public int SettingsSchemaVersion { get; set; } = DarkFlare.SettingsSchemaVersion.Current.Value;

        public string UpdatedUtc { get; set; } = string.Empty;

        public string PayloadSha256 { get; set; } = string.Empty;

        public UserSettingsDataDto Payload { get; set; } = new UserSettingsDataDto();
    }

    public sealed class UserSettingsDataDto : IPersistenceDto
    {
        public UserLanguagePreference Language { get; set; } = UserLanguagePreference.Auto;

        public AudioSettingsDataDto Audio { get; set; } = new AudioSettingsDataDto();

        public InputSettingsDataDto Input { get; set; } = new InputSettingsDataDto();

        public DisplaySettingsDataDto Display { get; set; } = new DisplaySettingsDataDto();

        public AccessibilitySettingsDataDto Accessibility { get; set; } =
            new AccessibilitySettingsDataDto();
    }

    public sealed class AudioSettingsDataDto : IPersistenceDto
    {
        public float MasterVolume { get; set; } = 1f;

        public float MusicVolume { get; set; } = 1f;

        public float SoundEffectsVolume { get; set; } = 1f;

        public float UiVolume { get; set; } = 1f;

        public bool Muted { get; set; }
    }

    public sealed class InputSettingsDataDto : IPersistenceDto
    {
        public string BindingOverridesJson { get; set; } = string.Empty;

        public InputGlyphPreference GlyphPreference { get; set; } = InputGlyphPreference.Auto;
    }

    public sealed class DisplaySettingsDataDto : IPersistenceDto
    {
        public int Width { get; set; } = 1920;

        public int Height { get; set; } = 1080;

        public PreferredDisplayMode Mode { get; set; } = PreferredDisplayMode.FullscreenWindow;

        public float UiScale { get; set; } = 1f;
    }

    public sealed class AccessibilitySettingsDataDto : IPersistenceDto
    {
        public float TextScale { get; set; } = 1f;

        public bool ReduceMotion { get; set; }

        public float ScreenShakeIntensity { get; set; } = 1f;

        public bool HighContrast { get; set; }
    }

    public sealed class UserSettingsSnapshot
    {
        public UserLanguagePreference Language { get; }

        public float MasterVolume { get; }

        public float MusicVolume { get; }

        public float SoundEffectsVolume { get; }

        public float UiVolume { get; }

        public bool Muted { get; }

        public string BindingOverridesJson { get; }

        public InputGlyphPreference GlyphPreference { get; }

        public int DisplayWidth { get; }

        public int DisplayHeight { get; }

        public PreferredDisplayMode DisplayMode { get; }

        public float UiScale { get; }

        public float TextScale { get; }

        public bool ReduceMotion { get; }

        public float ScreenShakeIntensity { get; }

        public bool HighContrast { get; }

        public UserSettingsSnapshot(
            UserLanguagePreference language,
            float masterVolume,
            float musicVolume,
            float soundEffectsVolume,
            float uiVolume,
            bool muted,
            string bindingOverridesJson,
            InputGlyphPreference glyphPreference,
            int displayWidth,
            int displayHeight,
            PreferredDisplayMode displayMode,
            float uiScale,
            float textScale,
            bool reduceMotion,
            float screenShakeIntensity,
            bool highContrast)
        {
            Language = language;
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SoundEffectsVolume = soundEffectsVolume;
            UiVolume = uiVolume;
            Muted = muted;
            BindingOverridesJson = bindingOverridesJson ?? string.Empty;
            GlyphPreference = glyphPreference;
            DisplayWidth = displayWidth;
            DisplayHeight = displayHeight;
            DisplayMode = displayMode;
            UiScale = uiScale;
            TextScale = textScale;
            ReduceMotion = reduceMotion;
            ScreenShakeIntensity = screenShakeIntensity;
            HighContrast = highContrast;
        }

        public static UserSettingsSnapshot Default => FromDto(new UserSettingsDataDto());

        public UserSettingsSnapshot WithLanguage(UserLanguagePreference language)
        {
            return new UserSettingsSnapshot(
                language,
                MasterVolume,
                MusicVolume,
                SoundEffectsVolume,
                UiVolume,
                Muted,
                BindingOverridesJson,
                GlyphPreference,
                DisplayWidth,
                DisplayHeight,
                DisplayMode,
                UiScale,
                TextScale,
                ReduceMotion,
                ScreenShakeIntensity,
                HighContrast);
        }

        public UserSettingsDataDto ToDto()
        {
            return new UserSettingsDataDto
            {
                Language = Language,
                Audio = new AudioSettingsDataDto
                {
                    MasterVolume = MasterVolume,
                    MusicVolume = MusicVolume,
                    SoundEffectsVolume = SoundEffectsVolume,
                    UiVolume = UiVolume,
                    Muted = Muted,
                },
                Input = new InputSettingsDataDto
                {
                    BindingOverridesJson = BindingOverridesJson,
                    GlyphPreference = GlyphPreference,
                },
                Display = new DisplaySettingsDataDto
                {
                    Width = DisplayWidth,
                    Height = DisplayHeight,
                    Mode = DisplayMode,
                    UiScale = UiScale,
                },
                Accessibility = new AccessibilitySettingsDataDto
                {
                    TextScale = TextScale,
                    ReduceMotion = ReduceMotion,
                    ScreenShakeIntensity = ScreenShakeIntensity,
                    HighContrast = HighContrast,
                },
            };
        }

        public static UserSettingsSnapshot FromDto(UserSettingsDataDto data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            AudioSettingsDataDto audio = data.Audio ?? new AudioSettingsDataDto();
            InputSettingsDataDto input = data.Input ?? new InputSettingsDataDto();
            DisplaySettingsDataDto display = data.Display ?? new DisplaySettingsDataDto();
            AccessibilitySettingsDataDto accessibility = data.Accessibility
                ?? new AccessibilitySettingsDataDto();
            return new UserSettingsSnapshot(
                data.Language,
                audio.MasterVolume,
                audio.MusicVolume,
                audio.SoundEffectsVolume,
                audio.UiVolume,
                audio.Muted,
                input.BindingOverridesJson,
                input.GlyphPreference,
                display.Width,
                display.Height,
                display.Mode,
                display.UiScale,
                accessibility.TextScale,
                accessibility.ReduceMotion,
                accessibility.ScreenShakeIntensity,
                accessibility.HighContrast);
        }
    }

    public enum SettingsDataIssueCode
    {
        MissingValue,
        InvalidValue,
        LimitExceeded,
        NonFiniteNumber,
    }

    public sealed class SettingsDataIssue
    {
        public SettingsDataIssueCode Code { get; }

        public string Path { get; }

        public string Message { get; }

        public SettingsDataIssue(SettingsDataIssueCode code, string path, string message)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public sealed class SettingsDataValidationResult
    {
        public IReadOnlyList<SettingsDataIssue> Issues { get; }

        public bool Succeeded => Issues.Count == 0;

        internal SettingsDataValidationResult(IReadOnlyList<SettingsDataIssue> issues)
        {
            Issues = issues;
        }
    }

    public static class SettingsDataValidator
    {
        public static SettingsDataValidationResult ValidateDocument(UserSettingsDocumentDto document)
        {
            List<SettingsDataIssue> issues = new List<SettingsDataIssue>();

            if (document == null)
            {
                Add(issues, SettingsDataIssueCode.MissingValue, "$", "设置文档为空");
                return Result(issues);
            }

            if (!string.Equals(document.FormatId, LocalSettingsFormat.FormatId, StringComparison.Ordinal))
            {
                Add(issues, SettingsDataIssueCode.InvalidValue, "formatId", "设置格式标识非法");
            }

            if (document.FormatVersion != LocalSettingsFormat.FormatVersion)
            {
                Add(issues, SettingsDataIssueCode.InvalidValue, "formatVersion", "设置格式版本非法");
            }

            if (document.SettingsSchemaVersion != SettingsSchemaVersion.Current.Value)
            {
                Add(
                    issues,
                    SettingsDataIssueCode.InvalidValue,
                    "settingsSchemaVersion",
                    "设置 Schema 版本不是当前版本");
            }

            if (!DateTimeOffset.TryParseExact(
                    document.UpdatedUtc,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _))
            {
                Add(issues, SettingsDataIssueCode.InvalidValue, "updatedUtc", "设置更新时间非法");
            }

            ValidatePayload(document.Payload, "payload", issues);
            return Result(issues);
        }

        public static SettingsDataValidationResult ValidateSnapshot(UserSettingsSnapshot snapshot)
        {
            List<SettingsDataIssue> issues = new List<SettingsDataIssue>();

            if (snapshot == null)
            {
                Add(issues, SettingsDataIssueCode.MissingValue, "$", "设置快照为空");
                return Result(issues);
            }

            ValidatePayload(snapshot.ToDto(), "payload", issues);
            return Result(issues);
        }

        static void ValidatePayload(
            UserSettingsDataDto payload,
            string path,
            List<SettingsDataIssue> issues)
        {
            if (payload == null)
            {
                Add(issues, SettingsDataIssueCode.MissingValue, path, "设置 Payload 为空");
                return;
            }

            ValidateEnum(payload.Language, path + ".language", issues);

            if (payload.Audio == null)
            {
                Add(issues, SettingsDataIssueCode.MissingValue, path + ".audio", "音频设置为空");
            }
            else
            {
                ValidateUnit(payload.Audio.MasterVolume, path + ".audio.masterVolume", issues);
                ValidateUnit(payload.Audio.MusicVolume, path + ".audio.musicVolume", issues);
                ValidateUnit(
                    payload.Audio.SoundEffectsVolume,
                    path + ".audio.soundEffectsVolume",
                    issues);
                ValidateUnit(payload.Audio.UiVolume, path + ".audio.uiVolume", issues);
            }

            if (payload.Input == null)
            {
                Add(issues, SettingsDataIssueCode.MissingValue, path + ".input", "输入设置为空");
            }
            else
            {
                string overrides = payload.Input.BindingOverridesJson ?? string.Empty;

                if (overrides.Length > LocalSettingsFormat.MaximumBindingOverridesLength)
                {
                    Add(
                        issues,
                        SettingsDataIssueCode.LimitExceeded,
                        path + ".input.bindingOverridesJson",
                        "输入覆盖数据超过上限");
                }

                ValidateEnum(payload.Input.GlyphPreference, path + ".input.glyphPreference", issues);
            }

            if (payload.Display == null)
            {
                Add(issues, SettingsDataIssueCode.MissingValue, path + ".display", "显示设置为空");
            }
            else
            {
                if (payload.Display.Width < 640 || payload.Display.Width > 7680)
                {
                    Add(issues, SettingsDataIssueCode.InvalidValue, path + ".display.width", "显示宽度越界");
                }

                if (payload.Display.Height < 360 || payload.Display.Height > 4320)
                {
                    Add(issues, SettingsDataIssueCode.InvalidValue, path + ".display.height", "显示高度越界");
                }

                ValidateEnum(payload.Display.Mode, path + ".display.mode", issues);
                ValidateRange(payload.Display.UiScale, 0.75f, 2f, path + ".display.uiScale", issues);
            }

            if (payload.Accessibility == null)
            {
                Add(
                    issues,
                    SettingsDataIssueCode.MissingValue,
                    path + ".accessibility",
                    "可访问性设置为空");
            }
            else
            {
                ValidateRange(
                    payload.Accessibility.TextScale,
                    0.75f,
                    2f,
                    path + ".accessibility.textScale",
                    issues);
                ValidateUnit(
                    payload.Accessibility.ScreenShakeIntensity,
                    path + ".accessibility.screenShakeIntensity",
                    issues);
            }
        }

        static void ValidateEnum<T>(T value, string path, List<SettingsDataIssue> issues)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                Add(issues, SettingsDataIssueCode.InvalidValue, path, $"{path} 枚举值非法");
            }
        }

        static void ValidateUnit(float value, string path, List<SettingsDataIssue> issues)
        {
            ValidateRange(value, 0f, 1f, path, issues);
        }

        static void ValidateRange(
            float value,
            float minimum,
            float maximum,
            string path,
            List<SettingsDataIssue> issues)
        {
            if (!float.IsFinite(value))
            {
                Add(issues, SettingsDataIssueCode.NonFiniteNumber, path, $"{path} 不是有限数值");
                return;
            }

            if (value < minimum || value > maximum)
            {
                Add(issues, SettingsDataIssueCode.InvalidValue, path, $"{path} 超出允许范围");
            }
        }

        static void Add(
            List<SettingsDataIssue> issues,
            SettingsDataIssueCode code,
            string path,
            string message)
        {
            issues.Add(new SettingsDataIssue(code, path, message));
        }

        static SettingsDataValidationResult Result(List<SettingsDataIssue> issues)
        {
            return new SettingsDataValidationResult(issues.AsReadOnly());
        }
    }
}
