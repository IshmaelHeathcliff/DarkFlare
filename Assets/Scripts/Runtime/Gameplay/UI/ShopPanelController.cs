using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class ShopPanelController : MonoBehaviour, IController
    {
        [SerializeField]
        UIDocument _document;

        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();
        readonly Dictionary<ItemInstance, Button> _merchantButtons = new Dictionary<ItemInstance, Button>();
        readonly Dictionary<ItemInstance, Button> _playerButtons = new Dictionary<ItemInstance, Button>();
        readonly ShopViewState _viewState = new ShopViewState();

        VisualElement _page;
        VisualElement _merchantList;
        VisualElement _playerList;
        ScrollView _merchantScroll;
        ScrollView _playerScroll;
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
            if (_merchantList == null || _playerList == null)
            {
                return;
            }

            CaptureViewState();
            int generation = ++_refreshGeneration;
            LastSnapshot = this.SendQuery(new GetShopSnapshotQuery());
            _goldLabel.text = $"持有金币  {LastSnapshot.Gold}";
            _merchantList.Clear();
            _playerList.Clear();
            _merchantButtons.Clear();
            _playerButtons.Clear();

            BuildList(LastSnapshot.MerchantItems, _merchantList, _merchantButtons);
            BuildList(LastSnapshot.PlayerItems, _playerList, _playerButtons);
            _merchantEmptyLabel.style.display = LastSnapshot.MerchantItems.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _playerEmptyLabel.style.display = LastSnapshot.PlayerItems.Count == 0
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
            if (_selectedItem == null || _viewState.FocusTarget == ShopFocusTarget.CloseFallback)
            {
                return false;
            }

            ScheduleRestoreLayout(_refreshGeneration);
            return true;
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
            _merchantList = null;
            _playerList = null;
            _merchantScroll = null;
            _playerScroll = null;
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
        }

        bool BindVisualTree()
        {
            if (_document == null)
            {
                Debug.LogError("[ShopPanelController] 缺少 UIDocument，无法初始化商店", this);
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            _page = root.Q<VisualElement>("shop-page");
            _merchantList = root.Q<VisualElement>("shop-merchant-list");
            _playerList = root.Q<VisualElement>("shop-player-list");
            _merchantScroll = root.Q<ScrollView>("shop-merchant-scroll");
            _playerScroll = root.Q<ScrollView>("shop-player-scroll");
            _goldLabel = root.Q<Label>("shop-gold");
            _merchantEmptyLabel = root.Q<Label>("shop-merchant-empty");
            _playerEmptyLabel = root.Q<Label>("shop-player-empty");
            _selectedSourceLabel = root.Q<Label>("shop-selected-source");
            _selectedPriceLabel = root.Q<Label>("shop-selected-price");
            _feedbackLabel = root.Q<Label>("shop-feedback");
            _buyButton = root.Q<Button>("shop-buy");
            _sellButton = root.Q<Button>("shop-sell");
            _detailView = new ItemDetailView(root.Q<VisualElement>("shop-item-detail"));

            if (_page == null
                || _merchantList == null
                || _playerList == null
                || _merchantScroll == null
                || _playerScroll == null
                || _goldLabel == null
                || _merchantEmptyLabel == null
                || _playerEmptyLabel == null
                || _selectedSourceLabel == null
                || _selectedPriceLabel == null
                || _feedbackLabel == null
                || _buyButton == null
                || _sellButton == null
                || !_detailView.IsValid)
            {
                Debug.LogError("[ShopPanelController] 商店 UXML 缺少必要的命名元素", this);
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
            for (int i = 0; i < items.Count; i++)
            {
                ShopItemSnapshot item = items[i];
                Button button = new Button(() => SelectItem(item.Item, item.Source));
                button.text = string.Empty;
                button.tooltip = $"{item.DisplayName} · {ItemDetailFormatter.GetItemTypeText(item.Type)} · {item.AffixCount} 条词缀";
                button.AddToClassList("shop-item");
                button.AddToClassList(item.Source == ShopItemSource.Merchant
                    ? "shop-item--merchant"
                    : "shop-item--player");
                button.AddToClassList(GetRarityClass(item.Rarity));

                VisualElement icon = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                };
                icon.AddToClassList("shop-item-icon");
                ItemVisualPresenter.ApplyIcon(icon, item.Detail.IconGuid);
                Label summary = new Label($"{item.DisplayName}\n{ItemDetailFormatter.GetRarityText(item.Rarity)} · {item.Price} 金币")
                {
                    pickingMode = PickingMode.Ignore,
                };
                summary.AddToClassList("shop-item-summary");
                button.Add(icon);
                button.Add(summary);
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
            _previewItem = null;
            _viewState.ActiveSource = source;
            _viewState.FocusTarget = ShopFocusTarget.Item;
            _viewState.ClearFeedback();
            UpdateSelectedListState(source, item);
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
                _detailView.Clear();
                _selectedSourceLabel.text = "来源：-";
                _selectedPriceLabel.text = "价格：-";
                _feedbackLabel.text = _viewState.HasFeedback ? _viewState.Feedback : "商店和背包均为空";
                _buyButton.SetEnabled(false);
                _sellButton.SetEnabled(false);
                _viewState.FocusTarget = ShopFocusTarget.CloseFallback;
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
            ItemInstance item = _previewItem != null ? _previewItem : _selectedItem;
            ShopItemSource source = _previewItem != null ? _previewSource : _selectedSource;

            if (TryFindSnapshot(item, source, out ShopItemSnapshot snapshot))
            {
                _detailView.Show(snapshot.Detail, ItemVisualPresenter.GetSprite(snapshot.Detail.IconGuid));
                return;
            }

            _detailView.Clear();
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
            if (_merchantScroll != null)
            {
                _viewState.Merchant.ScrollOffset = _merchantScroll.scrollOffset;
            }

            if (_playerScroll != null)
            {
                _viewState.Player.ScrollOffset = _playerScroll.scrollOffset;
            }

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

            _merchantScroll.scrollOffset = _viewState.Merchant.ScrollOffset;
            _playerScroll.scrollOffset = _viewState.Player.ScrollOffset;

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

            Dictionary<ItemInstance, Button> buttons = _selectedSource == ShopItemSource.Merchant
                ? _merchantButtons
                : _playerButtons;

            if (_selectedItem == null || !buttons.TryGetValue(_selectedItem, out Button button))
            {
                return;
            }

            ScrollView scroll = _selectedSource == ShopItemSource.Merchant ? _merchantScroll : _playerScroll;
            scroll.ScrollTo(button);
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
