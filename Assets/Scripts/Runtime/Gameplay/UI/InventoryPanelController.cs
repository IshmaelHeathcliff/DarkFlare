using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class InventoryPanelController : MonoBehaviour, IController
    {
        const float CellSize = 48f;
        const float CellGap = 4f;
        const float DragThreshold = 10f;
        const string DropValidClass = "inventory-cell--drop-valid";
        const string DropInvalidClass = "inventory-cell--drop-invalid";
        const string SlotDropValidClass = "inventory-equipment-slot--drop-valid";
        const string SlotDropInvalidClass = "inventory-equipment-slot--drop-invalid";
        const string ExternalDropValidClass = "inventory-external-drop--valid";
        const string ExternalDropInvalidClass = "inventory-external-drop--invalid";

        enum DragSourceKind
        {
            None,
            Inventory,
            Equipment
        }

        enum DropTargetKind
        {
            None,
            Inventory,
            Equipment,
            External
        }

        enum SelectionHighlightSource
        {
            None,
            Inventory,
            Equipment
        }

        [SerializeField]
        UIDocument _document;

        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();
        readonly Dictionary<ItemInstance, Button> _itemButtons = new Dictionary<ItemInstance, Button>();
        readonly Dictionary<EquipmentSlot, Button> _slotButtons = new Dictionary<EquipmentSlot, Button>();
        readonly ItemListViewState _selectionState = new ItemListViewState();
        readonly List<VisualElement> _gridCells = new List<VisualElement>();

        VisualElement _page;
        VisualElement _workbench;
        VisualElement _grid;
        VisualElement _gridFrame;
        VisualElement _dragLayer;
        VisualElement _menuPanel;
        Label _emptyLabel;
        Label _targetSlotLabel;
        Label _feedbackLabel;
        VisualElement _attributeGrid;
        Button _equipButton;
        Button _unequipButton;
        ItemTooltipView _tooltip;
        GameInput _gameInput;
        ItemInstance _selectedItem;
        ItemInstance _previewItem;
        ItemInstance _externalSlotItem;
        EquipmentSlot? _targetSlot;
        DragSourceKind _dragSourceKind;
        DropTargetKind _dropTargetKind;
        ItemInstance _dragItem;
        EquipmentSlot _dragSourceSlot;
        EquipmentSlot _dropTargetSlot;
        Button _dragSourceButton;
        Vector2 _pointerDownPosition;
        Vector2Int _dragGrabOffset;
        Vector2Int _dropOrigin;
        Vector2Int _keyboardTargetOrigin;
        int _dragPointerId = -1;
        bool _pointerPending;
        bool _isDragging;
        bool _isKeyboardDrag;
        bool _dropValid;
        bool _endingDrag;
        bool _suppressTooltipUntilPreview;
        bool _selectionHighlightSuppressed;
        SelectionHighlightSource _selectionHighlightSource;
        VisualElement _dragGhost;
        VisualElement _externalDropTarget;
        Func<ItemInstance, bool> _externalDropValidator;
        Action<ItemInstance> _externalDropHandler;

        public InventorySnapshot LastSnapshot { get; private set; }

        public ItemInstance SelectedItem => _selectedItem;

        public EquipmentSlot? TargetSlot => _targetSlot;

        public bool IsVisible { get; private set; }

        public bool IsOpen => IsVisible;

        public bool IsDragging => _isDragging;

        public bool IsPointerPending => _pointerPending;

        public event Action<ItemInstance> SelectionChanged;

        public event Action<ItemInstance> ContextActionRequested;

        public event Action<Vector2> NavigationBoundaryRequested;

        public event Action<ItemInstance> PreviewChanged;

        public void SetSelectionHighlightSuppressed(bool suppressed)
        {
            if (_selectionHighlightSuppressed == suppressed)
            {
                return;
            }

            _selectionHighlightSuppressed = suppressed;
            RefreshSelectionHighlights();
        }

        public bool IsInExternalSlot(ItemInstance item)
        {
            return IsSameItem(_externalSlotItem, item);
        }

        public void SetExternalSlotItem(ItemInstance item)
        {
            if (IsSameItem(_externalSlotItem, item))
            {
                return;
            }

            _externalSlotItem = item;
            RefreshInventory();
        }

        public void ClearSelection()
        {
            ItemInstance previous = _selectedItem;
            bool hadPreview = _previewItem != null;
            _selectedItem = null;
            _previewItem = null;
            _targetSlot = null;
            _selectionHighlightSource = SelectionHighlightSource.None;
            _selectionState.Reset();
            _suppressTooltipUntilPreview = false;

            if (_grid != null)
            {
                RefreshSelection();
            }

            if (previous != null)
            {
                SelectionChanged?.Invoke(null);
            }

            if (hadPreview)
            {
                PreviewChanged?.Invoke(null);
            }
        }

        public void ConfigureExternalDropTarget(
            VisualElement target,
            Func<ItemInstance, bool> validator,
            Action<ItemInstance> handler)
        {
            ClearExternalDropTarget();
            _externalDropTarget = target;
            _externalDropValidator = validator;
            _externalDropHandler = handler;
        }

        public void ClearExternalDropTarget(VisualElement target)
        {
            if (target != null && target != _externalDropTarget)
            {
                return;
            }

            ClearExternalDropTarget();
        }

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
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
            _gridCells.Clear();
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
                SelectionChanged?.Invoke(_selectedItem);
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

        public void ShowMerchantTooltip(ItemInstance item, string context)
        {
            if (!_isDragging)
            {
                _suppressTooltipUntilPreview = false;
                _tooltip?.Show(item, context, ItemTooltipSide.Right);
            }
        }

        public void RestoreTooltip()
        {
            RefreshDetail();
        }

        public void ShowPlayerTooltip(ItemInstance item, string context)
        {
            if (!_isDragging && item != null)
            {
                _tooltip?.Show(item, context, ItemTooltipSide.Left);
            }
            else
            {
                _tooltip?.Hide();
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

            if (!BindVisualTree())
            {
                return;
            }

            _gameInput = this.GetUtility<GameInput>();

            if (_gameInput != null)
            {
                _gameInput.RearrangePerformed += OnRearrangePerformed;
                _gameInput.NavigatePerformed += OnNavigatePerformed;
                _gameInput.CancelRequested += OnCancelRequested;
            }

            RegisterEvents();
            RefreshInventory();
            SetVisible(IsVisible);
            Debug.Log("[InventoryPanelController] 背包与四槽装备面板初始化完成", this);
        }

        void OnDisable()
        {
            CancelDrag(false);
            ClearExternalDropTarget();
            UnbindButtons();

            if (_gameInput != null)
            {
                _gameInput.RearrangePerformed -= OnRearrangePerformed;
                _gameInput.NavigatePerformed -= OnNavigatePerformed;
                _gameInput.CancelRequested -= OnCancelRequested;
            }

            for (int i = 0; i < _eventRegistrations.Count; i++)
            {
                _eventRegistrations[i].UnRegister();
            }

            _eventRegistrations.Clear();
            _itemButtons.Clear();
            _slotButtons.Clear();
            _gridCells.Clear();
            _selectionState.Reset();
            _page = null;
            _workbench = null;
            _grid = null;
            _gridFrame = null;
            _dragLayer = null;
            _menuPanel = null;
            _emptyLabel = null;
            _targetSlotLabel = null;
            _feedbackLabel = null;
            _attributeGrid = null;
            _equipButton = null;
            _unequipButton = null;
            _tooltip = null;
            _gameInput = null;
            _selectedItem = null;
            _previewItem = null;
            _externalSlotItem = null;
            _targetSlot = null;
            _suppressTooltipUntilPreview = false;
            _selectionHighlightSuppressed = false;
            _selectionHighlightSource = SelectionHighlightSource.None;
            SelectionChanged = null;
            ContextActionRequested = null;
            NavigationBoundaryRequested = null;
            PreviewChanged = null;
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
                Debug.LogError("[InventoryPanelController] 缺少 UIDocument，无法初始化背包面板", this);
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            _page = root.Q<VisualElement>("inventory-page");
            _workbench = root.Q<VisualElement>("item-workbench-shared");
            _grid = root.Q<VisualElement>("inventory-grid");
            _gridFrame = root.Q<VisualElement>("inventory-grid-frame");
            _dragLayer = root.Q<VisualElement>("item-drag-layer");
            _menuPanel = root.Q<VisualElement>("game-menu-panel");
            _emptyLabel = root.Q<Label>("inventory-empty");
            _targetSlotLabel = root.Q<Label>("inventory-target-slot");
            _feedbackLabel = root.Q<Label>("inventory-feedback");
            _attributeGrid = root.Q<VisualElement>("inventory-attribute-grid");
            _equipButton = root.Q<Button>("inventory-equip");
            _unequipButton = root.Q<Button>("inventory-unequip");
            _tooltip = new ItemTooltipView(
                root.Q<VisualElement>("item-tooltip"),
                root.Q<VisualElement>("item-tooltip-layer"),
                _menuPanel,
                root.Q<Label>("item-tooltip-context"));
            _slotButtons.Clear();
            AddSlotButton(root, EquipmentSlot.Weapon, "inventory-slot-weapon");
            AddSlotButton(root, EquipmentSlot.Armor, "inventory-slot-armor");
            AddSlotButton(root, EquipmentSlot.RingLeft, "inventory-slot-ring-left");
            AddSlotButton(root, EquipmentSlot.RingRight, "inventory-slot-ring-right");

            if (_page == null
                || _workbench == null
                || _grid == null
                || _gridFrame == null
                || _dragLayer == null
                || _menuPanel == null
                || _emptyLabel == null
                || _targetSlotLabel == null
                || _feedbackLabel == null
                || _attributeGrid == null
                || _equipButton == null
                || _unequipButton == null
                || _slotButtons.Count != EquipmentSlots.All.Count
                || !_tooltip.IsValid)
            {
                Debug.LogError("[InventoryPanelController] 背包 UXML 缺少四槽装备面板所需的命名元素", this);
                return false;
            }

            _equipButton.clicked += OnEquipClicked;
            _unequipButton.clicked += OnUnequipClicked;
            _slotButtons[EquipmentSlot.Weapon].clicked += OnWeaponSlotClicked;
            _slotButtons[EquipmentSlot.Armor].clicked += OnArmorSlotClicked;
            _slotButtons[EquipmentSlot.RingLeft].clicked += OnRingLeftSlotClicked;
            _slotButtons[EquipmentSlot.RingRight].clicked += OnRingRightSlotClicked;
            RegisterSlotDragCallbacks(EquipmentSlot.Weapon);
            RegisterSlotDragCallbacks(EquipmentSlot.Armor);
            RegisterSlotDragCallbacks(EquipmentSlot.RingLeft);
            RegisterSlotDragCallbacks(EquipmentSlot.RingRight);
            _workbench.RegisterCallback<PointerMoveEvent>(OnDragPointerMove, TrickleDown.TrickleDown);
            _workbench.RegisterCallback<PointerUpEvent>(OnDragPointerUp, TrickleDown.TrickleDown);
            _workbench.RegisterCallback<PointerCancelEvent>(OnDragPointerCancel, TrickleDown.TrickleDown);
            return true;
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

            if (_slotButtons.TryGetValue(EquipmentSlot.Weapon, out Button weapon))
            {
                weapon.clicked -= OnWeaponSlotClicked;
            }

            if (_slotButtons.TryGetValue(EquipmentSlot.Armor, out Button armor))
            {
                armor.clicked -= OnArmorSlotClicked;
            }

            if (_slotButtons.TryGetValue(EquipmentSlot.RingLeft, out Button ringLeft))
            {
                ringLeft.clicked -= OnRingLeftSlotClicked;
            }

            if (_slotButtons.TryGetValue(EquipmentSlot.RingRight, out Button ringRight))
            {
                ringRight.clicked -= OnRingRightSlotClicked;
            }

            foreach (Button button in _slotButtons.Values)
            {
                button.UnregisterCallback<PointerDownEvent>(OnEquipmentPointerDown, TrickleDown.TrickleDown);
                button.UnregisterCallback<PointerMoveEvent>(OnDragPointerMove, TrickleDown.TrickleDown);
                button.UnregisterCallback<PointerUpEvent>(OnDragPointerUp, TrickleDown.TrickleDown);
                button.UnregisterCallback<PointerEnterEvent>(OnEquipmentPointerEnter);
                button.UnregisterCallback<PointerLeaveEvent>(OnEquipmentPointerLeave);
                button.UnregisterCallback<FocusInEvent>(OnEquipmentFocusIn);
                button.UnregisterCallback<FocusOutEvent>(OnEquipmentFocusOut);
            }

            if (_workbench != null)
            {
                _workbench.UnregisterCallback<PointerMoveEvent>(OnDragPointerMove, TrickleDown.TrickleDown);
                _workbench.UnregisterCallback<PointerUpEvent>(OnDragPointerUp, TrickleDown.TrickleDown);
                _workbench.UnregisterCallback<PointerCancelEvent>(OnDragPointerCancel, TrickleDown.TrickleDown);
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
                    _gridCells.Add(cell);
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
            button.RegisterCallback<PointerEnterEvent>(_ => PreviewItem(item.Item));
            button.RegisterCallback<PointerLeaveEvent>(_ => EndPreview(item.Item));
            button.RegisterCallback<FocusInEvent>(_ => PreviewItem(item.Item));
            button.RegisterCallback<FocusOutEvent>(_ => EndPreview(item.Item));
            button.RegisterCallback<PointerDownEvent>(
                evt => OnInventoryPointerDown(evt, item, button),
                TrickleDown.TrickleDown);
            button.RegisterCallback<PointerMoveEvent>(OnDragPointerMove, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(OnDragPointerUp, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(
                evt => OnInventoryContextPointerUp(evt, item.Item),
                TrickleDown.TrickleDown);
            return button;
        }

        void SelectItem(ItemInstance item)
        {
            ItemInstance previous = _selectedItem;
            _suppressTooltipUntilPreview = false;
            _selectedItem = item;
            _selectionHighlightSource = SelectionHighlightSource.Inventory;
            CaptureSelectionState();
            ResolveTargetSlotForSelectedItem(true);
            RefreshSelection();

            if (previous != _selectedItem)
            {
                SelectionChanged?.Invoke(_selectedItem);
            }
        }

        void SelectSlot(EquipmentSlot slot)
        {
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
                SelectionChanged?.Invoke(_selectedItem);
            }
        }

        void ResolveTargetSlotForSelectedItem(bool clearAmbiguousRingTarget)
        {
            if (_selectedItem == null || _selectedItem.BaseDefinition == null)
            {
                return;
            }

            EquipmentSlotMask slots = _selectedItem.BaseDefinition.AllowedEquipmentSlots;

            if (slots == EquipmentSlotMask.Weapon)
            {
                _targetSlot = EquipmentSlot.Weapon;
            }
            else if (slots == EquipmentSlotMask.Armor)
            {
                _targetSlot = EquipmentSlot.Armor;
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
            if (_isDragging || _suppressTooltipUntilPreview)
            {
                return;
            }

            _previewItem = item;
            RefreshSelectionHighlights();
            RefreshDetail();
            PreviewChanged?.Invoke(item);
        }

        void EndPreview(ItemInstance item)
        {
            if (_suppressTooltipUntilPreview)
            {
                _suppressTooltipUntilPreview = false;
                _tooltip.Hide();
                return;
            }

            if (_previewItem != item)
            {
                return;
            }

            _previewItem = null;
            RefreshSelectionHighlights();
            RefreshDetail();
            PreviewChanged?.Invoke(null);
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
                && IsCompatible(selected.Item, _targetSlot.Value);
            bool canUnequip = LastSnapshot.HasPlayer
                && hasTarget
                && TryGetSlotSnapshot(_targetSlot.Value, out EquipmentSlotSnapshot slotSnapshot)
                && slotSnapshot.Item != null;
            _equipButton.SetEnabled(canEquip);
            _unequipButton.SetEnabled(canUnequip);

            if (!LastSnapshot.HasPlayer)
            {
                _feedbackLabel.text = "等待玩家生成";
            }
            else if (hasCandidate && !hasTarget && selected.CompatibleSlots == EquipmentSlotMask.Rings)
            {
                _feedbackLabel.text = "请选择左戒指或右戒指槽";
            }
            else if (canEquip)
            {
                _feedbackLabel.text = canUnequip ? "可替换目标槽装备" : "可装备到目标槽";
            }
            else if (canUnequip)
            {
                _feedbackLabel.text = "可卸下当前槽位装备";
            }
            else if (!hasCandidate)
            {
                _feedbackLabel.text = "选择背包物品或装备槽";
            }
            else
            {
                _feedbackLabel.text = selected.CanEquip ? "请选择兼容的装备槽" : "该物品不能装备";
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
                slotLabel.text = snapshot.Item != null
                    ? snapshot.SlotName
                    : $"{snapshot.SlotName} · 空";
                button.tooltip = snapshot.Item != null
                    ? $"{snapshot.Detail.DisplayName} · {ItemDetailFormatter.GetRarityText(snapshot.Detail.Rarity)}"
                    : $"{snapshot.SlotName}为空";
                button.EnableInClassList("inventory-equipment-slot--filled", snapshot.Item != null);
            }
        }

        void RefreshSelectionHighlights()
        {
            ItemInstance highlightedInventoryItem = null;
            EquipmentSlot? highlightedEquipmentSlot = null;

            if (_selectionHighlightSuppressed)
            {
                highlightedInventoryItem = null;
                highlightedEquipmentSlot = null;
            }
            else if (_previewItem != null)
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
                entry.Value.EnableInClassList(
                    "inventory-item--selected",
                    entry.Key == highlightedInventoryItem);
            }

            foreach (KeyValuePair<EquipmentSlot, Button> entry in _slotButtons)
            {
                entry.Value.EnableInClassList(
                    "inventory-equipment-slot--selected",
                    highlightedEquipmentSlot == entry.Key);
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
            if (_isDragging)
            {
                _tooltip.Hide();
                return;
            }

            if (_suppressTooltipUntilPreview && _previewItem == null)
            {
                _tooltip.Hide();
                return;
            }

            if (_previewItem != null)
            {
                _tooltip.Show(
                    _previewItem,
                    BuildTooltipContext(_previewItem),
                    ItemTooltipSide.Left);
                return;
            }

            _tooltip.Hide();
        }

        void RefreshTargetSlotLabel()
        {
            if (!_targetSlot.HasValue)
            {
                _targetSlotLabel.text = "操作槽位：未选择";
                return;
            }

            _targetSlotLabel.text = $"操作槽位：{EquipmentSlots.GetDisplayName(_targetSlot.Value)}";
        }

        void RefreshAttributes()
        {
            HudSnapshot snapshot = this.SendQuery(new GetHudSnapshotQuery());
            IReadOnlyList<HudAttributeValue> attributes = snapshot.Attributes.Values;
            int firstColumnCount = (attributes.Count + 1) / 2;
            _attributeGrid.Clear();
            _attributeGrid.Add(CreateAttributeColumn(attributes, 0, firstColumnCount, false, snapshot.HasPlayer));
            _attributeGrid.Add(CreateAttributeColumn(
                attributes,
                firstColumnCount,
                attributes.Count,
                true,
                snapshot.HasPlayer));
        }

        static VisualElement CreateAttributeColumn(
            IReadOnlyList<HudAttributeValue> attributes,
            int startIndex,
            int endIndex,
            bool separated,
            bool hasPlayer)
        {
            VisualElement column = new VisualElement();
            column.AddToClassList("inventory-attribute-column");

            if (separated)
            {
                column.AddToClassList("inventory-attribute-column--separated");
            }

            for (int i = startIndex; i < endIndex; i++)
            {
                HudAttributeValue attribute = attributes[i];
                VisualElement row = new VisualElement();
                row.AddToClassList("inventory-attribute-row");
                Label name = new Label(attribute.DisplayName);
                name.AddToClassList("inventory-attribute-name");
                Label value = new Label
                {
                    name = $"inventory-attribute-{attribute.StatId.Replace('_', '-')}",
                    text = attribute.IsPercentage
                        ? FormatPercentage(attribute.Value, hasPlayer)
                        : FormatAttribute(attribute.Value, hasPlayer),
                };
                value.AddToClassList("inventory-attribute-value");
                AddAttributeColorClass(value, attribute.StatId);
                row.Add(name);
                row.Add(value);
                column.Add(row);
            }

            return column;
        }

        static void AddAttributeColorClass(VisualElement value, string statId)
        {
            if (statId == StatIds.FireDamage || statId == StatIds.FireResistance)
            {
                value.AddToClassList("inventory-attribute-value--fire");
            }
            else if (statId == StatIds.ColdDamage || statId == StatIds.ColdResistance)
            {
                value.AddToClassList("inventory-attribute-value--cold");
            }
            else if (statId == StatIds.LightningDamage || statId == StatIds.LightningResistance)
            {
                value.AddToClassList("inventory-attribute-value--lightning");
            }
            else if (statId == StatIds.ChaosDamage || statId == StatIds.ChaosResistance)
            {
                value.AddToClassList("inventory-attribute-value--chaos");
            }
        }

        static string FormatAttribute(float value, bool hasPlayer)
        {
            return hasPlayer ? value.ToString("0.##") : "--";
        }

        static string FormatPercentage(float value, bool hasPlayer)
        {
            return hasPlayer ? $"{value:0.#}%" : "--";
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
                _feedbackLabel.text = "请选择可装备物品和目标槽位";
                return;
            }

            EquipmentSlot slot = _targetSlot.Value;
            string selectedName = _selectedItem.BaseDefinition.DisplayName;
            _selectionHighlightSource = SelectionHighlightSource.Equipment;
            bool equipped = this.SendCommand(new EquipItemCommand(LastSnapshot.Player, _selectedItem, slot));

            if (!equipped)
            {
                _feedbackLabel.text = "装备失败，请检查背包空间、目标槽位和物品类型";
                return;
            }

            RefreshInventory();
            _targetSlot = slot;
            RefreshSelection();
            _feedbackLabel.text = $"已将 {selectedName} 装备到{EquipmentSlots.GetDisplayName(slot)}";
            FocusSlot(slot);
        }

        void OnUnequipClicked()
        {
            if (!LastSnapshot.HasPlayer
                || !_targetSlot.HasValue
                || !TryGetSlotSnapshot(_targetSlot.Value, out EquipmentSlotSnapshot snapshot)
                || snapshot.Item == null)
            {
                _feedbackLabel.text = "当前槽位没有可卸下的装备";
                return;
            }

            EquipmentSlot slot = _targetSlot.Value;
            ItemInstance item = snapshot.Item;
            string displayName = snapshot.Detail.DisplayName;
            _selectionHighlightSource = SelectionHighlightSource.Equipment;
            bool unequipped = this.SendCommand(new UnequipItemCommand(LastSnapshot.Player, slot));

            if (!unequipped)
            {
                _feedbackLabel.text = "卸下失败，请检查背包空间";
                return;
            }

            _selectionState.SelectedInstanceId = item.InstanceId;
            RefreshInventory();
            _targetSlot = slot;
            RefreshSelection();
            _feedbackLabel.text = $"已卸下 {displayName}";
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

        void OnWeaponSlotClicked()
        {
            SelectSlot(EquipmentSlot.Weapon);
        }

        void OnArmorSlotClicked()
        {
            SelectSlot(EquipmentSlot.Armor);
        }

        void OnRingLeftSlotClicked()
        {
            SelectSlot(EquipmentSlot.RingLeft);
        }

        void OnRingRightSlotClicked()
        {
            SelectSlot(EquipmentSlot.RingRight);
        }

        void RegisterSlotDragCallbacks(EquipmentSlot slot)
        {
            Button button = _slotButtons[slot];
            button.RegisterCallback<PointerDownEvent>(OnEquipmentPointerDown, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerMoveEvent>(OnDragPointerMove, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(OnDragPointerUp, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerEnterEvent>(OnEquipmentPointerEnter);
            button.RegisterCallback<PointerLeaveEvent>(OnEquipmentPointerLeave);
            button.RegisterCallback<FocusInEvent>(OnEquipmentFocusIn);
            button.RegisterCallback<FocusOutEvent>(OnEquipmentFocusOut);
        }

        void OnInventoryPointerDown(
            PointerDownEvent evt,
            InventoryItemSnapshot snapshot,
            Button button)
        {
            if (evt.button != 0 || _pointerPending || _isDragging || !LastSnapshot.HasPlayer)
            {
                return;
            }

            Vector2Int size = snapshot.Detail.GridSize;
            float step = CellSize + CellGap;
            Vector2 localPosition = evt.localPosition;
            Vector2Int grabOffset = new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(localPosition.x / step), 0, Mathf.Max(0, size.x - 1)),
                Mathf.Clamp(Mathf.FloorToInt(localPosition.y / step), 0, Mathf.Max(0, size.y - 1)));
            BeginPointerDrag(
                evt,
                DragSourceKind.Inventory,
                snapshot.Item,
                default,
                button,
                grabOffset);
        }

        void OnEquipmentPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0
                || _pointerPending
                || _isDragging
                || !LastSnapshot.HasPlayer
                || evt.currentTarget is not Button button
                || !TryGetSlotForButton(button, out EquipmentSlot slot)
                || !TryGetSlotSnapshot(slot, out EquipmentSlotSnapshot snapshot)
                || snapshot.Item == null)
            {
                return;
            }

            BeginPointerDrag(
                evt,
                DragSourceKind.Equipment,
                snapshot.Item,
                slot,
                button,
                Vector2Int.zero);
        }

        void OnInventoryContextPointerUp(PointerUpEvent evt, ItemInstance item)
        {
            if (evt.button != 1 || _isDragging || item == null)
            {
                return;
            }

            SelectItem(item);
            ContextActionRequested?.Invoke(item);
            evt.StopPropagation();
        }

        void BeginPointerDrag(
            PointerDownEvent evt,
            DragSourceKind sourceKind,
            ItemInstance item,
            EquipmentSlot sourceSlot,
            Button button,
            Vector2Int grabOffset)
        {
            _dragSourceKind = sourceKind;
            _dragItem = item;
            _dragSourceSlot = sourceSlot;
            _dragSourceButton = button;
            _dragGrabOffset = grabOffset;
            _pointerDownPosition = evt.position;
            _dragPointerId = evt.pointerId;
            _pointerPending = true;
        }

        void OnDragPointerMove(PointerMoveEvent evt)
        {
            if (_dragPointerId != evt.pointerId || (!_pointerPending && !_isDragging))
            {
                return;
            }

            if (_pointerPending && (evt.pressedButtons & 1) == 0)
            {
                ResetDragState();
                return;
            }

            Vector2 position = evt.position;

            if (_pointerPending
                && !_isDragging
                && Vector2.Distance(position, _pointerDownPosition) >= DragThreshold)
            {
                StartPointerDrag(position);
            }

            if (!_isDragging || _isKeyboardDrag)
            {
                return;
            }

            UpdateDragGhost(position);
            ResolvePointerDrop(position);
            evt.StopPropagation();
            evt.PreventDefault();
        }

        void OnDragPointerUp(PointerUpEvent evt)
        {
            if (_dragPointerId != evt.pointerId)
            {
                return;
            }

            if (!_isDragging)
            {
                ReleasePointerCapture();
                ResetDragState();
                return;
            }

            ResolvePointerDrop(evt.position);
            DropTargetKind targetKind = _dropTargetKind;
            EquipmentSlot targetSlot = _dropTargetSlot;
            Vector2Int targetOrigin = _dropOrigin;
            bool valid = _dropValid;
            ItemInstance item = _dragItem;
            DragSourceKind sourceKind = _dragSourceKind;
            EquipmentSlot sourceSlot = _dragSourceSlot;
            FinishDragVisuals();
            ExecuteDrop(sourceKind, item, sourceSlot, targetKind, targetSlot, targetOrigin, valid);
            evt.StopPropagation();
            evt.PreventDefault();
        }

        void OnDragPointerCancel(PointerCancelEvent evt)
        {
            if (_dragPointerId != evt.pointerId)
            {
                return;
            }

            if (_pointerPending || _isDragging)
            {
                CancelDrag(false);
            }
        }

        void StartPointerDrag(Vector2 position)
        {
            _pointerPending = false;
            _isDragging = true;
            _isKeyboardDrag = false;
            _workbench?.CapturePointer(_dragPointerId);
            _dragSourceButton.EnableInClassList("inventory-item--drag-source", true);
            _tooltip.Hide();
            CreateDragGhost();
            UpdateDragGhost(position);
            ResolvePointerDrop(position);
        }

        void CreateDragGhost()
        {
            if (_dragLayer == null || _dragItem == null || _dragItem.BaseDefinition == null)
            {
                return;
            }

            Vector2Int size = _dragItem.BaseDefinition.GridSize;
            _dragGhost = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            _dragGhost.AddToClassList("item-drag-ghost");
            _dragGhost.style.width = size.x * CellSize + (size.x - 1) * CellGap;
            _dragGhost.style.height = size.y * CellSize + (size.y - 1) * CellGap;
            ItemDetailSnapshot detail = ItemDetailSnapshotFactory.Create(_dragItem);
            ItemVisualPresenter.ApplyIcon(_dragGhost, detail.IconGuid);
            _dragLayer.Add(_dragGhost);
            _dragGhost.BringToFront();
        }

        void UpdateDragGhost(Vector2 position)
        {
            if (_dragGhost == null || _dragLayer == null)
            {
                return;
            }

            float step = CellSize + CellGap;
            Vector2 localPosition = _dragLayer.WorldToLocal(position);
            _dragGhost.style.left = localPosition.x - _dragGrabOffset.x * step - CellSize * 0.5f;
            _dragGhost.style.top = localPosition.y - _dragGrabOffset.y * step - CellSize * 0.5f;
        }

        void ResolvePointerDrop(Vector2 position)
        {
            ClearDropVisuals();

            foreach (KeyValuePair<EquipmentSlot, Button> entry in _slotButtons)
            {
                if (!entry.Value.worldBound.Contains(position))
                {
                    continue;
                }

                ResolveEquipmentDrop(entry.Key);
                return;
            }

            if (_externalDropTarget != null && _externalDropTarget.worldBound.Contains(position))
            {
                ResolveExternalDrop();
                return;
            }

            if (_grid == null || !_grid.worldBound.Contains(position))
            {
                _dropTargetKind = DropTargetKind.None;
                _dropValid = false;
                return;
            }

            float step = CellSize + CellGap;
            Vector2 localPosition = _grid.WorldToLocal(position);
            Vector2Int origin = new Vector2Int(
                Mathf.FloorToInt(localPosition.x / step) - _dragGrabOffset.x,
                Mathf.FloorToInt(localPosition.y / step) - _dragGrabOffset.y);
            ResolveInventoryDrop(origin);
        }

        void ResolveEquipmentDrop(EquipmentSlot slot)
        {
            ClearDropVisuals();
            _dropTargetKind = DropTargetKind.Equipment;
            _dropTargetSlot = slot;
            _dropValid = _dragSourceKind == DragSourceKind.Inventory
                ? this.SendQuery(new CanEquipItemFromGridQuery(LastSnapshot.Player, _dragItem, slot))
                : this.SendQuery(new CanMoveEquippedItemQuery(LastSnapshot.Player, _dragSourceSlot, slot));

            if (_slotButtons.TryGetValue(slot, out Button button))
            {
                button.EnableInClassList(SlotDropValidClass, _dropValid);
                button.EnableInClassList(SlotDropInvalidClass, !_dropValid);
            }
        }

        void ResolveInventoryDrop(Vector2Int origin)
        {
            ClearDropVisuals();
            _dropTargetKind = DropTargetKind.Inventory;
            _dropOrigin = origin;
            _dropValid = _dragSourceKind == DragSourceKind.Inventory
                ? this.SendQuery(new CanMoveInventoryItemQuery(_dragItem, origin))
                : this.SendQuery(new CanUnequipItemToGridQuery(LastSnapshot.Player, _dragSourceSlot, origin));
            HighlightGridFootprint(origin, _dropValid);
        }

        void ResolveExternalDrop()
        {
            ClearDropVisuals();
            _dropTargetKind = DropTargetKind.External;
            _dropValid = _dragSourceKind == DragSourceKind.Inventory
                && _dragItem != null
                && _externalDropValidator != null
                && _externalDropValidator(_dragItem);
            _externalDropTarget.EnableInClassList(ExternalDropValidClass, _dropValid);
            _externalDropTarget.EnableInClassList(ExternalDropInvalidClass, !_dropValid);
        }

        void HighlightGridFootprint(Vector2Int origin, bool valid)
        {
            if (_dragItem == null || _dragItem.BaseDefinition == null)
            {
                return;
            }

            Vector2Int size = _dragItem.BaseDefinition.GridSize;
            string className = valid ? DropValidClass : DropInvalidClass;

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int cellX = origin.x + x;
                    int cellY = origin.y + y;

                    if (cellX < 0
                        || cellY < 0
                        || cellX >= LastSnapshot.Width
                        || cellY >= LastSnapshot.Height)
                    {
                        continue;
                    }

                    int index = cellY * LastSnapshot.Width + cellX;

                    if (index >= 0 && index < _gridCells.Count)
                    {
                        _gridCells[index].AddToClassList(className);
                    }
                }
            }
        }

        void ExecuteDrop(
            DragSourceKind sourceKind,
            ItemInstance item,
            EquipmentSlot sourceSlot,
            DropTargetKind targetKind,
            EquipmentSlot targetSlot,
            Vector2Int targetOrigin,
            bool valid)
        {
            _suppressTooltipUntilPreview = true;

            if (!valid || item == null)
            {
                _feedbackLabel.text = "无法放置到目标位置";
                RestoreTooltip();
                return;
            }

            bool completed = false;

            if (sourceKind == DragSourceKind.Inventory && targetKind == DropTargetKind.Inventory)
            {
                _selectionHighlightSource = SelectionHighlightSource.Inventory;
                _selectionState.SelectedInstanceId = item.InstanceId;
                completed = this.SendCommand(new MoveInventoryItemCommand(item, targetOrigin));
            }
            else if (sourceKind == DragSourceKind.Inventory && targetKind == DropTargetKind.Equipment)
            {
                _selectionHighlightSource = SelectionHighlightSource.Equipment;
                _targetSlot = targetSlot;
                completed = this.SendCommand(new EquipItemFromGridCommand(LastSnapshot.Player, item, targetSlot));
            }
            else if (sourceKind == DragSourceKind.Equipment && targetKind == DropTargetKind.Inventory)
            {
                _selectionHighlightSource = SelectionHighlightSource.Inventory;
                _selectionState.SelectedInstanceId = item.InstanceId;
                completed = this.SendCommand(new UnequipItemToGridCommand(
                    LastSnapshot.Player,
                    sourceSlot,
                    targetOrigin));
            }
            else if (sourceKind == DragSourceKind.Equipment && targetKind == DropTargetKind.Equipment)
            {
                _selectionHighlightSource = SelectionHighlightSource.Equipment;
                _targetSlot = targetSlot;
                completed = this.SendCommand(new MoveEquippedItemCommand(
                    LastSnapshot.Player,
                    sourceSlot,
                    targetSlot));
            }
            else if (sourceKind == DragSourceKind.Inventory
                     && targetKind == DropTargetKind.External
                     && _externalDropHandler != null)
            {
                _externalDropHandler(item);
                completed = true;
            }

            if (!completed)
            {
                _feedbackLabel.text = "整理失败，背包或装备状态已变化";
                RefreshInventory();
                return;
            }

            _feedbackLabel.text = targetKind == DropTargetKind.External
                ? "已放入打造槽"
                : "物品位置已更新";

            if (targetKind != DropTargetKind.External)
            {
                RefreshInventory();
            }
        }

        void OnRearrangePerformed()
        {
            if (_workbench == null || _workbench.panel == null || !LastSnapshot.HasPlayer)
            {
                return;
            }

            if (_isDragging)
            {
                if (!_isKeyboardDrag)
                {
                    return;
                }

                DropTargetKind targetKind = _dropTargetKind;
                EquipmentSlot targetSlot = _dropTargetSlot;
                Vector2Int targetOrigin = _dropOrigin;
                bool valid = _dropValid;
                ItemInstance item = _dragItem;
                DragSourceKind sourceKind = _dragSourceKind;
                EquipmentSlot sourceSlot = _dragSourceSlot;
                FinishDragVisuals();
                ExecuteDrop(sourceKind, item, sourceSlot, targetKind, targetSlot, targetOrigin, valid);
                return;
            }

            Focusable focused = _workbench.panel.focusController.focusedElement;

            if (focused is not VisualElement focusedElement)
            {
                return;
            }

            foreach (KeyValuePair<ItemInstance, Button> entry in _itemButtons)
            {
                if (entry.Value != focusedElement)
                {
                    continue;
                }

                if (TryGetItemSnapshot(entry.Key, out InventoryItemSnapshot snapshot))
                {
                    StartKeyboardDrag(
                        DragSourceKind.Inventory,
                        entry.Key,
                        default,
                        entry.Value,
                        snapshot.Placement.position);
                }

                return;
            }

            if (TryGetSlotForButton(focusedElement, out EquipmentSlot slot)
                && TryGetSlotSnapshot(slot, out EquipmentSlotSnapshot slotSnapshot)
                && slotSnapshot.Item != null)
            {
                StartKeyboardDrag(
                    DragSourceKind.Equipment,
                    slotSnapshot.Item,
                    slot,
                    _slotButtons[slot],
                    Vector2Int.zero);
            }
        }

        void StartKeyboardDrag(
            DragSourceKind sourceKind,
            ItemInstance item,
            EquipmentSlot sourceSlot,
            Button sourceButton,
            Vector2Int sourceOrigin)
        {
            _dragSourceKind = sourceKind;
            _dragItem = item;
            _dragSourceSlot = sourceSlot;
            _dragSourceButton = sourceButton;
            _keyboardTargetOrigin = sourceOrigin;
            _dropTargetKind = DropTargetKind.None;
            _dropValid = false;
            _isDragging = true;
            _isKeyboardDrag = true;
            sourceButton.EnableInClassList("inventory-item--drag-source", true);
            _tooltip.Hide();
            _feedbackLabel.text = "已拿起物品：方向键选择位置，再按整理键放下";
        }

        void OnNavigatePerformed(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.25f)
            {
                return;
            }

            if (!_isDragging)
            {
                RequestBoundaryNavigation(direction);
                return;
            }

            if (!_isKeyboardDrag)
            {
                return;
            }

            Vector2Int delta = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                ? new Vector2Int(direction.x > 0f ? 1 : -1, 0)
                : new Vector2Int(0, direction.y > 0f ? -1 : 1);

            if (_dropTargetKind == DropTargetKind.Equipment)
            {
                if (delta.x > 0)
                {
                    _keyboardTargetOrigin = Vector2Int.zero;
                    ResolveInventoryDrop(_keyboardTargetOrigin);
                }
                else if (TryFindCompatibleSlot(_dropTargetSlot, delta, out EquipmentSlot nextSlot))
                {
                    ResolveEquipmentDrop(nextSlot);
                }
            }
            else
            {
                Vector2Int candidate = _dropTargetKind == DropTargetKind.Inventory
                    ? _keyboardTargetOrigin + delta
                    : GetKeyboardStartOrigin() + delta;

                if (candidate.x < 0 && TryFindCompatibleSlot(_dragSourceSlot, delta, out EquipmentSlot slot))
                {
                    ResolveEquipmentDrop(slot);
                }
                else
                {
                    _keyboardTargetOrigin = candidate;
                    ClearDropVisuals();
                    ResolveInventoryDrop(candidate);
                }
            }

            _dragSourceButton?.Focus();
        }

        void RequestBoundaryNavigation(Vector2 direction)
        {
            if (direction.x <= 0f
                || Mathf.Abs(direction.x) < Mathf.Abs(direction.y)
                || _workbench == null
                || _workbench.panel == null)
            {
                return;
            }

            Focusable focused = _workbench.panel.focusController.focusedElement;

            if (focused is not VisualElement focusedElement)
            {
                return;
            }

            Button focusedButton = null;

            foreach (Button button in _itemButtons.Values)
            {
                if (button == focusedElement)
                {
                    focusedButton = button;
                    break;
                }
            }

            if (focusedButton == null)
            {
                return;
            }

            float focusedCenter = focusedButton.worldBound.center.x;

            foreach (Button button in _itemButtons.Values)
            {
                if (button != focusedButton && button.worldBound.center.x > focusedCenter + 1f)
                {
                    return;
                }
            }

            NavigationBoundaryRequested?.Invoke(direction);
        }

        static bool IsSameItem(ItemInstance first, ItemInstance second)
        {
            if (first == null || second == null)
            {
                return first == second;
            }

            return first == second || first.InstanceId == second.InstanceId;
        }

        Vector2Int GetKeyboardStartOrigin()
        {
            if (_dragSourceKind == DragSourceKind.Inventory
                && TryGetItemSnapshot(_dragItem, out InventoryItemSnapshot snapshot))
            {
                return snapshot.Placement.position;
            }

            return Vector2Int.zero;
        }

        bool TryFindCompatibleSlot(
            EquipmentSlot current,
            Vector2Int direction,
            out EquipmentSlot result)
        {
            int startIndex = 0;

            for (int i = 0; i < EquipmentSlots.All.Count; i++)
            {
                if (EquipmentSlots.All[i] == current)
                {
                    startIndex = i;
                    break;
                }
            }

            int step = direction.y < 0 || direction.x > 0 ? 1 : -1;

            for (int offset = 1; offset <= EquipmentSlots.All.Count; offset++)
            {
                int index = (startIndex + step * offset + EquipmentSlots.All.Count) % EquipmentSlots.All.Count;
                EquipmentSlot candidate = EquipmentSlots.All[index];

                if (_dragSourceKind == DragSourceKind.Inventory
                    ? IsCompatible(_dragItem, candidate)
                    : candidate != _dragSourceSlot && IsCompatible(_dragItem, candidate))
                {
                    result = candidate;
                    return true;
                }
            }

            result = default;
            return false;
        }

        bool OnCancelRequested()
        {
            return CancelDrag(true);
        }

        bool CancelDrag(bool showFeedback)
        {
            bool hadDrag = _pointerPending || _isDragging;

            if (!hadDrag)
            {
                return false;
            }

            _suppressTooltipUntilPreview = true;
            FinishDragVisuals();

            if (showFeedback && _feedbackLabel != null)
            {
                _feedbackLabel.text = "已取消物品整理";
            }

            RestoreTooltip();
            return true;
        }

        void FinishDragVisuals()
        {
            _endingDrag = true;
            ReleasePointerCapture();
            _dragSourceButton?.EnableInClassList("inventory-item--drag-source", false);
            _dragGhost?.RemoveFromHierarchy();
            ClearDropVisuals();
            ResetDragState();
            _endingDrag = false;
        }

        void ReleasePointerCapture()
        {
            if (_workbench != null
                && _dragPointerId >= 0
                && _workbench.HasPointerCapture(_dragPointerId))
            {
                _workbench.ReleasePointer(_dragPointerId);
            }
        }

        void ResetDragState()
        {
            _dragSourceKind = DragSourceKind.None;
            _dropTargetKind = DropTargetKind.None;
            _dragItem = null;
            _dragSourceSlot = default;
            _dropTargetSlot = default;
            _dragSourceButton = null;
            _dragPointerId = -1;
            _pointerPending = false;
            _isDragging = false;
            _isKeyboardDrag = false;
            _dropValid = false;
            _dragGhost = null;
        }

        void ClearDropVisuals()
        {
            for (int i = 0; i < _gridCells.Count; i++)
            {
                _gridCells[i].RemoveFromClassList(DropValidClass);
                _gridCells[i].RemoveFromClassList(DropInvalidClass);
            }

            foreach (Button button in _slotButtons.Values)
            {
                button.RemoveFromClassList(SlotDropValidClass);
                button.RemoveFromClassList(SlotDropInvalidClass);
            }

            if (_externalDropTarget != null)
            {
                _externalDropTarget.RemoveFromClassList(ExternalDropValidClass);
                _externalDropTarget.RemoveFromClassList(ExternalDropInvalidClass);
            }
        }

        void ClearExternalDropTarget()
        {
            if (_externalDropTarget != null)
            {
                _externalDropTarget.RemoveFromClassList(ExternalDropValidClass);
                _externalDropTarget.RemoveFromClassList(ExternalDropInvalidClass);
            }

            _externalDropTarget = null;
            _externalDropValidator = null;
            _externalDropHandler = null;
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
                return $"候选装备 · {EquipmentSlots.GetDisplayName(_targetSlot.Value)}";
            }

            return "玩家背包";
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
            switch (slot)
            {
                case EquipmentSlot.Weapon:
                    return "inventory-equipment-slot-icon--weapon";
                case EquipmentSlot.Armor:
                    return "inventory-equipment-slot-icon--armor";
                default:
                    return "inventory-equipment-slot-icon--ring";
            }
        }
    }
}
