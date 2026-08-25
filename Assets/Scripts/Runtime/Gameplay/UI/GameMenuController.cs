using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(InventoryPanelController))]
    [RequireComponent(typeof(ShopPanelController))]
    [RequireComponent(typeof(CraftingPanelController))]
    public class GameMenuController : MonoBehaviour, IController
    {
        const string ActiveTabClass = "game-menu-tab--active";

        [SerializeField]
        UIDocument _document;

        [SerializeField]
        InventoryPanelController _inventoryPanel;

        [SerializeField]
        ShopPanelController _shopPanel;

        [SerializeField]
        CraftingPanelController _craftingPanel;

        SceneSessionBinding _sessionBinding;
        GameInput _gameInput;
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

        public bool IsOpen => _gameInput != null && _gameInput.CurrentMode == GameInputMode.UI;

        public int SessionBindCount => _sessionBinding?.BindCount ?? 0;

        public bool IsSaveOperationBusy => _saveOperationBusy;

        public string SaveStatusText => _saveStatus?.text ?? string.Empty;

        public IArchitecture GetArchitecture()
        {
            return _sessionBinding.RequireArchitecture();
        }

        public void OpenPage(GameMenuPage page)
        {
            if (!IsPageAvailable(page))
            {
                return;
            }

            CurrentPage = page;

            if (_gameInput == null)
            {
                return;
            }

            if (_gameInput.CurrentMode != GameInputMode.UI)
            {
                _gameInput.SwitchToUi();
                return;
            }

            ApplyPage();
        }

        public bool IsPageAvailable(GameMenuPage page)
        {
            return AvailablePages.Contains(page);
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
            _sessionBinding.Enable();
        }

        void OnDisable()
        {
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
            if (_document == null || _document.panelSettings == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[GameMenuController] 缺少 UIDocument 或 PanelSettings，无法初始化菜单", this);
                return SceneSessionBindResult.Failed;
            }

            VisualElement root = _document.rootVisualElement;

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
            _localizationService = ResolveLocalizationService();
            _saveOperationBusy = false;
            SetSaveStatus(_saveFacade != null
                ? "save.status.ready"
                : "save.status.service_unavailable");
            RefreshSaveControls();

            _openRequestRegistration = this.RegisterEvent<GameMenuOpenRequestedEvent>(OnMenuOpenRequested);
            _gameInput.ModeChanged += OnInputModeChanged;
            ApplyInputMode(_gameInput.CurrentMode);
            ApplicationLog.Info(LogEventIds.GameplayUi, "[GameMenuController] 游戏菜单初始化完成", this);
            return SceneSessionBindResult.Success;
        }

        void UnbindSession()
        {
            if (_gameInput != null)
            {
                _gameInput.ModeChanged -= OnInputModeChanged;
            }

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

            if (_panel != null)
            {
                _panel.UnregisterCallback<GeometryChangedEvent>(OnPanelGeometryChanged);
            }

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
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            if (_document == null && gameObject.scene.IsValid())
            {
                _document = gameObject.AddComponent<UIDocument>();
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
            if (_document == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[GameMenuController] 缺少 UIDocument，无法初始化菜单", this);
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            _overlay = root.Q<VisualElement>("game-menu-overlay");
            _panel = root.Q<VisualElement>("game-menu-panel");
            _inventoryTemplate = root.Q<VisualElement>(className: "inventory-template");
            _shopTemplate = root.Q<VisualElement>(className: "shop-template");
            _craftingTemplate = root.Q<VisualElement>(className: "crafting-template");
            _inventoryTab = root.Q<Button>("game-menu-inventory-tab");
            _shopTab = root.Q<Button>("game-menu-shop-tab");
            _craftingTab = root.Q<Button>("game-menu-crafting-tab");
            _closeButton = root.Q<Button>("game-menu-close");
            _saveButton = root.Q<Button>("game-menu-save");
            _returnFrontEndButton = root.Q<Button>("game-menu-return-front-end");
            _settingsButton = root.Q<Button>("game-menu-settings");
            _saveStatus = root.Q<Label>("game-menu-save-status");

            if (_overlay == null
                || _panel == null
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
                || _saveStatus == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[GameMenuController] 菜单 UXML 缺少必要的命名元素", this);
                return false;
            }

            _inventoryTab.clicked += OnInventoryTabClicked;
            _shopTab.clicked += OnShopTabClicked;
            _craftingTab.clicked += OnCraftingTabClicked;
            _closeButton.clicked += OnCloseClicked;
            _saveButton.clicked += OnSaveClicked;
            _returnFrontEndButton.clicked += OnReturnFrontEndClicked;
            _settingsButton.clicked += OnSettingsClicked;
            _panel.RegisterCallback<GeometryChangedEvent>(OnPanelGeometryChanged);
            return true;
        }

        void OnInputModeChanged(GameInputMode mode)
        {
            ApplyInputMode(mode);
        }

        void OnMenuOpenRequested(GameMenuOpenRequestedEvent e)
        {
            AvailablePages = e.AvailablePages | GameMenuAccess.Inventory;
            OpenPage(e.Page);
        }

        void ApplyInputMode(GameInputMode mode)
        {
            bool isOpen = mode == GameInputMode.UI;
            this.SendCommand(new SetGameplayPausedCommand(isOpen));

            if (_overlay == null)
            {
                return;
            }

            _overlay.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;

            if (isOpen)
            {
                _inventoryPanel?.ClearSelection();
                RefreshSaveControls();
                ApplyPage();
                return;
            }

            _inventoryPanel?.SetVisible(false);
            _inventoryPanel?.CancelActiveDrag();
            _shopPanel?.SetVisible(false);
            _craftingPanel?.SetVisible(false);
            AvailablePages = GameMenuAccess.Inventory;
            CurrentPage = GameMenuPage.Inventory;
        }

        void ApplyPage()
        {
            if (!IsPageAvailable(CurrentPage))
            {
                CurrentPage = GameMenuPage.Inventory;
            }

            _inventoryTab?.SetEnabled(IsPageAvailable(GameMenuPage.Inventory));
            _shopTab?.SetEnabled(IsPageAvailable(GameMenuPage.Shop));
            _craftingTab?.SetEnabled(IsPageAvailable(GameMenuPage.Crafting));
            bool showInventory = CurrentPage == GameMenuPage.Inventory;
            bool showShop = CurrentPage == GameMenuPage.Shop;
            bool showCrafting = CurrentPage == GameMenuPage.Crafting;
            SetTemplateVisible(_inventoryTemplate, showInventory);
            SetTemplateVisible(_shopTemplate, showShop);
            SetTemplateVisible(_craftingTemplate, showCrafting);
            _inventoryPanel?.CancelActiveDrag();
            _inventoryPanel?.SetVisible(showInventory);

            if (!showInventory)
            {
                _inventoryPanel?.RefreshInventory();
            }
            _shopPanel?.SetVisible(showShop);
            _craftingPanel?.SetVisible(showCrafting);
            SetTabActive(_inventoryTab, showInventory);
            SetTabActive(_shopTab, showShop);
            SetTabActive(_craftingTab, showCrafting);

            Button activeTab = showInventory
                ? _inventoryTab
                : showShop
                    ? _shopTab
                    : _craftingTab;
            activeTab?.Focus();
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
            _inventoryPanel?.CancelActiveDrag();
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

                        if (focusTarget != null && focusTarget.enabledSelf)
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

        void OnPanelGeometryChanged(GeometryChangedEvent evt)
        {
            float width = evt.newRect.width;
            float height = evt.newRect.height;
            _panel.EnableInClassList("game-menu-panel--compact", width < 1100f || height < 820f);
            _panel.EnableInClassList("game-menu-panel--wide", width >= 1580f);
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
