using System;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public partial class GameMenuController
    {
        SkillBoardPanelController _board;
        VisualElement _boardTemplate;
        Button _boardTab;
        Button _boardClose;
        Button _hudBoard;
        GameMenuAccess _beforeBoardWindows;
        GameMenuPage _beforeBoardPage;
        public SkillBoardPanelController Board => _board;

        void BindBoardTree(VisualElement root)
        {
            _boardTemplate = root.Q("board-window");
            _boardTab = root.Q<Button>("game-menu-board-tab");
            _boardClose = root.Q<Button>("board-window-close");
            _hudBoard = root.Q<Button>("game-hud-board");
            if (_boardTemplate == null || _boardTab == null || _boardClose == null || _hudBoard == null)
            { throw new InvalidOperationException("棋盘 UXML 缺少必要控件"); }
            _boardTab.clicked += OpenBoard; _hudBoard.clicked += OpenBoard; _boardClose.clicked += CloseBoard;
            _boardTemplate.RegisterCallback<PointerDownEvent>(OnBoardActivated, TrickleDown.TrickleDown);
        }

        void BindBoard(IArchitecture architecture)
        {
            _board = new SkillBoardPanelController(this, architecture, _boardTemplate, _localizationService,
                ApplicationHost.Current.CurrentSession.SceneScope);
        }

        void UnbindBoard()
        {
            _board?.Dispose(); _board = null;
            if (_boardTab != null) { _boardTab.clicked -= OpenBoard; }
            if (_hudBoard != null) { _hudBoard.clicked -= OpenBoard; }
            if (_boardClose != null) { _boardClose.clicked -= CloseBoard; }
            _boardTemplate?.UnregisterCallback<PointerDownEvent>(OnBoardActivated, TrickleDown.TrickleDown);
            _boardTemplate = null; _beforeBoardWindows = GameMenuAccess.None;
        }

        void OpenBoard() { OpenPage(GameMenuPage.SkillBoard); }
        void CloseBoard() { ClosePage(GameMenuPage.SkillBoard); }
        void OnBoardActivated(PointerDownEvent evt)
        {
            if (!Workspace.IsDragging) { Workspace.Activate(GameMenuPage.SkillBoard); }
        }

        void PrepareBoardWindow(GameMenuPage page)
        {
            if (page == GameMenuPage.SkillBoard && !OpenWindows.Contains(page))
            {
                Workspace.Suspend(); _beforeBoardWindows = OpenWindows; _beforeBoardPage = CurrentPage;
                OpenWindows = GameMenuAccess.Inventory;
            }
            else if (page != GameMenuPage.SkillBoard && page != GameMenuPage.Inventory && OpenWindows.Contains(GameMenuPage.SkillBoard))
            {
                RestoreBoardWindows();
            }
        }

        void RestoreBoardWindows()
        {
            Workspace.Suspend();
            OpenWindows = GameMenuAccess.None;
            foreach (GameMenuPage page in Enum.GetValues(typeof(GameMenuPage)))
            {
                if (page != GameMenuPage.SkillBoard && _beforeBoardWindows.Contains(page) && IsPageAvailable(page)) { OpenWindows |= page.ToAccess(); }
            }
            _beforeBoardWindows = GameMenuAccess.None;
            CurrentPage = OpenWindows.Contains(_beforeBoardPage) ? _beforeBoardPage
                : OpenWindows.Contains(GameMenuPage.Inventory) ? GameMenuPage.Inventory : GameMenuPage.Attributes;
        }

        bool CloseBoardWindow(GameMenuPage page)
        {
            if (page != GameMenuPage.SkillBoard || !OpenWindows.Contains(page)) { return false; }
            RestoreBoardWindows();
            if (OpenWindows == GameMenuAccess.None && !_pauseOpen) { _gameInput.SwitchToGameplay(); }
            else { ApplyPage(); FocusWindow(CurrentPage); }
            return true;
        }

        void ApplyBoardWindow(bool hudEntries)
        {
            bool visible = IsWindowVisible(GameMenuPage.SkillBoard);
            SetTemplateVisible(_boardTemplate, visible); _board?.SetVisible(visible);
            SetTemplateVisible(_hudBoard, hudEntries);
            _boardTab?.SetEnabled(!_pauseOpen);
            SetTabActive(_boardTab, visible);
            _panel.EnableInClassList("game-menu-panel--board", visible);
        }
    }
}
