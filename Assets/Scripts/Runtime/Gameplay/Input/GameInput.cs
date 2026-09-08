using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkFlare
{
    public enum GameInputMode
    {
        Gameplay,
        UI
    }

    public sealed class GameInput : IUtility, IDisposable
    {
        readonly ApplicationInputService _inputService;

        bool _cancelSwitchPending;
        bool _disposed;

        public GameInput(ApplicationInputService inputService)
        {
            _inputService = inputService
                ?? throw new ArgumentNullException(nameof(inputService));

            if (_inputService.IsClosed)
            {
                throw new ObjectDisposedException(nameof(inputService));
            }

            _inputService.InteractPerformed += OnInteract;
            _inputService.ToggleMenuPerformed += OnToggleMenu;
            _inputService.PausePerformed += OnPause;
            _inputService.CycleWindowPerformed += OnCycleWindow;
            _inputService.SetSessionMenuActive(true);
            _inputService.NavigatePerformed += OnNavigate;
            _inputService.RearrangePerformed += OnRearrange;
            _inputService.CancelPerformed += OnCancel;
            _inputService.RequestedContextChanged += OnContextChanged;
            _inputService.BindingsChanged += OnBindingDisplayChanged;
            _inputService.GlyphChanged += OnBindingDisplayChanged;
            SwitchToGameplay();
        }

        public event Action ToggleInventoryRequested;
        public event Action PauseRequested;
        public event Action<int> CycleWindowRequested;
        public event Action InteractPerformed;

        public event Action RearrangePerformed;

        public event Action<Vector2> NavigatePerformed;

        public event Func<bool> CancelRequested;

        public event Action<GameInputMode> ModeChanged;

        public event Action BindingDisplayChanged;

        public GameInputMode CurrentMode => ToGameInputMode(_inputService.RequestedContext);

        public Vector2 Move => _disposed ? Vector2.zero : _inputService.Move;

        public bool IsGameplayEnabled => !_disposed && _inputService.IsGameplayEnabled;

        public bool IsUiEnabled => !_disposed && _inputService.IsUiEnabled;

        public string GetInteractBindingDisplayString()
        {
            return _disposed
                ? string.Empty
                : _inputService.GetInteractBindingDisplayString();
        }

        public void SwitchToGameplay()
        {
            SetMode(GameInputMode.Gameplay);
        }

        public void SwitchToUi()
        {
            SetMode(GameInputMode.UI);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _inputService.InteractPerformed -= OnInteract;
            _inputService.ToggleMenuPerformed -= OnToggleMenu;
            _inputService.PausePerformed -= OnPause;
            _inputService.CycleWindowPerformed -= OnCycleWindow;
            if (!_inputService.IsClosed) { _inputService.SetSessionMenuActive(false); }
            _inputService.NavigatePerformed -= OnNavigate;
            _inputService.RearrangePerformed -= OnRearrange;
            _inputService.CancelPerformed -= OnCancel;
            _inputService.RequestedContextChanged -= OnContextChanged;
            _inputService.BindingsChanged -= OnBindingDisplayChanged;
            _inputService.GlyphChanged -= OnBindingDisplayChanged;
            CancelPendingContextSwitch();

            if (!_inputService.IsClosed)
            {
                _inputService.SwitchContext(InputContext.UI);
            }

            ToggleInventoryRequested = null;
            PauseRequested = null;
            CycleWindowRequested = null;
            InteractPerformed = null;
            RearrangePerformed = null;
            NavigatePerformed = null;
            CancelRequested = null;
            ModeChanged = null;
            BindingDisplayChanged = null;
            _disposed = true;
        }

        void SetMode(GameInputMode mode)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GameInput));
            }

            _inputService.SwitchContext(ToInputContext(mode));
        }

        void OnToggleMenu()
        {
            if (ToggleInventoryRequested != null) { ToggleInventoryRequested.Invoke(); }
            else { SwitchToUi(); }
        }

        void OnPause() { PauseRequested?.Invoke(); }
        void OnCycleWindow(int step) { CycleWindowRequested?.Invoke(step); }

        void OnInteract()
        {
            InteractPerformed?.Invoke();
        }

        void OnNavigate(Vector2 value)
        {
            if (!_inputService.HasUiContextOverride)
            {
                NavigatePerformed?.Invoke(value);
            }
        }

        void OnRearrange()
        {
            if (!_inputService.HasUiContextOverride)
            {
                RearrangePerformed?.Invoke();
            }
        }

        void OnCancel()
        {
            if (TryHandleCancelRequest())
            {
                return;
            }

            if (_cancelSwitchPending)
            {
                return;
            }

            _cancelSwitchPending = true;
            InputSystem.onAfterUpdate += OnAfterInputUpdate;
        }

        void OnAfterInputUpdate()
        {
            CancelPendingContextSwitch();

            if (_disposed
                || _inputService.IsClosed
                || _inputService.HasUiContextOverride
                || CurrentMode != GameInputMode.UI)
            {
                return;
            }

            SwitchToGameplay();
        }

        void CancelPendingContextSwitch()
        {
            if (!_cancelSwitchPending)
            {
                return;
            }

            InputSystem.onAfterUpdate -= OnAfterInputUpdate;
            _cancelSwitchPending = false;
        }

        void OnContextChanged(InputContext context)
        {
            ModeChanged?.Invoke(ToGameInputMode(context));
        }

        void OnBindingDisplayChanged()
        {
            BindingDisplayChanged?.Invoke();
        }

        bool TryHandleCancelRequest()
        {
            if (CancelRequested == null)
            {
                return false;
            }

            Delegate[] callbacks = CancelRequested.GetInvocationList();

            for (int i = 0; i < callbacks.Length; i++)
            {
                if (callbacks[i] is Func<bool> callback && callback())
                {
                    return true;
                }
            }

            return false;
        }

        static InputContext ToInputContext(GameInputMode mode)
        {
            switch (mode)
            {
                case GameInputMode.Gameplay:
                    return InputContext.Gameplay;
                case GameInputMode.UI:
                    return InputContext.UI;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        static GameInputMode ToGameInputMode(InputContext context)
        {
            switch (context)
            {
                case InputContext.Gameplay:
                    return GameInputMode.Gameplay;
                case InputContext.UI:
                    return GameInputMode.UI;
                default:
                    throw new ArgumentOutOfRangeException(nameof(context), context, null);
            }
        }
    }
}
