using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimePanelView))]
    public class CraftingPanelController : MonoBehaviour, IController
    {
        const string SelectedScopeClass = "crafting-scope--selected";
        const string FilledSlotClass = "crafting-input-slot--filled";

        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();

        SceneSessionBinding _sessionBinding;
        LocalizationService _localizationService;

        [SerializeField]
        RuntimePanelView _uiPanel;

        ItemWorkspace _workspace;
        GameMenuController _menu;
        ItemSourceListView _sourceList;

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
        readonly Dictionary<ItemInstance, Button> _candidateButtons = new Dictionary<ItemInstance, Button>();
        ItemInstance _candidateItem;
        ItemInstance _previewItem;
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
            var candidates = new List<ItemInstance>();
            foreach (CraftingItemSnapshot snapshot in LastSnapshot.Items)
            {
                if (snapshot.Item != _workspace.CraftingItem) { candidates.Add(snapshot.Item); }
            }
            _sourceList.Refresh(candidates, item => Resolve(ItemDetailSnapshotFactory.Create(item).Name),
                SelectCandidate, PreviewCandidate, EndCandidatePreview);
            _candidateItem = FindSnapshotItem(_candidateItem);
            ItemInstance previousSlottedItem = _slottedItem;
            _slottedItem = FindSnapshotItem(_slottedItem);

            if (previousSlottedItem != null && _slottedItem == null)
            {
                _workspace.SetCraftingItem(null);
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

            if (_candidateItem != null && _candidateButtons.TryGetValue(_candidateItem, out Button candidate))
            {
                candidate.Focus();
                return true;
            }
            foreach (Button button in _candidateButtons.Values) { button.Focus(); return true; }
            _inputSlotButton?.Focus();
            return _inputSlotButton != null;
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
                ApplicationLog.Error(LogEventIds.GameplayUi, "[CraftingPanelController] 缺少 RuntimePanelView 或 PanelSettings，无法初始化打造", this);
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
            RefreshCrafting();
            SetVisible(IsVisible);
            ApplicationLog.Info(LogEventIds.GameplayUi, "[CraftingPanelController] 随机打造工作台初始化完成", this);
            return SceneSessionBindResult.Success;
        }

        void UnbindSession()
        {
            UnbindButtons();

            if (_workspace != null)
            {
                _workspace.InventorySelectionChanged -= OnInventorySelectionChanged;
                _workspace.SetCraftingItem(null);
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
            _previewItem = null;
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
                ApplicationLog.Error(LogEventIds.GameplayUi, "[CraftingPanelController] 缺少 RuntimePanelView 或物品窗口宿主", this);
                return false;
            }

            VisualElement root = _uiPanel.Root;
            _page = root.Q<VisualElement>("crafting-page");
            _sourceList = new ItemSourceListView(root.Q("crafting-candidates"), _candidateButtons);
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
                || root.Q("crafting-candidates") == null
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
                ApplicationLog.Error(LogEventIds.GameplayUi, "[CraftingPanelController] 打造 UXML 缺少随机打造工作台所需元素", this);
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

        public void CloseWindow()
        {
            _slottedItem = null;
            _workspace?.SetCraftingItem(null);
            _workspace?.HidePreview(GameMenuPage.Crafting);
        }

        void SelectCandidate(ItemInstance item)
        {
            _workspace.Activate(GameMenuPage.Crafting);
            _candidateItem = item;
            RefreshSelection();
            PreviewCandidate(item);
        }

        void PreviewCandidate(ItemInstance item)
        {
            if (!IsVisible || _workspace.IsDragging) { return; }
            _workspace.Activate(GameMenuPage.Crafting);
            _previewItem = item;
            foreach (var entry in _candidateButtons)
            {
                _workspace.Highlight(GameMenuPage.Crafting, entry.Value, "item-source-row--selected", entry.Key == item);
            }
            _workspace.Preview(GameMenuPage.Crafting, _uiPanel.Root.Q("crafting-window"), item, string.Empty);
        }

        void EndCandidatePreview(ItemInstance item)
        {
            if (_previewItem != item) { return; }
            _previewItem = null;
            _workspace.HidePreview(GameMenuPage.Crafting);
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

        public bool CanAcceptItem(ItemInstance item)
        {
            if (!IsVisible || !_menu.IsPageAvailable(GameMenuPage.Crafting)) { return false; }
            foreach (CraftingItemSnapshot candidate in this.SendQuery(new GetCraftingSnapshotQuery()).Items)
            {
                if (candidate.Item == item) { return true; }
            }
            return false;
        }

        public bool PlaceItem(ItemInstance item)
        {
            if (!CanAcceptItem(item) || item == _slottedItem) { return false; }
            PlaceInCraftingSlot(item);
            return _slottedItem == item;
        }

        public bool ReturnItem()
        {
            if (!IsVisible || !_menu.IsPageAvailable(GameMenuPage.Crafting) || _slottedItem == null) { return false; }
            RemoveFromCraftingSlot();
            return true;
        }

        void PlaceInCraftingSlot(ItemInstance item)
        {
            ItemInstance snapshotItem = FindSnapshotItem(item);

            if (!IsVisible || !_menu.IsPageAvailable(GameMenuPage.Crafting) || snapshotItem == null)
            {
                return;
            }

            _candidateItem = snapshotItem;
            _slottedItem = snapshotItem;
            _lastResult = null;
            _workspace.SetCraftingItem(_slottedItem);
            RefreshCrafting();
        }

        void RemoveFromCraftingSlot()
        {
            if (_slottedItem == null)
            {
                return;
            }

            _slottedItem = null;
            _lastResult = null;
            _workspace.SetCraftingItem(null);
            _workspace.HidePreview(GameMenuPage.Crafting);
            RefreshCrafting();
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
                _previewItem = _slottedItem;
                _workspace.Activate(GameMenuPage.Crafting);
                _workspace.Highlight(GameMenuPage.Crafting, _inputSlotButton, "item-source-row--selected", true);
                _workspace.Preview(GameMenuPage.Crafting, _uiPanel.Root.Q("crafting-window"),
                    _slottedItem,
                    Localize("crafting.tooltip.slotted"));
            }
        }

        void EndSlottedItemPreview()
        {
            _previewItem = null;
            _workspace.HidePreview(GameMenuPage.Crafting);
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
            if (!IsVisible || !_menu.IsPageAvailable(GameMenuPage.Crafting)) { return; }
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
                _sourceList.RefreshTitles(item => Resolve(ItemDetailSnapshotFactory.Create(item).Name));
                RefreshSelection();
                if (_previewItem != null && _workspace.ActiveWindow == GameMenuPage.Crafting)
                {
                    if (_previewItem == _slottedItem) { PreviewSlottedItem(); }
                    else { PreviewCandidate(_previewItem); }
                }
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
