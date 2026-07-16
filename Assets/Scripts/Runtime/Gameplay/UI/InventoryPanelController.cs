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

        GameInput _gameInput;
        VisualElement _overlay;
        VisualElement _grid;
        Label _emptyLabel;
        Label _currentWeaponLabel;
        Label _selectedNameLabel;
        Label _selectedTypeLabel;
        Label _selectedRarityLabel;
        Label _selectedAffixesLabel;
        Label _feedbackLabel;
        Button _equipButton;
        Button _closeButton;
        ItemInstance _selectedItem;

        public InventorySnapshot LastSnapshot { get; private set; }

        public ItemInstance SelectedItem => _selectedItem;

        public bool IsOpen => _gameInput != null && _gameInput.CurrentMode == GameInputMode.UI;

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

            InventorySnapshot snapshot = this.SendQuery(new GetInventorySnapshotQuery());
            LastSnapshot = snapshot;
            _currentWeaponLabel.text = $"当前武器：{snapshot.CurrentWeaponSummary}";
            _emptyLabel.style.display = snapshot.Items.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            ItemInstance previousSelection = _selectedItem;
            _selectedItem = null;
            _itemButtons.Clear();
            _grid.Clear();
            BuildGrid(snapshot);

            for (int i = 0; i < snapshot.Items.Count; i++)
            {
                InventoryItemSnapshot item = snapshot.Items[i];
                Button button = CreateItemButton(item);
                _grid.Add(button);
                _itemButtons.Add(item.Item, button);

                if (item.Item == previousSelection)
                {
                    _selectedItem = item.Item;
                }
            }

            if (_selectedItem == null && snapshot.Items.Count > 0)
            {
                _selectedItem = snapshot.Items[0].Item;
            }

            RefreshSelection();
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
            _gameInput = this.GetUtility<GameInput>();

            if (_gameInput == null)
            {
                Debug.LogError("[InventoryPanelController] 缺少 GameInput，无法控制背包开关", this);
                return;
            }

            _gameInput.ModeChanged += OnInputModeChanged;
            ApplyInputMode(_gameInput.CurrentMode);
            Debug.Log("[InventoryPanelController] 背包面板初始化完成", this);
        }

        void OnDisable()
        {
            if (_gameInput != null)
            {
                _gameInput.ModeChanged -= OnInputModeChanged;
            }

            if (_equipButton != null)
            {
                _equipButton.clicked -= OnEquipClicked;
            }

            if (_closeButton != null)
            {
                _closeButton.clicked -= OnCloseClicked;
            }

            for (int i = 0; i < _eventRegistrations.Count; i++)
            {
                _eventRegistrations[i].UnRegister();
            }

            _eventRegistrations.Clear();
            _itemButtons.Clear();
            _gameInput = null;
            _overlay = null;
            _grid = null;
            _emptyLabel = null;
            _currentWeaponLabel = null;
            _selectedNameLabel = null;
            _selectedTypeLabel = null;
            _selectedRarityLabel = null;
            _selectedAffixesLabel = null;
            _feedbackLabel = null;
            _equipButton = null;
            _closeButton = null;
            _selectedItem = null;
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
            _overlay = root.Q<VisualElement>("inventory-overlay");
            _grid = root.Q<VisualElement>("inventory-grid");
            _emptyLabel = root.Q<Label>("inventory-empty");
            _currentWeaponLabel = root.Q<Label>("inventory-current-weapon");
            _selectedNameLabel = root.Q<Label>("inventory-selected-name");
            _selectedTypeLabel = root.Q<Label>("inventory-selected-type");
            _selectedRarityLabel = root.Q<Label>("inventory-selected-rarity");
            _selectedAffixesLabel = root.Q<Label>("inventory-selected-affixes");
            _feedbackLabel = root.Q<Label>("inventory-feedback");
            _equipButton = root.Q<Button>("inventory-equip");
            _closeButton = root.Q<Button>("inventory-close");

            if (_overlay == null
                || _grid == null
                || _emptyLabel == null
                || _currentWeaponLabel == null
                || _selectedNameLabel == null
                || _selectedTypeLabel == null
                || _selectedRarityLabel == null
                || _selectedAffixesLabel == null
                || _feedbackLabel == null
                || _equipButton == null
                || _closeButton == null)
            {
                Debug.LogError("[InventoryPanelController] 背包 UXML 缺少必要的命名元素", this);
                return false;
            }

            _equipButton.clicked += OnEquipClicked;
            _closeButton.clicked += OnCloseClicked;
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

        void OnInputModeChanged(GameInputMode mode)
        {
            ApplyInputMode(mode);
        }

        void ApplyInputMode(GameInputMode mode)
        {
            if (_overlay == null)
            {
                return;
            }

            bool isOpen = mode == GameInputMode.UI;
            _overlay.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;

            if (!isOpen)
            {
                return;
            }

            RefreshInventory();
            FocusSelection();
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
            button.text = item.DisplayName;
            button.tooltip = $"{item.DisplayName} · {GetRarityText(item.Rarity)} · {item.AffixCount} 条词缀";
            button.AddToClassList("inventory-item");
            button.AddToClassList(GetRarityClass(item.Rarity));
            button.style.position = Position.Absolute;
            button.style.left = item.Placement.x * step;
            button.style.top = item.Placement.y * step;
            button.style.width = item.Placement.width * CellSize + (item.Placement.width - 1) * CellGap;
            button.style.height = item.Placement.height * CellSize + (item.Placement.height - 1) * CellGap;
            return button;
        }

        void SelectItem(ItemInstance item)
        {
            _selectedItem = item;
            RefreshSelection();
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
                _selectedNameLabel.text = "未选择物品";
                _selectedTypeLabel.text = "类型：-";
                _selectedRarityLabel.text = "稀有度：-";
                _selectedAffixesLabel.text = "词缀：-";
                _feedbackLabel.text = LastSnapshot.HasPlayer ? "背包为空" : "等待玩家生成";
                _equipButton.SetEnabled(false);
                return;
            }

            _selectedNameLabel.text = selected.DisplayName;
            _selectedTypeLabel.text = $"类型：{GetItemTypeText(selected.Type)}";
            _selectedRarityLabel.text = $"稀有度：{GetRarityText(selected.Rarity)}";
            _selectedAffixesLabel.text = $"词缀：{selected.AffixCount} 条";

            bool canEquip = LastSnapshot.HasPlayer && selected.CanEquip;
            _equipButton.SetEnabled(canEquip);
            _feedbackLabel.text = canEquip
                ? "按下装备，将自动把旧武器放回背包"
                : selected.CanEquip
                    ? "等待玩家生成"
                    : "该物品不能装备";
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

        void FocusSelection()
        {
            if (_selectedItem != null && _itemButtons.TryGetValue(_selectedItem, out Button button))
            {
                button.Focus();
                return;
            }

            _closeButton.Focus();
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
            FocusSelection();
        }

        void OnCloseClicked()
        {
            _gameInput?.SwitchToGameplay();
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
