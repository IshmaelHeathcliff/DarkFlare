using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimePanelView))]
    public class InventoryPanelController : MonoBehaviour, IController
    {
        const float CellSize = 48f;
        const float CellGap = 4f;

        enum SelectionHighlightSource
        {
            None,
            Inventory,
            Equipment
        }

        [SerializeField]
        RuntimePanelView _uiPanel;

        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();
        readonly Dictionary<ItemInstance, Button> _itemButtons = new Dictionary<ItemInstance, Button>();
        readonly Dictionary<EquipmentSlot, Action> _slotClicks = new Dictionary<EquipmentSlot, Action>();
        readonly Dictionary<EquipmentSlot, Button> _slotButtons = new Dictionary<EquipmentSlot, Button>();
        readonly ItemListViewState _selectionState = new ItemListViewState();

        SceneSessionBinding _sessionBinding;
        VisualElement _page;
        VisualElement _workbench;
        VisualElement _grid;
        VisualElement _gridFrame;
        Label _emptyLabel;
        Label _targetSlotLabel;
        Label _feedbackLabel;
        VisualElement _attributeGrid;
        Button _equipButton;
        Button _unequipButton;
        ItemWorkspace _workspace;
        LocalizationService _localizationService;
        ItemDetailFormatter _itemDetailFormatter;
        LocalizedMessage _feedbackMessage;
        ItemInstance _selectedItem;
        ItemInstance _previewItem;
        ItemInstance _externalSlotItem => _workspace?.CraftingItem;
        EquipmentSlot? _targetSlot;
        bool _suppressTooltipUntilPreview;
        SelectionHighlightSource _selectionHighlightSource;

        public InventorySnapshot LastSnapshot { get; private set; }

        public ItemInstance SelectedItem => _selectedItem;

        public EquipmentSlot? TargetSlot => _targetSlot;

        public bool IsVisible { get; private set; }

        public bool IsOpen => IsVisible;

        public bool IsDragging => _workspace?.Interactions?.IsDragging == true;

        public bool IsPointerPending => _workspace?.Interactions?.IsPointerPending == true;

        bool IsInExternalSlot(ItemInstance item)
        {
            return IsSameItem(_externalSlotItem, item);
        }

        public IArchitecture GetArchitecture()
        {
            return _sessionBinding.RequireArchitecture();
        }

        public void RefreshInventory()
        {
            if (_grid == null)
            {
                return;
            }

            ItemInstance previousSelection = _selectedItem;
            CancelDrag(false);
            CaptureSelectionState();

            if (IsInExternalSlot(previousSelection))
            {
                _selectionState.Reset();
            }

            InventorySnapshot snapshot = this.SendQuery(new GetInventorySnapshotQuery());
            LastSnapshot = snapshot;
            RefreshAttributes();
            List<InventoryItemSnapshot> visibleItems = new List<InventoryItemSnapshot>(snapshot.Items.Count);

            for (int i = 0; i < snapshot.Items.Count; i++)
            {
                if (!IsInExternalSlot(snapshot.Items[i].Item))
                {
                    visibleItems.Add(snapshot.Items[i]);
                }
            }

            _emptyLabel.style.display = visibleItems.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            _selectedItem = null;
            _previewItem = null;
            _itemButtons.Clear();
            _grid.Clear();
            BuildGrid(snapshot);

            for (int i = 0; i < visibleItems.Count; i++)
            {
                InventoryItemSnapshot item = visibleItems[i];
                Button button = CreateItemButton(item);
                _grid.Add(button);
                _itemButtons.Add(item.Item, button);

            }

            int selectedIndex = string.IsNullOrEmpty(_selectionState.SelectedInstanceId)
                ? -1
                : ItemSelectionResolver.ResolveIndex(
                    visibleItems,
                    _selectionState.SelectedInstanceId,
                    _selectionState.FallbackIndex,
                    item => item.Detail.InstanceId);

            if (selectedIndex >= 0)
            {
                _selectedItem = visibleItems[selectedIndex].Item;
                _selectionState.SelectedInstanceId = _selectedItem.InstanceId;
                _selectionState.FallbackIndex = selectedIndex;
            }

            ResolveTargetSlotForSelectedItem(false);
            RefreshSelection();

            if (previousSelection != _selectedItem)
            {
                _workspace?.SelectInventoryItem(_selectedItem);
            }
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
                _suppressTooltipUntilPreview = false;
                RefreshInventory();
            }
            else
            {
                CancelDrag(false);
            }
        }

        public bool CancelActiveDrag()
        {
            return CancelDrag(true);
        }

        public bool FocusDefault()
        {
            if (_selectedItem != null && _itemButtons.TryGetValue(_selectedItem, out Button selectedButton))
            {
                selectedButton.Focus();
                return true;
            }

            if (_targetSlot.HasValue && FocusSlot(_targetSlot.Value))
            {
                return true;
            }

            return false;
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
                ApplicationLog.Error(LogEventIds.GameplayUi, "[InventoryPanelController] 缺少 RuntimePanelView 或 PanelSettings，无法初始化背包", this);
                return SceneSessionBindResult.Failed;
            }

            VisualElement root = _uiPanel.Root;

            if (root == null || root.panel == null)
            {
                return SceneSessionBindResult.Retry;
            }

            _workspace = GetComponent<GameMenuController>().Workspace;
            _workspace.CraftingItemChanged += OnCraftingItemChanged;
            if (!BindVisualTree())
            {
                return SceneSessionBindResult.Failed;
            }

            BindLocalization();
            _itemDetailFormatter = new ItemDetailFormatter(Localize);

            RegisterEvents();
            RefreshInventory();
            SetVisible(IsVisible);
            ApplicationLog.Info(LogEventIds.GameplayUi, "[InventoryPanelController] 背包与四槽装备面板初始化完成", this);
            return SceneSessionBindResult.Success;
        }

        void UnbindSession()
        {
            CancelDrag(false);
            UnbindButtons();
            if (_workspace != null) { _workspace.CraftingItemChanged -= OnCraftingItemChanged; }

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged -= OnLocaleChanged;
                _localizationService = null;
            }

            for (int i = 0; i < _eventRegistrations.Count; i++)
            {
                _eventRegistrations[i].UnRegister();
            }

            _eventRegistrations.Clear();
            _itemButtons.Clear();
            _slotButtons.Clear();
            _selectionState.Reset();
            _page = null;
            _workbench = null;
            _grid = null;
            _gridFrame = null;
            _emptyLabel = null;
            _targetSlotLabel = null;
            _feedbackLabel = null;
            _attributeGrid = null;
            _equipButton = null;
            _unequipButton = null;
            _workspace = null;
            _itemDetailFormatter = null;
            _feedbackMessage = default;
            _selectedItem = null;
            _previewItem = null;

            _targetSlot = null;
            _suppressTooltipUntilPreview = false;
            _selectionHighlightSource = SelectionHighlightSource.None;
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
        }

        bool BindVisualTree()
        {
            if (_uiPanel == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[InventoryPanelController] 缺少 RuntimePanelView，无法初始化背包面板", this);
                return false;
            }

            VisualElement root = _uiPanel.Root;
            _page = root.Q<VisualElement>("inventory-page");
            _workbench = root.Q<VisualElement>("game-menu-overlay");
            _grid = root.Q<VisualElement>("inventory-grid");
            _gridFrame = root.Q<VisualElement>("inventory-grid-frame");
            if (_gridFrame is ScrollView scroll)
            {
                scroll.focusable = false;
                scroll.contentContainer.focusable = false;
                foreach (VisualElement control in scroll.verticalScroller.Query<VisualElement>().ToList())
                {
                    control.focusable = false;
                    control.tabIndex = -1;
                }
                scroll.verticalScroller.focusable = false;
            }
            _emptyLabel = root.Q<Label>("inventory-empty");
            _targetSlotLabel = root.Q<Label>("inventory-target-slot");
            _feedbackLabel = root.Q<Label>("inventory-feedback");
            _attributeGrid = root.Q<VisualElement>("inventory-attribute-grid");
            _equipButton = root.Q<Button>("inventory-equip");
            _unequipButton = root.Q<Button>("inventory-unequip");
            _slotButtons.Clear();
            foreach (EquipmentSlot slot in EquipmentSlots.All)
            {
                string suffix = System.Text.RegularExpressions.Regex.Replace(slot.ToString(), "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
                AddSlotButton(root, slot, $"inventory-slot-{suffix}");
            }

            if (_page == null
                || _workbench == null
                || _grid == null
                || _gridFrame == null
                || _emptyLabel == null
                || _targetSlotLabel == null
                || _feedbackLabel == null
                || _attributeGrid == null
                || _equipButton == null
                || _unequipButton == null
                || _slotButtons.Count != EquipmentSlots.All.Count
                || _workspace == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayUi, "[InventoryPanelController] 背包 UXML 缺少装备面板所需的命名元素", this);
                return false;
            }

            _equipButton.clicked += OnEquipClicked;
            _unequipButton.clicked += OnUnequipClicked;
            foreach (EquipmentSlot slot in EquipmentSlots.All)
            {
                Action click = () => SelectSlot(slot);
                _slotClicks.Add(slot, click);
                _slotButtons[slot].clicked += click;
                RegisterSlotPreviewCallbacks(slot);
            }
            return true;
        }

        void OnCraftingItemChanged()
        {
            if (IsVisible) { RefreshInventory(); }
        }

        void AddSlotButton(VisualElement root, EquipmentSlot slot, string name)
        {
            Button button = root.Q<Button>(name);

            if (button != null)
            {
                _slotButtons.Add(slot, button);
            }
        }

        void UnbindButtons()
        {
            if (_equipButton != null)
            {
                _equipButton.clicked -= OnEquipClicked;
            }

            if (_unequipButton != null)
            {
                _unequipButton.clicked -= OnUnequipClicked;
            }

            foreach (var entry in _slotClicks)
            {
                if (_slotButtons.TryGetValue(entry.Key, out Button button)) { button.clicked -= entry.Value; }
            }
            _slotClicks.Clear();

            foreach (Button button in _slotButtons.Values)
            {
                button.UnregisterCallback<PointerEnterEvent>(OnEquipmentPointerEnter);
                button.UnregisterCallback<PointerLeaveEvent>(OnEquipmentPointerLeave);
                button.UnregisterCallback<FocusInEvent>(OnEquipmentFocusIn);
                button.UnregisterCallback<FocusOutEvent>(OnEquipmentFocusOut);
            }

        }

        void RegisterEvents()
        {
            if (_eventRegistrations.Count > 0)
            {
                return;
            }

            _eventRegistrations.Add(this.RegisterEvent<ActorRegisteredEvent>(_ => RefreshInventory()));
            _eventRegistrations.Add(this.RegisterEvent<ActorUnregisteredEvent>(_ => RefreshInventory()));
            _eventRegistrations.Add(this.RegisterEvent<InventoryChangedEvent>(_ => RefreshInventory()));
            _eventRegistrations.Add(this.RegisterEvent<EquipmentChangedEvent>(_ => RefreshInventory()));
            _eventRegistrations.Add(this.RegisterEvent<ItemCraftedEvent>(_ => RefreshInventory()));
        }

        void BuildGrid(InventorySnapshot snapshot)
        {
            float step = CellSize + CellGap;
            _grid.style.width = snapshot.Width * CellSize + (snapshot.Width - 1) * CellGap;
            _grid.style.height = snapshot.Height * CellSize + (snapshot.Height - 1) * CellGap;

            for (int y = 0; y < snapshot.Height; y++)
            {
                for (int x = 0; x < snapshot.Width; x++)
                {
                    VisualElement cell = new VisualElement();
                    cell.AddToClassList("inventory-cell");
                    cell.style.position = Position.Absolute;
                    cell.style.left = x * step;
                    cell.style.top = y * step;
                    cell.style.width = CellSize;
                    cell.style.height = CellSize;
                    _grid.Add(cell);
                }
            }
        }

        Button CreateItemButton(InventoryItemSnapshot item)
        {
            float step = CellSize + CellGap;
            Button button = new Button(() => SelectItem(item.Item));
            button.name = $"inventory-item-{item.Detail.InstanceId}";
            button.text = string.Empty;
            button.userData = item.Item;
            button.AddToClassList("inventory-item");
            button.AddToClassList(GetRarityClass(item.Rarity));
            button.style.position = Position.Absolute;
            button.style.left = item.Placement.x * step;
            button.style.top = item.Placement.y * step;
            button.style.width = item.Placement.width * CellSize + (item.Placement.width - 1) * CellGap;
            button.style.height = item.Placement.height * CellSize + (item.Placement.height - 1) * CellGap;

            VisualElement icon = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            icon.AddToClassList("inventory-item-icon");
            ItemVisualPresenter.ApplyIcon(icon, item.Detail.IconGuid);
            button.Add(icon);
            if (item.Item.BaseDefinition.IsStackable)
            {
                Label quantity = new Label(item.Item.Quantity.ToString()) { pickingMode = PickingMode.Ignore };
                quantity.style.position = Position.Absolute;
                quantity.style.right = 3;
                quantity.style.bottom = 1;
                quantity.style.fontSize = 16;
                quantity.style.color = Color.white;
                quantity.style.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
                button.Add(quantity);
            }
            button.RegisterCallback<PointerEnterEvent>(_ => PreviewItem(item.Item));
            button.RegisterCallback<PointerLeaveEvent>(_ => EndPreview(item.Item));
            button.RegisterCallback<FocusInEvent>(_ => PreviewItem(item.Item));
            button.RegisterCallback<FocusOutEvent>(_ => EndPreview(item.Item));
            return button;
        }

        void SelectItem(ItemInstance item)
        {
            _workspace.Activate(GameMenuPage.Inventory);
            ItemInstance previous = _selectedItem;
            _suppressTooltipUntilPreview = false;
            _selectedItem = item;
            _selectionHighlightSource = SelectionHighlightSource.Inventory;
            CaptureSelectionState();
            ResolveTargetSlotForSelectedItem(true);
            RefreshSelection();

            if (previous != _selectedItem)
            {
                _workspace?.SelectInventoryItem(_selectedItem);
            }
        }

        void SelectSlot(EquipmentSlot slot)
        {
            _workspace.Activate(GameMenuPage.Inventory);
            ItemInstance previous = _selectedItem;
            _suppressTooltipUntilPreview = false;
            _targetSlot = slot;
            _selectionHighlightSource = SelectionHighlightSource.Equipment;

            if (_selectedItem != null && !IsCompatible(_selectedItem, slot))
            {
                _selectedItem = null;
                _selectionState.SelectedInstanceId = string.Empty;
            }

            RefreshSelection();

            if (previous != _selectedItem)
            {
                _workspace?.SelectInventoryItem(_selectedItem);
            }
        }

        void ResolveTargetSlotForSelectedItem(bool clearAmbiguousRingTarget)
        {
            if (_selectedItem == null || _selectedItem.BaseDefinition == null)
            {
                return;
            }

            EquipmentSlotMask slots = _selectedItem.BaseDefinition.AllowedEquipmentSlots;

            EquipmentSlot? uniqueTarget = EquipmentSlots.GetUniqueTarget(slots);
            if (uniqueTarget.HasValue)
            {
                _targetSlot = uniqueTarget;
            }
            else if (slots == EquipmentSlotMask.Rings && clearAmbiguousRingTarget)
            {
                _targetSlot = null;
            }
            else if (!_targetSlot.HasValue || !IsCompatible(_selectedItem, _targetSlot.Value))
            {
                _targetSlot = null;
            }
        }

        void CaptureSelectionState()
        {
            if (_selectedItem == null)
            {
                return;
            }

            _selectionState.SelectedInstanceId = _selectedItem.InstanceId;

            if (LastSnapshot.Items == null)
            {
                return;
            }

            for (int i = 0; i < LastSnapshot.Items.Count; i++)
            {
                if (LastSnapshot.Items[i].Item == _selectedItem)
                {
                    _selectionState.FallbackIndex = i;
                    return;
                }
            }
        }

        void PreviewItem(ItemInstance item)
        {
            if (_workspace == null || !IsVisible || IsDragging || _suppressTooltipUntilPreview)
            {
                return;
            }

            _workspace.Activate(GameMenuPage.Inventory);
            _previewItem = item;
            RefreshSelectionHighlights();
            RefreshDetail();
        }

        void EndPreview(ItemInstance item)
        {
            if (_suppressTooltipUntilPreview)
            {
                _suppressTooltipUntilPreview = false;
                _workspace?.HidePreview(GameMenuPage.Inventory);
                return;
            }

            if (_previewItem != item)
            {
                return;
            }

            _previewItem = null;
            RefreshSelectionHighlights();
            RefreshDetail();
        }

        void RefreshSelection()
        {
            RefreshEquipmentSlots();
            RefreshSelectionHighlights();
            RefreshDetail();
            RefreshTargetSlotLabel();

            bool hasTarget = _targetSlot.HasValue;
            bool hasCandidate = TryGetSelectedSnapshot(out InventoryItemSnapshot selected);
            bool canEquip = LastSnapshot.HasPlayer
                && hasTarget
                && hasCandidate
                && this.SendQuery(new CanEquipItemFromGridQuery(LastSnapshot.Player, selected.Item, _targetSlot.Value));
            bool canUnequip = LastSnapshot.HasPlayer
                && hasTarget
                && TryGetSlotSnapshot(_targetSlot.Value, out EquipmentSlotSnapshot slotSnapshot)
                && slotSnapshot.Item != null;
            _equipButton.SetEnabled(canEquip);
            _unequipButton.SetEnabled(canUnequip);

            if (!LastSnapshot.HasPlayer)
            {
                SetFeedback("inventory.feedback.waiting_player");
            }
            else if (hasCandidate && !hasTarget && selected.CompatibleSlots == EquipmentSlotMask.Rings)
            {
                SetFeedback("inventory.feedback.select_ring");
            }
            else if (canEquip)
            {
                SetFeedback(canUnequip
                    ? "inventory.feedback.can_replace"
                    : "inventory.feedback.can_equip");
            }
            else if (canUnequip)
            {
                SetFeedback("inventory.feedback.can_unequip");
            }
            else if (!hasCandidate)
            {
                SetFeedback("inventory.feedback.select_item_or_slot");
            }
            else
            {
                SetFeedback(selected.CanEquip
                    ? "inventory.feedback.select_compatible_slot"
                    : "inventory.feedback.cannot_equip");
            }
        }

        void RefreshEquipmentSlots()
        {
            for (int i = 0; i < LastSnapshot.EquipmentSlots.Count; i++)
            {
                EquipmentSlotSnapshot snapshot = LastSnapshot.EquipmentSlots[i];

                if (!_slotButtons.TryGetValue(snapshot.Slot, out Button button))
                {
                    continue;
                }

                button.text = string.Empty;
                VisualElement icon = button.Q<VisualElement>("equipment-slot-icon");
                Label slotLabel = button.Q<Label>("equipment-slot-label");

                if (icon == null)
                {
                    icon = new VisualElement
                    {
                        name = "equipment-slot-icon",
                        pickingMode = PickingMode.Ignore,
                    };
                    icon.AddToClassList("inventory-equipment-slot-icon");
                    icon.AddToClassList(GetSlotIconClass(snapshot.Slot));
                    button.Insert(0, icon);
                }

                if (slotLabel == null)
                {
                    slotLabel = new Label
                    {
                        name = "equipment-slot-label",
                        pickingMode = PickingMode.Ignore,
                    };
                    slotLabel.AddToClassList("inventory-equipment-slot-label");
                    button.Add(slotLabel);
                }

                ItemVisualPresenter.ApplyIcon(icon, snapshot.Detail.IconGuid);
                icon.EnableInClassList("inventory-equipment-slot-icon--empty", snapshot.Item == null);
                string slotName = GetSlotName(snapshot.Slot);
                slotLabel.text = slotName;
                button.tooltip = snapshot.Item != null
                    ? Localize(
                        "ui",
                        "inventory.slot.tooltip.filled",
                        Resolve(snapshot.Detail.Name),
                        _itemDetailFormatter.GetRarityText(snapshot.Detail.Rarity))
                    : Localize("ui", "inventory.slot.tooltip.empty", slotName);
                button.EnableInClassList("inventory-equipment-slot--filled", snapshot.Item != null);
            }
        }

        void RefreshSelectionHighlights()
        {
            ItemInstance highlightedInventoryItem = null;
            EquipmentSlot? highlightedEquipmentSlot = null;

            if (_previewItem != null)
            {
                if (_itemButtons.ContainsKey(_previewItem))
                {
                    highlightedInventoryItem = _previewItem;
                }
                else if (TryGetEquipmentSlot(_previewItem, out EquipmentSlot previewSlot))
                {
                    highlightedEquipmentSlot = previewSlot;
                }
            }
            else if (_selectionHighlightSource == SelectionHighlightSource.Inventory)
            {
                highlightedInventoryItem = _selectedItem;
            }
            else if (_selectionHighlightSource == SelectionHighlightSource.Equipment)
            {
                highlightedEquipmentSlot = _targetSlot;
            }

            foreach (KeyValuePair<ItemInstance, Button> entry in _itemButtons)
            {
                _workspace.Highlight(GameMenuPage.Inventory, entry.Value,
                    "inventory-item--selected", entry.Key == highlightedInventoryItem);
            }

            foreach (KeyValuePair<EquipmentSlot, Button> entry in _slotButtons)
            {
                _workspace.Highlight(GameMenuPage.Inventory, entry.Value,
                    "inventory-equipment-slot--selected", highlightedEquipmentSlot == entry.Key);
            }
        }

        bool TryGetEquipmentSlot(ItemInstance item, out EquipmentSlot slot)
        {
            if (LastSnapshot.EquipmentSlots != null)
            {
                for (int i = 0; i < LastSnapshot.EquipmentSlots.Count; i++)
                {
                    EquipmentSlotSnapshot snapshot = LastSnapshot.EquipmentSlots[i];

                    if (snapshot.Item == item)
                    {
                        slot = snapshot.Slot;
                        return true;
                    }
                }
            }

            slot = default;
            return false;
        }

        void RefreshDetail()
        {
            if (IsDragging)
            {
                _workspace?.HidePreview(GameMenuPage.Inventory);
                return;
            }

            if (_suppressTooltipUntilPreview && _previewItem == null)
            {
                _workspace?.HidePreview(GameMenuPage.Inventory);
                return;
            }

            if (_previewItem != null)
            {
                _workspace.Preview(GameMenuPage.Inventory, _uiPanel.Root.Q("inventory-window"),
                    _previewItem, BuildTooltipContext(_previewItem));
                return;
            }

            _workspace?.HidePreview(GameMenuPage.Inventory);
        }

        void RefreshTargetSlotLabel()
        {
            if (!_targetSlot.HasValue)
            {
                _targetSlotLabel.text = Localize("ui", "inventory.target.none");
                return;
            }

            _targetSlotLabel.text = Localize(
                "ui",
                "inventory.target.selected",
                GetSlotName(_targetSlot.Value));
        }

        void RefreshAttributes()
        {
            HudSnapshot snapshot = this.SendQuery(new GetHudSnapshotQuery());
            var attributes = new List<HudAttributeValue>();
            foreach (HudAttributeValue value in snapshot.Attributes.Values)
            {
                if (value.StatId == StatIds.Strength || value.StatId == StatIds.Dexterity || value.StatId == StatIds.Intelligence)
                {
                    attributes.Add(value);
                }
            }
            _attributeGrid.Clear();
            foreach (HudAttributeValue attribute in attributes)
            {
                var label = new Label(Localize("stats", attribute.StatId) + "  " + FormatAttribute(attribute.Value, snapshot.HasPlayer))
                {
                    name = "inventory-attribute-" + attribute.StatId,
                    pickingMode = PickingMode.Ignore,
                };
                label.AddToClassList("inventory-primary-summary");
                _attributeGrid.Add(label);
            }
        }

        static string FormatAttribute(float value, bool hasPlayer)
        {
            return hasPlayer ? value.ToString("0.##") : "--";
        }

        bool TryGetSelectedSnapshot(out InventoryItemSnapshot selected)
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

        bool TryGetSlotSnapshot(EquipmentSlot slot, out EquipmentSlotSnapshot snapshot)
        {
            for (int i = 0; i < LastSnapshot.EquipmentSlots.Count; i++)
            {
                if (LastSnapshot.EquipmentSlots[i].Slot == slot)
                {
                    snapshot = LastSnapshot.EquipmentSlots[i];
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        void OnEquipClicked()
        {
            if (_selectedItem == null || !LastSnapshot.HasPlayer || !_targetSlot.HasValue)
            {
                SetFeedback("inventory.feedback.equip_select");
                return;
            }

            EquipmentSlot slot = _targetSlot.Value;
            string selectedName = Resolve(ItemDetailSnapshotFactory.Create(_selectedItem).Name);
            _selectionHighlightSource = SelectionHighlightSource.Equipment;
            bool equipped = this.SendCommand(new EquipItemFromGridCommand(LastSnapshot.Player, _selectedItem, slot));

            if (!equipped)
            {
                SetFeedback("inventory.feedback.equip_failed");
                return;
            }

            RefreshInventory();
            _targetSlot = slot;
            RefreshSelection();
            SetFeedback(
                "inventory.feedback.equip_succeeded",
                selectedName,
                GetSlotName(slot));
            FocusSlot(slot);
        }

        void OnUnequipClicked()
        {
            if (!LastSnapshot.HasPlayer
                || !_targetSlot.HasValue
                || !TryGetSlotSnapshot(_targetSlot.Value, out EquipmentSlotSnapshot snapshot)
                || snapshot.Item == null)
            {
                SetFeedback("inventory.feedback.unequip_no_item");
                return;
            }

            EquipmentSlot slot = _targetSlot.Value;
            ItemInstance item = snapshot.Item;
            string displayName = Resolve(snapshot.Detail.Name);
            _selectionHighlightSource = SelectionHighlightSource.Equipment;
            bool unequipped = this.SendCommand(new UnequipItemCommand(LastSnapshot.Player, slot));

            if (!unequipped)
            {
                SetFeedback("inventory.feedback.unequip_failed");
                return;
            }

            _selectionState.SelectedInstanceId = item.InstanceId;
            RefreshInventory();
            _targetSlot = slot;
            RefreshSelection();
            SetFeedback("inventory.feedback.unequip_succeeded", displayName);
            FocusSlot(slot);
        }

        bool FocusSlot(EquipmentSlot slot)
        {
            if (!_slotButtons.TryGetValue(slot, out Button button))
            {
                return false;
            }

            button.Focus();
            return true;
        }

        void RegisterSlotPreviewCallbacks(EquipmentSlot slot)
        {
            Button button = _slotButtons[slot];
            button.RegisterCallback<PointerEnterEvent>(OnEquipmentPointerEnter);
            button.RegisterCallback<PointerLeaveEvent>(OnEquipmentPointerLeave);
            button.RegisterCallback<FocusInEvent>(OnEquipmentFocusIn);
            button.RegisterCallback<FocusOutEvent>(OnEquipmentFocusOut);
        }

        static bool IsSameItem(ItemInstance first, ItemInstance second)
        {
            if (first == null || second == null)
            {
                return first == second;
            }

            return first == second || first.InstanceId == second.InstanceId;
        }

        bool CancelDrag(bool showFeedback)
        {
            bool cancelled = _workspace?.CancelDrag() == true;
            if (cancelled)
            {
                _suppressTooltipUntilPreview = true;
                if (showFeedback) { SetFeedback("inventory.feedback.drag_cancelled"); }
            }
            return cancelled;
        }

        void OnEquipmentPointerEnter(PointerEnterEvent evt)
        {
            PreviewEquipment(evt.currentTarget);
        }

        void OnEquipmentPointerLeave(PointerLeaveEvent evt)
        {
            EndEquipmentPreview(evt.currentTarget);
        }

        void OnEquipmentFocusIn(FocusInEvent evt)
        {
            PreviewEquipment(evt.currentTarget);
        }

        void OnEquipmentFocusOut(FocusOutEvent evt)
        {
            EndEquipmentPreview(evt.currentTarget);
        }

        void PreviewEquipment(object target)
        {
            if (target is VisualElement element
                && TryGetSlotForButton(element, out EquipmentSlot slot)
                && TryGetSlotSnapshot(slot, out EquipmentSlotSnapshot snapshot)
                && snapshot.Item != null)
            {
                PreviewItem(snapshot.Item);
            }
        }

        void EndEquipmentPreview(object target)
        {
            if (target is VisualElement element
                && TryGetSlotForButton(element, out EquipmentSlot slot)
                && TryGetSlotSnapshot(slot, out EquipmentSlotSnapshot snapshot)
                && snapshot.Item != null)
            {
                EndPreview(snapshot.Item);
            }
        }

        bool TryGetSlotForButton(VisualElement element, out EquipmentSlot slot)
        {
            foreach (KeyValuePair<EquipmentSlot, Button> entry in _slotButtons)
            {
                if (entry.Value == element)
                {
                    slot = entry.Key;
                    return true;
                }
            }

            slot = default;
            return false;
        }

        bool TryGetItemSnapshot(ItemInstance item, out InventoryItemSnapshot snapshot)
        {
            for (int i = 0; i < LastSnapshot.Items.Count; i++)
            {
                if (LastSnapshot.Items[i].Item == item)
                {
                    snapshot = LastSnapshot.Items[i];
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        string BuildTooltipContext(ItemInstance item)
        {
            if (_targetSlot.HasValue && IsCompatible(item, _targetSlot.Value))
            {
                return Localize(
                    "ui",
                    "inventory.tooltip.candidate",
                    GetSlotName(_targetSlot.Value));
            }

            return Localize("ui", "inventory.tooltip.player");
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
            if (_grid == null)
            {
                return;
            }

            RefreshAttributes();
            RefreshEquipmentSlots();
            RefreshTargetSlotLabel();
            RefreshFeedbackText();
            RefreshDetail();
        }

        void SetFeedback(string entryKey, params object[] arguments)
        {
            _feedbackMessage = LocalizedMessage.Ui(entryKey, arguments);
            RefreshFeedbackText();
        }

        void RefreshFeedbackText()
        {
            if (_feedbackLabel != null && !_feedbackMessage.IsEmpty)
            {
                _feedbackLabel.text = Resolve(_feedbackMessage);
            }
        }

        string GetSlotName(EquipmentSlot slot)
        {
            string entryKey = "equipment.slot." + EquipmentSlots.GetKey(slot);
            return Localize("ui", entryKey);
        }

        string Localize(string tableName, string entryKey, params object[] arguments)
        {
            return Resolve(new LocalizedMessage(tableName, entryKey, arguments));
        }

        string Resolve(LocalizedMessage message)
        {
            return _localizationService?.GetString(message)
                ?? $"[{message.TableName}.{message.EntryKey}]";
        }

        static bool IsCompatible(ItemInstance item, EquipmentSlot slot)
        {
            return item != null && item.BaseDefinition != null && item.BaseDefinition.CanEquipTo(slot);
        }

        static string GetRarityClass(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic:
                    return "inventory-item--magic";
                case ItemRarity.Rare:
                    return "inventory-item--rare";
                case ItemRarity.Unique:
                    return "inventory-item--unique";
                default:
                    return "inventory-item--normal";
            }
        }

        static string GetSlotIconClass(EquipmentSlot slot)
        {
            string key = slot == EquipmentSlot.RingLeft || slot == EquipmentSlot.RingRight
                ? "ring" : EquipmentSlots.GetKey(slot).Replace('_', '-');
            return "inventory-equipment-slot-icon--" + key;
        }
    }
}
