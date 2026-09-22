using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [RequireComponent(typeof(RuntimePanelView))]
    public sealed class BehaviorDebugPanel : MonoBehaviour, IController
    {
        [SerializeField] RuntimePanelView _panel;
        [SerializeField] BehaviorSampleHost _host;
        readonly BehaviorDebugSelection _selection = new BehaviorDebugSelection();
        BehaviorDebugView _view;
        IVisualElementScheduledItem _refresh;
        VisualElement _root;
        IDisposable _inputLease;
        ApplicationInputService _input;
        GameInput _gameInput;
        Focusable _previousFocus;
        public bool IsOpen { get; private set; }
        public BehaviorHandle SelectedHandle => _selection.Handle;
        public IArchitecture GetArchitecture() { return _host.GetArchitecture(); }
        void OnValidate()
        {
            if (_panel == null) { _panel = GetComponent<RuntimePanelView>(); }
            if (_panel == null && gameObject.scene.IsValid()) { _panel = gameObject.AddComponent<RuntimePanelView>(); }
            if (_host == null) { _host = GetComponentInParent<BehaviorSampleHost>(); }
        }
        void OnEnable()
        {
            if (!Debug.isDebugBuild)
            {
                if (_panel != null) { _panel.gameObject.SetActive(false); }
                return;
            }
            OnValidate();
            _panel.Reloaded += Bind; _panel.Reloading += Unbind;
            _host.Ready += Bind; _host.Closing += Unbind;
            Bind();
        }
        void OnDisable()
        {
            if (_panel != null) { _panel.Reloaded -= Bind; _panel.Reloading -= Unbind; }
            if (_host != null) { _host.Ready -= Bind; _host.Closing -= Unbind; }
            Unbind();
        }
        void Bind()
        {
            Unbind();
            if (!isActiveAndEnabled || _panel.Root == null || !_host.IsReady) { return; }
            _root = _panel.Root;
            _root.AddToClassList("player-mode");
            _view = new BehaviorDebugView(_root);
            _view.Cycle += Cycle;
            _root.Q<Button>("open").clicked += Toggle;
            _root.Q<Button>("close").clicked += Toggle;
            _input = _host.Input;
            _gameInput = GetArchitecture().GetUtility<GameInput>();
            _gameInput.ToggleInventoryRequested += Toggle;
            _input.CancelRequested += Cancel;
            LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
            _refresh = _root.schedule.Execute(Refresh).Every(100);
            _root.Q("debugPanel").AddToClassList("closed");
        }
        public void Toggle()
        {
            if (_view == null) { return; }
            if (IsOpen) { Close(); return; }
            IsOpen = true;
            _previousFocus = _root.panel?.focusController.focusedElement;
            _inputLease = _input.AcquireUiContext("behavior-debug");
            _root.Q("debugPanel").RemoveFromClassList("closed");
            _root.Q<Button>("open").AddToClassList("closed");
            _selection.Cycle(GetArchitecture(), 1);
            Refresh(); _root.Q<Button>("next").Focus();
        }
        void Close()
        {
            IsOpen = false; _selection.Dispose(); _inputLease?.Dispose(); _inputLease = null;
            _root?.Q("debugPanel")?.AddToClassList("closed");
            _root?.Q<Button>("open")?.RemoveFromClassList("closed");
            _previousFocus?.Focus(); _previousFocus = null;
        }
        bool Cancel() { if (!IsOpen) { return false; } Close(); return true; }
        void Cycle(int direction) { if (IsOpen) { _selection.Cycle(GetArchitecture(), direction); Refresh(); } }
        void Refresh()
        {
            _view?.Localize();
            if (!IsOpen) { return; }
            BehaviorDebugSnapshot snapshot = _selection.Capture();
            if (snapshot == null) { _selection.Cycle(GetArchitecture(), 1); snapshot = _selection.Capture(); }
            _view?.Show(snapshot);
        }
        void LocaleChanged(Locale locale) { _view?.Localize(); Refresh(); }
        void Unbind()
        {
            Close(); _refresh?.Pause(); _refresh = null;
            if (_gameInput != null) { _gameInput.ToggleInventoryRequested -= Toggle; }
            if (_input != null) { _input.CancelRequested -= Cancel; }
            LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
            if (_root != null)
            {
                _root.Q<Button>("open").clicked -= Toggle; _root.Q<Button>("close").clicked -= Toggle;
            }
            _view?.Dispose(); _view = null; _root = null; _input = null; _gameInput = null;
        }
    }
}
