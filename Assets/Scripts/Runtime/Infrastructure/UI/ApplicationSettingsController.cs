using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed class ApplicationSettingsController : IDisposable
    {
        sealed class BindingRow
        {
            public InputBindingTarget Target { get; }

            public VisualElement Glyph { get; }

            public Label GlyphText { get; }

            public Button Action { get; }

            public BindingRow(
                InputBindingTarget target,
                VisualElement glyph,
                Label glyphText,
                Button action)
            {
                Target = target;
                Glyph = glyph;
                GlyphText = glyphText;
                Action = action;
            }
        }

        static readonly InputGlyphPreference[] GlyphPreferences =
        {
            InputGlyphPreference.Auto,
            InputGlyphPreference.KeyboardMouse,
            InputGlyphPreference.Gamepad,
        };

        static readonly string[] GlyphPreferenceKeys =
        {
            "settings.input.glyph.auto",
            "settings.input.device.keyboard_mouse",
            "settings.input.device.gamepad",
        };

        readonly VisualElement _root;
        readonly ApplicationHost _host;
        readonly Action<LocalizedMessage> _showToast;
        readonly Action<LocalizedMessage, LocalizedMessage, Action> _showConfirmation;
        readonly Action<LocalizedMessage> _showBusy;
        readonly Action _hideBusy;
        readonly List<BindingRow> _bindingRows = new List<BindingRow>();
        readonly List<string> _glyphChoices = new List<string>();

        VisualElement _page;
        VisualElement _bindingList;
        Button _back;
        Button _restoreDefaults;
        Slider _masterVolume;
        Slider _musicVolume;
        Slider _soundEffectsVolume;
        Slider _uiVolume;
        Toggle _muted;
        DropdownField _glyphPreference;
        Toggle _reduceMotion;
        Label _status;
        Action _restoreFocus;
        CancellationTokenSource _rebindCancellation;
        UserSettingsSnapshot _pendingSettings;
        bool _settingsCommitRunning;
        bool _updatingControls;
        bool _updatingGlyphPreference;
        bool _bound;

        public ApplicationSettingsController(
            VisualElement root,
            ApplicationHost host,
            Action<LocalizedMessage> showToast,
            Action<LocalizedMessage, LocalizedMessage, Action> showConfirmation,
            Action<LocalizedMessage> showBusy,
            Action hideBusy)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _showToast = showToast ?? throw new ArgumentNullException(nameof(showToast));
            _showConfirmation = showConfirmation
                ?? throw new ArgumentNullException(nameof(showConfirmation));
            _showBusy = showBusy ?? throw new ArgumentNullException(nameof(showBusy));
            _hideBusy = hideBusy ?? throw new ArgumentNullException(nameof(hideBusy));
        }

        public bool IsOpen { get; private set; }

        public void Bind()
        {
            if (_bound)
            {
                return;
            }

            _page = Require<VisualElement>("application-settings");
            _bindingList = Require<VisualElement>("settings-binding-list");
            _back = Require<Button>("settings-back");
            _restoreDefaults = Require<Button>("settings-restore-defaults");
            _masterVolume = Require<Slider>("settings-master-volume");
            _musicVolume = Require<Slider>("settings-music-volume");
            _soundEffectsVolume = Require<Slider>("settings-sfx-volume");
            _uiVolume = Require<Slider>("settings-ui-volume");
            _muted = Require<Toggle>("settings-muted");
            _glyphPreference = Require<DropdownField>("settings-glyph-preference");
            _reduceMotion = Require<Toggle>("settings-reduce-motion");
            _status = Require<Label>("settings-status");
            _back.clicked += Close;
            _restoreDefaults.clicked += RequestRestoreDefaults;
            _masterVolume.RegisterValueChangedCallback(OnAudioChanged);
            _musicVolume.RegisterValueChangedCallback(OnAudioChanged);
            _soundEffectsVolume.RegisterValueChangedCallback(OnAudioChanged);
            _uiVolume.RegisterValueChangedCallback(OnAudioChanged);
            _muted.RegisterValueChangedCallback(OnMutedChanged);
            _glyphPreference.RegisterValueChangedCallback(OnGlyphPreferenceChanged);
            _reduceMotion.RegisterValueChangedCallback(OnReduceMotionChanged);
            _host.Settings.Changed += OnSettingsChanged;
            _host.Localization.LocaleChanged += OnLocaleChanged;
            _host.Input.BindingsChanged += RefreshBindingRows;
            _host.Input.GlyphChanged += RefreshBindingRows;
            _bound = true;
            BuildBindingRows();
            RefreshLocalizedText();
            RefreshFromSettings();
            SetVisible(false);
        }

        public void Open(Action restoreFocus = null)
        {
            if (!_bound || IsOpen)
            {
                return;
            }

            _restoreFocus = restoreFocus;
            IsOpen = true;
            RefreshLocalizedText();
            RefreshFromSettings();
            SetVisible(true);
            _back.Focus();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            CancelRebind();
            IsOpen = false;
            SetVisible(false);
            Action restore = _restoreFocus;
            _restoreFocus = null;
            restore?.Invoke();
        }

        public void Dispose()
        {
            if (!_bound)
            {
                return;
            }

            CancelRebind();
            _back.clicked -= Close;
            _restoreDefaults.clicked -= RequestRestoreDefaults;
            _masterVolume.UnregisterValueChangedCallback(OnAudioChanged);
            _musicVolume.UnregisterValueChangedCallback(OnAudioChanged);
            _soundEffectsVolume.UnregisterValueChangedCallback(OnAudioChanged);
            _uiVolume.UnregisterValueChangedCallback(OnAudioChanged);
            _muted.UnregisterValueChangedCallback(OnMutedChanged);
            _glyphPreference.UnregisterValueChangedCallback(OnGlyphPreferenceChanged);
            _reduceMotion.UnregisterValueChangedCallback(OnReduceMotionChanged);
            SettingsService settings = _host.Settings;
            LocalizationService localization = _host.Localization;
            ApplicationInputService input = _host.Input;
            AudioService audio = _host.Audio;

            if (settings != null)
            {
                settings.Changed -= OnSettingsChanged;
            }

            if (localization != null)
            {
                localization.LocaleChanged -= OnLocaleChanged;
            }

            if (input != null)
            {
                input.BindingsChanged -= RefreshBindingRows;
                input.GlyphChanged -= RefreshBindingRows;
            }

            audio?.StopOwner(this);
            _bindingRows.Clear();
            _bindingList?.Clear();
            _bound = false;
            IsOpen = false;
        }

        void BuildBindingRows()
        {
            _bindingRows.Clear();
            _bindingList.Clear();

            for (int i = 0; i < RebindableInputCatalog.Actions.Count; i++)
            {
                RebindableInputAction action = RebindableInputCatalog.Actions[i];

                if (RebindableInputCatalog.UsesDirectionalParts(action))
                {
                    AddBindingRow(action, InputBindingPart.Up, InputDeviceFamily.KeyboardMouse);
                    AddBindingRow(action, InputBindingPart.Down, InputDeviceFamily.KeyboardMouse);
                    AddBindingRow(action, InputBindingPart.Left, InputDeviceFamily.KeyboardMouse);
                    AddBindingRow(action, InputBindingPart.Right, InputDeviceFamily.KeyboardMouse);
                    AddBindingRow(action, InputBindingPart.Primary, InputDeviceFamily.Gamepad);
                    continue;
                }

                AddBindingRow(action, InputBindingPart.Primary, InputDeviceFamily.KeyboardMouse);
                AddBindingRow(action, InputBindingPart.Primary, InputDeviceFamily.Gamepad);
            }
        }

        void AddBindingRow(
            RebindableInputAction action,
            InputBindingPart part,
            InputDeviceFamily family)
        {
            InputBindingTarget target = new InputBindingTarget(action, part, family);
            VisualElement row = new VisualElement();
            row.AddToClassList("application-settings-binding-row");
            Label name = new Label();
            name.name = $"settings-binding-name-{action}-{part}-{family}";
            name.AddToClassList("application-settings-binding-name");
            VisualElement glyph = new VisualElement();
            glyph.AddToClassList("application-settings-binding-glyph");
            Label glyphText = new Label();
            glyphText.AddToClassList("application-settings-binding-glyph-text");
            glyph.Add(glyphText);
            Button bindingAction = new Button();
            bindingAction.name = $"settings-binding-{action}-{part}-{family}";
            bindingAction.AddToClassList("application-settings-binding-action");
            BindingRow bindingRow = new BindingRow(target, glyph, glyphText, bindingAction);
            bindingAction.clicked += () => StartRebind(bindingRow);
            row.Add(name);
            row.Add(glyph);
            row.Add(bindingAction);
            _bindingList.Add(row);
            _bindingRows.Add(bindingRow);
        }

        void RefreshLocalizedText()
        {
            _masterVolume.label = Resolve("settings.audio.master", "Master");
            _musicVolume.label = Resolve("settings.audio.music", "Music");
            _soundEffectsVolume.label = Resolve("settings.audio.sfx", "SFX");
            _uiVolume.label = Resolve("settings.audio.ui", "UI");
            _muted.label = Resolve("settings.audio.muted", "Mute");
            _glyphPreference.label = Resolve(
                "settings.input.glyph_preference",
                "Glyph Preference");
            _reduceMotion.label = Resolve(
                "settings.accessibility.reduce_motion",
                "Reduce Motion");
            _glyphChoices.Clear();

            for (int i = 0; i < GlyphPreferenceKeys.Length; i++)
            {
                _glyphChoices.Add(Resolve(GlyphPreferenceKeys[i], GlyphPreferences[i].ToString()));
            }

            RefreshBindingRows();
        }

        void RefreshFromSettings()
        {
            UserSettingsSnapshot settings = _host.Settings.Current;
            _updatingControls = true;

            try
            {
                _masterVolume.SetValueWithoutNotify(settings.MasterVolume);
                _musicVolume.SetValueWithoutNotify(settings.MusicVolume);
                _soundEffectsVolume.SetValueWithoutNotify(settings.SoundEffectsVolume);
                _uiVolume.SetValueWithoutNotify(settings.UiVolume);
                _muted.SetValueWithoutNotify(settings.Muted);
                _reduceMotion.SetValueWithoutNotify(settings.ReduceMotion);
                int index = Array.IndexOf(GlyphPreferences, settings.GlyphPreference);
                index = Math.Max(0, Math.Min(index, _glyphChoices.Count - 1));
                _glyphPreference.choices = new List<string>(_glyphChoices);
                _glyphPreference.SetValueWithoutNotify(_glyphChoices[index]);
            }
            finally
            {
                _updatingControls = false;
            }

            RefreshBindingRows();
        }

        void RefreshBindingRows()
        {
            for (int i = 0; i < _bindingRows.Count; i++)
            {
                BindingRow row = _bindingRows[i];
                Label name = _root.Q<Label>(
                    $"settings-binding-name-{row.Target.Action}-{row.Target.Part}-{row.Target.DeviceFamily}");

                if (name != null)
                {
                    string action = Resolve(
                        $"settings.input.action.{ToSnakeCase(row.Target.Action.ToString())}",
                        row.Target.Action.ToString());
                    string part = row.Target.Part == InputBindingPart.Primary
                        ? string.Empty
                        : Resolve(
                            $"settings.input.part.{ToSnakeCase(row.Target.Part.ToString())}",
                            row.Target.Part.ToString());
                    string family = Resolve(
                        row.Target.DeviceFamily == InputDeviceFamily.Gamepad
                            ? "settings.input.device.gamepad"
                            : "settings.input.device.keyboard_mouse",
                        row.Target.DeviceFamily.ToString());
                    name.text = string.IsNullOrWhiteSpace(part)
                        ? $"{action} · {family}"
                        : $"{action} · {part} · {family}";
                }

                row.Action.text = _host.Input.GetBindingDisplayString(row.Target);
                InputGlyphToken token = _host.Input.GetGlyphToken(row.Target);
                UpdateGlyph(row, token);
            }
        }

        void UpdateGlyph(BindingRow row, InputGlyphToken token)
        {
            List<string> classes = new List<string>(row.Glyph.GetClasses());

            for (int i = 0; i < classes.Count; i++)
            {
                if (classes[i].StartsWith("input-glyph--", StringComparison.Ordinal))
                {
                    row.Glyph.RemoveFromClassList(classes[i]);
                }
            }

            if (!string.IsNullOrWhiteSpace(token.GlyphId))
            {
                row.Glyph.AddToClassList($"input-glyph--{token.GlyphId.Replace('.', '-')}");
            }

            row.GlyphText.text = token.UsesTextFallback
                || string.Equals(token.GlyphId, "keyboard.keycap", StringComparison.Ordinal)
                    ? token.FallbackText
                    : string.Empty;
        }

        void OnAudioChanged(ChangeEvent<float> evt)
        {
            if (_updatingControls)
            {
                return;
            }

            QueueSettingsUpdate(_host.Settings.Current.WithAudio(
                _masterVolume.value,
                _musicVolume.value,
                _soundEffectsVolume.value,
                _uiVolume.value,
                _muted.value));
        }

        void OnMutedChanged(ChangeEvent<bool> evt)
        {
            OnAudioChanged(null);
        }

        void OnReduceMotionChanged(ChangeEvent<bool> evt)
        {
            if (!_updatingControls)
            {
                QueueSettingsUpdate(_host.Settings.Current.WithReduceMotion(evt.newValue));
            }
        }

        void OnGlyphPreferenceChanged(ChangeEvent<string> evt)
        {
            if (_updatingControls || _updatingGlyphPreference)
            {
                return;
            }

            int index = _glyphChoices.IndexOf(evt.newValue);

            if (index < 0 || index >= GlyphPreferences.Length)
            {
                return;
            }

            _updatingGlyphPreference = true;
            _glyphPreference.SetEnabled(false);

            try
            {
                _host.ApplicationScope.Tasks.Run(
                    "settings-glyph-preference",
                    async token =>
                    {
                        SettingsOperationResult result = await _host.Input.SetGlyphPreferenceAsync(
                            GlyphPreferences[index],
                            token);
                        _updatingGlyphPreference = false;
                        _glyphPreference.SetEnabled(true);

                        if (!result.Succeeded)
                        {
                            RefreshFromSettings();
                            SetStatus("settings.status.save_failed", "Save failed");
                        }
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch
            {
                _updatingGlyphPreference = false;
                _glyphPreference.SetEnabled(true);
                RefreshFromSettings();
            }
        }

        void QueueSettingsUpdate(UserSettingsSnapshot candidate)
        {
            _pendingSettings = candidate;

            if (_settingsCommitRunning)
            {
                return;
            }

            _settingsCommitRunning = true;

            try
            {
                _host.ApplicationScope.Tasks.Run(
                    "application-settings-commit",
                    CommitPendingSettingsAsync,
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch
            {
                _settingsCommitRunning = false;
                _pendingSettings = null;
                RefreshFromSettings();
            }
        }

        async UniTask CommitPendingSettingsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (_pendingSettings != null)
                {
                    UserSettingsSnapshot candidate = _pendingSettings;
                    _pendingSettings = null;
                    bool previewAudio = HasAudioDifference(
                        _host.Settings.Current,
                        candidate);
                    SettingsOperationResult result = await _host.Settings.UpdateAsync(
                        candidate,
                        cancellationToken);

                    if (!result.Succeeded)
                    {
                        _pendingSettings = null;
                        SetStatus("settings.status.save_failed", "Save failed");
                        break;
                    }

                    if (previewAudio && !candidate.Muted)
                    {
                        if (_host.Audio != null)
                        {
                            await _host.Audio.PlayAsync(
                                AudioCueIds.UiConfirm,
                                this,
                                cancellationToken);
                        }
                    }
                }
            }
            finally
            {
                _settingsCommitRunning = false;
                RefreshFromSettings();
            }
        }

        void StartRebind(BindingRow row)
        {
            if (_rebindCancellation != null)
            {
                return;
            }

            _rebindCancellation = new CancellationTokenSource();
            SetBindingsEnabled(false);
            SetStatus("settings.input.rebind_listening", "Press a control");

            try
            {
                _host.ApplicationScope.Tasks.Run(
                    $"settings-rebind:{row.Target.Action}:{row.Target.Part}:{row.Target.DeviceFamily}",
                    async token =>
                    {
                        using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                            token,
                            _rebindCancellation.Token))
                        {
                            InputRebindResult result = await _host.Input.StartInteractiveRebindAsync(
                                row.Target,
                                10f,
                                linked.Token);
                            FinishRebind(result);
                        }
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch
            {
                FinishRebind(InputRebindResult.Failure(
                    InputRebindResultCode.Cancelled,
                    row.Target));
            }
        }

        void FinishRebind(InputRebindResult result)
        {
            CancellationTokenSource cancellation = _rebindCancellation;
            _rebindCancellation = null;
            cancellation?.Dispose();
            SetBindingsEnabled(true);
            RefreshBindingRows();

            if (result.Succeeded)
            {
                SetStatus("settings.status.saved", "Saved");
                _showToast(LocalizedMessage.Ui("settings.toast.saved"));
                return;
            }

            string key = result.Code == InputRebindResultCode.Conflict
                ? "settings.input.rebind_conflict"
                : result.Code == InputRebindResultCode.TimedOut
                    ? "settings.input.rebind_timeout"
                    : "settings.input.rebind_cancelled";
            SetStatus(key, result.Code.ToString());
        }

        void RequestRestoreDefaults()
        {
            _showConfirmation(
                LocalizedMessage.Ui("settings.restore.title"),
                LocalizedMessage.Ui("settings.restore.message"),
                RestoreDefaults);
        }

        void RestoreDefaults()
        {
            if (_settingsCommitRunning || _host.Settings.IsBusy)
            {
                SetStatus("settings.status.busy", "Settings are busy");
                return;
            }

            CancelRebind();
            _page.SetEnabled(false);
            _showBusy(LocalizedMessage.Ui("settings.restore.busy"));

            try
            {
                _host.ApplicationScope.Tasks.Run(
                    "settings-restore-all-defaults",
                    async token =>
                    {
                        SettingsOperationResult result =
                            await _host.Settings.ResetToDefaultsAsync(token);
                        _hideBusy();
                        _page.SetEnabled(true);
                        RefreshBindingRows();
                        RefreshFromSettings();
                        SetStatus(
                            result.Succeeded
                                ? "settings.status.saved"
                                : "settings.status.save_failed",
                            result.Code.ToString());
                        _showToast(LocalizedMessage.Ui(
                            result.Succeeded
                                ? "settings.restore.success"
                                : "settings.restore.failed"));
                        _restoreDefaults.Focus();
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch
            {
                _hideBusy();
                _page.SetEnabled(true);
                RefreshFromSettings();
                _showToast(LocalizedMessage.Ui("settings.restore.failed"));
            }
        }

        void CancelRebind()
        {
            CancellationTokenSource cancellation = _rebindCancellation;
            _rebindCancellation = null;
            cancellation?.Cancel();
            cancellation?.Dispose();
            SetBindingsEnabled(true);
        }

        void SetBindingsEnabled(bool enabled)
        {
            for (int i = 0; i < _bindingRows.Count; i++)
            {
                _bindingRows[i].Action.SetEnabled(enabled);
            }

            _restoreDefaults.SetEnabled(enabled);
        }

        void OnSettingsChanged(UserSettingsSnapshot settings)
        {
            if (!_settingsCommitRunning)
            {
                RefreshFromSettings();
            }
        }

        void OnLocaleChanged(string localeCode)
        {
            RefreshLocalizedText();
            RefreshFromSettings();
        }

        public void HandleCancel()
        {
            if (!IsOpen || _rebindCancellation != null)
            {
                return;
            }

            Close();
        }

        void SetStatus(string key, string fallback)
        {
            _status.text = Resolve(key, fallback);
        }

        string Resolve(string key, string fallback)
        {
            string value = _host.Localization?.GetString(LocalizedMessage.Ui(key));
            return string.IsNullOrWhiteSpace(value)
                || value.StartsWith("[", StringComparison.Ordinal)
                ? fallback
                : value;
        }

        T Require<T>(string name) where T : VisualElement
        {
            T element = _root.Q<T>(name);

            if (element == null)
            {
                throw new InvalidOperationException($"Application Settings 缺少元素 {name}");
            }

            return element;
        }

        void SetVisible(bool visible)
        {
            if (_page != null)
            {
                _page.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        static string ToSnakeCase(string value)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length + 8);

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (char.IsUpper(character) && i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }

        static bool HasAudioDifference(
            UserSettingsSnapshot current,
            UserSettingsSnapshot candidate)
        {
            return !current.MasterVolume.Equals(candidate.MasterVolume)
                || !current.MusicVolume.Equals(candidate.MusicVolume)
                || !current.SoundEffectsVolume.Equals(candidate.SoundEffectsVolume)
                || !current.UiVolume.Equals(candidate.UiVolume)
                || current.Muted != candidate.Muted;
        }
    }
}
