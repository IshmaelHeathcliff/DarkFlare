using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(InventoryPanelController))]
    public class CraftingPanelController : MonoBehaviour, IController
    {
        const string SelectedScopeClass = "crafting-scope--selected";

        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();

        [SerializeField]
        UIDocument _document;

        [SerializeField]
        InventoryPanelController _inventoryPanel;

        VisualElement _page;
        Label _goldLabel;
        Label _selectedRarityLabel;
        Label _selectedCapacityLabel;
        Label _selectedValueLabel;
        Label _feedbackLabel;
        Label _resultLabel;
        Button _upgradeRarityButton;
        Button _resetNormalButton;
        Button _scopeAnyButton;
        Button _scopePrefixButton;
        Button _scopeSuffixButton;
        Button _rerollAffixesButton;
        Button _addAffixButton;
        Button _removeAffixButton;
        Button _rerollValuesButton;
        ItemInstance _selectedItem;
        CraftingAffixScope _scope = CraftingAffixScope.Any;
        string _resultText = string.Empty;

        public CraftingSnapshot LastSnapshot { get; private set; }

        public bool IsVisible { get; private set; }

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void RefreshCrafting()
        {
            if (_page == null)
            {
                return;
            }

            LastSnapshot = this.SendQuery(new GetCraftingSnapshotQuery());
            _selectedItem = FindInventorySelection();
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
                _resultText = string.Empty;
                RefreshCrafting();
            }
        }

        public bool FocusDefault()
        {
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
            RefreshCrafting();
            SetVisible(IsVisible);
            Debug.Log("[CraftingPanelController] 随机打造工作台初始化完成", this);
        }

        void OnDisable()
        {
            UnbindButtons();

            if (_inventoryPanel != null)
            {
                _inventoryPanel.SelectionChanged -= OnInventorySelectionChanged;
            }

            for (int i = 0; i < _eventRegistrations.Count; i++)
            {
                _eventRegistrations[i].UnRegister();
            }

            _eventRegistrations.Clear();
            _page = null;
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

            if (_inventoryPanel == null)
            {
                _inventoryPanel = GetComponent<InventoryPanelController>();
            }
        }

        bool BindVisualTree()
        {
            if (_document == null || _inventoryPanel == null)
            {
                Debug.LogError("[CraftingPanelController] 缺少 UIDocument 或共享背包控制器", this);
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            _page = root.Q<VisualElement>("crafting-page");
            _goldLabel = root.Q<Label>("crafting-gold");
            _selectedRarityLabel = root.Q<Label>("crafting-selected-rarity");
            _selectedCapacityLabel = root.Q<Label>("crafting-selected-capacity");
            _selectedValueLabel = root.Q<Label>("crafting-selected-value");
            _feedbackLabel = root.Q<Label>("crafting-feedback");
            _resultLabel = root.Q<Label>("crafting-result");
            _upgradeRarityButton = root.Q<Button>("crafting-upgrade-rarity");
            _resetNormalButton = root.Q<Button>("crafting-reset-normal");
            _scopeAnyButton = root.Q<Button>("crafting-scope-any");
            _scopePrefixButton = root.Q<Button>("crafting-scope-prefix");
            _scopeSuffixButton = root.Q<Button>("crafting-scope-suffix");
            _rerollAffixesButton = root.Q<Button>("crafting-reroll-affixes");
            _addAffixButton = root.Q<Button>("crafting-add-affix");
            _removeAffixButton = root.Q<Button>("crafting-remove-affix");
            _rerollValuesButton = root.Q<Button>("crafting-reroll-values");

            if (_page == null
                || _goldLabel == null
                || _selectedRarityLabel == null
                || _selectedCapacityLabel == null
                || _selectedValueLabel == null
                || _feedbackLabel == null
                || _resultLabel == null
                || _upgradeRarityButton == null
                || _resetNormalButton == null
                || _scopeAnyButton == null
                || _scopePrefixButton == null
                || _scopeSuffixButton == null
                || _rerollAffixesButton == null
                || _addAffixButton == null
                || _removeAffixButton == null
                || _rerollValuesButton == null)
            {
                Debug.LogError("[CraftingPanelController] 打造 UXML 缺少随机打造工作台所需元素", this);
                return false;
            }

            BindButtons();
            return true;
        }

        void BindButtons()
        {
            _upgradeRarityButton.clicked += OnUpgradeRarityClicked;
            _resetNormalButton.clicked += OnResetNormalClicked;
            _scopeAnyButton.clicked += OnScopeAnyClicked;
            _scopePrefixButton.clicked += OnScopePrefixClicked;
            _scopeSuffixButton.clicked += OnScopeSuffixClicked;
            _rerollAffixesButton.clicked += OnRerollAffixesClicked;
            _addAffixButton.clicked += OnAddAffixClicked;
            _removeAffixButton.clicked += OnRemoveAffixClicked;
            _rerollValuesButton.clicked += OnRerollValuesClicked;
        }

        void UnbindButtons()
        {
            if (_upgradeRarityButton == null)
            {
                return;
            }

            _upgradeRarityButton.clicked -= OnUpgradeRarityClicked;
            _resetNormalButton.clicked -= OnResetNormalClicked;
            _scopeAnyButton.clicked -= OnScopeAnyClicked;
            _scopePrefixButton.clicked -= OnScopePrefixClicked;
            _scopeSuffixButton.clicked -= OnScopeSuffixClicked;
            _rerollAffixesButton.clicked -= OnRerollAffixesClicked;
            _addAffixButton.clicked -= OnAddAffixClicked;
            _removeAffixButton.clicked -= OnRemoveAffixClicked;
            _rerollValuesButton.clicked -= OnRerollValuesClicked;
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

        void OnInventorySelectionChanged(ItemInstance item)
        {
            if (!IsVisible)
            {
                return;
            }

            _selectedItem = item;
            _resultText = string.Empty;
            RefreshCrafting();
        }

        ItemInstance FindInventorySelection()
        {
            ItemInstance selected = _inventoryPanel.SelectedItem;

            for (int i = 0; i < LastSnapshot.Items.Count; i++)
            {
                if (LastSnapshot.Items[i].Item == selected)
                {
                    return selected;
                }
            }

            return null;
        }

        void RefreshSelection()
        {
            _goldLabel.text = $"持有金币  {LastSnapshot.Gold}";
            _scopeAnyButton.EnableInClassList(SelectedScopeClass, _scope == CraftingAffixScope.Any);
            _scopePrefixButton.EnableInClassList(SelectedScopeClass, _scope == CraftingAffixScope.Prefix);
            _scopeSuffixButton.EnableInClassList(SelectedScopeClass, _scope == CraftingAffixScope.Suffix);
            _resultLabel.text = _resultText;
            _resultLabel.style.display = string.IsNullOrEmpty(_resultText)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            if (!TryGetSelectedItem(out CraftingItemSnapshot item))
            {
                ShowEmptySelection();
                return;
            }

            int totalCount = item.PrefixCount + item.SuffixCount;
            _selectedRarityLabel.text = $"{item.DisplayName} · {ItemDetailFormatter.GetRarityText(item.Rarity)}";
            _selectedCapacityLabel.text = $"词缀：前缀 {item.PrefixCount}/{item.MaxPrefixCount} · 后缀 {item.SuffixCount}/{item.MaxSuffixCount} · 总数 {totalCount}/{item.MinimumTotalCount}-{item.MaximumTotalCount}";
            _selectedValueLabel.text = $"物品价值：{item.Value} · 出售价：{item.SellPrice}";
            RefreshButton(_upgradeRarityButton, item, CraftOperation.UpgradeRarity, CraftingAffixScope.Any, "提升稀有度");
            RefreshButton(_resetNormalButton, item, CraftOperation.ResetToNormal, CraftingAffixScope.Any, "还原为普通");
            RefreshButton(_rerollAffixesButton, item, CraftOperation.RerollAffixes, _scope, "随机全部词缀");
            RefreshButton(_addAffixButton, item, CraftOperation.AddAffix, _scope, "随机增加一条");
            RefreshButton(_removeAffixButton, item, CraftOperation.RemoveAffix, _scope, "随机移除一条");
            RefreshButton(_rerollValuesButton, item, CraftOperation.RerollAffixValues, _scope, "重随一条数值");

            _feedbackLabel.text = !LastSnapshot.IsConfigured
                ? "打造配置尚未加载"
                : _scope == CraftingAffixScope.Any
                    ? "任意范围：从全部可用前后缀中随机，按基础价格结算"
                    : $"精准{GetScopeText(_scope)}：只影响{GetScopeText(_scope)}，价格为任意范围的 3 倍";
        }

        void ShowEmptySelection()
        {
            _selectedRarityLabel.text = "未选择物品";
            _selectedCapacityLabel.text = "词缀容量：-";
            _selectedValueLabel.text = "物品价值：-";
            _feedbackLabel.text = !LastSnapshot.IsConfigured
                ? "打造配置尚未加载"
                : LastSnapshot.Items.Count == 0
                    ? "背包中没有可打造物品"
                    : "从玩家背包选择要打造的物品";
            SetAllActionsEnabled(false);
            SetButtonTextWithoutCost();
        }

        void RefreshButton(
            Button button,
            CraftingItemSnapshot item,
            CraftOperation operation,
            CraftingAffixScope scope,
            string label)
        {
            if (!item.TryGetAction(operation, scope, out CraftingActionSnapshot action))
            {
                button.text = label;
                button.SetEnabled(false);
                return;
            }

            button.text = $"{label} · {action.Cost} 金币";
            button.tooltip = action.IsAvailable
                ? $"{label}，消耗 {action.Cost} 金币"
                : GetFailureText(action.FailureReason);
            button.SetEnabled(action.IsAvailable);
        }

        void SetAllActionsEnabled(bool enabled)
        {
            _upgradeRarityButton.SetEnabled(enabled);
            _resetNormalButton.SetEnabled(enabled);
            _rerollAffixesButton.SetEnabled(enabled);
            _addAffixButton.SetEnabled(enabled);
            _removeAffixButton.SetEnabled(enabled);
            _rerollValuesButton.SetEnabled(enabled);
        }

        void SetButtonTextWithoutCost()
        {
            _upgradeRarityButton.text = "提升稀有度";
            _resetNormalButton.text = "还原为普通";
            _rerollAffixesButton.text = "随机全部词缀";
            _addAffixButton.text = "随机增加一条";
            _removeAffixButton.text = "随机移除一条";
            _rerollValuesButton.text = "重随一条数值";
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

        void OnUpgradeRarityClicked()
        {
            Craft(CraftOperation.UpgradeRarity, CraftingAffixScope.Any);
        }

        void OnResetNormalClicked()
        {
            Craft(CraftOperation.ResetToNormal, CraftingAffixScope.Any);
        }

        void OnScopeAnyClicked()
        {
            SetScope(CraftingAffixScope.Any);
        }

        void OnScopePrefixClicked()
        {
            SetScope(CraftingAffixScope.Prefix);
        }

        void OnScopeSuffixClicked()
        {
            SetScope(CraftingAffixScope.Suffix);
        }

        void OnRerollAffixesClicked()
        {
            Craft(CraftOperation.RerollAffixes, _scope);
        }

        void OnAddAffixClicked()
        {
            Craft(CraftOperation.AddAffix, _scope);
        }

        void OnRemoveAffixClicked()
        {
            Craft(CraftOperation.RemoveAffix, _scope);
        }

        void OnRerollValuesClicked()
        {
            Craft(CraftOperation.RerollAffixValues, _scope);
        }

        void SetScope(CraftingAffixScope scope)
        {
            _scope = scope;
            _resultText = string.Empty;
            RefreshSelection();
        }

        void Craft(CraftOperation operation, CraftingAffixScope scope)
        {
            if (_selectedItem == null)
            {
                _feedbackLabel.text = "请先选择背包物品";
                return;
            }

            CraftingResult result = this.SendCommand(new CraftItemCommand(operation, scope, _selectedItem));
            _resultText = result.Succeeded
                ? BuildSuccessText(result)
                : $"未生效：{GetFailureText(result.FailureReason)}";
            RefreshCrafting();
            FocusDefault();
        }

        static string BuildSuccessText(CraftingResult result)
        {
            int previousCount = result.PreviousPrefixes.Count + result.PreviousSuffixes.Count;
            int currentCount = result.CurrentPrefixes.Count + result.CurrentSuffixes.Count;
            return $"完成：{GetOperationText(result.Operation)} · {GetScopeText(result.Scope)} · 花费 {result.Cost} 金币\n"
                + $"{ItemDetailFormatter.GetRarityText(result.PreviousRarity)} {previousCount} 条 → "
                + $"{ItemDetailFormatter.GetRarityText(result.CurrentRarity)} {currentCount} 条";
        }

        static string GetOperationText(CraftOperation operation)
        {
            switch (operation)
            {
                case CraftOperation.UpgradeRarity:
                    return "提升稀有度";
                case CraftOperation.ResetToNormal:
                    return "还原普通";
                case CraftOperation.RerollAffixes:
                    return "随机全部词缀";
                case CraftOperation.AddAffix:
                    return "增加词缀";
                case CraftOperation.RemoveAffix:
                    return "移除词缀";
                case CraftOperation.RerollAffixValues:
                    return "重随数值";
                default:
                    return operation.ToString();
            }
        }

        static string GetScopeText(CraftingAffixScope scope)
        {
            switch (scope)
            {
                case CraftingAffixScope.Prefix:
                    return "前缀";
                case CraftingAffixScope.Suffix:
                    return "后缀";
                default:
                    return "任意";
            }
        }

        static string GetFailureText(CraftingFailureReason reason)
        {
            switch (reason)
            {
                case CraftingFailureReason.NotConfigured:
                    return "打造配置尚未加载";
                case CraftingFailureReason.ItemMissing:
                case CraftingFailureReason.ItemNotInInventory:
                    return "物品不在玩家背包中";
                case CraftingFailureReason.InsufficientGold:
                    return "金币不足";
                case CraftingFailureReason.MaximumRarity:
                    return "已达到最高稀有度";
                case CraftingFailureReason.NoChange:
                    return "当前状态无需执行该操作";
                case CraftingFailureReason.NoCapacity:
                    return "当前范围已达到词缀容量";
                case CraftingFailureReason.ScopeHasNoAffix:
                    return "当前范围没有可操作词缀";
                case CraftingFailureReason.NoVariableAffix:
                    return "当前范围没有可重随数值的词缀";
                case CraftingFailureReason.NoLegalAffix:
                    return "词缀池没有合法候选";
                case CraftingFailureReason.CannotBuildCompleteResult:
                    return "词缀池无法生成满足稀有度规则的完整结果";
                case CraftingFailureReason.CommitFailed:
                    return "状态提交失败，未扣除金币";
                default:
                    return "当前操作不可用";
            }
        }
    }
}
