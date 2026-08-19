using System;
using System.Collections.Generic;
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

        static readonly UserLanguagePreference[] LanguagePreferences =
        {
            UserLanguagePreference.Auto,
            UserLanguagePreference.SimplifiedChinese,
            UserLanguagePreference.English,
        };

        static readonly string[] LanguageOptionKeys =
        {
            "settings.language.auto",
            "settings.language.zh_hans",
            "settings.language.en",
        };

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
        Button _continueButton;
        Button _newGameButton;
        Label _saveStatus;
        DropdownField _languageDropdown;
        readonly List<string> _languageChoices = new List<string>();
        SessionSaveFacade _saveFacade;
        LocalizationService _localizationService;
        int _languageRefreshGeneration;
        bool _saveOperationBusy;
        bool _hasAutoSave;
        bool _languageOperationBusy;
        bool _updatingLanguageChoice;

        public GameMenuAccess AvailablePages { get; private set; } = GameMenuAccess.Inventory;

        public GameMenuPage CurrentPage { get; private set; } = GameMenuPage.Inventory;

        public bool IsOpen => _gameInput != null && _gameInput.CurrentMode == GameInputMode.UI;

        public int SessionBindCount => _sessionBinding?.BindCount ?? 0;

        public bool IsSaveOperationBusy => _saveOperationBusy;

        public bool CanContinueAutoSave => _hasAutoSave && !_saveOperationBusy;

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
                Debug.LogError("[GameMenuController] 缺少 UIDocument 或 PanelSettings，无法初始化菜单", this);
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
                Debug.LogError("[GameMenuController] 缺少 GameInput，无法控制菜单", this);
                return SceneSessionBindResult.Failed;
            }

            _saveFacade = ResolveSaveFacade();
            _localizationService = ResolveLocalizationService();
            _saveOperationBusy = false;
            _hasAutoSave = false;
            _languageOperationBusy = false;
            RefreshSaveControls();
            ScheduleLanguageRefresh();
            ScheduleAutoSaveProbe();

            _openRequestRegistration = this.RegisterEvent<GameMenuOpenRequestedEvent>(OnMenuOpenRequested);
            _gameInput.ModeChanged += OnInputModeChanged;
            ApplyInputMode(_gameInput.CurrentMode);
            Debug.Log("[GameMenuController] 游戏菜单初始化完成", this);
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

            if (_continueButton != null)
            {
                _continueButton.clicked -= OnContinueClicked;
            }

            if (_newGameButton != null)
            {
                _newGameButton.clicked -= OnNewGameClicked;
            }

            if (_languageDropdown != null)
            {
                _languageDropdown.UnregisterValueChangedCallback(OnLanguageChanged);
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
            _continueButton = null;
            _newGameButton = null;
            _saveStatus = null;
            _languageDropdown = null;
            _languageChoices.Clear();
            _saveFacade = null;
            _localizationService = null;
            _languageRefreshGeneration++;
            _saveOperationBusy = false;
            _hasAutoSave = false;
            _languageOperationBusy = false;
            _updatingLanguageChoice = false;
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
                Debug.LogError("[GameMenuController] 缺少 UIDocument，无法初始化菜单", this);
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
            _continueButton = root.Q<Button>("game-menu-continue");
            _newGameButton = root.Q<Button>("game-menu-new-game");
            _saveStatus = root.Q<Label>("game-menu-save-status");
            _languageDropdown = root.Q<DropdownField>("game-menu-language-dropdown");

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
                || _continueButton == null
                || _newGameButton == null
                || _saveStatus == null
                || _languageDropdown == null)
            {
                Debug.LogError("[GameMenuController] 菜单 UXML 缺少必要的命名元素", this);
                return false;
            }

            _inventoryTab.clicked += OnInventoryTabClicked;
            _shopTab.clicked += OnShopTabClicked;
            _craftingTab.clicked += OnCraftingTabClicked;
            _closeButton.clicked += OnCloseClicked;
            _saveButton.clicked += OnSaveClicked;
            _continueButton.clicked += OnContinueClicked;
            _newGameButton.clicked += OnNewGameClicked;
            _languageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
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

        void OnContinueClicked()
        {
            if (!_hasAutoSave)
            {
                return;
            }

            RunSaveOperation(
                "continue-auto",
                facade => facade.ContinueAutoAsync(),
                _continueButton);
        }

        void OnNewGameClicked()
        {
            RunSaveOperation(
                "new-game",
                facade => facade.StartNewGameAsync(),
                _newGameButton);
        }

        void OnLanguageChanged(ChangeEvent<string> evt)
        {
            if (_updatingLanguageChoice
                || _languageOperationBusy
                || _localizationService == null)
            {
                return;
            }

            int index = _languageChoices.IndexOf(evt.newValue);

            if (index < 0 || index >= LanguagePreferences.Length)
            {
                ScheduleLanguageRefresh();
                return;
            }

            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host)
                || host.ApplicationScope == null
                || !host.ApplicationScope.CanAcceptWork)
            {
                ScheduleLanguageRefresh();
                return;
            }

            UserLanguagePreference preference = LanguagePreferences[index];
            LocalizationService service = _localizationService;
            _languageOperationBusy = true;
            _languageDropdown.SetEnabled(false);

            try
            {
                host.ApplicationScope.Tasks.Run(
                    "game-menu-language-change",
                    async token =>
                    {
                        LocalizationOperationResult result =
                            await service.ChangeLanguageAsync(preference, token);

                        if (!ReferenceEquals(_localizationService, service)
                            || _languageDropdown == null)
                        {
                            return;
                        }

                        _languageOperationBusy = false;
                        ScheduleLanguageRefresh();

                        if (result.Succeeded && IsOpen)
                        {
                            _languageDropdown?.Focus();
                        }
                        else if (!result.Succeeded
                            && result.Code != LocalizationOperationCode.Cancelled)
                        {
                            Debug.LogError(
                                $"[GameMenuController] 语言切换失败：{result.Code}\n"
                                + result.Exception,
                                this);
                        }
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                _languageOperationBusy = false;
                _languageDropdown.SetEnabled(true);
                Debug.LogException(exception, this);
                ScheduleLanguageRefresh();
            }
        }

        void OnLocaleChanged(string localeCode)
        {
            ScheduleLanguageRefresh();
        }

        void ScheduleLanguageRefresh()
        {
            LocalizationService service = _localizationService;

            if (_languageDropdown == null || service == null)
            {
                return;
            }

            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host)
                || host.ApplicationScope == null
                || !host.ApplicationScope.CanAcceptWork)
            {
                _languageDropdown.SetEnabled(false);
                return;
            }

            int generation = ++_languageRefreshGeneration;

            try
            {
                host.ApplicationScope.Tasks.Run(
                    "game-menu-language-refresh",
                    async token =>
                    {
                        List<string> choices = new List<string>(LanguageOptionKeys.Length);

                        for (int i = 0; i < LanguageOptionKeys.Length; i++)
                        {
                            choices.Add(await service.GetStringAsync(
                                "ui",
                                LanguageOptionKeys[i],
                                cancellationToken: token));
                        }

                        if (generation != _languageRefreshGeneration
                            || !ReferenceEquals(_localizationService, service)
                            || _languageDropdown == null)
                        {
                            return;
                        }

                        _languageChoices.Clear();
                        _languageChoices.AddRange(choices);
                        _updatingLanguageChoice = true;

                        try
                        {
                            _languageDropdown.choices = new List<string>(_languageChoices);
                            int selectedIndex = Array.IndexOf(
                                LanguagePreferences,
                                host.Settings.Current.Language);
                            selectedIndex = Mathf.Clamp(selectedIndex, 0, _languageChoices.Count - 1);
                            _languageDropdown.SetValueWithoutNotify(
                                _languageChoices[selectedIndex]);
                            _languageDropdown.SetEnabled(!_languageOperationBusy);
                        }
                        finally
                        {
                            _updatingLanguageChoice = false;
                        }
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                _languageDropdown.SetEnabled(false);
            }
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

        void ScheduleAutoSaveProbe()
        {
            SessionSaveFacade facade = _saveFacade;

            if (facade == null
                || !ApplicationHost.TryGetCurrent(out ApplicationHost host)
                || host.ApplicationScope == null
                || !host.ApplicationScope.CanAcceptWork)
            {
                SetSaveStatus("存档服务当前不可用");
                RefreshSaveControls();
                return;
            }

            SetSaveOperationBusy(true, "正在检查自动存档…");

            try
            {
                host.ApplicationScope.Tasks.Run(
                    "game-menu-probe-auto-save",
                    async token =>
                    {
                        SaveOperationResult result = await facade.ProbeAutoSaveAsync(token);

                        if (!ReferenceEquals(_saveFacade, facade))
                        {
                            return;
                        }

                        _hasAutoSave = result.Succeeded;
                        SetSaveOperationBusy(
                            false,
                            result.Succeeded
                                ? result.RecoverySource == SaveRecoverySource.Backup
                                    ? "自动存档可用（将从备份恢复）"
                                    : "自动存档可用"
                                : DescribeSaveResult(result));
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                SetSaveOperationBusy(false, "无法检查自动存档");
            }
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
                SetSaveStatus("存档服务当前不可用");
                return;
            }

            SetSaveOperationBusy(true, "正在处理存档请求…");

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

                        if (result.Succeeded && result.Operation == SaveOperation.Save)
                        {
                            _hasAutoSave = true;
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
                Debug.LogException(exception, this);
                SetSaveOperationBusy(false, "无法提交存档请求");
            }
        }

        void SetSaveOperationBusy(bool busy, string status)
        {
            _saveOperationBusy = busy;
            SetSaveStatus(status);
            RefreshSaveControls();
        }

        void RefreshSaveControls()
        {
            bool available = _saveFacade != null && !_saveOperationBusy;
            _saveButton?.SetEnabled(available);
            _continueButton?.SetEnabled(available && _hasAutoSave);
            _newGameButton?.SetEnabled(available);
        }

        void SetSaveStatus(string status)
        {
            if (_saveStatus != null)
            {
                _saveStatus.text = status ?? string.Empty;
            }
        }

        static string DescribeSaveResult(SaveOperationResult result)
        {
            if (result == null)
            {
                return "存档操作没有返回结果";
            }

            if (result.Succeeded)
            {
                return result.Operation switch
                {
                    SaveOperation.Save => "进度已保存",
                    SaveOperation.Continue => result.RecoverySource == SaveRecoverySource.Backup
                        ? "已从备份恢复进度"
                        : "已恢复自动存档",
                    SaveOperation.NewGame => "新游戏已开始",
                    _ => "存档已就绪",
                };
            }

            return result.ErrorCode switch
            {
                SaveErrorCode.SlotNotFound => "尚无自动存档",
                SaveErrorCode.NoValidGeneration => "自动存档已损坏",
                SaveErrorCode.OperationInProgress => "已有存档操作正在进行",
                SaveErrorCode.SessionUnavailable => "当前游戏状态不可存档",
                SaveErrorCode.SnapshotUnavailable => "无法取得当前进度",
                SaveErrorCode.Cancelled => "存档操作已取消",
                SaveErrorCode.ContentVersionMismatch => "存档内容版本不兼容",
                SaveErrorCode.ContentMissing => "存档引用的内容缺失",
                SaveErrorCode.FutureSchemaUnsupported => "存档来自更高版本",
                SaveErrorCode.ChecksumMismatch => "存档完整性校验失败",
                SaveErrorCode.PermissionDenied => "没有存档目录写入权限",
                SaveErrorCode.StorageFull => "存储空间不足",
                SaveErrorCode.FlushTimedOut => "存档写入超时",
                _ => $"存档操作失败（{result.ErrorCode}）",
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
