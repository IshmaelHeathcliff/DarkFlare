using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class CraftingPanelController : MonoBehaviour, IController
    {
        const string SelectedItemClass = "crafting-item--selected";
        const string SelectedAffixClass = "crafting-affix--selected";

        readonly Dictionary<ItemInstance, Button> _itemButtons = new Dictionary<ItemInstance, Button>();
        readonly Dictionary<AffixInstance, Button> _affixButtons = new Dictionary<AffixInstance, Button>();
        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();

        [SerializeField]
        UIDocument _document;

        VisualElement _page;
        VisualElement _itemList;
        VisualElement _affixList;
        Label _itemEmptyLabel;
        Label _affixEmptyLabel;
        Label _goldLabel;
        Label _selectedNameLabel;
        Label _selectedTypeLabel;
        Label _selectedRarityLabel;
        Label _selectedCapacityLabel;
        Label _selectedValueLabel;
        Label _selectedAffixLabel;
        Label _selectedAffixDetailLabel;
        Label _feedbackLabel;
        Button _addAffixButton;
        Button _rerollAllButton;
        Button _removeRerollButton;
        Button _upgradeAffixButton;
        ItemInstance _selectedItem;
        AffixInstance _selectedAffix;

        public CraftingSnapshot LastSnapshot { get; private set; }

        public bool IsVisible { get; private set; }

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void RefreshCrafting()
        {
            if (_itemList == null)
            {
                return;
            }

            ItemInstance previousItem = _selectedItem;
            AffixInstance previousAffix = _selectedAffix;
            LastSnapshot = this.SendQuery(new GetCraftingSnapshotQuery());
            _selectedItem = null;
            _selectedAffix = null;
            _itemButtons.Clear();
            _affixButtons.Clear();
            _itemList.Clear();
            _affixList.Clear();

            for (int i = 0; i < LastSnapshot.Items.Count; i++)
            {
                CraftingItemSnapshot item = LastSnapshot.Items[i];
                Button button = CreateItemButton(item);
                _itemList.Add(button);
                _itemButtons.Add(item.Item, button);

                if (item.Item == previousItem)
                {
                    _selectedItem = item.Item;
                }
            }

            if (_selectedItem == null && LastSnapshot.Items.Count > 0)
            {
                _selectedItem = LastSnapshot.Items[0].Item;
            }

            BuildAffixList(previousAffix);
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
                RefreshCrafting();
            }
        }

        public bool FocusDefault()
        {
            if (_selectedItem == null || !_itemButtons.TryGetValue(_selectedItem, out Button button))
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
            RefreshCrafting();
            SetVisible(IsVisible);
            Debug.Log("[CraftingPanelController] 打造面板初始化完成", this);
        }

        void OnDisable()
        {
            UnbindButtons();

            for (int i = 0; i < _eventRegistrations.Count; i++)
            {
                _eventRegistrations[i].UnRegister();
            }

            _eventRegistrations.Clear();
            _itemButtons.Clear();
            _affixButtons.Clear();
            _page = null;
            _itemList = null;
            _affixList = null;
            _itemEmptyLabel = null;
            _affixEmptyLabel = null;
            _goldLabel = null;
            _selectedNameLabel = null;
            _selectedTypeLabel = null;
            _selectedRarityLabel = null;
            _selectedCapacityLabel = null;
            _selectedValueLabel = null;
            _selectedAffixLabel = null;
            _selectedAffixDetailLabel = null;
            _feedbackLabel = null;
            _addAffixButton = null;
            _rerollAllButton = null;
            _removeRerollButton = null;
            _upgradeAffixButton = null;
            _selectedItem = null;
            _selectedAffix = null;
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
                Debug.LogError("[CraftingPanelController] 缺少 UIDocument，无法初始化打造面板", this);
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            _page = root.Q<VisualElement>("crafting-page");
            _itemList = root.Q<VisualElement>("crafting-item-list");
            _affixList = root.Q<VisualElement>("crafting-affix-list");
            _itemEmptyLabel = root.Q<Label>("crafting-item-empty");
            _affixEmptyLabel = root.Q<Label>("crafting-affix-empty");
            _goldLabel = root.Q<Label>("crafting-gold");
            _selectedNameLabel = root.Q<Label>("crafting-selected-name");
            _selectedTypeLabel = root.Q<Label>("crafting-selected-type");
            _selectedRarityLabel = root.Q<Label>("crafting-selected-rarity");
            _selectedCapacityLabel = root.Q<Label>("crafting-selected-capacity");
            _selectedValueLabel = root.Q<Label>("crafting-selected-value");
            _selectedAffixLabel = root.Q<Label>("crafting-selected-affix");
            _selectedAffixDetailLabel = root.Q<Label>("crafting-selected-affix-detail");
            _feedbackLabel = root.Q<Label>("crafting-feedback");
            _addAffixButton = root.Q<Button>("crafting-add-affix");
            _rerollAllButton = root.Q<Button>("crafting-reroll-all");
            _removeRerollButton = root.Q<Button>("crafting-remove-reroll");
            _upgradeAffixButton = root.Q<Button>("crafting-upgrade-affix");

            if (_page == null
                || _itemList == null
                || _affixList == null
                || _itemEmptyLabel == null
                || _affixEmptyLabel == null
                || _goldLabel == null
                || _selectedNameLabel == null
                || _selectedTypeLabel == null
                || _selectedRarityLabel == null
                || _selectedCapacityLabel == null
                || _selectedValueLabel == null
                || _selectedAffixLabel == null
                || _selectedAffixDetailLabel == null
                || _feedbackLabel == null
                || _addAffixButton == null
                || _rerollAllButton == null
                || _removeRerollButton == null
                || _upgradeAffixButton == null)
            {
                Debug.LogError("[CraftingPanelController] 打造 UXML 缺少必要的命名元素", this);
                return false;
            }

            _addAffixButton.clicked += OnAddAffixClicked;
            _rerollAllButton.clicked += OnRerollAllClicked;
            _removeRerollButton.clicked += OnRemoveRerollClicked;
            _upgradeAffixButton.clicked += OnUpgradeAffixClicked;
            return true;
        }

        void UnbindButtons()
        {
            if (_addAffixButton != null)
            {
                _addAffixButton.clicked -= OnAddAffixClicked;
            }

            if (_rerollAllButton != null)
            {
                _rerollAllButton.clicked -= OnRerollAllClicked;
            }

            if (_removeRerollButton != null)
            {
                _removeRerollButton.clicked -= OnRemoveRerollClicked;
            }

            if (_upgradeAffixButton != null)
            {
                _upgradeAffixButton.clicked -= OnUpgradeAffixClicked;
            }
        }

        void RegisterEvents()
        {
            if (_eventRegistrations.Count > 0)
            {
                return;
            }

            _eventRegistrations.Add(this.RegisterEvent<InventoryChangedEvent>(_ => RefreshCrafting()));
            _eventRegistrations.Add(this.RegisterEvent<GoldChangedEvent>(_ => RefreshCrafting()));
            _eventRegistrations.Add(this.RegisterEvent<ItemCraftedEvent>(_ => RefreshCrafting()));
        }

        Button CreateItemButton(CraftingItemSnapshot item)
        {
            Button button = new Button(() => SelectItem(item.Item));
            int affixCount = item.PrefixCount + item.SuffixCount;
            button.text = $"{item.DisplayName}\n{GetRarityText(item.Rarity)} · {affixCount} 条 · 价值 {item.Value}";
            button.tooltip = $"{item.DisplayName} · 售价 {item.SellPrice} · 前缀 {item.PrefixCount}/{item.MaxPrefixCount} · 后缀 {item.SuffixCount}/{item.MaxSuffixCount}";
            button.AddToClassList("crafting-item");
            button.AddToClassList(GetRarityClass(item.Rarity));
            return button;
        }

        void SelectItem(ItemInstance item)
        {
            _selectedItem = item;
            _selectedAffix = null;
            BuildAffixList(null);
            RefreshSelection();
        }

        void BuildAffixList(AffixInstance previousAffix)
        {
            _affixButtons.Clear();
            _affixList.Clear();

            if (!TryGetSelectedItem(out CraftingItemSnapshot item))
            {
                return;
            }

            for (int i = 0; i < item.Affixes.Count; i++)
            {
                CraftingAffixSnapshot affix = item.Affixes[i];
                Button button = new Button(() => SelectAffix(affix.Affix));
                button.text = $"{GetAffixTypeText(affix.Type)} · {affix.DisplayName}\n{affix.ModifierSummary}";
                button.tooltip = $"{affix.DisplayName} · 总数值 {affix.TotalValue:0.##}";
                button.AddToClassList("crafting-affix");
                button.AddToClassList(affix.Type == AffixType.Prefix
                    ? "crafting-affix--prefix"
                    : "crafting-affix--suffix");
                _affixList.Add(button);
                _affixButtons.Add(affix.Affix, button);

                if (affix.Affix == previousAffix)
                {
                    _selectedAffix = affix.Affix;
                }
            }

            if (_selectedAffix == null && item.Affixes.Count > 0)
            {
                _selectedAffix = item.Affixes[0].Affix;
            }
        }

        void SelectAffix(AffixInstance affix)
        {
            _selectedAffix = affix;
            RefreshSelection();
        }

        void RefreshSelection()
        {
            _goldLabel.text = $"持有金币  {LastSnapshot.Gold}";
            _addAffixButton.text = $"添加词缀 · {LastSnapshot.AddAffixCost} 金币";
            _rerollAllButton.text = $"重随全部 · {LastSnapshot.RerollAllCost} 金币";
            _removeRerollButton.text = $"移除并重随 · {LastSnapshot.RemoveRerollCost} 金币";
            _upgradeAffixButton.text = $"提升数值 · {LastSnapshot.UpgradeAffixCost} 金币";
            _itemEmptyLabel.style.display = LastSnapshot.Items.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (KeyValuePair<ItemInstance, Button> entry in _itemButtons)
            {
                entry.Value.EnableInClassList(SelectedItemClass, entry.Key == _selectedItem);
            }

            foreach (KeyValuePair<AffixInstance, Button> entry in _affixButtons)
            {
                entry.Value.EnableInClassList(SelectedAffixClass, entry.Key == _selectedAffix);
            }

            if (!TryGetSelectedItem(out CraftingItemSnapshot item))
            {
                ShowEmptySelection();
                return;
            }

            _affixEmptyLabel.style.display = item.Affixes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _selectedNameLabel.text = item.DisplayName;
            _selectedTypeLabel.text = $"类型：{GetItemTypeText(item.Type)}";
            _selectedRarityLabel.text = $"稀有度：{GetRarityText(item.Rarity)}";
            _selectedCapacityLabel.text = $"词缀容量：前缀 {item.PrefixCount}/{item.MaxPrefixCount} · 后缀 {item.SuffixCount}/{item.MaxSuffixCount}";
            _selectedValueLabel.text = $"物品价值：{item.Value} · 出售价：{item.SellPrice}";

            if (TryGetSelectedAffix(item, out CraftingAffixSnapshot affix))
            {
                _selectedAffixLabel.text = $"{GetAffixTypeText(affix.Type)} · {affix.DisplayName}";
                _selectedAffixDetailLabel.text = $"{affix.ModifierSummary} · 总数值 {affix.TotalValue:0.##}";
            }
            else
            {
                _selectedAffixLabel.text = "未选择词缀";
                _selectedAffixDetailLabel.text = "添加词缀不需要选择现有词缀";
            }

            RefreshActions(item);
        }

        void ShowEmptySelection()
        {
            _affixEmptyLabel.style.display = DisplayStyle.Flex;
            _selectedNameLabel.text = "未选择物品";
            _selectedTypeLabel.text = "类型：-";
            _selectedRarityLabel.text = "稀有度：-";
            _selectedCapacityLabel.text = "词缀容量：-";
            _selectedValueLabel.text = "物品价值：-";
            _selectedAffixLabel.text = "未选择词缀";
            _selectedAffixDetailLabel.text = "选择词缀后可移除重随或提升数值";
            _feedbackLabel.text = LastSnapshot.IsConfigured ? "背包中没有可打造物品" : "打造配置尚未加载";
            SetActionEnabled(_addAffixButton, false);
            SetActionEnabled(_rerollAllButton, false);
            SetActionEnabled(_removeRerollButton, false);
            SetActionEnabled(_upgradeAffixButton, false);
        }

        void RefreshActions(CraftingItemSnapshot item)
        {
            bool configured = LastSnapshot.IsConfigured;
            bool hasAffix = item.Affixes.Count > 0;
            bool hasSelectedAffix = _selectedAffix != null;
            SetActionEnabled(
                _addAffixButton,
                configured && item.HasAffixCapacity && CanAfford(CraftOperation.AddAffix));
            SetActionEnabled(
                _rerollAllButton,
                configured && hasAffix && CanAfford(CraftOperation.RerollAll));
            SetActionEnabled(
                _removeRerollButton,
                configured && hasSelectedAffix && CanAfford(CraftOperation.RemoveReroll));
            SetActionEnabled(
                _upgradeAffixButton,
                configured && hasSelectedAffix && CanAfford(CraftOperation.UpgradeAffix));

            if (!configured)
            {
                _feedbackLabel.text = "打造配置尚未加载";
            }
            else if (!CanAfford(CraftOperation.AddAffix)
                && !CanAfford(CraftOperation.RerollAll)
                && !CanAfford(CraftOperation.RemoveReroll)
                && !CanAfford(CraftOperation.UpgradeAffix))
            {
                _feedbackLabel.text = "金币不足，无法进行打造";
            }
            else if (!hasAffix)
            {
                _feedbackLabel.text = "当前没有词缀，可先添加一个随机词缀";
            }
            else
            {
                _feedbackLabel.text = "选择打造方式；移除重随和提升数值会作用于选中词缀";
            }
        }

        bool TryGetSelectedItem(out CraftingItemSnapshot selected)
        {
            for (int i = 0; i < LastSnapshot.Items.Count; i++)
            {
                if (LastSnapshot.Items[i].Item == _selectedItem)
                {
                    selected = LastSnapshot.Items[i];
                    return true;
                }
            }

            selected = default;
            return false;
        }

        bool TryGetSelectedAffix(CraftingItemSnapshot item, out CraftingAffixSnapshot selected)
        {
            for (int i = 0; i < item.Affixes.Count; i++)
            {
                if (item.Affixes[i].Affix == _selectedAffix)
                {
                    selected = item.Affixes[i];
                    return true;
                }
            }

            selected = default;
            return false;
        }

        bool CanAfford(CraftOperation operation)
        {
            return LastSnapshot.Gold >= LastSnapshot.GetCost(operation);
        }

        void OnAddAffixClicked()
        {
            Craft(CraftOperation.AddAffix, null);
        }

        void OnRerollAllClicked()
        {
            Craft(CraftOperation.RerollAll, null);
        }

        void OnRemoveRerollClicked()
        {
            Craft(CraftOperation.RemoveReroll, _selectedAffix);
        }

        void OnUpgradeAffixClicked()
        {
            Craft(CraftOperation.UpgradeAffix, _selectedAffix);
        }

        void Craft(CraftOperation operation, AffixInstance targetAffix)
        {
            if (_selectedItem == null)
            {
                _feedbackLabel.text = "请先选择背包物品";
                return;
            }

            string itemName = _selectedItem.BaseDefinition != null
                ? _selectedItem.BaseDefinition.DisplayName
                : _selectedItem.InstanceId;
            bool crafted = this.SendCommand(new CraftItemCommand(operation, _selectedItem, targetAffix));
            RefreshCrafting();

            if (!crafted)
            {
                _feedbackLabel.text = "打造未生效：请检查金币、词缀容量和可用词缀池";
                FocusDefault();
                return;
            }

            _feedbackLabel.text = $"{itemName}：{GetOperationText(operation)}完成";
            FocusDefault();
        }

        static void SetActionEnabled(Button button, bool enabled)
        {
            button.SetEnabled(enabled);
        }

        static string GetOperationText(CraftOperation operation)
        {
            switch (operation)
            {
                case CraftOperation.AddAffix:
                    return "添加词缀";
                case CraftOperation.RerollAll:
                    return "重随全部";
                case CraftOperation.RemoveReroll:
                    return "移除并重随";
                case CraftOperation.UpgradeAffix:
                    return "提升数值";
                default:
                    return operation.ToString();
            }
        }

        static string GetAffixTypeText(AffixType type)
        {
            return type == AffixType.Prefix ? "前缀" : type == AffixType.Suffix ? "后缀" : "固有";
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
                    return "crafting-item--magic";
                case ItemRarity.Rare:
                    return "crafting-item--rare";
                case ItemRarity.Unique:
                    return "crafting-item--unique";
                default:
                    return "crafting-item--normal";
            }
        }
    }
}
