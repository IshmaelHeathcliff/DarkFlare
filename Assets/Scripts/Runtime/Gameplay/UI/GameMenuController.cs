using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimePanelView))]
    [RequireComponent(typeof(InventoryPanelController))]
    [RequireComponent(typeof(ShopPanelController))]
    [RequireComponent(typeof(CraftingPanelController))]
    public class GameMenuController : MonoBehaviour, IController
    {
        const string ActiveTabClass = "game-menu-tab--active";

        [SerializeField]
        RuntimePanelView _uiPanel;

        [SerializeField]
        InventoryPanelController _inventoryPanel;

        [SerializeField]
        ShopPanelController _shopPanel;

        [SerializeField]
        CraftingPanelController _craftingPanel;

        AttributePanelController _attributes;
        VisualElement _attributesTemplate;
        Button _attributesTab;
        Button _attributesClose;
        Button _attributesEntry;
        Button _hudAttributes;
        Button _hudInventory;
        public AttributePanelController Attributes => _attributes;
        ItemWorkspace _workspace;
        VisualElement _pausePanel;
        VisualElement _windows;
        Button _pauseButton;
        Button _hudPauseButton;
        Button _resumeButton;
        Button _inventoryClose;
        Button _shopClose;
        Button _craftingClose;
        WorldInteractionTarget _interactionTarget;
        CombatActor _interactionPlayer;
        int _interactionGeneration;
        IUnRegister _focusRegistration;
        IUnRegister _actorDiedRegistration;
        IUnRegister _actorRemovedRegistration;
        bool _pauseOpen;
        readonly Dictionary<GameMenuPage, VisualElement> _windowFocus = new Dictionary<GameMenuPage, VisualElement>();
        public GameMenuAccess OpenWindows { get; private set; }
        public bool IsPauseOpen => _pauseOpen;
        public ItemWorkspace Workspace => _workspace ??= new ItemWorkspace(
            _uiPanel.Root, IsWindowVisible, ActivateWindow,
            () => this.SendQuery(new GetInventorySnapshotQuery()));

        SceneSessionBinding _sessionBinding;
        GameInput _gameInput;
        ApplicationShellController _applicationShell;
        IUnRegister _openRequestRegistration;
        VisualElement _overlay;
        VisualElement _panel;
        VisualElement _inventoryTemplate;
        VisualElement _shopTemplate;
        VisualElement _craftingTemplate;
        Button _inventoryTab;
        Button _shopTab;
        Button _craftingTab;
        Button _closeButton;
        Button _saveButton;
        Button _returnFrontEndButton;
        Button _settingsButton;
        Label _saveStatus;
        SessionSaveFacade _saveFacade;
        LocalizationService _localizationService;
        bool _saveOperationBusy;
        LocalizedMessage _saveStatusMessage;

        public GameMenuAccess AvailablePages { get; private set; } = GameMenuAccess.Inventory;

        public GameMenuPage CurrentPage { get; private set; } = GameMenuPage.Inventory;

        public bool IsOpen => _gameInput != null && _gameInput.CurrentMode == GameInputMode.UI
            && !(_applicationShell?.BlocksGameplay ?? false);

        public int SessionBindCount => _sessionBinding?.BindCount ?? 0;
        public bool IsReady => _sessionBinding?.IsBound ?? false;

        public bool IsSaveOperationBusy => _saveOperationBusy;

        public string SaveStatusText => _saveStatus?.text ?? string.Empty;

        public IArchitecture GetArchitecture()
        {
            return _sessionBinding.RequireArchitecture();
        }

        public void OpenPage(GameMenuPage page)
        {
            if (!IsPageAvailable(page) || _gameInput == null) { return; }
            OpenWindows |= page.ToAccess();
            if (page == GameMenuPage.Shop || page == GameMenuPage.Crafting)
            {
                OpenWindows |= GameMenuAccess.Inventory;
            }
            CurrentPage = page;
            _pauseOpen = false;
            if (_gameInput.CurrentMode != GameInputMode.UI) { _gameInput.SwitchToUi(); }
            else { ApplyPage(); }
            FocusWindow(page);
        }

        public bool IsWindowVisible(GameMenuPage page)
        {
            return IsOpen && !_pauseOpen && OpenWindows.Contains(page) && IsPageAvailable(page);
        }

        public void ClosePage(GameMenuPage page)
        {
            if (!OpenWindows.Contains(page)) { return; }
            Workspace.Suspend();
            OpenWindows &= ~page.ToAccess();
            if (page == GameMenuPage.Crafting) { _craftingPanel?.CloseWindow(); }
            if (OpenWindows == GameMenuAccess.None && !_pauseOpen) { _gameInput?.SwitchToGameplay(); return; }
            if (CurrentPage == page)
            {
                CurrentPage = OpenWindows.Contains(GameMenuPage.Inventory) ? GameMenuPage.Inventory
                    : OpenWindows.Contains(GameMenuPage.Shop) ? GameMenuPage.Shop
                    : OpenWindows.Contains(GameMenuPage.Crafting) ? GameMenuPage.Crafting : GameMenuPage.Attributes;
            }
            ApplyPage();
            FocusWindow(CurrentPage);
        }

        public void TogglePause()
        {
            if (_gameInput == null || (_applicationShell?.BlocksGameplay ?? false)) { return; }
            _pauseOpen = !_pauseOpen;
            Workspace.Suspend();
            if (_pauseOpen) { _gameInput.SwitchToUi(); }
            else if (OpenWindows == GameMenuAccess.None) { _gameInput.SwitchToGameplay(); }
            ApplyPage();
            if (_pauseOpen) { _resumeButton?.Focus(); }
            else { FocusWindow(CurrentPage); }
        }

        void ActivateWindow(GameMenuPage page)
        {
            if (!IsWindowVisible(page)) { return; }
            CurrentPage = page;
            _inventoryTemplate?.EnableInClassList("item-window--active", page == GameMenuPage.Inventory);
            _shopTemplate?.EnableInClassList("item-window--active", page == GameMenuPage.Shop);
            _craftingTemplate?.EnableInClassList("item-window--active", page == GameMenuPage.Crafting);
            _attributesTemplate?.EnableInClassList("item-window--active", page == GameMenuPage.Attributes);
        }

        void FocusWindow(GameMenuPage page)
        {
            if (!IsWindowVisible(page)) { return; }
            Workspace.Activate(page);
            if (_windowFocus.TryGetValue(page, out VisualElement previous) && IsNavigable(previous))
            {
                previous.Focus();
                return;
            }
            bool focused = page == GameMenuPage.Inventory ? _inventoryPanel.FocusDefault()
                : page == GameMenuPage.Shop ? _shopPanel.FocusDefault()
                : page == GameMenuPage.Crafting ? _craftingPanel.FocusDefault() : _attributes.FocusDefault();
            if (!focused)
            {
                (page == GameMenuPage.Inventory ? _inventoryClose : page == GameMenuPage.Shop ? _shopClose : page == GameMenuPage.Crafting ? _craftingClose : _attributesClose)?.Focus();
            }
        }

        void OnToggleInventory()
        {
            if (_pauseOpen) { return; }
            if (OpenWindows.Contains(GameMenuPage.Inventory)) { ClosePage(GameMenuPage.Inventory); }
            else { OpenPage(GameMenuPage.Inventory); }
        }

        bool OnCancelRequested()
        {
            if (!IsOpen) { return false; }
            if (_pauseOpen) { TogglePause(); return true; }
            if (Workspace.Interactions?.CloseMenu(true) == true) { return true; }
            if (Workspace.CancelDrag()) { return true; }
            VisualElement focused = _panel.panel?.focusController.focusedElement as VisualElement;
            Foldout source = focused?.GetFirstAncestorOfType<Foldout>();
            if (source != null && source.ClassListContains("item-source-section") && source.value)
            {
                source.value = false;
                source.Q<Toggle>()?.Focus();
                return true;
            }
            ClosePage(CurrentPage);
            return true;
        }

        void OnCycleWindow(int step)
        {
            if (!IsOpen || _pauseOpen || Workspace.IsDragging || Workspace.Interactions?.IsMenuOpen == true) { return; }
            for (int i = 1; i <= 4; i++)
            {
                GameMenuPage page = (GameMenuPage)(((int)CurrentPage + step * i + 8) % 4);
                if (IsWindowVisible(page)) { FocusWindow(page); return; }
            }
        }

        public bool IsPageAvailable(GameMenuPage page)
        {
            return page == GameMenuPage.Inventory || page == GameMenuPage.Attributes || AvailablePages.Contains(page)
                && _interactionTarget != null && _interactionTarget.CanInteract
                && _interactionPlayer != null && _interactionPlayer.isActiveAndEnabled && _interactionPlayer.IsAlive
                && ApplicationHost.TryGetCurrent(out ApplicationHost host)
                && host.CurrentSession?.ArchitectureGeneration == _interactionGeneration
                && _interactionTarget.MenuPage == page;
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            EnsureComponents();
            _sessionBinding ??= new SceneSessionBinding(
                this,
                BindSession,
                UnbindSession);
            _uiPanel.Reloading += OnPanelReloading;
            _uiPanel.Reloaded += OnPanelReloaded;
            _sessionBinding.Enable();
        }

        void OnPanelReloading()
        {
            if (_gameInput != null) { _gameInput.SwitchToGameplay(); }
            _sessionBinding?.Disable();
        }

        void OnPanelReloaded()
        {
            _sessionBinding?.Enable();
        }

        void OnDisable()
        {
            _uiPanel.Reloading -= OnPanelReloading;
            _uiPanel.Reloaded -= OnPanelReloaded;
            if (_sessionBinding != null
                && _sessionBinding.IsBound
                && _gameInput != null
                && _gameInput.IsUiEnabled)
            {
                _gameInput.SwitchToGameplay();
            }

            _sessionBinding?.Disable();
        }

        SceneSessionBindResult BindSession(IArchitecture architecture)
        {
            if (_uiPanel == null || _uiPanel.Renderer.panelSettings == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[GameMenuController] 缺少 RuntimePanelView 或 PanelSettings，无法初始化菜单", this);
                return SceneSessionBindResult.Failed;
            }

            VisualElement root = _uiPanel.Root;

            if (root == null || root.panel == null)
            {
                return SceneSessionBindResult.Retry;
            }

            if (!BindVisualTree())
            {
                return SceneSessionBindResult.Failed;
            }

            _gameInput = architecture.GetUtility<GameInput>();

            if (_gameInput == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[GameMenuController] 缺少 GameInput，无法控制菜单", this);
                return SceneSessionBindResult.Failed;
            }

            _saveFacade = ResolveSaveFacade();
            Workspace.Interactions = new ItemInteractionSession(this, _gameInput);
            _localizationService = ResolveLocalizationService();
            _attributes = new AttributePanelController(architecture, _attributesTemplate, _localizationService,
                ApplicationHost.Current.CurrentSession.SceneScope);
            _saveOperationBusy = false;
            SetSaveStatus(_saveFacade != null
                ? "save.status.ready"
                : "save.status.service_unavailable");
            RefreshSaveControls();

            _openRequestRegistration = this.RegisterEvent<GameMenuOpenRequestedEvent>(OnMenuOpenRequested);
            _applicationShell = ApplicationHost.Current.ApplicationShell;

            if (_applicationShell != null)
            {
                _applicationShell.BlockingChanged += OnShellBlockingChanged;
            }

            _gameInput.ModeChanged += OnInputModeChanged;
            _gameInput.ToggleInventoryRequested += OnToggleInventory;
            _gameInput.PauseRequested += TogglePause;
            _gameInput.CycleWindowRequested += OnCycleWindow;
            _gameInput.CancelRequested += OnCancelRequested;
            _focusRegistration = this.RegisterEvent<InteractionFocusChangedEvent>(e =>
            {
                if (_interactionTarget != null && e.Target != _interactionTarget) { InvalidateTarget(); }
            });
            _actorDiedRegistration = this.RegisterEvent<ActorDiedEvent>(e =>
            {
                if (e.Actor == _interactionPlayer) { InvalidateTarget(); }
            });
            _actorRemovedRegistration = this.RegisterEvent<ActorUnregisteredEvent>(e =>
            {
                if (e.Actor == _interactionPlayer) { InvalidateTarget(); }
            });
            ApplyInputMode(_gameInput.CurrentMode);
            ApplicationLog.Info(LogEventIds.GameplayUi, "[GameMenuController] 游戏菜单初始化完成", this);
            return SceneSessionBindResult.Success;
        }

        void UnbindSession()
        {
            _attributes?.Dispose();
            _attributes = null;
            if (_attributesTab != null) { _attributesTab.clicked -= OnAttributesOpen; }
            if (_attributesEntry != null) { _attributesEntry.clicked -= OnAttributesOpen; }
            if (_hudAttributes != null) { _hudAttributes.clicked -= OnAttributesOpen; }
            if (_hudInventory != null) { _hudInventory.clicked -= OnInventoryTabClicked; }
            if (_attributesClose != null) { _attributesClose.clicked -= OnAttributesClose; }
            _attributesTemplate?.UnregisterCallback<PointerDownEvent>(OnAttributesActivated, TrickleDown.TrickleDown);
            if (_applicationShell != null)
            {
                _applicationShell.BlockingChanged -= OnShellBlockingChanged;
                _applicationShell = null;
            }

            if (_gameInput != null)
            {
                _gameInput.ModeChanged -= OnInputModeChanged;
                _gameInput.ToggleInventoryRequested -= OnToggleInventory;
                _gameInput.PauseRequested -= TogglePause;
                _gameInput.CycleWindowRequested -= OnCycleWindow;
                _gameInput.CancelRequested -= OnCancelRequested;
            }

            _focusRegistration?.UnRegister();
            _focusRegistration = null;
            _actorDiedRegistration?.UnRegister();
            _actorRemovedRegistration?.UnRegister();
            _actorDiedRegistration = null;
            _actorRemovedRegistration = null;
            ReleaseTarget();
            _workspace?.Dispose();
            _workspace = null;
            OpenWindows = GameMenuAccess.None;
            _windowFocus.Clear();
            _pauseOpen = false;
            _openRequestRegistration?.UnRegister();
            _openRequestRegistration = null;

            if (_inventoryTab != null)
            {
                _inventoryTab.clicked -= OnInventoryTabClicked;
            }

            if (_shopTab != null)
            {
                _shopTab.clicked -= OnShopTabClicked;
            }

            if (_craftingTab != null)
            {
                _craftingTab.clicked -= OnCraftingTabClicked;
            }

            if (_closeButton != null)
            {
                _closeButton.clicked -= OnCloseClicked;
            }

            if (_saveButton != null)
            {
                _saveButton.clicked -= OnSaveClicked;
            }

            if (_returnFrontEndButton != null)
            {
                _returnFrontEndButton.clicked -= OnReturnFrontEndClicked;
            }

            if (_settingsButton != null)
            {
                _settingsButton.clicked -= OnSettingsClicked;
            }

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged -= OnLocaleChanged;
            }

            if (_pauseButton != null) { _pauseButton.clicked -= TogglePause; }
            if (_hudPauseButton != null) { _hudPauseButton.clicked -= TogglePause; }
            if (_resumeButton != null) { _resumeButton.clicked -= TogglePause; }
            if (_inventoryClose != null) { _inventoryClose.clicked -= OnInventoryClose; }
            if (_shopClose != null) { _shopClose.clicked -= OnShopClose; }
            if (_craftingClose != null) { _craftingClose.clicked -= OnCraftingClose; }
            _overlay?.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            _panel?.UnregisterCallback<FocusInEvent>(OnWindowFocus, TrickleDown.TrickleDown);
            _inventoryTemplate?.UnregisterCallback<PointerDownEvent>(OnInventoryActivated, TrickleDown.TrickleDown);
            _shopTemplate?.UnregisterCallback<PointerDownEvent>(OnShopActivated, TrickleDown.TrickleDown);
            _craftingTemplate?.UnregisterCallback<PointerDownEvent>(OnCraftingActivated, TrickleDown.TrickleDown);
            _gameInput = null;
            _overlay = null;
            _panel = null;
            _inventoryTemplate = null;
            _shopTemplate = null;
            _craftingTemplate = null;
            _inventoryTab = null;
            _shopTab = null;
            _craftingTab = null;
            _closeButton = null;
            _saveButton = null;
            _returnFrontEndButton = null;
            _settingsButton = null;
            _saveStatus = null;
            _saveFacade = null;
            _localizationService = null;
            _saveOperationBusy = false;
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_uiPanel == null)
            {
                _uiPanel = GetComponent<RuntimePanelView>();
            }

            if (_uiPanel == null && gameObject.scene.IsValid())
            {
                _uiPanel = gameObject.AddComponent<RuntimePanelView>();
            }

            if (_inventoryPanel == null)
            {
                _inventoryPanel = GetComponent<InventoryPanelController>();
            }

            if (_shopPanel == null)
            {
                _shopPanel = GetComponent<ShopPanelController>();
            }

            if (_craftingPanel == null)
            {
                _craftingPanel = GetComponent<CraftingPanelController>();
            }
        }

        bool BindVisualTree()
        {
            if (_uiPanel == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[GameMenuController] 缺少 RuntimePanelView，无法初始化菜单", this);
                return false;
            }

            VisualElement root = _uiPanel.Root;
            _overlay = root.Q<VisualElement>("game-menu-overlay");
            _panel = root.Q<VisualElement>("game-menu-panel");
            _inventoryTemplate = root.Q("inventory-window");
            _shopTemplate = root.Q("shop-window");
            _craftingTemplate = root.Q("crafting-window");
            _attributesTemplate = root.Q("attributes-window");
            _attributesTab = root.Q<Button>("game-menu-attributes-tab");
            _attributesClose = root.Q<Button>("attributes-window-close");
            _attributesEntry = root.Q<Button>("inventory-attributes-open");
            _hudAttributes = root.Q<Button>("game-hud-attributes");
            _hudInventory = root.Q<Button>("game-hud-inventory");
            _inventoryTab = root.Q<Button>("game-menu-inventory-tab");
            _shopTab = root.Q<Button>("game-menu-shop-tab");
            _craftingTab = root.Q<Button>("game-menu-crafting-tab");
            _closeButton = root.Q<Button>("game-menu-close");
            _saveButton = root.Q<Button>("game-menu-save");
            _returnFrontEndButton = root.Q<Button>("game-menu-return-front-end");
            _settingsButton = root.Q<Button>("game-menu-settings");
            _saveStatus = root.Q<Label>("game-menu-save-status");
            _pausePanel = root.Q("game-pause-panel");
            _windows = root.Q("game-menu-content");
            _pauseButton = root.Q<Button>("game-menu-pause");
            _hudPauseButton = root.Q<Button>("game-hud-pause");
            _resumeButton = root.Q<Button>("game-menu-resume");
            _inventoryClose = root.Q<Button>("inventory-window-close");
            _shopClose = root.Q<Button>("shop-window-close");
            _craftingClose = root.Q<Button>("crafting-window-close");

            if (_overlay == null
                || _panel == null || _attributesTemplate == null || _attributesTab == null
                || _attributesClose == null || _attributesEntry == null || _hudAttributes == null || _hudInventory == null
                || root.Q<ScrollView>("attributes-scroll") == null
                || _inventoryTemplate == null
                || _shopTemplate == null
                || _craftingTemplate == null
                || _inventoryTab == null
                || _shopTab == null
                || _craftingTab == null
                || _closeButton == null
                || _saveButton == null
                || _returnFrontEndButton == null
                || _settingsButton == null
                || _saveStatus == null || _pausePanel == null || _windows == null
                || _pauseButton == null || _hudPauseButton == null || _resumeButton == null
                || _inventoryClose == null || _shopClose == null || _craftingClose == null
                || root.Q("item-operation-bar") == null || root.Q<Label>("item-operation-status") == null
                || root.Q<Button>("item-operation-actions") == null || root.Q<Button>("item-discard-zone") == null
                || root.Q<Button>("shop-buy-zone") == null || root.Q<Button>("shop-sell-zone") == null
                || root.Q("item-action-menu") == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[GameMenuController] 菜单 UXML 缺少必要的命名元素", this);
                return false;
            }

            _attributesTab.clicked += OnAttributesOpen;
            _attributesEntry.clicked += OnAttributesOpen;
            _hudAttributes.clicked += OnAttributesOpen;
            _hudInventory.clicked += OnInventoryTabClicked;
            _attributesClose.clicked += OnAttributesClose;
            _attributesTemplate.RegisterCallback<PointerDownEvent>(OnAttributesActivated, TrickleDown.TrickleDown);
            _pauseButton.clicked += TogglePause;
            _hudPauseButton.clicked += TogglePause;
            _resumeButton.clicked += TogglePause;
            _inventoryClose.clicked += OnInventoryClose;
            _shopClose.clicked += OnShopClose;
            _craftingClose.clicked += OnCraftingClose;
            _overlay.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            _panel.RegisterCallback<FocusInEvent>(OnWindowFocus, TrickleDown.TrickleDown);
            _inventoryTemplate.RegisterCallback<PointerDownEvent>(OnInventoryActivated, TrickleDown.TrickleDown);
            _shopTemplate.RegisterCallback<PointerDownEvent>(OnShopActivated, TrickleDown.TrickleDown);
            _craftingTemplate.RegisterCallback<PointerDownEvent>(OnCraftingActivated, TrickleDown.TrickleDown);
            _inventoryTab.clicked += OnInventoryTabClicked;
            _shopTab.clicked += OnShopTabClicked;
            _craftingTab.clicked += OnCraftingTabClicked;
            _closeButton.clicked += OnCloseClicked;
            _saveButton.clicked += OnSaveClicked;
            _returnFrontEndButton.clicked += OnReturnFrontEndClicked;
            _settingsButton.clicked += OnSettingsClicked;

            return true;
        }

        void OnInputModeChanged(GameInputMode mode)
        {
            ApplyInputMode(mode);
        }

        void OnMenuOpenRequested(GameMenuOpenRequestedEvent e)
        {
            if (e.Target == null || !e.Target.CanInteract) { return; }
            if (_interactionTarget != null && _interactionTarget != e.Target)
            {
                ClosePage(_interactionTarget.MenuPage);
            }
            ReleaseTarget();
            _interactionTarget = e.Target;
            _interactionPlayer = this.SendQuery(new GetInventorySnapshotQuery()).Player;
            _interactionGeneration = ApplicationHost.Current.CurrentSession.ArchitectureGeneration;
            _interactionTarget.Unavailable += OnTargetUnavailable;
            AvailablePages = e.AvailablePages | GameMenuAccess.Inventory;
            OpenWindows &= AvailablePages | GameMenuAccess.Attributes;
            OpenPage(e.Page);
        }

        void OnTargetUnavailable(WorldInteractionTarget target) { InvalidateTarget(); }

        void InvalidateTarget()
        {
            if (_interactionTarget == null) { return; }
            GameMenuPage page = _interactionTarget.MenuPage;
            ReleaseTarget();
            AvailablePages = GameMenuAccess.Inventory;
            ClosePage(page);
        }

        void ReleaseTarget()
        {
            if (_interactionTarget != null) { _interactionTarget.Unavailable -= OnTargetUnavailable; }
            _interactionTarget = null;
            _interactionPlayer = null;
            _interactionGeneration = 0;
        }

        void ApplyInputMode(GameInputMode mode)
        {
            bool isOpen = mode == GameInputMode.UI;
            this.SendCommand(new SetGameplayPausedCommand(isOpen));
            if (_overlay == null) { return; }
            if (!isOpen)
            {
                Workspace.Suspend();
                _craftingPanel?.CloseWindow();
                OpenWindows = GameMenuAccess.None;
                _pauseOpen = false;
                AvailablePages = GameMenuAccess.Inventory;
                ReleaseTarget();
            }
            else if (OpenWindows == GameMenuAccess.None && !_pauseOpen)
            {
                OpenWindows = GameMenuAccess.Inventory;
                CurrentPage = GameMenuPage.Inventory;
            }
            ApplyPage();
        }

        void OnShellBlockingChanged(bool blocked)
        {
            if (blocked) { Workspace.Suspend(); }
            ApplyPage();
        }

        void ApplyPage()
        {
            if (_overlay == null) { return; }
            _overlay.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            bool hudEntries = !IsOpen && !(_applicationShell?.BlocksGameplay ?? false);
            SetTemplateVisible(_hudAttributes, hudEntries);
            SetTemplateVisible(_hudInventory, hudEntries);
            SetTemplateVisible(_hudPauseButton, hudEntries);
            _windows.style.display = IsOpen && !_pauseOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _pausePanel.style.display = IsOpen && _pauseOpen ? DisplayStyle.Flex : DisplayStyle.None;
            bool inventory = IsWindowVisible(GameMenuPage.Inventory);
            bool shop = IsWindowVisible(GameMenuPage.Shop);
            bool crafting = IsWindowVisible(GameMenuPage.Crafting);
            bool attributes = IsWindowVisible(GameMenuPage.Attributes);
            SetTemplateVisible(_attributesTemplate, attributes);
            _attributes?.SetVisible(attributes);
            _panel.EnableInClassList("game-menu-panel--attributes", attributes);
            _panel.EnableInClassList("game-menu-panel--with-inventory", inventory);
            _attributesTab.SetEnabled(!_pauseOpen);
            SetTabActive(_attributesTab, attributes);
            SetTemplateVisible(_inventoryTemplate, inventory);
            SetTemplateVisible(_shopTemplate, shop);
            SetTemplateVisible(_craftingTemplate, crafting);
            if (_inventoryPanel.IsVisible != inventory) { _inventoryPanel.SetVisible(inventory); }
            if (_shopPanel.IsVisible != shop) { _shopPanel.SetVisible(shop); }
            if (_craftingPanel.IsVisible != crafting) { _craftingPanel.SetVisible(crafting); }
            _inventoryTab.SetEnabled(!_pauseOpen);
            SetTemplateVisible(_shopTab, !_pauseOpen && IsPageAvailable(GameMenuPage.Shop));
            SetTemplateVisible(_craftingTab, !_pauseOpen && IsPageAvailable(GameMenuPage.Crafting));
            SetTabActive(_inventoryTab, inventory);
            SetTabActive(_shopTab, shop);
            SetTabActive(_craftingTab, crafting);
            RefreshSaveControls();
            if (!IsOpen || _pauseOpen) { Workspace.Suspend(); }
            Workspace.Interactions?.Refresh();
        }

        void OnAttributesOpen() { OpenPage(GameMenuPage.Attributes); }
        void OnAttributesClose() { ClosePage(GameMenuPage.Attributes); }
        void OnAttributesActivated(PointerDownEvent evt) { if (!Workspace.IsDragging) { Workspace.Activate(GameMenuPage.Attributes); } }
        void OnInventoryClose() { ClosePage(GameMenuPage.Inventory); }
        void OnShopClose() { ClosePage(GameMenuPage.Shop); }
        void OnCraftingClose() { ClosePage(GameMenuPage.Crafting); }
        void OnInventoryActivated(PointerDownEvent evt) { Workspace.Activate(GameMenuPage.Inventory); }
        void OnShopActivated(PointerDownEvent evt) { Workspace.Activate(GameMenuPage.Shop); }
        void OnCraftingActivated(PointerDownEvent evt) { Workspace.Activate(GameMenuPage.Crafting); }

        void OnNavigationMove(NavigationMoveEvent evt)
        {
            if (!IsOpen || _pauseOpen) { return; }
            Vector2 direction = evt.direction switch
            {
                NavigationMoveEvent.Direction.Left => Vector2.left,
                NavigationMoveEvent.Direction.Right => Vector2.right,
                NavigationMoveEvent.Direction.Up => Vector2.up,
                NavigationMoveEvent.Direction.Down => Vector2.down,
                _ => Vector2.zero,
            };
            if (direction == Vector2.zero) { return; }
            evt.StopImmediatePropagation();
            if (Workspace.Interactions?.NavigateMenu(direction) == true) { return; }
            Workspace.AllowPreview();
            if (Workspace.IsDragging) { Workspace.NavigateDrag(direction); return; }
            VisualElement current = _panel.panel?.focusController.focusedElement as VisualElement;
            if (current == null) { FocusWindow(CurrentPage); return; }
            var candidates = new List<VisualElement>();
            var rectangles = new List<Rect>();
            foreach (VisualElement button in _panel.Query<VisualElement>().ToList())
            {
                if (button is not Button && button is not Toggle) { continue; }
                if (button == current || !button.focusable || !IsNavigable(button)) { continue; }
                if (GetWindow(button) != GetWindow(current) && !IsInScrollViewport(button)) { continue; }
                candidates.Add(button);
                rectangles.Add(button.worldBound);
            }
            int index = SpatialNavigation.FindNeighbor(current.worldBound, rectangles, direction);
            if (index >= 0)
            {
                VisualElement target = candidates[index];
                target.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(target);
                target.Focus();
            }
        }

        void OnWindowFocus(FocusInEvent evt)
        {
            if (evt.target is not VisualElement element || Workspace.IsDragging) { return; }
            for (VisualElement ancestor = element; ancestor != null; ancestor = ancestor.parent)
            {
                GameMenuPage? page = ancestor == _inventoryTemplate ? GameMenuPage.Inventory
                    : ancestor == _shopTemplate ? GameMenuPage.Shop
                    : ancestor == _craftingTemplate ? GameMenuPage.Crafting
                    : ancestor == _attributesTemplate ? GameMenuPage.Attributes : null;
                if (!page.HasValue) { continue; }
                _windowFocus[page.Value] = element;
                Workspace.Activate(page.Value);
                return;
            }
        }

        public static bool IsNavigable(VisualElement element)
        {
            if (element == null || element.panel == null || !element.enabledInHierarchy || element.worldBound.width <= 1f) { return false; }
            for (VisualElement ancestor = element; ancestor != null; ancestor = ancestor.parent)
            {
                if (ancestor.resolvedStyle.display == DisplayStyle.None || ancestor.resolvedStyle.visibility == Visibility.Hidden) { return false; }
            }
            return true;
        }

        static VisualElement GetWindow(VisualElement element)
        {
            for (VisualElement ancestor = element; ancestor != null; ancestor = ancestor.parent)
            {
                if (ancestor.ClassListContains("item-window")) { return ancestor; }
            }
            return null;
        }

        public static bool IsInScrollViewport(VisualElement element)
        {
            Rect bounds = element.worldBound;
            for (VisualElement ancestor = element.parent; ancestor != null; ancestor = ancestor.parent)
            {
                if (ancestor is not ScrollView scroll) { continue; }
                Rect viewport = scroll.contentViewport.worldBound;
                bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, viewport.xMin), Mathf.Max(bounds.yMin, viewport.yMin),
                    Mathf.Min(bounds.xMax, viewport.xMax), Mathf.Min(bounds.yMax, viewport.yMax));
                if (bounds.width <= 0f || bounds.height <= 0f) { return false; }
            }
            return true;
        }

        void OnInventoryTabClicked()
        {
            OpenPage(GameMenuPage.Inventory);
        }

        void OnShopTabClicked()
        {
            OpenPage(GameMenuPage.Shop);
        }

        void OnCraftingTabClicked()
        {
            OpenPage(GameMenuPage.Crafting);
        }

        void OnCloseClicked()
        {
            _gameInput?.SwitchToGameplay();
        }

        void OnSaveClicked()
        {
            RunSaveOperation(
                "save-auto",
                facade => facade.SaveAutoAsync(),
                _saveButton);
        }

        void OnReturnFrontEndClicked()
        {
            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                return;
            }

            host.ApplicationShell?.RequestReturnToFrontEnd();
        }

        void OnSettingsClicked()
        {
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                host.ApplicationShell?.OpenSettings(() => _settingsButton?.Focus());
            }
        }

        void OnLocaleChanged(string localeCode)
        {
            RefreshSaveStatusText();
        }

        SessionSaveFacade ResolveSaveFacade()
        {
            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                return null;
            }

            SessionSaveFacade facade = host.SessionSaveFacade;
            return facade != null
                && facade.ArchitectureGeneration == host.CurrentSession?.ArchitectureGeneration
                ? facade
                : null;
        }

        LocalizationService ResolveLocalizationService()
        {
            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                return null;
            }

            LocalizationService service = host.Localization;

            if (service != null)
            {
                service.LocaleChanged += OnLocaleChanged;
            }

            return service;
        }

        void RunSaveOperation(
            string operationName,
            Func<SessionSaveFacade, UniTask<SaveOperationResult>> operation,
            Button focusTarget)
        {
            SessionSaveFacade facade = _saveFacade;

            if (_saveOperationBusy || facade == null || operation == null)
            {
                return;
            }

            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host)
                || host.ApplicationScope == null
                || !host.ApplicationScope.CanAcceptWork)
            {
                SetSaveStatus("save.status.service_unavailable");
                return;
            }

            SetSaveOperationBusy(true, "save.status.processing");

            try
            {
                host.ApplicationScope.Tasks.Run(
                    $"game-menu-{operationName}",
                    async _ =>
                    {
                        SaveOperationResult result = await operation(facade);

                        if (!ReferenceEquals(_saveFacade, facade))
                        {
                            return;
                        }

                        SetSaveOperationBusy(false, DescribeSaveResult(result));

                        if (IsOpen && _pauseOpen && focusTarget != null && IsNavigable(focusTarget))
                        {
                            focusTarget.Focus();
                        }
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                ApplicationLog.Exception(LogEventIds.GameplayUi, exception, this);
                SetSaveOperationBusy(false, "save.status.submit_failed");
            }
        }

        void SetSaveOperationBusy(bool busy, string statusKey)
        {
            _saveOperationBusy = busy;
            SetSaveStatus(statusKey);
            RefreshSaveControls();
        }

        void SetSaveOperationBusy(bool busy, LocalizedMessage status)
        {
            _saveOperationBusy = busy;
            SetSaveStatus(status);
            RefreshSaveControls();
        }

        void RefreshSaveControls()
        {
            bool available = _saveFacade != null && !_saveOperationBusy;
            _saveButton?.SetEnabled(available);
            bool canReturn = ApplicationHost.TryGetCurrent(out ApplicationHost host)
                && host.ApplicationShell != null
                && host.SceneFlow != null
                && (host.SceneFlow.State == GameFlowState.InGame
                    || host.SceneFlow.State == GameFlowState.Paused);
            _returnFrontEndButton?.SetEnabled(!_saveOperationBusy && canReturn);
        }

        void SetSaveStatus(string statusKey, params object[] arguments)
        {
            SetSaveStatus(LocalizedMessage.Ui(statusKey, arguments));
        }

        void SetSaveStatus(LocalizedMessage status)
        {
            _saveStatusMessage = status;
            RefreshSaveStatusText();
        }

        void RefreshSaveStatusText()
        {
            if (_saveStatus == null || _saveStatusMessage.IsEmpty)
            {
                return;
            }

            _saveStatus.text = _localizationService?.GetString(_saveStatusMessage)
                ?? $"[{_saveStatusMessage.TableName}.{_saveStatusMessage.EntryKey}]";
        }

        static LocalizedMessage DescribeSaveResult(SaveOperationResult result)
        {
            if (result == null)
            {
                return LocalizedMessage.Ui("save.status.no_result");
            }

            if (result.Succeeded)
            {
                return result.Operation switch
                {
                    SaveOperation.Save => LocalizedMessage.Ui("save.status.save_succeeded"),
                    SaveOperation.Continue => result.RecoverySource == SaveRecoverySource.Backup
                        ? LocalizedMessage.Ui("save.status.continue_backup")
                        : LocalizedMessage.Ui("save.status.continue_succeeded"),
                    SaveOperation.NewGame => LocalizedMessage.Ui("save.status.new_game_succeeded"),
                    _ => LocalizedMessage.Ui("save.status.ready"),
                };
            }

            return result.ErrorCode switch
            {
                SaveErrorCode.SlotNotFound => LocalizedMessage.Ui("save.error.slot_not_found"),
                SaveErrorCode.NoValidGeneration => LocalizedMessage.Ui("save.error.no_valid_generation"),
                SaveErrorCode.OperationInProgress => LocalizedMessage.Ui("save.error.operation_in_progress"),
                SaveErrorCode.SessionUnavailable => LocalizedMessage.Ui("save.error.session_unavailable"),
                SaveErrorCode.SnapshotUnavailable => LocalizedMessage.Ui("save.error.snapshot_unavailable"),
                SaveErrorCode.Cancelled => LocalizedMessage.Ui("save.error.cancelled"),
                SaveErrorCode.ContentVersionMismatch => LocalizedMessage.Ui("save.error.content_version_mismatch"),
                SaveErrorCode.ContentMissing => LocalizedMessage.Ui("save.error.content_missing"),
                SaveErrorCode.FutureSchemaUnsupported => LocalizedMessage.Ui("save.error.future_schema_unsupported"),
                SaveErrorCode.ChecksumMismatch => LocalizedMessage.Ui("save.error.checksum_mismatch"),
                SaveErrorCode.PermissionDenied => LocalizedMessage.Ui("save.error.permission_denied"),
                SaveErrorCode.StorageFull => LocalizedMessage.Ui("save.error.storage_full"),
                SaveErrorCode.FlushTimedOut => LocalizedMessage.Ui("save.error.flush_timed_out"),
                _ => LocalizedMessage.Ui("save.error.unknown", result.ErrorCode),
            };
        }

        static void SetTabActive(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            if (active)
            {
                button.AddToClassList(ActiveTabClass);
                return;
            }

            button.RemoveFromClassList(ActiveTabClass);
        }

        static void SetTemplateVisible(VisualElement template, bool visible)
        {
            if (template != null)
            {
                template.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
