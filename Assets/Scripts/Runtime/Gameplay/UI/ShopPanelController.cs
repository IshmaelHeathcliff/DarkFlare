using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimePanelView))]
    public class ShopPanelController : MonoBehaviour, IController
    {
        const int MerchantGridWidth = 10;
        const int MerchantGridMinimumHeight = 6;
        const float CellSize = 48f;
        const float CellGap = 4f;

        [SerializeField]
        RuntimePanelView _uiPanel;

        ItemWorkspace _workspace;
        GameMenuController _menu;
        ItemSourceListView _sourceList;

        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();
        readonly Dictionary<ItemInstance, Button> _merchantButtons = new Dictionary<ItemInstance, Button>();
        readonly Dictionary<ItemInstance, Button> _playerButtons = new Dictionary<ItemInstance, Button>();
        readonly ShopViewState _viewState = new ShopViewState();

        SceneSessionBinding _sessionBinding;
        LocalizationService _localizationService;
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
            return _sessionBinding.RequireArchitecture();
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
            _goldLabel.text = Localize("shop.gold", LastSnapshot.Gold);
            _merchantList.Clear();
            _merchantButtons.Clear();

            BuildList(LastSnapshot.MerchantItems, _merchantList, _merchantButtons);
            var playerItems = new List<ItemInstance>();
            foreach (ShopItemSnapshot snapshot in LastSnapshot.PlayerItems)
            {
                if (snapshot.Item != _workspace.CraftingItem) { playerItems.Add(snapshot.Item); }
            }
            _sourceList.Refresh(playerItems, item => Resolve(ItemDetailSnapshotFactory.Create(item).Name),
                item => SelectItem(item, ShopItemSource.Player), item => PreviewItem(item, ShopItemSource.Player), EndPreview);
            _playerEmptyLabel.style.display = playerItems.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
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

            return FocusPlayerSource();
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
            _sessionBinding?.Disable();
        }

        SceneSessionBindResult BindSession(IArchitecture architecture)
        {
            if (_uiPanel == null || _uiPanel.Renderer.panelSettings == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[ShopPanelController] 缺少 RuntimePanelView 或 PanelSettings，无法初始化商店", this);
                return SceneSessionBindResult.Failed;
            }

            VisualElement root = _uiPanel.Root;

            if (root == null || root.panel == null)
            {
                return SceneSessionBindResult.Retry;
            }

            _workspace = _menu.Workspace;
            if (!BindVisualTree())
            {
                return SceneSessionBindResult.Failed;
            }

            RegisterEvents();
            BindLocalization();
            _workspace.InventorySelectionChanged += OnInventorySelectionChanged;

            RefreshShop();
            SetVisible(IsVisible);
            ApplicationLog.Info(LogEventIds.GameplayUi, "[ShopPanelController] 商店面板初始化完成", this);
            return SceneSessionBindResult.Success;
        }

        void UnbindSession()
        {
            if (_buyButton != null)
            {
                _buyButton.clicked -= OnBuyClicked;
            }

            if (_sellButton != null)
            {
                _sellButton.clicked -= OnSellClicked;
            }

            if (_workspace != null)
            {
                _workspace.InventorySelectionChanged -= OnInventorySelectionChanged;

            }

            for (int i = 0; i < _eventRegistrations.Count; i++)
            {
                _eventRegistrations[i].UnRegister();
            }

            _eventRegistrations.Clear();

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged -= OnLocaleChanged;
                _localizationService = null;
            }

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
            if (_uiPanel == null)
            {
                _uiPanel = GetComponent<RuntimePanelView>();
            }

            if (_uiPanel == null && gameObject.scene.IsValid())
            {
                _uiPanel = gameObject.AddComponent<RuntimePanelView>();
            }

            _menu = GetComponent<GameMenuController>();
        }

        bool BindVisualTree()
        {
            if (_uiPanel == null || _workspace == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[ShopPanelController] 缺少 RuntimePanelView 或物品窗口宿主", this);
                return false;
            }

            VisualElement root = _uiPanel.Root;
            _page = root.Q<VisualElement>("shop-page");
            _merchantFrame = root.Q<VisualElement>("shop-merchant-frame");
            _merchantList = root.Q<VisualElement>("shop-merchant-list");
            _goldLabel = root.Q<Label>("shop-gold");
            _merchantEmptyLabel = root.Q<Label>("shop-merchant-empty");
            _selectedSourceLabel = root.Q<Label>("shop-selected-source");
            _selectedPriceLabel = root.Q<Label>("shop-selected-price");
            _playerList = root.Q("shop-player-list");
            _playerEmptyLabel = root.Q<Label>("shop-player-empty");
            _sourceList = new ItemSourceListView(_playerList, _playerButtons);
            _feedbackLabel = root.Q<Label>("shop-feedback");
            _buyButton = root.Q<Button>("shop-buy");
            _sellButton = root.Q<Button>("shop-sell");

            if (_page == null
                || _merchantFrame == null
                || _merchantList == null
                || _playerList == null || _playerEmptyLabel == null
                || _goldLabel == null
                || _merchantEmptyLabel == null
                || _selectedSourceLabel == null
                || _selectedPriceLabel == null
                || _feedbackLabel == null
                || _buyButton == null
                || _sellButton == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[ShopPanelController] 商店 UXML 缺少格子背包所需的命名元素", this);
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

            _eventRegistrations.Add(this.RegisterEvent<TradeCompletedEvent>(OnTradeCompleted));
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
            _workspace.Activate(GameMenuPage.Shop);

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
            if (!IsVisible || !_menu.IsPageAvailable(GameMenuPage.Shop) || _isTransactionInProgress || item == null)
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

        void OnTradeCompleted(TradeCompletedEvent trade)
        {
            RefreshShop();
            SetFeedback(trade.Operation == TradeOperation.Buy ? "shop.feedback.buy_succeeded" : "shop.feedback.sell_succeeded",
                Resolve(ItemDetailSnapshotFactory.Create(trade.Item).Name));
        }

        void PreviewItem(ItemInstance item, ShopItemSource source)
        {
            if (_workspace.IsDragging || !IsVisible) { return; }
            _workspace.Activate(GameMenuPage.Shop);
            _previewItem = item;
            _previewSource = source;

            RefreshSelectionClasses();
            RefreshDetail();
        }

        void EndPreview(ItemInstance item)
        {
            if (_previewItem != item)
            {
                return;
            }

            _previewItem = null;

            RefreshSelectionClasses();
            RefreshDetail();
        }

        void RefreshSelection()
        {
            RefreshSelectionClasses();

            if (!TryGetSelectedSnapshot(out ShopItemSnapshot selected))
            {
                _selectedSourceLabel.text = Localize("shop.source.empty");
                _selectedPriceLabel.text = Localize("shop.price.empty");
                _feedbackLabel.text = _viewState.HasFeedback
                    ? Resolve(_viewState.Feedback)
                    : Localize(LastSnapshot.MerchantItems.Count == 0 && LastSnapshot.PlayerItems.Count == 0
                        ? "shop.feedback.empty" : "shop.feedback.select");
                _buyButton.SetEnabled(false);
                _sellButton.SetEnabled(false);
                _viewState.FocusTarget = ShopFocusTarget.CloseFallback;
                _workspace.HidePreview(GameMenuPage.Shop);
                return;
            }

            bool isMerchantItem = selected.Source == ShopItemSource.Merchant;
            RefreshDetail();
            _selectedSourceLabel.text = Localize(isMerchantItem
                ? "shop.source.merchant"
                : "shop.source.player");
            _selectedPriceLabel.text = Localize(
                isMerchantItem ? "shop.price.buy" : "shop.price.sell",
                selected.Price);
            bool canAfford = LastSnapshot.Gold >= selected.Price;
            _buyButton.SetEnabled(isMerchantItem && canAfford);
            _sellButton.SetEnabled(!isMerchantItem);
            if (_viewState.HasFeedback)
            {
                _feedbackLabel.text = Resolve(_viewState.Feedback);
                return;
            }

            _feedbackLabel.text = isMerchantItem
                ? canAfford
                    ? Localize("shop.feedback.buy_ready")
                    : Localize("shop.feedback.insufficient_gold", selected.Price - LastSnapshot.Gold)
                : Localize("shop.feedback.sell_warning");
        }

        void RefreshSelectionClasses()
        {
            ItemInstance active = _previewItem ?? _selectedItem;
            ShopItemSource source = _previewItem != null ? _previewSource : _selectedSource;
            ItemInstance merchantItem = source == ShopItemSource.Merchant ? active : null;
            SetSelectedClass(_merchantButtons, merchantItem);
            SetSelectedClass(_playerButtons, source == ShopItemSource.Player ? active : null);
        }

        void RefreshDetail()
        {
            ItemInstance item = _previewItem;
            ShopItemSource source = _previewSource;

            if (TryFindSnapshot(item, source, out ShopItemSnapshot snapshot))
            {
                if (source == ShopItemSource.Merchant)
                {
                    _workspace.Preview(GameMenuPage.Shop, _uiPanel.Root.Q("shop-window"),
                        item,
                        Localize("shop.tooltip.merchant", snapshot.Price));
                }
                else
                {
                    _workspace.Preview(GameMenuPage.Shop, _uiPanel.Root.Q("shop-window"),
                        item,
                        Localize("shop.tooltip.player", snapshot.Price));
                }

                return;
            }

            _workspace.HidePreview(GameMenuPage.Shop);
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
            if (!IsVisible || !_menu.IsPageAvailable(GameMenuPage.Shop)) { return; }
            _viewState.ClearFeedback();

            if (!TryGetSelectedSnapshot(out ShopItemSnapshot selected)
                || selected.Source != ShopItemSource.Merchant)
            {
                SetFeedback("shop.feedback.select_merchant");
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

            SetFeedback(
                bought ? "shop.feedback.buy_succeeded" : "shop.feedback.buy_failed",
                bought ? new object[] { Resolve(selected.Detail.Name) } : null);
        }

        void OnSellClicked()
        {
            if (!IsVisible || !_menu.IsPageAvailable(GameMenuPage.Shop)) { return; }
            _viewState.ClearFeedback();

            if (!TryGetSelectedSnapshot(out ShopItemSnapshot selected)
                || selected.Source != ShopItemSource.Player)
            {
                SetFeedback("shop.feedback.select_player");
                return;
            }

            SellItem(selected);
        }

        void SellItem(ShopItemSnapshot selected)
        {
            if (selected.Source != ShopItemSource.Player)
            {
                SetFeedback("shop.feedback.select_player");
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

            SetFeedback(
                sold ? "shop.feedback.sell_succeeded" : "shop.feedback.sell_failed",
                sold ? new object[] { Resolve(selected.Detail.Name) } : null);
        }

        void SetFeedback(string entryKey, params object[] arguments)
        {
            LocalizedMessage feedback = LocalizedMessage.Ui(entryKey, arguments);
            _viewState.SetFeedback(feedback);
            _feedbackLabel.text = Resolve(feedback);
        }

        void BindLocalization()
        {
            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host)
                || ReferenceEquals(_localizationService, host.Localization))
            {
                return;
            }

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged -= OnLocaleChanged;
            }

            _localizationService = host.Localization;

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged += OnLocaleChanged;
            }
        }

        void OnLocaleChanged(string localeCode)
        {
            if (_merchantList == null)
            {
                return;
            }

            _goldLabel.text = Localize("shop.gold", LastSnapshot.Gold);
            _sourceList.RefreshTitles(item => Resolve(ItemDetailSnapshotFactory.Create(item).Name));
            RefreshSelection();
        }

        string Localize(string entryKey, params object[] arguments)
        {
            return Resolve(LocalizedMessage.Ui(entryKey, arguments));
        }

        string Resolve(LocalizedMessage message)
        {
            return _localizationService?.GetString(message)
                ?? $"[{message.TableName}.{message.EntryKey}]";
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
                || !IsVisible || _workspace.ActiveWindow != GameMenuPage.Shop)
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
                FocusPlayerSource();
                return;
            }

            if (_selectedItem == null || !_merchantButtons.TryGetValue(_selectedItem, out Button button))
            {
                return;
            }

            button.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(button);
            button.Focus();
        }

        bool FocusPlayerSource()
        {
            if (_selectedItem != null && _playerButtons.TryGetValue(_selectedItem, out Button button))
            {
                button.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(button);
                button.Focus();
                return true;
            }
            return false;
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

        void SetSelectedClass(Dictionary<ItemInstance, Button> buttons, ItemInstance selectedItem)
        {
            foreach (KeyValuePair<ItemInstance, Button> entry in buttons)
            {
                _workspace.Highlight(GameMenuPage.Shop, entry.Value, buttons == _playerButtons ? "item-source-row--selected" : "shop-item--selected", entry.Key == selectedItem);
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
