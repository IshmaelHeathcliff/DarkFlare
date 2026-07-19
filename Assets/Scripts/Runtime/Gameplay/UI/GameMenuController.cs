using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public enum GameMenuPage
    {
        Inventory,
        Shop,
        Crafting
    }

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
        VisualElement _overlay;
        Button _inventoryTab;
        Button _shopTab;
        Button _craftingTab;
        Button _closeButton;

        public GameMenuPage CurrentPage { get; private set; } = GameMenuPage.Inventory;

        public bool IsOpen => _gameInput != null && _gameInput.CurrentMode == GameInputMode.UI;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void OpenPage(GameMenuPage page)
        {
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

            _gameInput.ModeChanged += OnInputModeChanged;
            ApplyInputMode(_gameInput.CurrentMode);
            Debug.Log("[GameMenuController] 游戏菜单初始化完成", this);
        }

        void OnDisable()
        {
            if (_gameInput != null)
            {
                _gameInput.ModeChanged -= OnInputModeChanged;
            }

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

            _gameInput = null;
            _overlay = null;
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
            _inventoryTab = root.Q<Button>("game-menu-inventory-tab");
            _shopTab = root.Q<Button>("game-menu-shop-tab");
            _craftingTab = root.Q<Button>("game-menu-crafting-tab");
            _closeButton = root.Q<Button>("game-menu-close");

            if (_overlay == null
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
            return true;
        }

        void OnInputModeChanged(GameInputMode mode)
        {
            ApplyInputMode(mode);
        }

        void ApplyInputMode(GameInputMode mode)
        {
            if (_overlay == null)
            {
                return;
            }

            bool isOpen = mode == GameInputMode.UI;
            _overlay.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;

            if (isOpen)
            {
                ApplyPage();
                return;
            }

            _inventoryPanel?.SetVisible(false);
            _shopPanel?.SetVisible(false);
            _craftingPanel?.SetVisible(false);
            CurrentPage = GameMenuPage.Inventory;
        }

        void ApplyPage()
        {
            bool showInventory = CurrentPage == GameMenuPage.Inventory;
            bool showShop = CurrentPage == GameMenuPage.Shop;
            bool showCrafting = CurrentPage == GameMenuPage.Crafting;
            _inventoryPanel?.SetVisible(showInventory);
            _shopPanel?.SetVisible(showShop);
            _craftingPanel?.SetVisible(showCrafting);
            SetTabActive(_inventoryTab, showInventory);
            SetTabActive(_shopTab, showShop);
            SetTabActive(_craftingTab, showCrafting);

            bool focused = false;

            if (showInventory)
            {
                focused = _inventoryPanel != null && _inventoryPanel.FocusDefault();
            }
            else if (showShop)
            {
                focused = _shopPanel != null && _shopPanel.FocusDefault();
            }
            else if (showCrafting)
            {
                focused = _craftingPanel != null && _craftingPanel.FocusDefault();
            }

            if (!focused)
            {
                _closeButton.Focus();
            }
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
    }
}
