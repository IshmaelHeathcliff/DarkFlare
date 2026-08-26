using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace DarkFlare
{
    public sealed class ApplicationInputService : IDisposable
    {
        sealed class SuspensionLease : IDisposable
        {
            ApplicationInputService _owner;

            readonly InputSuspensionReason _reason;

            public SuspensionLease(
                ApplicationInputService owner,
                InputSuspensionReason reason)
            {
                _owner = owner;
                _reason = reason;
            }

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                ApplicationInputService owner = _owner;
                _owner = null;
                owner.ReleaseSuspension(_reason);
            }
        }

        readonly InputSystem_Actions _actions;
        readonly Dictionary<InputSuspensionReason, int> _suspensionCounts =
            new Dictionary<InputSuspensionReason, int>();
        readonly SettingsService _settings;
        readonly InputGlyphResolver _glyphResolver;
        readonly IDisposable _buttonActivitySubscription;

        InputActionRebindingExtensions.RebindingOperation _rebindOperation;
        CancellationTokenRegistration _rebindCancellationRegistration;
        IDisposable _rebindSuspension;
        InputDeviceFamily _activeRebindFamily;
        InputGlyphPreference _glyphPreference;
        bool _rebindCancelledByToken;
        bool _rebindDeviceRemoved;
        float _rebindStartedAt;
        float _rebindTimeoutSeconds;
        bool _closed;

        public ApplicationInputService(SettingsService settings = null)
        {
            _settings = settings;
            _glyphResolver = new InputGlyphResolver();
            _actions = new InputSystem_Actions();
            _actions.Player.Interact.performed += OnInteract;
            _actions.Player.ToggleMenu.performed += OnToggleMenu;
            _actions.UI.Navigate.performed += OnNavigate;
            _actions.UI.Rearrange.performed += OnRearrange;
            _actions.UI.Cancel.performed += OnCancel;
            _actions.Player.Get().actionTriggered += OnActionTriggered;
            _actions.UI.Get().actionTriggered += OnActionTriggered;
            _buttonActivitySubscription = InputSystem.onAnyButtonPress.Call(OnAnyButtonPress);
            InputSystem.onDeviceChange += OnDeviceChange;
            _glyphPreference = _settings?.Current.GlyphPreference
                ?? InputGlyphPreference.Auto;
            InitialBindingLoadResult = LoadInitialBindingOverrides();

            if (_settings != null)
            {
                _settings.Changed += OnSettingsChanged;
            }

            CurrentContext = InputContext.UI;
            ApplyState();
        }

        public event Action InteractPerformed;

        public event Action ToggleMenuPerformed;

        public event Action<Vector2> NavigatePerformed;

        public event Action RearrangePerformed;

        public event Action CancelPerformed;

        public event Action<InputContext> ContextChanged;

        public event Action<InputSuspensionReason> SuspensionChanged;

        public event Action<InputDeviceFamily> DeviceFamilyChanged;

        public event Action BindingsChanged;

        public event Action GlyphChanged;

        public event Action Closing;

        public InputActionAsset ActionAsset => _closed ? null : _actions.asset;

        public InputContext CurrentContext { get; private set; }

        public InputSuspensionReason SuspensionReasons { get; private set; }

        public bool IsSuspended => SuspensionReasons != InputSuspensionReason.None;

        public bool IsClosed => _closed;

        public bool IsGameplayEnabled => !_closed && _actions.Player.enabled;

        public bool IsUiEnabled => !_closed && _actions.UI.enabled;

        public InputDeviceFamily ActiveDeviceFamily { get; private set; }

        public InputDeviceFamily DisplayDeviceFamily
        {
            get
            {
                switch (_glyphPreference)
                {
                    case InputGlyphPreference.KeyboardMouse:
                        return InputDeviceFamily.KeyboardMouse;
                    case InputGlyphPreference.Gamepad:
                        return InputDeviceFamily.Gamepad;
                    default:
                        return ActiveDeviceFamily == InputDeviceFamily.Unknown
                            ? InputDeviceFamily.KeyboardMouse
                            : ActiveDeviceFamily;
                }
            }
        }

        public InputGlyphPreference GlyphPreference => _glyphPreference;

        public bool IsRebinding => _rebindOperation != null;

        public InputRebindResult InitialBindingLoadResult { get; }

        public Vector2 Move => _closed
            ? Vector2.zero
            : _actions.Player.Move.ReadValue<Vector2>();

        public void SwitchContext(InputContext context)
        {
            ThrowIfClosed();

            if (!Enum.IsDefined(typeof(InputContext), context))
            {
                throw new ArgumentOutOfRangeException(nameof(context), context, null);
            }

            if (CurrentContext == context)
            {
                return;
            }

            CurrentContext = context;
            ApplyState();
            ContextChanged?.Invoke(context);
        }

        public IDisposable AcquireSuspension(InputSuspensionReason reason)
        {
            ThrowIfClosed();
            ValidateSuspensionReason(reason);

            _suspensionCounts.TryGetValue(reason, out int count);
            _suspensionCounts[reason] = count + 1;

            if (count == 0)
            {
                SuspensionReasons |= reason;
                ApplyState();
                SuspensionChanged?.Invoke(SuspensionReasons);
            }

            return new SuspensionLease(this, reason);
        }

        public string GetInteractBindingDisplayString()
        {
            if (_closed)
            {
                return string.Empty;
            }

            InputBindingTarget target = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                DisplayDeviceFamily);
            return GetBindingDisplayString(target);
        }

        public string GetBindingControlPath(InputBindingTarget target)
        {
            if (_closed || !TryResolveBinding(target, out InputAction action, out int index))
            {
                return string.Empty;
            }

            return action.bindings[index].effectivePath ?? string.Empty;
        }

        public string GetBindingDisplayString(InputBindingTarget target)
        {
            if (_closed || !TryResolveBinding(target, out InputAction action, out int index))
            {
                return string.Empty;
            }

            return action.GetBindingDisplayString(index);
        }

        public InputGlyphToken GetGlyphToken(
            RebindableInputAction action,
            InputBindingPart part = InputBindingPart.Primary)
        {
            InputDeviceFamily family = DisplayDeviceFamily;
            InputBindingTarget target = new InputBindingTarget(action, part, family);
            return GetGlyphToken(target);
        }

        public InputGlyphToken GetGlyphToken(InputBindingTarget target)
        {
            string controlPath = GetBindingControlPath(target);
            string display = GetBindingDisplayString(target);
            return _glyphResolver.Resolve(
                controlPath,
                target.DeviceFamily,
                display);
        }

        public async UniTask<InputRebindResult> ApplyBindingOverrideAsync(
            InputBindingTarget target,
            string controlPath,
            CancellationToken cancellationToken = default)
        {
            if (_closed)
            {
                return InputRebindResult.Failure(InputRebindResultCode.Closed, target);
            }

            if (!TryResolveBinding(target, out InputAction action, out int bindingIndex))
            {
                return InputRebindResult.Failure(InputRebindResultCode.InvalidTarget, target);
            }

            if (!IsControlPathValid(controlPath, target.DeviceFamily))
            {
                return InputRebindResult.Failure(InputRebindResultCode.InvalidControl, target);
            }

            if (HasTargetPath(target, controlPath))
            {
                return InputRebindResult.AlreadyApplied(target, controlPath);
            }

            InputBindingConflict conflict = FindConflict(target, controlPath);

            if (conflict != null)
            {
                return InputRebindResult.ConflictFailure(conflict);
            }

            string oldOverrides = SaveOverrides();

            try
            {
                action.ApplyBindingOverride(bindingIndex, controlPath);

                if (!HasRequiredBindings())
                {
                    RestoreOverrides(oldOverrides);
                    return InputRebindResult.Failure(
                        InputRebindResultCode.RequiredBindingMissing,
                        target);
                }

                if (_settings != null)
                {
                    string newOverrides = SaveOverrides();
                    UserSettingsSnapshot candidate = _settings.Current.WithInput(
                        newOverrides,
                        _glyphPreference);
                    SettingsOperationResult committed = await _settings.UpdateAsync(
                        candidate,
                        cancellationToken);

                    if (!committed.Succeeded)
                    {
                        RestoreOverrides(oldOverrides);
                        return InputRebindResult.Failure(
                            InputRebindResultCode.SettingsCommitFailed,
                            target,
                            committed.Exception);
                    }
                }

                BindingsChanged?.Invoke();
                GlyphChanged?.Invoke();
                return InputRebindResult.Success(target, controlPath);
            }
            catch (OperationCanceledException exception)
            {
                RestoreOverrides(oldOverrides);
                return InputRebindResult.Failure(
                    InputRebindResultCode.Cancelled,
                    target,
                    exception);
            }
            catch (Exception exception)
            {
                RestoreOverrides(oldOverrides);
                return InputRebindResult.Failure(
                    InputRebindResultCode.InvalidControl,
                    target,
                    exception);
            }
        }

        public async UniTask<InputRebindResult> ResetBindingOverridesAsync(
            InputDeviceFamily? deviceFamily = null,
            CancellationToken cancellationToken = default)
        {
            InputDeviceFamily resultFamily = deviceFamily ?? InputDeviceFamily.KeyboardMouse;
            InputBindingTarget resultTarget = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                resultFamily);

            if (_closed)
            {
                return InputRebindResult.Failure(InputRebindResultCode.Closed, resultTarget);
            }

            if (deviceFamily.HasValue && deviceFamily.Value == InputDeviceFamily.Unknown)
            {
                return InputRebindResult.Failure(
                    InputRebindResultCode.InvalidTarget,
                    resultTarget);
            }

            string oldOverrides = SaveOverrides();

            if (deviceFamily.HasValue)
            {
                RemoveBindingOverrides(deviceFamily.Value);
            }
            else
            {
                _actions.asset.RemoveAllBindingOverrides();
            }

            string newOverrides = SaveOverrides();

            if (string.Equals(oldOverrides, newOverrides, StringComparison.Ordinal))
            {
                return InputRebindResult.AlreadyApplied(resultTarget, string.Empty);
            }

            if (_settings != null)
            {
                SettingsOperationResult committed = await _settings.UpdateAsync(
                    _settings.Current.WithInput(newOverrides, _glyphPreference),
                    cancellationToken);

                if (!committed.Succeeded)
                {
                    RestoreOverrides(oldOverrides);
                    return InputRebindResult.Failure(
                        InputRebindResultCode.SettingsCommitFailed,
                        resultTarget,
                        committed.Exception);
                }
            }

            BindingsChanged?.Invoke();
            GlyphChanged?.Invoke();
            return InputRebindResult.Success(resultTarget, string.Empty);
        }

        public async UniTask<SettingsOperationResult> SetGlyphPreferenceAsync(
            InputGlyphPreference preference,
            CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();

            if (!Enum.IsDefined(typeof(InputGlyphPreference), preference))
            {
                throw new ArgumentOutOfRangeException(nameof(preference), preference, null);
            }

            if (_settings == null)
            {
                SetGlyphPreference(preference);
                UserSettingsSnapshot settings = UserSettingsSnapshot.Default.WithInput(
                    SaveOverrides(),
                    preference);
                return new SettingsOperationResult(
                    SettingsOperationCode.Success,
                    settings,
                    SettingsRecoverySource.Current,
                    null);
            }

            SettingsOperationResult result = await _settings.UpdateAsync(
                _settings.Current.WithInput(
                    _settings.Current.BindingOverridesJson,
                    preference),
                cancellationToken);

            if (result.Succeeded)
            {
                SetGlyphPreference(preference);
            }

            return result;
        }

        public async UniTask<InputRebindResult> StartInteractiveRebindAsync(
            InputBindingTarget target,
            float timeoutSeconds = 10f,
            CancellationToken cancellationToken = default)
        {
            if (_closed)
            {
                return InputRebindResult.Failure(InputRebindResultCode.Closed, target);
            }

            if (_rebindOperation != null
                || timeoutSeconds <= 0f
                || float.IsNaN(timeoutSeconds)
                || float.IsInfinity(timeoutSeconds)
                || !TryResolveBinding(target, out InputAction action, out int bindingIndex))
            {
                return InputRebindResult.Failure(InputRebindResultCode.InvalidTarget, target);
            }

            if (!IsDeviceFamilyAvailable(target.DeviceFamily))
            {
                return InputRebindResult.Failure(
                    InputRebindResultCode.DeviceUnavailable,
                    target);
            }

            UniTaskCompletionSource<InputRebindResult> completion =
                new UniTaskCompletionSource<InputRebindResult>();
            string capturedPath = string.Empty;
            _activeRebindFamily = target.DeviceFamily;
            _rebindCancelledByToken = false;
            _rebindDeviceRemoved = false;
            _rebindStartedAt = Time.realtimeSinceStartup;
            _rebindTimeoutSeconds = timeoutSeconds;
            _rebindSuspension = AcquireSuspension(InputSuspensionReason.Rebinding);
            _rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsHavingToMatchPath(GetDeviceLayout(target.DeviceFamily))
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(timeoutSeconds)
                .OnApplyBinding((operation, path) => capturedPath = path)
                .OnCancel(operation => completion.TrySetResult(
                    CreateInteractiveCancellationResult(target)))
                .OnComplete(operation => CompleteInteractiveRebindAsync(
                    completion,
                    target,
                    capturedPath,
                    cancellationToken).Forget());

            if (target.DeviceFamily == InputDeviceFamily.Gamepad)
            {
                _rebindOperation.WithCancelingThrough("<Gamepad>/buttonEast");
            }

            _rebindCancellationRegistration = cancellationToken.Register(() =>
            {
                _rebindCancelledByToken = true;
                _rebindOperation?.Cancel();
            });

            try
            {
                _rebindOperation.Start();
                return await completion.Task;
            }
            finally
            {
                ReleaseInteractiveRebind();
            }
        }

        public void RefreshDevices()
        {
            ThrowIfClosed();

            if (IsDeviceFamilyAvailable(ActiveDeviceFamily))
            {
                GlyphChanged?.Invoke();
                return;
            }

            InputDeviceFamily family = IsDeviceFamilyAvailable(InputDeviceFamily.KeyboardMouse)
                ? InputDeviceFamily.KeyboardMouse
                : IsDeviceFamilyAvailable(InputDeviceFamily.Gamepad)
                    ? InputDeviceFamily.Gamepad
                    : InputDeviceFamily.Unknown;
            SetActiveDeviceFamily(family);
        }

        internal void SetGlyphPreferenceForTests(InputGlyphPreference preference)
        {
            SetGlyphPreference(preference);
        }

        async UniTaskVoid CompleteInteractiveRebindAsync(
            UniTaskCompletionSource<InputRebindResult> completion,
            InputBindingTarget target,
            string controlPath,
            CancellationToken cancellationToken)
        {
            InputRebindResult result = await ApplyBindingOverrideAsync(
                target,
                controlPath,
                cancellationToken);
            completion.TrySetResult(result);
        }

        InputRebindResult CreateInteractiveCancellationResult(InputBindingTarget target)
        {
            if (_rebindDeviceRemoved)
            {
                return InputRebindResult.Failure(
                    InputRebindResultCode.DeviceUnavailable,
                    target);
            }

            if (_rebindCancelledByToken)
            {
                return InputRebindResult.Failure(InputRebindResultCode.Cancelled, target);
            }

            bool timedOut = Time.realtimeSinceStartup - _rebindStartedAt
                >= _rebindTimeoutSeconds - 0.01f;
            return InputRebindResult.Failure(
                timedOut
                    ? InputRebindResultCode.TimedOut
                    : InputRebindResultCode.Cancelled,
                target);
        }

        void ReleaseInteractiveRebind()
        {
            _rebindCancellationRegistration.Dispose();
            _rebindCancellationRegistration = default;
            _rebindOperation?.Dispose();
            _rebindOperation = null;
            _rebindSuspension?.Dispose();
            _rebindSuspension = null;
            _activeRebindFamily = InputDeviceFamily.Unknown;
            _rebindCancelledByToken = false;
            _rebindDeviceRemoved = false;
            _rebindStartedAt = 0f;
            _rebindTimeoutSeconds = 0f;
        }

        InputRebindResult LoadInitialBindingOverrides()
        {
            InputBindingTarget target = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);
            string overrides = _settings?.Current.BindingOverridesJson ?? string.Empty;

            if (string.IsNullOrWhiteSpace(overrides))
            {
                return InputRebindResult.AlreadyApplied(target, string.Empty);
            }

            if (overrides.Length > LocalSettingsFormat.MaximumBindingOverridesLength)
            {
                return InputRebindResult.Failure(InputRebindResultCode.InvalidOverrides, target);
            }

            try
            {
                _actions.asset.LoadBindingOverridesFromJson(overrides);
                return HasRequiredBindings()
                    ? InputRebindResult.Success(target, string.Empty)
                    : ResetInvalidInitialOverrides(target);
            }
            catch (Exception exception)
            {
                _actions.asset.RemoveAllBindingOverrides();
                return InputRebindResult.Failure(
                    InputRebindResultCode.InvalidOverrides,
                    target,
                    exception);
            }
        }

        InputRebindResult ResetInvalidInitialOverrides(InputBindingTarget target)
        {
            _actions.asset.RemoveAllBindingOverrides();
            return InputRebindResult.Failure(
                InputRebindResultCode.RequiredBindingMissing,
                target);
        }

        void RestoreOverrides(string overrides)
        {
            _actions.asset.RemoveAllBindingOverrides();

            if (!string.IsNullOrWhiteSpace(overrides))
            {
                _actions.asset.LoadBindingOverridesFromJson(overrides);
            }
        }

        string SaveOverrides()
        {
            foreach (InputAction action in _actions.asset)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action.bindings[i].hasOverrides)
                    {
                        return _actions.asset.SaveBindingOverridesAsJson();
                    }
                }
            }

            return string.Empty;
        }

        bool TryResolveBinding(
            InputBindingTarget target,
            out InputAction action,
            out int bindingIndex)
        {
            action = null;
            bindingIndex = -1;

            if (!Enum.IsDefined(typeof(RebindableInputAction), target.Action)
                || !Enum.IsDefined(typeof(InputBindingPart), target.Part)
                || !Enum.IsDefined(typeof(InputDeviceFamily), target.DeviceFamily)
                || target.DeviceFamily == InputDeviceFamily.Unknown)
            {
                return false;
            }

            bool directional = RebindableInputCatalog.UsesDirectionalParts(target.Action);

            if ((!directional && target.Part != InputBindingPart.Primary)
                || (directional
                    && target.DeviceFamily == InputDeviceFamily.KeyboardMouse
                    && target.Part == InputBindingPart.Primary))
            {
                return false;
            }

            action = _actions.asset.FindAction(
                $"{RebindableInputCatalog.GetMapName(target.Action)}/{RebindableInputCatalog.GetActionName(target.Action)}",
                false);

            if (action == null)
            {
                return false;
            }

            string group = GetBindingGroup(target.DeviceFamily);

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];

                if (BindingMatchesTarget(binding, target.Part, group))
                {
                    bindingIndex = i;
                    return true;
                }
            }

            return false;
        }

        bool HasTargetPath(InputBindingTarget target, string controlPath)
        {
            InputAction action = _actions.asset.FindAction(
                $"{RebindableInputCatalog.GetMapName(target.Action)}/{RebindableInputCatalog.GetActionName(target.Action)}",
                false);

            if (action == null)
            {
                return false;
            }

            string group = GetBindingGroup(target.DeviceFamily);

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];

                if (BindingMatchesTarget(binding, target.Part, group)
                    && string.Equals(
                        binding.effectivePath,
                        controlPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        InputBindingConflict FindConflict(
            InputBindingTarget target,
            string controlPath)
        {
            string targetMap = RebindableInputCatalog.GetMapName(target.Action);
            string group = GetBindingGroup(target.DeviceFamily);

            for (int actionIndex = 0; actionIndex < RebindableInputCatalog.Actions.Count; actionIndex++)
            {
                RebindableInputAction candidateAction = RebindableInputCatalog.Actions[actionIndex];

                if (!string.Equals(
                    RebindableInputCatalog.GetMapName(candidateAction),
                    targetMap,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                InputAction action = _actions.asset.FindAction(
                    $"{targetMap}/{RebindableInputCatalog.GetActionName(candidateAction)}",
                    false);

                if (action == null)
                {
                    continue;
                }

                for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
                {
                    InputBinding binding = action.bindings[bindingIndex];

                    if (binding.isComposite
                        || !MatchesGroup(binding, group)
                        || !string.Equals(
                            binding.effectivePath,
                            controlPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    InputBindingPart part = binding.isPartOfComposite
                        ? ParseBindingPart(binding.name)
                        : InputBindingPart.Primary;

                    if (part == (InputBindingPart)(-1))
                    {
                        continue;
                    }

                    InputBindingTarget conflictTarget = new InputBindingTarget(
                        candidateAction,
                        part,
                        target.DeviceFamily);

                    if (conflictTarget != target)
                    {
                        return new InputBindingConflict(
                            target,
                            conflictTarget,
                            controlPath);
                    }
                }
            }

            return null;
        }

        bool HasRequiredBindings()
        {
            InputDeviceFamily[] families =
            {
                InputDeviceFamily.KeyboardMouse,
                InputDeviceFamily.Gamepad,
            };
            RebindableInputAction[] requiredActions =
            {
                RebindableInputAction.PlayerToggleMenu,
                RebindableInputAction.UiCancel,
            };

            for (int familyIndex = 0; familyIndex < families.Length; familyIndex++)
            {
                for (int actionIndex = 0; actionIndex < requiredActions.Length; actionIndex++)
                {
                    InputBindingTarget target = new InputBindingTarget(
                        requiredActions[actionIndex],
                        InputBindingPart.Primary,
                        families[familyIndex]);

                    if (string.IsNullOrWhiteSpace(GetBindingControlPath(target)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        void RemoveBindingOverrides(InputDeviceFamily deviceFamily)
        {
            string group = GetBindingGroup(deviceFamily);

            foreach (InputAction action in _actions.asset)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (MatchesGroup(action.bindings[i], group))
                    {
                        action.RemoveBindingOverride(i);
                    }
                }
            }
        }

        void OnActionTriggered(InputAction.CallbackContext context)
        {
            if (context.phase != InputActionPhase.Performed || context.control == null)
            {
                return;
            }

            SetActiveDeviceFamily(GetDeviceFamily(context.control.device));
        }

        void OnAnyButtonPress(InputControl control)
        {
            if (control != null)
            {
                SetActiveDeviceFamily(GetDeviceFamily(control.device));
            }
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Removed
                && change != InputDeviceChange.Disconnected)
            {
                return;
            }

            InputDeviceFamily family = GetDeviceFamily(device);

            if (_rebindOperation != null && family == _activeRebindFamily)
            {
                _rebindDeviceRemoved = true;
                _rebindOperation.Cancel();
            }

            if (family == ActiveDeviceFamily && !IsDeviceFamilyAvailable(family))
            {
                RefreshDevices();
            }
        }

        void OnSettingsChanged(UserSettingsSnapshot settings)
        {
            string currentOverrides = SaveOverrides();

            if (!string.Equals(
                    currentOverrides,
                    settings.BindingOverridesJson,
                    StringComparison.Ordinal))
            {
                try
                {
                    RestoreOverrides(settings.BindingOverridesJson);
                    BindingsChanged?.Invoke();
                }
                catch (Exception exception)
                {
                    RestoreOverrides(currentOverrides);
                    ApplicationLog.Exception(
                        LogEventIds.InfrastructureInput,
                        exception);
                }
            }

            SetGlyphPreference(settings.GlyphPreference);
        }

        void SetGlyphPreference(InputGlyphPreference preference)
        {
            if (_glyphPreference == preference)
            {
                return;
            }

            _glyphPreference = preference;
            GlyphChanged?.Invoke();
        }

        void SetActiveDeviceFamily(InputDeviceFamily family)
        {
            if (family == InputDeviceFamily.Unknown || ActiveDeviceFamily == family)
            {
                return;
            }

            ActiveDeviceFamily = family;
            DeviceFamilyChanged?.Invoke(family);

            if (_glyphPreference == InputGlyphPreference.Auto)
            {
                GlyphChanged?.Invoke();
            }
        }

        static bool BindingMatchesTarget(
            InputBinding binding,
            InputBindingPart part,
            string group)
        {
            if (!MatchesGroup(binding, group) || binding.isComposite)
            {
                return false;
            }

            if (part == InputBindingPart.Primary)
            {
                return !binding.isPartOfComposite;
            }

            return binding.isPartOfComposite
                && string.Equals(
                    binding.name,
                    part.ToString(),
                    StringComparison.OrdinalIgnoreCase);
        }

        static bool MatchesGroup(InputBinding binding, string group)
        {
            if (string.IsNullOrWhiteSpace(binding.groups))
            {
                return false;
            }

            string[] groups = binding.groups.Split(';');

            for (int i = 0; i < groups.Length; i++)
            {
                if (string.Equals(groups[i], group, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        static InputBindingPart ParseBindingPart(string bindingName)
        {
            return Enum.TryParse(bindingName, true, out InputBindingPart part)
                ? part
                : (InputBindingPart)(-1);
        }

        static bool IsControlPathValid(
            string controlPath,
            InputDeviceFamily deviceFamily)
        {
            if (string.IsNullOrWhiteSpace(controlPath)
                || controlPath.IndexOf('*') >= 0)
            {
                return false;
            }

            switch (deviceFamily)
            {
                case InputDeviceFamily.KeyboardMouse:
                    return controlPath.StartsWith(
                        "<Keyboard>/",
                        StringComparison.OrdinalIgnoreCase);
                case InputDeviceFamily.Gamepad:
                    return controlPath.StartsWith(
                        "<Gamepad>/",
                        StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        static bool IsDeviceFamilyAvailable(InputDeviceFamily family)
        {
            switch (family)
            {
                case InputDeviceFamily.KeyboardMouse:
                    return Keyboard.current != null || Mouse.current != null;
                case InputDeviceFamily.Gamepad:
                    return Gamepad.current != null;
                default:
                    return false;
            }
        }

        static InputDeviceFamily GetDeviceFamily(InputDevice device)
        {
            if (device is Keyboard || device is Mouse)
            {
                return InputDeviceFamily.KeyboardMouse;
            }

            return device is Gamepad
                ? InputDeviceFamily.Gamepad
                : InputDeviceFamily.Unknown;
        }

        static string GetBindingGroup(InputDeviceFamily family)
        {
            switch (family)
            {
                case InputDeviceFamily.KeyboardMouse:
                    return "Keyboard&Mouse";
                case InputDeviceFamily.Gamepad:
                    return "Gamepad";
                default:
                    throw new ArgumentOutOfRangeException(nameof(family), family, null);
            }
        }

        static string GetDeviceLayout(InputDeviceFamily family)
        {
            return family == InputDeviceFamily.Gamepad
                ? "<Gamepad>"
                : "<Keyboard>";
        }

        public void Dispose()
        {
            if (_closed)
            {
                return;
            }

            _rebindOperation?.Cancel();
            ReleaseInteractiveRebind();
            Closing?.Invoke();
            _closed = true;
            _actions.Player.Interact.performed -= OnInteract;
            _actions.Player.ToggleMenu.performed -= OnToggleMenu;
            _actions.UI.Navigate.performed -= OnNavigate;
            _actions.UI.Rearrange.performed -= OnRearrange;
            _actions.UI.Cancel.performed -= OnCancel;
            _actions.Player.Get().actionTriggered -= OnActionTriggered;
            _actions.UI.Get().actionTriggered -= OnActionTriggered;
            _buttonActivitySubscription.Dispose();
            InputSystem.onDeviceChange -= OnDeviceChange;

            if (_settings != null)
            {
                _settings.Changed -= OnSettingsChanged;
            }

            _actions.Disable();
            _suspensionCounts.Clear();
            SuspensionReasons = InputSuspensionReason.None;

            if (Application.isPlaying)
            {
                _actions.Dispose();
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_actions.asset);
            }

            InteractPerformed = null;
            ToggleMenuPerformed = null;
            NavigatePerformed = null;
            RearrangePerformed = null;
            CancelPerformed = null;
            ContextChanged = null;
            SuspensionChanged = null;
            DeviceFamilyChanged = null;
            BindingsChanged = null;
            GlyphChanged = null;
            Closing = null;
        }

        void ReleaseSuspension(InputSuspensionReason reason)
        {
            if (_closed || !_suspensionCounts.TryGetValue(reason, out int count))
            {
                return;
            }

            if (count > 1)
            {
                _suspensionCounts[reason] = count - 1;
                return;
            }

            _suspensionCounts.Remove(reason);
            SuspensionReasons &= ~reason;
            ApplyState();
            SuspensionChanged?.Invoke(SuspensionReasons);
        }

        void ApplyState()
        {
            _actions.Disable();

            if (_closed || IsSuspended)
            {
                return;
            }

            if (CurrentContext == InputContext.Gameplay)
            {
                _actions.Player.Enable();
                return;
            }

            _actions.UI.Enable();
        }

        void OnInteract(InputAction.CallbackContext context)
        {
            InteractPerformed?.Invoke();
        }

        void OnToggleMenu(InputAction.CallbackContext context)
        {
            ToggleMenuPerformed?.Invoke();
        }

        void OnNavigate(InputAction.CallbackContext context)
        {
            NavigatePerformed?.Invoke(context.ReadValue<Vector2>());
        }

        void OnRearrange(InputAction.CallbackContext context)
        {
            RearrangePerformed?.Invoke();
        }

        void OnCancel(InputAction.CallbackContext context)
        {
            CancelPerformed?.Invoke();
        }

        void ThrowIfClosed()
        {
            if (_closed)
            {
                throw new ObjectDisposedException(nameof(ApplicationInputService));
            }
        }

        static void ValidateSuspensionReason(InputSuspensionReason reason)
        {
            int value = (int)reason;

            if (value <= 0
                || (value & (value - 1)) != 0
                || !Enum.IsDefined(typeof(InputSuspensionReason), reason))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reason),
                    reason,
                    "暂停租约必须指定一个已定义的非空原因");
            }
        }

        static string GetBindingDisplayString(InputAction action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            List<string> displayNames = new List<string>();

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];

                if (binding.isComposite || binding.isPartOfComposite)
                {
                    continue;
                }

                string displayName = action.GetBindingDisplayString(i);

                if (!string.IsNullOrWhiteSpace(displayName)
                    && !displayNames.Contains(displayName))
                {
                    displayNames.Add(displayName);
                }
            }

            return string.Join(" / ", displayNames);
        }
    }
}
