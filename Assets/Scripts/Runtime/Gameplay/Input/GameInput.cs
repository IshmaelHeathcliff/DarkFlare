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
        readonly InputSystem_Actions _actions;

        bool _hasMode;
        bool _disposed;

        public event Action<GameInputMode> ModeChanged;

        public GameInputMode CurrentMode { get; private set; }

        public Vector2 Move => _disposed ? Vector2.zero : _actions.Player.Move.ReadValue<Vector2>();

        public bool IsGameplayEnabled => !_disposed && _actions.Player.enabled;

        public bool IsUiEnabled => !_disposed && _actions.UI.enabled;

        public GameInput()
        {
            _actions = new InputSystem_Actions();
            _actions.Player.ToggleMenu.performed += OnToggleMenu;
            _actions.UI.Cancel.performed += OnCancel;
            SwitchToGameplay();
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

            _actions.Player.ToggleMenu.performed -= OnToggleMenu;
            _actions.UI.Cancel.performed -= OnCancel;
            _actions.Disable();

            if (Application.isPlaying)
            {
                _actions.Dispose();
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_actions.asset);
            }

            ModeChanged = null;
            _disposed = true;
        }

        void SetMode(GameInputMode mode)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GameInput));
            }

            if (_hasMode && CurrentMode == mode)
            {
                return;
            }

            if (mode == GameInputMode.Gameplay)
            {
                _actions.UI.Disable();
                _actions.Player.Enable();
            }
            else
            {
                _actions.Player.Disable();
                _actions.UI.Enable();
            }

            CurrentMode = mode;
            _hasMode = true;
            Debug.Log($"[GameInput] 输入模式切换为 {mode}");
            ModeChanged?.Invoke(mode);
        }

        void OnToggleMenu(InputAction.CallbackContext context)
        {
            SwitchToUi();
        }

        void OnCancel(InputAction.CallbackContext context)
        {
            SwitchToGameplay();
        }
    }
}
