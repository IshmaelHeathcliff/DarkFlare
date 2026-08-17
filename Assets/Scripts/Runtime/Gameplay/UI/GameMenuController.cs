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

        public GameMenuAccess AvailablePages { get; private set; } = GameMenuAccess.Inventory;

        public GameMenuPage CurrentPage { get; private set; } = GameMenuPage.Inventory;

        public bool IsOpen => _gameInput != null && _gameInput.CurrentMode == GameInputMode.UI;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
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

            if (!BindVisualTree())
            {
                return;
            }

            _gameInput = this.GetUtility<GameInput>();

            if (_gameInput == null)
            {
                Debug.LogError("[GameMenuController] 缺少 GameInput，无法控制菜单", this);
                return;
            }

            _openRequestRegistration = this.RegisterEvent<GameMenuOpenRequestedEvent>(OnMenuOpenRequested);
            _gameInput.ModeChanged += OnInputModeChanged;
            ApplyInputMode(_gameInput.CurrentMode);
            Debug.Log("[GameMenuController] 游戏菜单初始化完成", this);
        }

        void OnDisable()
        {
            if (_gameInput != null && _gameInput.IsUiEnabled)
            {
                _gameInput.SwitchToGameplay();
            }

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

            if (_overlay == null
                || _panel == null
                || _inventoryTemplate == null
                || _shopTemplate == null
                || _craftingTemplate == null
                || _inventoryTab == null
                || _shopTab == null
                || _craftingTab == null
                || _closeButton == null)
            {
                Debug.LogError("[GameMenuController] 菜单 UXML 缺少必要的命名元素", this);
                return false;
            }

            _inventoryTab.clicked += OnInventoryTabClicked;
            _shopTab.clicked += OnShopTabClicked;
            _craftingTab.clicked += OnCraftingTabClicked;
            _closeButton.clicked += OnCloseClicked;
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
