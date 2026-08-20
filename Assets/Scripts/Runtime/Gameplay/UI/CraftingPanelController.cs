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
        const string FilledSlotClass = "crafting-input-slot--filled";

        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();

        SceneSessionBinding _sessionBinding;
        LocalizationService _localizationService;

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
        Button _inputSlotButton;
        VisualElement _inputSlotIcon;
        Label _inputSlotNameLabel;
        Button _placeInSlotButton;
        Button _removeFromSlotButton;
        Button _upgradeRarityButton;
        Button _resetNormalButton;
        Button _scopeAnyButton;
        Button _scopePrefixButton;
        Button _scopeSuffixButton;
        Button _rerollAffixesButton;
        Button _addAffixButton;
        Button _removeAffixButton;
        Button _rerollValuesButton;
        ItemInstance _candidateItem;
        ItemInstance _slottedItem;
        CraftingAffixScope _scope = CraftingAffixScope.Any;
        CraftingResult _lastResult;

        public CraftingSnapshot LastSnapshot { get; private set; }

        public ItemInstance SlottedItem => _slottedItem;

        public bool IsVisible { get; private set; }

        public IArchitecture GetArchitecture()
        {
            return _sessionBinding.RequireArchitecture();
        }

        public void RefreshCrafting()
        {
            if (_page == null)
            {
                return;
            }

            LastSnapshot = this.SendQuery(new GetCraftingSnapshotQuery());
            _candidateItem = FindSnapshotItem(_inventoryPanel.SelectedItem);
            ItemInstance previousSlottedItem = _slottedItem;
            _slottedItem = FindSnapshotItem(_slottedItem);

            if (previousSlottedItem != null && _slottedItem == null)
            {
                _inventoryPanel.SetExternalSlotItem(null);
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
                _lastResult = null;
                RefreshCrafting();
            }
        }

        public bool FocusDefault()
        {
            if (_slottedItem != null && _inputSlotButton != null)
            {
                _inputSlotButton.Focus();
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
            _sessionBinding ??= new SceneSessionBinding(
                this,
                BindSession,
                UnbindSession);
            _sessionBinding.Enable();
        }

        void OnDisable()
        {
            _sessionBinding?.Disable();
        }

        SceneSessionBindResult BindSession(IArchitecture architecture)
        {
            if (_document == null || _document.panelSettings == null)
            {
                Debug.LogError("[CraftingPanelController] 缺少 UIDocument 或 PanelSettings，无法初始化打造", this);
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

            RegisterEvents();
            BindLocalization();
            _inventoryPanel.SelectionChanged += OnInventorySelectionChanged;
            _inventoryPanel.ConfigureExternalDropTarget(
                _inputSlotButton,
                CanAcceptCraftingItem,
                PlaceInCraftingSlot);
            RefreshCrafting();
            SetVisible(IsVisible);
            Debug.Log("[CraftingPanelController] 随机打造工作台初始化完成", this);
            return SceneSessionBindResult.Success;
        }

        void UnbindSession()
        {
            UnbindButtons();

            if (_inventoryPanel != null)
            {
                _inventoryPanel.SelectionChanged -= OnInventorySelectionChanged;
                _inventoryPanel.ClearExternalDropTarget(_inputSlotButton);
                _inventoryPanel.SetExternalSlotItem(null);
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

            _page = null;
            _candidateItem = null;
            _slottedItem = null;
            _lastResult = null;
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
            _inputSlotButton = root.Q<Button>("crafting-input-slot");
            _inputSlotIcon = root.Q<VisualElement>("crafting-input-icon");
            _inputSlotNameLabel = root.Q<Label>("crafting-input-name");
            _placeInSlotButton = root.Q<Button>("crafting-slot-place");
            _removeFromSlotButton = root.Q<Button>("crafting-slot-remove");
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
                || _inputSlotButton == null
                || _inputSlotIcon == null
                || _inputSlotNameLabel == null
                || _placeInSlotButton == null
                || _removeFromSlotButton == null
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
            _inputSlotButton.clicked += OnInputSlotClicked;
            _inputSlotButton.RegisterCallback<PointerEnterEvent>(OnInputSlotPointerEnter);
            _inputSlotButton.RegisterCallback<PointerLeaveEvent>(OnInputSlotPointerLeave);
            _inputSlotButton.RegisterCallback<FocusInEvent>(OnInputSlotFocusIn);
            _inputSlotButton.RegisterCallback<FocusOutEvent>(OnInputSlotFocusOut);
            _placeInSlotButton.clicked += OnPlaceInSlotClicked;
            _removeFromSlotButton.clicked += OnRemoveFromSlotClicked;
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
            if (_inputSlotButton != null)
            {
                _inputSlotButton.clicked -= OnInputSlotClicked;
                _inputSlotButton.UnregisterCallback<PointerEnterEvent>(OnInputSlotPointerEnter);
                _inputSlotButton.UnregisterCallback<PointerLeaveEvent>(OnInputSlotPointerLeave);
                _inputSlotButton.UnregisterCallback<FocusInEvent>(OnInputSlotFocusIn);
                _inputSlotButton.UnregisterCallback<FocusOutEvent>(OnInputSlotFocusOut);
            }

            if (_placeInSlotButton != null)
            {
                _placeInSlotButton.clicked -= OnPlaceInSlotClicked;
            }

            if (_removeFromSlotButton != null)
            {
                _removeFromSlotButton.clicked -= OnRemoveFromSlotClicked;
            }

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

            _candidateItem = item;
            _lastResult = null;
            RefreshCrafting();
        }

        ItemInstance FindSnapshotItem(ItemInstance item)
        {
            if (item == null)
            {
                return null;
            }

            for (int i = 0; i < LastSnapshot.Items.Count; i++)
            {
                ItemInstance snapshotItem = LastSnapshot.Items[i].Item;

                if (snapshotItem == item || snapshotItem.InstanceId == item.InstanceId)
                {
                    return snapshotItem;
                }
            }

            return null;
        }

        void RefreshSelection()
        {
            _goldLabel.text = Localize("crafting.gold", LastSnapshot.Gold);
            _scopeAnyButton.EnableInClassList(SelectedScopeClass, _scope == CraftingAffixScope.Any);
            _scopePrefixButton.EnableInClassList(SelectedScopeClass, _scope == CraftingAffixScope.Prefix);
            _scopeSuffixButton.EnableInClassList(SelectedScopeClass, _scope == CraftingAffixScope.Suffix);
            string resultText = BuildResultText();
            _resultLabel.text = resultText;
            _resultLabel.style.display = string.IsNullOrEmpty(resultText)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            RefreshInputSlot();

            if (!TryGetSlottedItem(out CraftingItemSnapshot item))
            {
                ShowEmptySlot();
                return;
            }

            int totalCount = item.PrefixCount + item.SuffixCount;
            _selectedRarityLabel.text = Localize(
                "crafting.selection.summary",
                Resolve(item.Detail.Name),
                GetRarityText(item.Rarity));
            _selectedCapacityLabel.text = Localize(
                "crafting.capacity.summary",
                item.PrefixCount,
                item.MaxPrefixCount,
                item.SuffixCount,
                item.MaxSuffixCount,
                totalCount,
                item.MinimumTotalCount,
                item.MaximumTotalCount);
            _selectedValueLabel.text = Localize(
                "crafting.value.summary",
                item.Value,
                item.SellPrice);
            RefreshButton(_upgradeRarityButton, item, CraftOperation.UpgradeRarity, CraftingAffixScope.Any);
            RefreshButton(_resetNormalButton, item, CraftOperation.ResetToNormal, CraftingAffixScope.Any);
            RefreshButton(_rerollAffixesButton, item, CraftOperation.RerollAffixes, _scope);
            RefreshButton(_addAffixButton, item, CraftOperation.AddAffix, _scope);
            RefreshButton(_removeAffixButton, item, CraftOperation.RemoveAffix, _scope);
            RefreshButton(_rerollValuesButton, item, CraftOperation.RerollAffixValues, _scope);

            _feedbackLabel.text = !LastSnapshot.IsConfigured
                ? Localize("crafting.failure.not_configured")
                : _scope == CraftingAffixScope.Any
                    ? Localize("crafting.feedback.scope_any")
                    : Localize("crafting.feedback.scope_precise", GetScopeText(_scope));
        }

        void RefreshInputSlot()
        {
            bool hasSlottedItem = _slottedItem != null;
            _inputSlotButton.EnableInClassList(FilledSlotClass, hasSlottedItem);
            _inputSlotNameLabel.text = hasSlottedItem
                ? Resolve(ItemDetailSnapshotFactory.Create(_slottedItem).Name)
                : Localize("crafting.slot.empty_name");
            _inputSlotButton.tooltip = hasSlottedItem
                ? Localize("crafting.slot.tooltip.filled")
                : Localize("crafting.slot.tooltip.empty");
            ItemVisualPresenter.ApplyIcon(
                _inputSlotIcon,
                hasSlottedItem ? ItemDetailSnapshotFactory.Create(_slottedItem).IconGuid : string.Empty);
            _placeInSlotButton.SetEnabled(_candidateItem != null && _candidateItem != _slottedItem);
            _placeInSlotButton.text = _candidateItem == null
                ? Localize("crafting.action.select_inventory")
                : _candidateItem == _slottedItem
                    ? Localize("crafting.action.in_slot")
                    : Localize("crafting.action.place");
            _removeFromSlotButton.SetEnabled(hasSlottedItem);
        }

        void ShowEmptySlot()
        {
            _selectedRarityLabel.text = Localize("crafting.slot.empty_state");
            _selectedCapacityLabel.text = Localize("crafting.capacity.empty");
            _selectedValueLabel.text = Localize("crafting.value.empty");
            _feedbackLabel.text = !LastSnapshot.IsConfigured
                ? Localize("crafting.failure.not_configured")
                : LastSnapshot.Items.Count == 0
                    ? Localize("crafting.feedback.empty_inventory")
                    : _candidateItem == null
                        ? Localize("crafting.feedback.place_item")
                        : Localize(
                            "crafting.feedback.candidate",
                            Resolve(ItemDetailSnapshotFactory.Create(_candidateItem).Name));
            SetAllActionsEnabled(false);
            SetButtonTextWithoutCost();
        }

        void RefreshButton(
            Button button,
            CraftingItemSnapshot item,
            CraftOperation operation,
            CraftingAffixScope scope)
        {
            string label = GetOperationText(operation);

            if (!item.TryGetAction(operation, scope, out CraftingActionSnapshot action))
            {
                button.text = label;
                button.SetEnabled(false);
                return;
            }

            button.text = Localize("crafting.action.cost", label, action.Cost);
            button.tooltip = action.IsAvailable
                ? Localize("crafting.action.tooltip", label, action.Cost)
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
            _upgradeRarityButton.text = GetOperationText(CraftOperation.UpgradeRarity);
            _resetNormalButton.text = GetOperationText(CraftOperation.ResetToNormal);
            _rerollAffixesButton.text = GetOperationText(CraftOperation.RerollAffixes);
            _addAffixButton.text = GetOperationText(CraftOperation.AddAffix);
            _removeAffixButton.text = GetOperationText(CraftOperation.RemoveAffix);
            _rerollValuesButton.text = GetOperationText(CraftOperation.RerollAffixValues);
        }

        bool TryGetSlottedItem(out CraftingItemSnapshot selected)
        {
            for (int i = 0; i < LastSnapshot.Items.Count; i++)
            {
                if (LastSnapshot.Items[i].Item == _slottedItem)
                {
                    selected = LastSnapshot.Items[i];
                    return true;
                }
            }

            selected = default;
            return false;
        }

        bool CanAcceptCraftingItem(ItemInstance item)
        {
            return IsVisible && FindSnapshotItem(item) != null;
        }

        void PlaceInCraftingSlot(ItemInstance item)
        {
            ItemInstance snapshotItem = FindSnapshotItem(item);

            if (!IsVisible || snapshotItem == null)
            {
                return;
            }

            _candidateItem = snapshotItem;
            _slottedItem = snapshotItem;
            _lastResult = null;
            _inventoryPanel.SetExternalSlotItem(_slottedItem);
            RefreshSelection();
        }

        void RemoveFromCraftingSlot()
        {
            if (_slottedItem == null)
            {
                return;
            }

            _slottedItem = null;
            _lastResult = null;
            _inventoryPanel.SetExternalSlotItem(null);
            _inventoryPanel.ShowPlayerTooltip(null, string.Empty);
            RefreshSelection();
        }

        void OnInputSlotClicked()
        {
            if (_slottedItem == null)
            {
                PlaceInCraftingSlot(_candidateItem);
            }
        }

        void OnPlaceInSlotClicked()
        {
            PlaceInCraftingSlot(_candidateItem);
        }

        void OnRemoveFromSlotClicked()
        {
            RemoveFromCraftingSlot();
        }

        void OnInputSlotPointerEnter(PointerEnterEvent evt)
        {
            PreviewSlottedItem();
        }

        void OnInputSlotPointerLeave(PointerLeaveEvent evt)
        {
            EndSlottedItemPreview();
        }

        void OnInputSlotFocusIn(FocusInEvent evt)
        {
            PreviewSlottedItem();
        }

        void OnInputSlotFocusOut(FocusOutEvent evt)
        {
            EndSlottedItemPreview();
        }

        void PreviewSlottedItem()
        {
            if (_slottedItem != null)
            {
                _inventoryPanel.ShowPlayerTooltip(
                    _slottedItem,
                    Localize("crafting.tooltip.slotted"));
            }
        }

        void EndSlottedItemPreview()
        {
            _inventoryPanel.ShowPlayerTooltip(null, string.Empty);
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
            _lastResult = null;
            RefreshSelection();
        }

        void Craft(CraftOperation operation, CraftingAffixScope scope)
        {
            if (_slottedItem == null)
            {
                _feedbackLabel.text = Localize("crafting.feedback.slot_required");
                return;
            }

            CraftingResult result = this.SendCommand(new CraftItemCommand(operation, scope, _slottedItem));
            _lastResult = result;
            RefreshCrafting();
            FocusDefault();
        }

        string BuildResultText()
        {
            if (_lastResult == null)
            {
                return string.Empty;
            }

            if (!_lastResult.Succeeded)
            {
                return Localize(
                    "crafting.result.failed",
                    GetFailureText(_lastResult.FailureReason));
            }

            int previousCount = _lastResult.PreviousPrefixes.Count
                + _lastResult.PreviousSuffixes.Count;
            int currentCount = _lastResult.CurrentPrefixes.Count
                + _lastResult.CurrentSuffixes.Count;
            return Localize(
                "crafting.result.succeeded",
                GetOperationText(_lastResult.Operation),
                GetScopeText(_lastResult.Scope),
                _lastResult.Cost,
                GetRarityText(_lastResult.PreviousRarity),
                previousCount,
                GetRarityText(_lastResult.CurrentRarity),
                currentCount);
        }

        string GetOperationText(CraftOperation operation)
        {
            string entryKey = operation switch
            {
                CraftOperation.UpgradeRarity => "crafting.action.upgrade_rarity",
                CraftOperation.ResetToNormal => "crafting.action.reset_normal",
                CraftOperation.RerollAffixes => "crafting.action.reroll_affixes",
                CraftOperation.AddAffix => "crafting.action.add_affix",
                CraftOperation.RemoveAffix => "crafting.action.remove_affix",
                CraftOperation.RerollAffixValues => "crafting.action.reroll_values",
                _ => string.Empty,
            };
            return string.IsNullOrEmpty(entryKey) ? operation.ToString() : Localize(entryKey);
        }

        string GetScopeText(CraftingAffixScope scope)
        {
            string entryKey = scope switch
            {
                CraftingAffixScope.Prefix => "crafting.scope.prefix",
                CraftingAffixScope.Suffix => "crafting.scope.suffix",
                _ => "crafting.scope.any",
            };
            return Localize(entryKey);
        }

        string GetFailureText(CraftingFailureReason reason)
        {
            string entryKey = reason switch
            {
                CraftingFailureReason.NotConfigured => "crafting.failure.not_configured",
                CraftingFailureReason.ItemMissing => "crafting.failure.item_not_in_inventory",
                CraftingFailureReason.ItemNotInInventory => "crafting.failure.item_not_in_inventory",
                CraftingFailureReason.InsufficientGold => "crafting.failure.insufficient_gold",
                CraftingFailureReason.MaximumRarity => "crafting.failure.maximum_rarity",
                CraftingFailureReason.NoChange => "crafting.failure.no_change",
                CraftingFailureReason.NoCapacity => "crafting.failure.no_capacity",
                CraftingFailureReason.ScopeHasNoAffix => "crafting.failure.scope_has_no_affix",
                CraftingFailureReason.NoVariableAffix => "crafting.failure.no_variable_affix",
                CraftingFailureReason.NoLegalAffix => "crafting.failure.no_legal_affix",
                CraftingFailureReason.CannotBuildCompleteResult => "crafting.failure.incomplete_result",
                CraftingFailureReason.CommitFailed => "crafting.failure.commit_failed",
                _ => "crafting.failure.unavailable",
            };
            return Localize(entryKey);
        }

        string GetRarityText(ItemRarity rarity)
        {
            string entryKey = rarity switch
            {
                ItemRarity.Normal => "item.rarity.normal",
                ItemRarity.Magic => "item.rarity.magic",
                ItemRarity.Rare => "item.rarity.rare",
                ItemRarity.Unique => "item.rarity.unique",
                _ => "item.rarity.unknown",
            };
            return Localize(entryKey);
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
            if (_page != null)
            {
                RefreshSelection();
            }
        }

        string Localize(string entryKey, params object[] arguments)
        {
            LocalizedMessage message = LocalizedMessage.Ui(entryKey, arguments);
            return Resolve(message);
        }

        string Resolve(LocalizedMessage message)
        {
            return _localizationService?.GetString(message)
                ?? $"[{message.TableName}.{message.EntryKey}]";
        }
    }
}
