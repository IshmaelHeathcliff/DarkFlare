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

        [SerializeField]
        UIDocument _document;

        readonly List<IUnRegister> _eventRegistrations = new List<IUnRegister>();
        readonly Dictionary<ItemInstance, Button> _itemButtons = new Dictionary<ItemInstance, Button>();
        readonly Dictionary<EquipmentSlot, Button> _slotButtons = new Dictionary<EquipmentSlot, Button>();
        readonly ItemListViewState _selectionState = new ItemListViewState();

        VisualElement _page;
        VisualElement _grid;
        Label _emptyLabel;
        Label _targetSlotLabel;
        Label _comparisonLabel;
        Label _feedbackLabel;
        Button _equipButton;
        Button _unequipButton;
        ItemDetailView _detailView;
        ItemInstance _selectedItem;
        ItemInstance _previewItem;
        EquipmentSlot? _targetSlot;

        public InventorySnapshot LastSnapshot { get; private set; }

        public ItemInstance SelectedItem => _selectedItem;

        public EquipmentSlot? TargetSlot => _targetSlot;

        public bool IsVisible { get; private set; }

        public bool IsOpen => IsVisible;

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

            CaptureSelectionState();
            InventorySnapshot snapshot = this.SendQuery(new GetInventorySnapshotQuery());
            LastSnapshot = snapshot;
            _emptyLabel.style.display = snapshot.Items.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            _selectedItem = null;
            _previewItem = null;
            _itemButtons.Clear();
            _grid.Clear();
            BuildGrid(snapshot);

            for (int i = 0; i < snapshot.Items.Count; i++)
            {
                InventoryItemSnapshot item = snapshot.Items[i];
                Button button = CreateItemButton(item);
                _grid.Add(button);
                _itemButtons.Add(item.Item, button);

            }

            int selectedIndex = ItemSelectionResolver.ResolveIndex(
                snapshot.Items,
                _selectionState.SelectedInstanceId,
                _selectionState.FallbackIndex,
                item => item.Detail.InstanceId);

            if (selectedIndex >= 0)
            {
                _selectedItem = snapshot.Items[selectedIndex].Item;
                _selectionState.SelectedInstanceId = _selectedItem.InstanceId;
                _selectionState.FallbackIndex = selectedIndex;
            }

            ResolveTargetSlotForSelectedItem(false);
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
                RefreshInventory();
            }
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

            RegisterEvents();
            RefreshInventory();
            SetVisible(IsVisible);
            Debug.Log("[InventoryPanelController] 背包与四槽装备面板初始化完成", this);
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
            _slotButtons.Clear();
            _selectionState.Reset();
            _page = null;
            _grid = null;
            _emptyLabel = null;
            _targetSlotLabel = null;
            _comparisonLabel = null;
            _feedbackLabel = null;
            _equipButton = null;
            _unequipButton = null;
            _detailView = null;
            _selectedItem = null;
            _previewItem = null;
            _targetSlot = null;
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
            _grid = root.Q<VisualElement>("inventory-grid");
            _emptyLabel = root.Q<Label>("inventory-empty");
            _targetSlotLabel = root.Q<Label>("inventory-target-slot");
            _comparisonLabel = root.Q<Label>("inventory-comparison");
            _feedbackLabel = root.Q<Label>("inventory-feedback");
            _equipButton = root.Q<Button>("inventory-equip");
            _unequipButton = root.Q<Button>("inventory-unequip");
            _detailView = new ItemDetailView(root.Q<VisualElement>("inventory-item-detail"));
            _slotButtons.Clear();
            AddSlotButton(root, EquipmentSlot.Weapon, "inventory-slot-weapon");
            AddSlotButton(root, EquipmentSlot.Armor, "inventory-slot-armor");
            AddSlotButton(root, EquipmentSlot.RingLeft, "inventory-slot-ring-left");
            AddSlotButton(root, EquipmentSlot.RingRight, "inventory-slot-ring-right");

            if (_page == null
                || _grid == null
                || _emptyLabel == null
                || _targetSlotLabel == null
                || _comparisonLabel == null
                || _feedbackLabel == null
                || _equipButton == null
                || _unequipButton == null
                || _slotButtons.Count != EquipmentSlots.All.Count
                || !_detailView.IsValid)
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
            button.text = string.Empty;
            button.tooltip = $"{item.DisplayName} · {ItemDetailFormatter.GetRarityText(item.Rarity)} · {item.AffixCount} 条词缀";
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

            if (item.Type == ItemType.Weapon)
            {
                icon.AddToClassList("inventory-item-icon--weapon");
            }

            Label label = new Label(item.DisplayName)
            {
                pickingMode = PickingMode.Ignore,
            };
            label.AddToClassList("inventory-item-label");
            button.Add(icon);
            button.Add(label);
            button.RegisterCallback<PointerEnterEvent>(_ => PreviewItem(item.Item));
            button.RegisterCallback<PointerLeaveEvent>(_ => EndPreview(item.Item));
            button.RegisterCallback<FocusInEvent>(_ => PreviewItem(item.Item));
            button.RegisterCallback<FocusOutEvent>(_ => EndPreview(item.Item));
            return button;
        }

        void SelectItem(ItemInstance item)
        {
            _selectedItem = item;
            _previewItem = null;
            CaptureSelectionState();
            ResolveTargetSlotForSelectedItem(true);
            RefreshSelection();
        }

        void SelectSlot(EquipmentSlot slot)
        {
            _targetSlot = slot;
            _previewItem = null;

            if (_selectedItem != null && !IsCompatible(_selectedItem, slot))
            {
                _selectedItem = null;
                _selectionState.SelectedInstanceId = string.Empty;
            }

            RefreshSelection();
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
            _previewItem = item;
            RefreshDetail();
            RefreshComparison();
        }

        void EndPreview(ItemInstance item)
        {
            if (_previewItem != item)
            {
                return;
            }

            _previewItem = null;
            RefreshDetail();
            RefreshComparison();
        }

        void RefreshSelection()
        {
            foreach (KeyValuePair<ItemInstance, Button> entry in _itemButtons)
            {
                entry.Value.EnableInClassList("inventory-item--selected", entry.Key == _selectedItem);
            }

            RefreshEquipmentSlots();
            RefreshDetail();
            RefreshComparison();

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

                button.text = snapshot.Summary;
                button.tooltip = snapshot.Item != null
                    ? $"{snapshot.Detail.DisplayName} · {ItemDetailFormatter.GetRarityText(snapshot.Detail.Rarity)}"
                    : $"{snapshot.SlotName}为空";
                button.EnableInClassList("inventory-equipment-slot--selected", _targetSlot == snapshot.Slot);
                button.EnableInClassList("inventory-equipment-slot--filled", snapshot.Item != null);
            }
        }

        void RefreshDetail()
        {
            ItemInstance item = _previewItem != null ? _previewItem : _selectedItem;

            if (item != null)
            {
                _detailView.Show(ItemDetailSnapshotFactory.Create(item));
                return;
            }

            if (_targetSlot.HasValue
                && TryGetSlotSnapshot(_targetSlot.Value, out EquipmentSlotSnapshot slotSnapshot)
                && slotSnapshot.Item != null)
            {
                _detailView.Show(slotSnapshot.Detail);
                return;
            }

            _detailView.Clear();
        }

        void RefreshComparison()
        {
            if (!_targetSlot.HasValue)
            {
                _targetSlotLabel.text = "目标槽位：未选择";
                _comparisonLabel.text = "选择候选物品和目标槽位后显示";
                return;
            }

            EquipmentSlot slot = _targetSlot.Value;
            _targetSlotLabel.text = $"目标槽位：{EquipmentSlots.GetDisplayName(slot)}";
            TryGetSlotSnapshot(slot, out EquipmentSlotSnapshot slotSnapshot);
            ItemInstance candidate = _previewItem != null ? _previewItem : _selectedItem;

            if (candidate == null || !IsCompatible(candidate, slot))
            {
                _comparisonLabel.text = slotSnapshot.Item != null
                    ? $"当前：{slotSnapshot.Detail.DisplayName}"
                    : "当前槽位为空";
                return;
            }

            EquipmentComparisonSnapshot comparison = EquipmentComparisonFactory.Create(
                slot,
                slotSnapshot.Item,
                candidate);
            _comparisonLabel.text = string.Join("\n", comparison.Lines);
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
    }
}
