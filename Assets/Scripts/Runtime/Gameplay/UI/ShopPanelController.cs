using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(InventoryPanelController))]
    public class ShopPanelController : MonoBehaviour, IController
    {
        const int MerchantGridWidth = 10;
        const int MerchantGridMinimumHeight = 6;
        const float CellSize = 48f;
        const float CellGap = 4f;

        [SerializeField]
        UIDocument _document;

        [SerializeField]
        InventoryPanelController _inventoryPanel;

        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();
        readonly Dictionary<ItemInstance, Button> _merchantButtons = new Dictionary<ItemInstance, Button>();
        readonly Dictionary<ItemInstance, Button> _playerButtons = new Dictionary<ItemInstance, Button>();
        readonly ShopViewState _viewState = new ShopViewState();

        VisualElement _page;
        VisualElement _merchantFrame;
        VisualElement _merchantList;
        VisualElement _playerList;
        Label _goldLabel;
        Label _merchantEmptyLabel;
        Label _playerEmptyLabel;
        Label _selectedSourceLabel;
        Label _selectedPriceLabel;
        Label _feedbackLabel;
        Button _buyButton;
        Button _sellButton;
        ItemDetailView _detailView;
        ItemInstance _selectedItem;
        ItemInstance _previewItem;
        ShopItemSource _selectedSource;
        ShopItemSource _previewSource;
        int _refreshGeneration;
        bool _isTransactionInProgress;

        public ShopSnapshot LastSnapshot { get; private set; }

        public ItemInstance SelectedItem => _selectedItem;

        public ShopItemSource SelectedSource => _selectedSource;

        public ShopViewState ViewState => _viewState;

        public bool IsVisible { get; private set; }

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void RefreshShop()
        {
            if (_merchantList == null)
            {
                return;
            }

            CaptureViewState();
            int generation = ++_refreshGeneration;
            LastSnapshot = this.SendQuery(new GetShopSnapshotQuery());
            _goldLabel.text = $"持有金币  {LastSnapshot.Gold}";
            _merchantList.Clear();
            _merchantButtons.Clear();

            BuildList(LastSnapshot.MerchantItems, _merchantList, _merchantButtons);
            _merchantEmptyLabel.style.display = LastSnapshot.MerchantItems.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            ResolveSelection();
            _previewItem = null;
            RefreshSelection();
            ScheduleRestoreLayout(generation);
        }

        public void SetVisible(bool visible)
        {
            if (!visible && IsVisible)
            {
                CaptureViewState();
                _refreshGeneration++;
            }

            IsVisible = visible;

            if (_page == null)
            {
                return;
            }

            _page.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (visible)
            {
                RefreshShop();
            }
        }

        public bool FocusDefault()
        {
            if (_selectedSource == ShopItemSource.Merchant
                && _selectedItem != null
                && _merchantButtons.TryGetValue(_selectedItem, out Button merchantButton))
            {
                merchantButton.Focus();
                return true;
            }

            return _inventoryPanel != null && _inventoryPanel.FocusDefault();
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

            RegisterEvents();
            _inventoryPanel.SelectionChanged += OnInventorySelectionChanged;
            RefreshShop();
            SetVisible(IsVisible);
            Debug.Log("[ShopPanelController] 商店面板初始化完成", this);
        }

        void OnDisable()
        {
            if (_buyButton != null)
            {
                _buyButton.clicked -= OnBuyClicked;
            }

            if (_sellButton != null)
            {
                _sellButton.clicked -= OnSellClicked;
            }

            if (_inventoryPanel != null)
            {
                _inventoryPanel.SelectionChanged -= OnInventorySelectionChanged;
            }

            for (int i = 0; i < _eventRegistrations.Count; i++)
            {
                _eventRegistrations[i].UnRegister();
            }

            _eventRegistrations.Clear();
            _merchantButtons.Clear();
            _playerButtons.Clear();
            _viewState.Reset();
            _refreshGeneration++;
            _page = null;
            _merchantFrame = null;
            _merchantList = null;
            _playerList = null;
            _goldLabel = null;
            _merchantEmptyLabel = null;
            _playerEmptyLabel = null;
            _selectedSourceLabel = null;
            _selectedPriceLabel = null;
            _feedbackLabel = null;
            _buyButton = null;
            _sellButton = null;
            _detailView = null;
            _selectedItem = null;
            _previewItem = null;
            _isTransactionInProgress = false;
            IsVisible = false;
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
        }

        bool BindVisualTree()
        {
            if (_document == null || _inventoryPanel == null)
            {
                Debug.LogError("[ShopPanelController] 缺少 UIDocument 或共享背包控制器", this);
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            _page = root.Q<VisualElement>("shop-page");
            _merchantFrame = root.Q<VisualElement>("shop-merchant-frame");
            _merchantList = root.Q<VisualElement>("shop-merchant-list");
            _goldLabel = root.Q<Label>("shop-gold");
            _merchantEmptyLabel = root.Q<Label>("shop-merchant-empty");
            _selectedSourceLabel = root.Q<Label>("shop-selected-source");
            _selectedPriceLabel = root.Q<Label>("shop-selected-price");
            _feedbackLabel = root.Q<Label>("shop-feedback");
            _buyButton = root.Q<Button>("shop-buy");
            _sellButton = root.Q<Button>("shop-sell");

            if (_page == null
                || _merchantFrame == null
                || _merchantList == null
                || _goldLabel == null
                || _merchantEmptyLabel == null
                || _selectedSourceLabel == null
                || _selectedPriceLabel == null
                || _feedbackLabel == null
                || _buyButton == null
                || _sellButton == null)
            {
                Debug.LogError("[ShopPanelController] 商店 UXML 缺少格子背包所需的命名元素", this);
                return false;
            }

            _buyButton.clicked += OnBuyClicked;
            _sellButton.clicked += OnSellClicked;
            return true;
        }

        void RegisterEvents()
        {
            if (_eventRegistrations.Count > 0)
            {
                return;
            }

            _eventRegistrations.Add(this.RegisterEvent<TradeCompletedEvent>(_ => RefreshShop()));
            _eventRegistrations.Add(this.RegisterEvent<ItemCraftedEvent>(_ => RefreshShop()));
        }

        void BuildList(
            IReadOnlyList<ShopItemSnapshot> items,
            VisualElement list,
            Dictionary<ItemInstance, Button> buttons)
        {
            List<Vector2Int> sizes = new List<Vector2Int>(items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                sizes.Add(items[i].Detail.GridSize);
            }

            IReadOnlyList<RectInt> placements = MerchantGridLayout.Pack(
                MerchantGridWidth,
                sizes,
                out int rowCount);
            int height = Mathf.Max(MerchantGridMinimumHeight, rowCount);
            float step = CellSize + CellGap;
            list.style.width = MerchantGridWidth * CellSize + (MerchantGridWidth - 1) * CellGap;
            list.style.height = height * CellSize + (height - 1) * CellGap;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < MerchantGridWidth; x++)
                {
                    VisualElement cell = new VisualElement
                    {
                        pickingMode = PickingMode.Ignore,
                    };
                    cell.AddToClassList("shop-grid-cell");
                    cell.style.left = x * step;
                    cell.style.top = y * step;
                    cell.style.width = CellSize;
                    cell.style.height = CellSize;
                    list.Add(cell);
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                ShopItemSnapshot item = items[i];
                RectInt placement = placements[i];
                Button button = new Button(() => SelectItem(item.Item, item.Source));
                button.name = $"shop-item-{item.Detail.InstanceId}";
                button.text = string.Empty;
                button.userData = item.Item;
                button.AddToClassList("shop-item");
                button.AddToClassList(item.Source == ShopItemSource.Merchant
                    ? "shop-item--merchant"
                    : "shop-item--player");
                button.AddToClassList(GetRarityClass(item.Rarity));
                button.style.left = placement.x * step;
                button.style.top = placement.y * step;
                button.style.width = placement.width * CellSize + (placement.width - 1) * CellGap;
                button.style.height = placement.height * CellSize + (placement.height - 1) * CellGap;

                VisualElement icon = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                };
                icon.AddToClassList("shop-item-icon");
                ItemVisualPresenter.ApplyIcon(icon, item.Detail.IconGuid);
                button.Add(icon);
                button.RegisterCallback<PointerEnterEvent>(_ => PreviewItem(item.Item, item.Source));
                button.RegisterCallback<PointerLeaveEvent>(_ => EndPreview(item.Item));
                button.RegisterCallback<FocusInEvent>(_ => PreviewItem(item.Item, item.Source));
                button.RegisterCallback<FocusOutEvent>(_ => EndPreview(item.Item));
                list.Add(button);
                buttons.Add(item.Item, button);
            }
        }

        void SelectItem(ItemInstance item, ShopItemSource source)
        {
            _selectedItem = item;
            _selectedSource = source;
            _viewState.ActiveSource = source;
            _viewState.FocusTarget = ShopFocusTarget.Item;
            _viewState.ClearFeedback();
            UpdateSelectedListState(source, item);
            RefreshSelection();
        }

        void OnInventorySelectionChanged(ItemInstance item)
        {
            if (!IsVisible || _isTransactionInProgress || item == null)
            {
                return;
            }

            _selectedItem = item;
            _selectedSource = ShopItemSource.Player;
            _previewItem = null;
            _viewState.ActiveSource = ShopItemSource.Player;
            _viewState.FocusTarget = ShopFocusTarget.Item;
            _viewState.ClearFeedback();
            UpdateSelectedListState(ShopItemSource.Player, item);
            RefreshSelection();
        }

        void PreviewItem(ItemInstance item, ShopItemSource source)
        {
            _previewItem = item;
            _previewSource = source;
            RefreshDetail();
        }

        void EndPreview(ItemInstance item)
        {
            if (_previewItem != item)
            {
                return;
            }

            _previewItem = null;
            RefreshDetail();
        }

        void RefreshSelection()
        {
            SetSelectedClass(_merchantButtons, _selectedSource == ShopItemSource.Merchant ? _selectedItem : null);
            SetSelectedClass(_playerButtons, _selectedSource == ShopItemSource.Player ? _selectedItem : null);

            if (!TryGetSelectedSnapshot(out ShopItemSnapshot selected))
            {
                _selectedSourceLabel.text = "来源：-";
                _selectedPriceLabel.text = "价格：-";
                _feedbackLabel.text = _viewState.HasFeedback ? _viewState.Feedback : "商店和背包均为空";
                _buyButton.SetEnabled(false);
                _sellButton.SetEnabled(false);
                _viewState.FocusTarget = ShopFocusTarget.CloseFallback;
                _inventoryPanel.RestoreTooltip();
                return;
            }

            bool isMerchantItem = selected.Source == ShopItemSource.Merchant;
            RefreshDetail();
            _selectedSourceLabel.text = $"来源：{(isMerchantItem ? "商人库存" : "玩家背包")}";
            _selectedPriceLabel.text = $"{(isMerchantItem ? "买入" : "卖出")}价格：{selected.Price} 金币";
            bool canAfford = LastSnapshot.Gold >= selected.Price;
            _buyButton.SetEnabled(isMerchantItem && canAfford);
            _sellButton.SetEnabled(!isMerchantItem);
            if (_viewState.HasFeedback)
            {
                _feedbackLabel.text = _viewState.Feedback;
                return;
            }

            _feedbackLabel.text = isMerchantItem
                ? canAfford
                    ? "确认购买后，物品将尝试放入背包"
                    : $"金币不足，还需要 {selected.Price - LastSnapshot.Gold}"
                : "出售后物品会离开背包，首版不提供回购";
        }

        void RefreshDetail()
        {
            ItemInstance item = _previewItem;
            ShopItemSource source = _previewSource;

            if (TryFindSnapshot(item, source, out ShopItemSnapshot snapshot))
            {
                if (source == ShopItemSource.Merchant)
                {
                    _inventoryPanel.ShowMerchantTooltip(
                        item,
                        $"商人库存 · 买入 {snapshot.Price} 金币");
                }
                else
                {
                    _inventoryPanel.ShowPlayerTooltip(
                        item,
                        $"玩家背包 · 卖出 {snapshot.Price} 金币");
                }

                return;
            }

            _inventoryPanel.RestoreTooltip();
        }

        bool TryGetSelectedSnapshot(out ShopItemSnapshot selected)
        {
            return TryFindSnapshot(_selectedItem, _selectedSource, out selected);
        }

        bool TryFindSnapshot(
            ItemInstance item,
            ShopItemSource source,
            out ShopItemSnapshot selected)
        {
            IReadOnlyList<ShopItemSnapshot> items = source == ShopItemSource.Merchant
                ? LastSnapshot.MerchantItems
                : LastSnapshot.PlayerItems;

            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Item == item)
                    {
                        selected = items[i];
                        return true;
                    }
                }
            }

            selected = default;
            return false;
        }

        void OnBuyClicked()
        {
            _viewState.ClearFeedback();

            if (!TryGetSelectedSnapshot(out ShopItemSnapshot selected)
                || selected.Source != ShopItemSource.Merchant)
            {
                SetFeedback("请选择商人库存中的物品");
                return;
            }

            _viewState.FocusTarget = ShopFocusTarget.Item;
            _isTransactionInProgress = true;
            bool bought;

            try
            {
                bought = this.SendCommand(new BuyItemCommand(selected.Item));
            }
            finally
            {
                _isTransactionInProgress = false;
            }

            SetFeedback(bought
                ? $"已购买 {selected.DisplayName}"
                : "购买失败：请检查金币、背包空间或库存");
        }

        void OnSellClicked()
        {
            _viewState.ClearFeedback();

            if (!TryGetSelectedSnapshot(out ShopItemSnapshot selected)
                || selected.Source != ShopItemSource.Player)
            {
                SetFeedback("请选择背包中的物品");
                return;
            }

            _viewState.FocusTarget = ShopFocusTarget.Item;
            _isTransactionInProgress = true;
            bool sold;

            try
            {
                sold = this.SendCommand(new SellItemCommand(selected.Item));
            }
            finally
            {
                _isTransactionInProgress = false;
            }

            SetFeedback(sold
                ? $"已出售 {selected.DisplayName}"
                : "出售失败：物品已不在背包中");
        }

        void SetFeedback(string feedback)
        {
            _viewState.SetFeedback(feedback);
            _feedbackLabel.text = feedback;
        }

        void CaptureViewState()
        {
            if (_selectedItem != null)
            {
                UpdateSelectedListState(_selectedSource, _selectedItem);
            }

            if (_isTransactionInProgress || _page == null || _page.panel == null)
            {
                return;
            }

            Focusable focused = _page.panel.focusController.focusedElement;

            if (focused == _buyButton)
            {
                _viewState.FocusTarget = ShopFocusTarget.BuyAction;
            }
            else if (focused == _sellButton)
            {
                _viewState.FocusTarget = ShopFocusTarget.SellAction;
            }
            else if (ContainsButton(_merchantButtons, focused) || ContainsButton(_playerButtons, focused))
            {
                _viewState.FocusTarget = ShopFocusTarget.Item;
            }
        }

        void UpdateSelectedListState(ShopItemSource source, ItemInstance selectedItem)
        {
            if (selectedItem == null)
            {
                return;
            }

            IReadOnlyList<ShopItemSnapshot> items = source == ShopItemSource.Merchant
                ? LastSnapshot.MerchantItems
                : LastSnapshot.PlayerItems;
            ItemListViewState state = _viewState.GetList(source);
            state.SelectedInstanceId = selectedItem.InstanceId;

            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item == selectedItem)
                {
                    state.FallbackIndex = i;
                    return;
                }
            }
        }

        void ResolveSelection()
        {
            int merchantIndex = ResolveColumn(LastSnapshot.MerchantItems, _viewState.Merchant);
            int playerIndex = ResolveColumn(LastSnapshot.PlayerItems, _viewState.Player);
            int activeIndex = _viewState.ActiveSource == ShopItemSource.Merchant
                ? merchantIndex
                : playerIndex;

            if (activeIndex < 0)
            {
                ShopItemSource otherSource = _viewState.ActiveSource == ShopItemSource.Merchant
                    ? ShopItemSource.Player
                    : ShopItemSource.Merchant;
                int otherIndex = otherSource == ShopItemSource.Merchant ? merchantIndex : playerIndex;

                if (otherIndex >= 0)
                {
                    _viewState.ActiveSource = otherSource;
                    activeIndex = otherIndex;
                }
            }

            _selectedSource = _viewState.ActiveSource;

            if (activeIndex < 0)
            {
                _selectedItem = null;
                return;
            }

            IReadOnlyList<ShopItemSnapshot> activeItems = _selectedSource == ShopItemSource.Merchant
                ? LastSnapshot.MerchantItems
                : LastSnapshot.PlayerItems;
            _selectedItem = activeItems[activeIndex].Item;
        }

        static int ResolveColumn(
            IReadOnlyList<ShopItemSnapshot> items,
            ItemListViewState state)
        {
            if (string.IsNullOrEmpty(state.SelectedInstanceId))
            {
                return -1;
            }

            int index = ItemSelectionResolver.ResolveIndex(
                items,
                state.SelectedInstanceId,
                state.FallbackIndex,
                item => item.Detail.InstanceId);

            if (index < 0)
            {
                state.SelectedInstanceId = string.Empty;
                state.FallbackIndex = 0;
                return -1;
            }

            state.SelectedInstanceId = items[index].Detail.InstanceId;
            state.FallbackIndex = index;
            return index;
        }

        void ScheduleRestoreLayout(int generation)
        {
            if (_page == null || !IsVisible)
            {
                return;
            }

            _page.schedule.Execute(() => RestoreLayout(generation));
        }

        void RestoreLayout(int generation)
        {
            if (generation != _refreshGeneration
                || _page == null
                || _page.panel == null
                || !IsVisible)
            {
                return;
            }

            if (_viewState.FocusTarget == ShopFocusTarget.BuyAction && _buyButton.enabledSelf)
            {
                _buyButton.Focus();
                return;
            }

            if (_viewState.FocusTarget == ShopFocusTarget.SellAction && _sellButton.enabledSelf)
            {
                _sellButton.Focus();
                return;
            }

            if (_selectedSource == ShopItemSource.Player)
            {
                _inventoryPanel.FocusDefault();
                return;
            }

            if (_selectedItem == null || !_merchantButtons.TryGetValue(_selectedItem, out Button button))
            {
                return;
            }

            button.Focus();
        }

        static bool ContainsButton(Dictionary<ItemInstance, Button> buttons, Focusable focused)
        {
            foreach (Button button in buttons.Values)
            {
                if (button == focused)
                {
                    return true;
                }
            }

            return false;
        }

        static void SetSelectedClass(Dictionary<ItemInstance, Button> buttons, ItemInstance selectedItem)
        {
            foreach (KeyValuePair<ItemInstance, Button> entry in buttons)
            {
                entry.Value.EnableInClassList("shop-item--selected", entry.Key == selectedItem);
            }
        }

        static string GetRarityClass(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic:
                    return "shop-item--magic";
                case ItemRarity.Rare:
                    return "shop-item--rare";
                case ItemRarity.Unique:
                    return "shop-item--unique";
                default:
                    return "shop-item--normal";
            }
        }
    }
}
