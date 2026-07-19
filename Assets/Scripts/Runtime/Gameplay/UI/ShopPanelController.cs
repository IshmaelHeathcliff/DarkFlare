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

        VisualElement _page;
        VisualElement _merchantList;
        VisualElement _playerList;
        Label _goldLabel;
        Label _merchantEmptyLabel;
        Label _playerEmptyLabel;
        Label _selectedNameLabel;
        Label _selectedSourceLabel;
        Label _selectedRarityLabel;
        Label _selectedAffixesLabel;
        Label _selectedPriceLabel;
        Label _feedbackLabel;
        Button _buyButton;
        Button _sellButton;
        ItemInstance _selectedItem;
        ShopItemSource _selectedSource;

        public ShopSnapshot LastSnapshot { get; private set; }

        public ItemInstance SelectedItem => _selectedItem;

        public ShopItemSource SelectedSource => _selectedSource;

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

            ItemInstance previousSelection = _selectedItem;
            ShopItemSource previousSource = _selectedSource;
            ShopSnapshot snapshot = this.SendQuery(new GetShopSnapshotQuery());
            LastSnapshot = snapshot;
            _goldLabel.text = $"持有金币  {snapshot.Gold}";
            _merchantList.Clear();
            _playerList.Clear();
            _merchantButtons.Clear();
            _playerButtons.Clear();

            BuildList(snapshot.MerchantItems, _merchantList, _merchantButtons);
            BuildList(snapshot.PlayerItems, _playerList, _playerButtons);
            _merchantEmptyLabel.style.display = snapshot.MerchantItems.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _playerEmptyLabel.style.display = snapshot.PlayerItems.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (ContainsItem(snapshot, previousSelection, previousSource))
            {
                _selectedItem = previousSelection;
                _selectedSource = previousSource;
            }
            else if (snapshot.MerchantItems.Count > 0)
            {
                _selectedItem = snapshot.MerchantItems[0].Item;
                _selectedSource = ShopItemSource.Merchant;
            }
            else if (snapshot.PlayerItems.Count > 0)
            {
                _selectedItem = snapshot.PlayerItems[0].Item;
                _selectedSource = ShopItemSource.Player;
            }
            else
            {
                _selectedItem = null;
                _selectedSource = ShopItemSource.Merchant;
            }

            RefreshSelection();
        }

        public void SetVisible(bool visible)
        {
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
            Dictionary<ItemInstance, Button> buttons = _selectedSource == ShopItemSource.Merchant
                ? _merchantButtons
                : _playerButtons;

            if (_selectedItem == null || !buttons.TryGetValue(_selectedItem, out Button button))
            {
                return false;
            }

            button.Focus();
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
            _page = null;
            _merchantList = null;
            _playerList = null;
            _goldLabel = null;
            _merchantEmptyLabel = null;
            _playerEmptyLabel = null;
            _selectedNameLabel = null;
            _selectedSourceLabel = null;
            _selectedRarityLabel = null;
            _selectedAffixesLabel = null;
            _selectedPriceLabel = null;
            _feedbackLabel = null;
            _buyButton = null;
            _sellButton = null;
            _selectedItem = null;
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
            _goldLabel = root.Q<Label>("shop-gold");
            _merchantEmptyLabel = root.Q<Label>("shop-merchant-empty");
            _playerEmptyLabel = root.Q<Label>("shop-player-empty");
            _selectedNameLabel = root.Q<Label>("shop-selected-name");
            _selectedSourceLabel = root.Q<Label>("shop-selected-source");
            _selectedRarityLabel = root.Q<Label>("shop-selected-rarity");
            _selectedAffixesLabel = root.Q<Label>("shop-selected-affixes");
            _selectedPriceLabel = root.Q<Label>("shop-selected-price");
            _feedbackLabel = root.Q<Label>("shop-feedback");
            _buyButton = root.Q<Button>("shop-buy");
            _sellButton = root.Q<Button>("shop-sell");

            if (_page == null
                || _merchantList == null
                || _playerList == null
                || _goldLabel == null
                || _merchantEmptyLabel == null
                || _playerEmptyLabel == null
                || _selectedNameLabel == null
                || _selectedSourceLabel == null
                || _selectedRarityLabel == null
                || _selectedAffixesLabel == null
                || _selectedPriceLabel == null
                || _feedbackLabel == null
                || _buyButton == null
                || _sellButton == null)
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
                button.text = $"{item.DisplayName}\n{GetRarityText(item.Rarity)} · {item.Price} 金币";
                button.tooltip = $"{item.DisplayName} · {GetItemTypeText(item.Type)} · {item.AffixCount} 条词缀";
                button.AddToClassList("shop-item");
                button.AddToClassList(item.Source == ShopItemSource.Merchant
                    ? "shop-item--merchant"
                    : "shop-item--player");
                button.AddToClassList(GetRarityClass(item.Rarity));
                list.Add(button);
                buttons.Add(item.Item, button);
            }
        }

        void SelectItem(ItemInstance item, ShopItemSource source)
        {
            _selectedItem = item;
            _selectedSource = source;
            RefreshSelection();
        }

        void RefreshSelection()
        {
            SetSelectedClass(_merchantButtons, _selectedSource == ShopItemSource.Merchant ? _selectedItem : null);
            SetSelectedClass(_playerButtons, _selectedSource == ShopItemSource.Player ? _selectedItem : null);

            if (!TryGetSelectedSnapshot(out ShopItemSnapshot selected))
            {
                _selectedNameLabel.text = "未选择物品";
                _selectedSourceLabel.text = "来源：-";
                _selectedRarityLabel.text = "稀有度：-";
                _selectedAffixesLabel.text = "词缀：-";
                _selectedPriceLabel.text = "价格：-";
                _feedbackLabel.text = "商店和背包均为空";
                _buyButton.SetEnabled(false);
                _sellButton.SetEnabled(false);
                return;
            }

            bool isMerchantItem = selected.Source == ShopItemSource.Merchant;
            _selectedNameLabel.text = selected.DisplayName;
            _selectedSourceLabel.text = $"来源：{(isMerchantItem ? "商人库存" : "玩家背包")} · {GetItemTypeText(selected.Type)}";
            _selectedRarityLabel.text = $"稀有度：{GetRarityText(selected.Rarity)}";
            _selectedAffixesLabel.text = $"词缀：{selected.AffixCount} 条";
            _selectedPriceLabel.text = $"{(isMerchantItem ? "买入" : "卖出")}价格：{selected.Price} 金币";
            bool canAfford = LastSnapshot.Gold >= selected.Price;
            _buyButton.SetEnabled(isMerchantItem && canAfford);
            _sellButton.SetEnabled(!isMerchantItem);
            _feedbackLabel.text = isMerchantItem
                ? canAfford
                    ? "确认购买后，物品将尝试放入背包"
                    : $"金币不足，还需要 {selected.Price - LastSnapshot.Gold}"
                : "出售后物品会离开背包，首版不提供回购";
        }

        bool TryGetSelectedSnapshot(out ShopItemSnapshot selected)
        {
            IReadOnlyList<ShopItemSnapshot> items = _selectedSource == ShopItemSource.Merchant
                ? LastSnapshot.MerchantItems
                : LastSnapshot.PlayerItems;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item == _selectedItem)
                {
                    selected = items[i];
                    return true;
                }
            }

            selected = default;
            return false;
        }

        void OnBuyClicked()
        {
            if (!TryGetSelectedSnapshot(out ShopItemSnapshot selected)
                || selected.Source != ShopItemSource.Merchant)
            {
                _feedbackLabel.text = "请选择商人库存中的物品";
                return;
            }

            bool bought = this.SendCommand(new BuyItemCommand(selected.Item));

            if (!bought)
            {
                RefreshShop();
                _feedbackLabel.text = "购买失败：请检查金币、背包空间或库存";
                return;
            }

            _feedbackLabel.text = $"已购买 {selected.DisplayName}";
            FocusDefault();
        }

        void OnSellClicked()
        {
            if (!TryGetSelectedSnapshot(out ShopItemSnapshot selected)
                || selected.Source != ShopItemSource.Player)
            {
                _feedbackLabel.text = "请选择背包中的物品";
                return;
            }

            bool sold = this.SendCommand(new SellItemCommand(selected.Item));

            if (!sold)
            {
                RefreshShop();
                _feedbackLabel.text = "出售失败：物品已不在背包中";
                return;
            }

            _feedbackLabel.text = $"已出售 {selected.DisplayName}";
            FocusDefault();
        }

        static bool ContainsItem(ShopSnapshot snapshot, ItemInstance item, ShopItemSource source)
        {
            if (item == null)
            {
                return false;
            }

            IReadOnlyList<ShopItemSnapshot> items = source == ShopItemSource.Merchant
                ? snapshot.MerchantItems
                : snapshot.PlayerItems;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item == item)
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
                entry.Value.RemoveFromClassList("shop-item--selected");

                if (entry.Key == selectedItem)
                {
                    entry.Value.AddToClassList("shop-item--selected");
                }
            }
        }

        static string GetItemTypeText(ItemType type)
        {
            switch (type)
            {
                case ItemType.Weapon:
                    return "武器";
                case ItemType.Armor:
                    return "护甲";
                case ItemType.Accessory:
                    return "饰品";
                case ItemType.Material:
                    return "材料";
                case ItemType.Currency:
                    return "货币";
                default:
                    return type.ToString();
            }
        }

        static string GetRarityText(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Normal:
                    return "普通";
                case ItemRarity.Magic:
                    return "魔法";
                case ItemRarity.Rare:
                    return "稀有";
                case ItemRarity.Unique:
                    return "传奇";
                default:
                    return rarity.ToString();
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
