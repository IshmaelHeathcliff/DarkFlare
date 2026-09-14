using UnityEngine.UIElements;

namespace DarkFlare
{
    public partial class GameMenuController
    {
        StatusPanelController _statuses;
        Button _statusEntry;
        Button _statusClose;
        bool _statusOpen;
        public bool IsStatusOpen => IsOpen && _statusOpen;

        void BindStatuses(IArchitecture architecture)
        {
            VisualElement root = _uiPanel.Root;
            _statusEntry = root.Q<Button>("game-menu-statuses");
            _statusClose = root.Q<Button>("status-close");
            _statuses = new StatusPanelController(architecture, root, _localizationService, ApplicationHost.Current.CurrentSession.SceneScope);
            _statusEntry.clicked += OpenStatuses;
            _statusClose.clicked += CloseStatuses;
        }

        public void OpenStatuses()
        {
            if (_gameInput == null || (_applicationShell?.BlocksGameplay ?? false)) { return; }
            Workspace.Suspend(); _statusOpen = true;
            _gameInput.SwitchToUi(); ApplyPage(); _statuses.FocusDefault();
        }

        public void CloseStatuses()
        {
            _statusOpen = false; ApplyPage(); _statusEntry?.Focus();
        }

        void UnbindStatuses()
        {
            _statuses?.Dispose(); _statuses = null; _statusOpen = false;
            if (_statusEntry != null) { _statusEntry.clicked -= OpenStatuses; }
            if (_statusClose != null) { _statusClose.clicked -= CloseStatuses; }
        }
    }
}
