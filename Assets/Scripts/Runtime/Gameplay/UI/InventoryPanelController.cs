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
        readonly ItemListViewState _selectionState = new ItemListViewState();

        VisualElement _page;
        VisualElement _grid;
        Label _emptyLabel;
        Label _currentWeaponLabel;
        Label _feedbackLabel;
        Button _equipButton;
        ItemDetailView _detailView;
        ItemInstance _selectedItem;
        ItemInstance _previewItem;

        public InventorySnapshot LastSnapshot { get; private set; }

        public ItemInstance SelectedItem => _selectedItem;

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
            _currentWeaponLabel.text = $"当前武器：{snapshot.CurrentWeaponSummary}";
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
            RefreshInventory();
            SetVisible(IsVisible);
            Debug.Log("[InventoryPanelController] 背包面板初始化完成", this);
        }

        void OnDisable()
        {
            if (_equipButton != null)
            {
                _equipButton.clicked -= OnEquipClicked;
            }

            for (int i = 0; i < _eventRegistrations.Count; i++)
            {
                _eventRegistrations[i].UnRegister();
            }

            _eventRegistrations.Clear();
            _itemButtons.Clear();
            _selectionState.Reset();
            _page = null;
            _grid = null;
            _emptyLabel = null;
            _currentWeaponLabel = null;
            _feedbackLabel = null;
            _equipButton = null;
            _detailView = null;
            _selectedItem = null;
            _previewItem = null;
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
            _currentWeaponLabel = root.Q<Label>("inventory-current-weapon");
            _feedbackLabel = root.Q<Label>("inventory-feedback");
            _equipButton = root.Q<Button>("inventory-equip");
            _detailView = new ItemDetailView(root.Q<VisualElement>("inventory-item-detail"));

            if (_page == null
                || _grid == null
                || _emptyLabel == null
                || _currentWeaponLabel == null
                || _feedbackLabel == null
                || _equipButton == null
                || !_detailView.IsValid)
            {
                Debug.LogError("[InventoryPanelController] 背包 UXML 缺少必要的命名元素", this);
                return false;
            }

            _equipButton.clicked += OnEquipClicked;
            return true;
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
            RefreshSelection();
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
            foreach (KeyValuePair<ItemInstance, Button> entry in _itemButtons)
            {
                entry.Value.RemoveFromClassList("inventory-item--selected");

                if (entry.Key == _selectedItem)
                {
                    entry.Value.AddToClassList("inventory-item--selected");
                }
            }

            if (!TryGetSelectedSnapshot(out InventoryItemSnapshot selected))
            {
                _detailView.Clear();
                _feedbackLabel.text = LastSnapshot.HasPlayer ? "背包为空" : "等待玩家生成";
                _equipButton.SetEnabled(false);
                return;
            }

            RefreshDetail();

            bool canEquip = LastSnapshot.HasPlayer && selected.CanEquip;
            _equipButton.SetEnabled(canEquip);
            _feedbackLabel.text = canEquip
                ? "按下装备，将自动把旧武器放回背包"
                : selected.CanEquip
                    ? "等待玩家生成"
                    : "该物品不能装备";
        }

        void RefreshDetail()
        {
            ItemInstance item = _previewItem != null ? _previewItem : _selectedItem;

            for (int i = 0; i < LastSnapshot.Items.Count; i++)
            {
                if (LastSnapshot.Items[i].Item != item)
                {
                    continue;
                }

                _detailView.Show(LastSnapshot.Items[i].Detail);
                return;
            }

            _detailView.Clear();
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

        void OnEquipClicked()
        {
            if (_selectedItem == null || !LastSnapshot.HasPlayer)
            {
                _feedbackLabel.text = "当前没有可装备的物品";
                return;
            }

            string selectedName = _selectedItem.BaseDefinition.DisplayName;
            bool equipped = this.SendCommand(new EquipItemCommand(LastSnapshot.Player, _selectedItem));

            if (!equipped)
            {
                _feedbackLabel.text = "装备失败，请检查背包空间和物品类型";
                return;
            }

            RefreshInventory();
            _feedbackLabel.text = $"已装备 {selectedName}";
            FocusDefault();
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
