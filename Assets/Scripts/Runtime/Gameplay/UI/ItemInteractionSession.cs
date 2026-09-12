using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed class ItemInteractionSession : IDisposable
    {
        const float CellSize = 48f;
        const float CellStep = 52f;
        const float DragThreshold = 10f;
        readonly GameMenuController _menu;
        readonly ItemWorkspace _workspace;
        readonly ItemActionRouter _router;
        readonly GameInput _input;
        readonly ApplicationInputService _applicationInput;
        readonly LocalizationService _locale;
        readonly VisualElement _root;
        readonly VisualElement _overlay;
        readonly VisualElement _grid;
        readonly VisualElement _dragLayer;
        readonly VisualElement _actionMenu;
        VisualElement _actionMenuFocus;
        readonly VisualElement _bar;
        readonly Label _status;
        readonly Button _actions;
        readonly Button _discard;
        readonly Button _buyZone;
        readonly Button _sellZone;
        readonly Dictionary<EquipmentSlot, Button> _equipment = new Dictionary<EquipmentSlot, Button>();
        readonly Dictionary<GameMenuPage, VisualElement> _windows = new Dictionary<GameMenuPage, VisualElement>();
        readonly List<VisualElement> _marked = new List<VisualElement>();
        readonly List<IUnRegister> _registrations = new List<IUnRegister>();
        ItemActionSource _selected;
        ItemActionSource _source;
        ItemActionTarget _target;
        VisualElement _ghost;
        Vector2 _pressPosition;
        Vector2Int _grab;
        Vector2Int _gridOrigin;
        InputDevice _device;
        int _pointerId = -1;
        int _dismissPointerId = -1;
        int _generation;
        bool _keyboard;
        bool _finishing;
        bool _executing;
        bool _disposed;
        bool _restoringFocus;
        string _feedback;
        ItemInstance _feedbackItem;

        public bool IsDragging { get; private set; }
        public bool IsPointerPending { get; private set; }
        public bool IsMenuOpen => _actionMenu.style.display == DisplayStyle.Flex;

        public ItemInteractionSession(GameMenuController menu, GameInput input)
        {
            _menu = menu;
            _workspace = menu.Workspace;
            _router = new ItemActionRouter(menu);
            _input = input;
            _applicationInput = ApplicationHost.Current.Input;
            _locale = ApplicationHost.Current.Localization;
            _root = menu.GetComponent<RuntimePanelView>().Root;
            _overlay = _root.Q("game-menu-overlay");
            _grid = _root.Q("inventory-grid");
            _dragLayer = _root.Q("item-drag-layer");
            _actionMenu = _root.Q("item-action-menu");
            _bar = _root.Q("item-operation-bar");
            _status = _root.Q<Label>("item-operation-status");
            _actions = _root.Q<Button>("item-operation-actions");
            _discard = _root.Q<Button>("item-discard-zone");
            _buyZone = _root.Q<Button>("shop-buy-zone");
            _sellZone = _root.Q<Button>("shop-sell-zone");
            foreach (GameMenuPage page in new[] { GameMenuPage.Inventory, GameMenuPage.Shop, GameMenuPage.Crafting })
            {
                VisualElement window = _root.Q(page.ToString().ToLowerInvariant() + "-window");
                _windows.Add(page, window);
                window.RegisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);
            }
            foreach (EquipmentSlot slot in EquipmentSlots.All)
            {
                string suffix = System.Text.RegularExpressions.Regex.Replace(slot.ToString(), "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
                Button button = _root.Q<Button>($"inventory-slot-{suffix}");
                if (button != null) { _equipment.Add(slot, button); }
            }
            _overlay.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
            _actions.clicked += OpenSelectedMenu;
            _discard.clicked += DiscardSelected;
            _buyZone.clicked += BuySelected;
            _sellZone.clicked += SellSelected;
            _input.RearrangePerformed += OnTakeOrPlace;
            _workspace.Suspending += OnSuspending;
            _locale.LocaleChanged += OnLocaleChanged;
            _applicationInput.SuspensionChanged += OnSuspensionChanged;
            InputSystem.onDeviceChange += OnDeviceChange;
            _registrations.Add(menu.GetArchitecture().RegisterEvent<InventoryChangedEvent>(_ => OnSourceChanged()));
            _registrations.Add(menu.GetArchitecture().RegisterEvent<EquipmentChangedEvent>(_ => OnSourceChanged()));
            _workspace.CraftingItemChanged += OnSourceChanged;
            _actionMenu.style.display = DisplayStyle.None;
            Refresh();
        }

        public void Refresh()
        {
            if (_disposed) { return; }
            _workspace.RefreshPreview();
            bool hasItems = (_menu.OpenWindows & (GameMenuAccess.Inventory | GameMenuAccess.Shop | GameMenuAccess.Crafting)) != 0;
            _bar.style.display = _menu.IsOpen && !_menu.IsPauseOpen && hasItems ? DisplayStyle.Flex : DisplayStyle.None;
            SyncBarWidth();
            if ((IsDragging || IsPointerPending) && !_router.IsCurrent(_source)) { Cancel(); }
            if (!_router.IsCurrent(_selected))
            {
                _selected = default;
                CloseMenu(false);
            }
            _actions.SetEnabled(_selected.Item != null);
            _discard.SetEnabled(_selected.Kind == ItemSourceKind.Inventory && _selected.Item != null || IsDragging);
            _status.text = _feedback != null ? Text(_feedback) + (_feedbackItem != null
                ? " · " + _locale.GetString(ItemDetailSnapshotFactory.Create(_feedbackItem).Name) : string.Empty)
                : _selected.Item == null ? Text("item.action.select") : Describe(_selected, _router.Primary(_selected));
        }

        void OnWindowGeometryChanged(GeometryChangedEvent evt)
        {
            SyncBarWidth();
        }

        void SyncBarWidth()
        {
            float width = 0f;
            foreach (var pair in _windows)
            {
                if (!_menu.IsWindowVisible(pair.Key)) { continue; }
                IResolvedStyle style = pair.Value.resolvedStyle;
                if (style.width > 0f) { width += style.width + style.marginLeft + style.marginRight; }
            }
            if (width <= 0f) { return; }
            _bar.style.width = width;
            _bar.style.maxWidth = width;
        }

        bool TrySource(VisualElement element, out ItemActionSource source)
        {
            source = default;
            Button button = element as Button ?? element?.GetFirstAncestorOfType<Button>();
            if (button == null || !GameMenuController.IsNavigable(button)) { return false; }
            GameMenuPage window;
            if (IsUnder(button, "inventory-window")) { window = GameMenuPage.Inventory; }
            else if (IsUnder(button, "shop-window")) { window = GameMenuPage.Shop; }
            else if (IsUnder(button, "crafting-window")) { window = GameMenuPage.Crafting; }
            else { return false; }
            if (!_menu.IsWindowVisible(window)) { return false; }
            if (button.name == "crafting-input-slot")
            {
                source = new ItemActionSource(_workspace.CraftingItem, ItemSourceKind.Crafting, window, button);
            }
            else if (window == GameMenuPage.Inventory && _equipment.ContainsValue(button))
            {
                foreach (EquipmentSlotSnapshot slot in _router.Inventory.EquipmentSlots)
                {
                    if (_equipment[slot.Slot] == button)
                    {
                        source = new ItemActionSource(slot.Item, ItemSourceKind.Equipment, window, button, slot.Slot);
                        break;
                    }
                }
            }
            else if (button.userData is ItemInstance item)
            {
                source = new ItemActionSource(item, IsUnder(button, "shop-merchant-list")
                    ? ItemSourceKind.Merchant : ItemSourceKind.Inventory, window, button);
            }
            return _router.IsCurrent(source);
        }

        static bool IsUnder(VisualElement element, string name)
        {
            for (VisualElement parent = element; parent != null; parent = parent.parent)
            {
                if (parent.name == name) { return true; }
            }
            return false;
        }

        void Select(ItemActionSource source)
        {
            _selected = source;
            if (!_restoringFocus)
            {
                ++_generation;
                _feedback = null;
                _feedbackItem = null;
            }
            _workspace.Activate(source.Window);
            Refresh();
        }

        void OnFocusIn(FocusInEvent evt)
        {
            if (IsMenuOpen)
            {
                if (IsUnder(evt.target as VisualElement, "item-action-menu"))
                {
                    _actionMenuFocus = evt.target as VisualElement;
                }
                else
                {
                    _overlay.schedule.Execute(() =>
                    {
                        if (!_disposed && IsMenuOpen) { _actionMenuFocus?.Focus(); }
                    });
                }
                return;
            }
            if (!IsDragging && !IsMenuOpen && TrySource(evt.target as VisualElement, out ItemActionSource source)) { Select(source); }
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (_disposed || _menu.IsPauseOpen || !_menu.IsOpen) { return; }
            if (IsMenuOpen)
            {
                if (!IsUnder(evt.target as VisualElement, "item-action-menu"))
                {
                    _dismissPointerId = evt.pointerId;
                    CloseMenu(false);
                    evt.StopImmediatePropagation();
                }
                return;
            }
            _dismissPointerId = -1;
            if (IsDragging || IsPointerPending) { return; }
            _workspace.AllowPreview();
            if (!TrySource(evt.target as VisualElement, out ItemActionSource source)) { return; }
            Select(source);
            if (evt.button != 0) { return; }
            _workspace.BeginDrag(Cancel, Navigate);
            _source = source;
            _pressPosition = evt.position;
            _pointerId = evt.pointerId;
            _device = Pointer.current;
            _keyboard = false;
            IsPointerPending = true;
            source.Element.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            source.Element.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            source.Element.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            Vector2Int size = source.Item.BaseDefinition.GridSize;
            Vector2 local = source.Element.WorldToLocal(evt.position);
            bool gridSource = source.Element.ClassListContains("inventory-item") || source.Element.ClassListContains("shop-item");
            _grab = gridSource ? new Vector2Int(Mathf.Clamp((int)(local.x / CellStep), 0, size.x - 1),
                Mathf.Clamp((int)(local.y / CellStep), 0, size.y - 1)) : Vector2Int.zero;
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!IsDragging && !IsPointerPending)
            {
                if (evt.deltaPosition.sqrMagnitude > 0f && !IsMenuOpen)
                {
                    _workspace.AllowPreview();
                    if (TrySource(evt.target as VisualElement, out ItemActionSource preview)) { Select(preview); }
                }
                return;
            }
            if (_keyboard || evt.pointerId != _pointerId) { return; }
            if (!_router.IsCurrent(_source)) { Cancel(); return; }
            if (IsPointerPending && Vector2.Distance(evt.position, _pressPosition) < DragThreshold) { return; }
            if (!IsDragging)
            {
                IsPointerPending = false;
                IsDragging = true;
                _overlay.CapturePointer(_pointerId);
                _source.Element.AddToClassList("inventory-item--drag-source");
                CreateGhost();
            }
            ResolvePointer(evt.position);
            MoveGhost(evt.position);
            evt.StopImmediatePropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _dismissPointerId)
            {
                _dismissPointerId = -1;
                evt.StopImmediatePropagation();
                return;
            }
            if (evt.button == 1 && !IsDragging && !IsPointerPending && !IsMenuOpen
                && TrySource(evt.target as VisualElement, out ItemActionSource source))
            {
                Select(source);
                ItemActionTarget primary = _router.Primary(source);
                if (primary.Kind == ItemActionKind.None) { OpenMenu(source); }
                else { Commit(source, primary); }
                evt.StopImmediatePropagation();
                return;
            }
            if (evt.pointerId != _pointerId || _keyboard) { return; }
            if (IsPointerPending) { Finish(); return; }
            if (!IsDragging) { return; }
            ResolvePointer(evt.position);
            ItemActionSource held = _source;
            ItemActionTarget target = _target;
            Finish();
            Commit(held, target);
            evt.StopImmediatePropagation();
        }

        void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _pointerId) { Cancel(); }
        }

        void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            if (!_finishing && IsDragging && evt.target == _overlay && evt.pointerId == _pointerId) { Cancel(); }
        }

        void OnSubmit(NavigationSubmitEvent evt)
        {
            if (IsMenuOpen) { return; }
            if (IsDragging)
            {
                OnTakeOrPlace();
                evt.StopImmediatePropagation();
            }
            else if (TrySource(evt.target as VisualElement, out ItemActionSource source))
            {
                OpenMenu(source);
                evt.StopImmediatePropagation();
            }
        }

        void OnTakeOrPlace()
        {
            if (_disposed || !_menu.IsOpen || _menu.IsPauseOpen || IsMenuOpen) { return; }
            if (IsDragging)
            {
                if (!_keyboard) { return; }
                ItemActionSource held = _source;
                ItemActionTarget target = _target;
                Finish();
                Commit(held, target);
                return;
            }
            if (IsPointerPending || !TrySource(_root.focusController.focusedElement as VisualElement, out ItemActionSource source)) { return; }
            _workspace.BeginDrag(Cancel, Navigate);
            _source = source;
            _keyboard = true;
            _grab = Vector2Int.zero;
            _device = _applicationInput.ItemMoveDevice;
            IsDragging = true;
            _source.Element.AddToClassList("inventory-item--drag-source");
            _gridOrigin = Vector2Int.zero;
            foreach (InventoryItemSnapshot item in _router.Inventory.Items)
            {
                if (item.Item == source.Item) { _gridOrigin = item.Placement.position; break; }
            }
            _target = source.Kind == ItemSourceKind.Inventory && source.Window == GameMenuPage.Inventory
                ? GridTarget(_gridOrigin) : default;
            CreateGhost();
            MoveGhost(source.Element.worldBound.center);
            SetTarget(_target);
        }

        ItemActionTarget GridTarget(Vector2Int origin)
        {
            ItemActionKind kind = _source.Kind == ItemSourceKind.Merchant ? ItemActionKind.Buy
                : _source.Kind == ItemSourceKind.Equipment ? ItemActionKind.Unequip : ItemActionKind.Move;
            return new ItemActionTarget(kind, _grid, origin);
        }

        List<ItemActionTarget> Targets()
        {
            var targets = new List<ItemActionTarget>();
            foreach (var slot in _equipment)
            {
                if (GameMenuController.IsNavigable(slot.Value))
                {
                    targets.Add(new ItemActionTarget(ItemActionKind.Equip, slot.Value, slot: slot.Key));
                }
            }
            Add("shop-buy-zone", ItemActionKind.Buy);
            Add("shop-sell-zone", ItemActionKind.Sell);
            Add("crafting-input-slot", ItemActionKind.Place);
            Add("crafting-slot-remove", ItemActionKind.Return);
            Add("item-discard-zone", ItemActionKind.Discard);
            return targets;
            void Add(string name, ItemActionKind kind)
            {
                VisualElement element = _root.Q(name);
                if (GameMenuController.IsNavigable(element)) { targets.Add(new ItemActionTarget(kind, element)); }
            }
        }

        void ResolvePointer(Vector2 position)
        {
            foreach (ItemActionTarget target in Targets())
            {
                if (target.Element.worldBound.Contains(position)) { SetTarget(target); return; }
            }
            if (_menu.IsWindowVisible(GameMenuPage.Inventory) && _grid.worldBound.Contains(position))
            {
                Vector2 local = _grid.WorldToLocal(position);
                SetTarget(GridTarget(new Vector2Int(Mathf.FloorToInt(local.x / CellStep) - _grab.x,
                    Mathf.FloorToInt(local.y / CellStep) - _grab.y)));
                return;
            }
            SetTarget(default);
        }

        public void Navigate(Vector2 direction)
        {
            if (!IsDragging || !_keyboard || direction.sqrMagnitude < 0.25f) { return; }
            Vector2Int delta = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                ? new Vector2Int(direction.x > 0 ? 1 : -1, 0) : new Vector2Int(0, direction.y > 0 ? -1 : 1);
            InventorySnapshot inventory = _router.Inventory;
            Vector2Int size = _source.Item.BaseDefinition.GridSize;
            bool inGrid = _target.Origin.HasValue;
            Vector2Int next = _gridOrigin + delta;
            if (inGrid && next.x >= 0 && next.y >= 0 && next.x + size.x <= inventory.Width && next.y + size.y <= inventory.Height)
            {
                _gridOrigin = next;
                SetTarget(GridTarget(next));
                MoveGhost(GridBounds(next).center);
                return;
            }
            Rect from = inGrid ? GridBounds(_gridOrigin) : _target.Element?.worldBound ?? _source.Element.worldBound;
            var targets = new List<ItemActionTarget>();
            var bounds = new List<Rect>();
            if (!inGrid && _menu.IsWindowVisible(GameMenuPage.Inventory))
            {
                for (int y = 0; y <= inventory.Height - size.y; y++)
                {
                    for (int x = 0; x <= inventory.Width - size.x; x++)
                    {
                        var origin = new Vector2Int(x, y);
                        targets.Add(GridTarget(origin));
                        bounds.Add(GridBounds(origin));
                    }
                }
            }
            foreach (ItemActionTarget target in Targets())
            {
                if (target.Element == _target.Element || !CanNavigateTo(target)) { continue; }
                targets.Add(target);
                bounds.Add(target.Element.worldBound);
            }
            int neighbor = SpatialNavigation.FindNeighbor(from, bounds, direction, false);
            if (neighbor < 0) { return; }
            ItemActionTarget selected = targets[neighbor];
            if (selected.Origin.HasValue) { _gridOrigin = selected.Origin.Value; }
            SetTarget(selected);
            MoveGhost(bounds[neighbor].center);
        }

        bool CanNavigateTo(ItemActionTarget target)
        {
            switch (target.Kind)
            {
                case ItemActionKind.Equip:
                    return (_source.Kind == ItemSourceKind.Inventory || _source.Kind == ItemSourceKind.Equipment)
                        && _source.Item.BaseDefinition.CanEquipTo(target.Slot);
                case ItemActionKind.Buy: return _source.Kind == ItemSourceKind.Merchant;
                case ItemActionKind.Return: return _source.Kind == ItemSourceKind.Crafting;
                default: return _source.Kind == ItemSourceKind.Inventory;
            }
        }

        Rect GridBounds(Vector2Int origin)
        {
            Vector2 position = _grid.LocalToWorld(new Vector2(origin.x * CellStep, origin.y * CellStep));
            return new Rect(position, new Vector2(CellSize, CellSize));
        }

        void SetTarget(ItemActionTarget target)
        {
            ClearMarks();
            _target = target;
            bool valid = _router.Validate(_source, target) == null;
            if (target.Origin.HasValue)
            {
                Vector2Int size = _source.Item.BaseDefinition.GridSize;
                List<VisualElement> cells = _grid.Query<VisualElement>(className: "inventory-cell").ToList();
                int width = _router.Inventory.Width;
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        int column = target.Origin.Value.x + x;
                        int row = target.Origin.Value.y + y;
                        int index = row * width + column;
                        if (column < 0 || column >= width || row < 0 || index >= cells.Count) { continue; }
                        Mark(cells[index], valid ? "inventory-cell--drop-valid" : "inventory-cell--drop-invalid");
                    }
                }
            }
            else if (target.Element != null)
            {
                string prefix = target.Kind == ItemActionKind.Equip ? "inventory-equipment-slot--drop-" : "inventory-external-drop--";
                Mark(target.Element, prefix + (valid ? "valid" : "invalid"));
            }
            string reason = _router.Validate(_source, target);
            _status.text = Describe(_source, target) + (reason == null ? string.Empty : " · " + Text(reason));
            _discard.SetEnabled(true);
        }

        void Mark(VisualElement element, string className)
        {
            element.AddToClassList(className);
            _marked.Add(element);
        }

        void ClearMarks()
        {
            foreach (VisualElement element in _marked)
            {
                element.RemoveFromClassList("inventory-cell--drop-valid");
                element.RemoveFromClassList("inventory-cell--drop-invalid");
                element.RemoveFromClassList("inventory-equipment-slot--drop-valid");
                element.RemoveFromClassList("inventory-equipment-slot--drop-invalid");
                element.RemoveFromClassList("inventory-external-drop--valid");
                element.RemoveFromClassList("inventory-external-drop--invalid");
            }
            _marked.Clear();
        }

        void CreateGhost()
        {
            Vector2Int size = _source.Item.BaseDefinition.GridSize;
            _ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            _ghost.AddToClassList("item-drag-ghost");
            _ghost.style.width = size.x * CellStep - 4f;
            _ghost.style.height = size.y * CellStep - 4f;
            ItemVisualPresenter.ApplyIcon(_ghost, ItemDetailSnapshotFactory.Create(_source.Item).IconGuid);
            _dragLayer.Add(_ghost);
        }

        void MoveGhost(Vector2 position)
        {
            if (_ghost == null) { return; }
            Vector2 local = _dragLayer.WorldToLocal(position);
            _ghost.style.left = local.x - _grab.x * CellStep - CellSize * 0.5f;
            _ghost.style.top = local.y - _grab.y * CellStep - CellSize * 0.5f;
        }

        public bool Cancel()
        {
            bool active = IsDragging || IsPointerPending;
            if (!active) { return false; }
            Finish();
            _feedback = "item.action.cancelled";
            _feedbackItem = null;
            Refresh();
            return true;
        }

        void Finish()
        {
            _finishing = true;
            _source.Element?.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            _source.Element?.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            _source.Element?.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            if (_pointerId >= 0 && _overlay.HasPointerCapture(_pointerId)) { _overlay.ReleasePointer(_pointerId); }
            _source.Element?.RemoveFromClassList("inventory-item--drag-source");
            _ghost?.RemoveFromHierarchy();
            _ghost = null;
            ClearMarks();
            IsDragging = false;
            IsPointerPending = false;
            _pointerId = -1;
            _grab = Vector2Int.zero;
            _source = default;
            _target = default;
            _workspace.EndDrag();
            _finishing = false;
        }

        void Commit(ItemActionSource source, ItemActionTarget target)
        {
            if (_executing || _disposed) { return; }
            _executing = true;
            Vector2 previous = source.Element?.worldBound.center ?? Vector2.zero;
            string reason = _router.Validate(source, target);
            bool success;
            try
            {
                success = reason == null && _router.Execute(source, target);
            }
            finally
            {
                _executing = false;
            }
            _feedback = success ? "item.action.completed" : reason ?? "item.action.stale";
            _feedbackItem = source.Item;
            CloseMenu(false);
            _workspace.SuppressPreview();
            int generation = ++_generation;
            GameMenuPage owner = target.Element != null && IsUnder(target.Element, "inventory-window")
                ? GameMenuPage.Inventory : target.Element != null && IsUnder(target.Element, "crafting-window")
                    ? GameMenuPage.Crafting : source.Window;
            _workspace.Activate(owner);
            _overlay.schedule.Execute(() =>
            {
                if (_disposed || generation != _generation || !_menu.IsWindowVisible(owner) || _workspace.ActiveWindow != owner) { return; }
                Button best = null;
                float distance = float.MaxValue;
                foreach (Button button in _root.Query<Button>().ToList())
                {
                    if (!TrySource(button, out ItemActionSource candidate) || candidate.Window != owner) { continue; }
                    if (candidate.Item == source.Item) { best = button; break; }
                    float next = Vector2.SqrMagnitude((Vector2)button.worldBound.center - previous);
                    if (next < distance) { best = button; distance = next; }
                }
                _restoringFocus = true;
                try
                {
                    if (best != null) { best.Focus(); }
                    else { _root.Q<Button>(owner == GameMenuPage.Inventory ? "inventory-window-close"
                        : owner == GameMenuPage.Shop ? "shop-window-close" : "crafting-window-close")?.Focus(); }
                }
                finally
                {
                    _restoringFocus = false;
                }
                Refresh();
            });
            Refresh();
        }

        void OpenSelectedMenu() { if (_router.IsCurrent(_selected)) { OpenMenu(_selected); } }
        void DiscardSelected() { if (_router.IsCurrent(_selected)) { OpenMenu(_selected); } }
        void BuySelected() { Commit(_selected, new ItemActionTarget(ItemActionKind.Buy, _buyZone)); }
        void SellSelected() { Commit(_selected, new ItemActionTarget(ItemActionKind.Sell, _sellZone)); }

        void OpenMenu(ItemActionSource source)
        {
            if (!_router.IsCurrent(source)) { return; }
            Cancel();
            Select(source);
            _actionMenu.Clear();
            ItemActionTarget primary = _router.Primary(source);
            if (primary.Kind != ItemActionKind.None) { Add(primary); }
            else if (source.Kind == ItemSourceKind.Inventory)
            {
                foreach (EquipmentSlot slot in EquipmentSlots.All)
                {
                    if (source.Item.BaseDefinition.CanEquipTo(slot)) { Add(new ItemActionTarget(ItemActionKind.Equip, slot: slot)); }
                }
            }
            if (source.Kind == ItemSourceKind.Inventory) { Add(new ItemActionTarget(ItemActionKind.Discard)); }
            var close = new Button(() => CloseMenu(true)) { text = Text("window.close") };
            close.AddToClassList("item-action-option");
            _actionMenu.Add(close);
            _actionMenu.style.display = DisplayStyle.Flex;
            _workspace.SuppressPreview();
            _actionMenu.BringToFront();
            foreach (Button button in _actionMenu.Query<Button>().ToList())
            {
                if (button.enabledInHierarchy) { button.Focus(); break; }
            }
            void Add(ItemActionTarget target)
            {
                string reason = _router.Validate(source, target);
                var button = new Button(() => Commit(source, target))
                {
                    name = "item-action-" + target.Kind.ToString().ToLowerInvariant() + (target.Kind == ItemActionKind.Equip ? "-" + target.Slot : string.Empty),
                    text = Describe(source, target) + (reason == null ? string.Empty : " · " + Text(reason))
                };
                button.AddToClassList("item-action-option");
                button.SetEnabled(reason == null);
                _actionMenu.Add(button);
            }
        }

        public bool CloseMenu(bool restoreFocus)
        {
            if (!IsMenuOpen) { return false; }
            _actionMenuFocus = null;
            _actionMenu.style.display = DisplayStyle.None;
            _actionMenu.Clear();
            if (restoreFocus && GameMenuController.IsNavigable(_selected.Element)) { _selected.Element.Focus(); }
            return true;
        }

        public bool NavigateMenu(Vector2 direction)
        {
            if (!IsMenuOpen) { return false; }
            var buttons = _actionMenu.Query<Button>().ToList();
            VisualElement current = _root.focusController.focusedElement as VisualElement;
            var bounds = new List<Rect>();
            foreach (Button button in buttons) { bounds.Add(button.enabledInHierarchy ? button.worldBound : default); }
            int next = SpatialNavigation.FindNeighbor(current?.worldBound ?? _actionMenu.worldBound, bounds, direction);
            if (next >= 0) { buttons[next].Focus(); }
            return true;
        }

        string Describe(ItemActionSource source, ItemActionTarget target)
        {
            string text = Text("item.action." + target.Kind.ToString().ToLowerInvariant());
            if (target.Kind == ItemActionKind.Buy || target.Kind == ItemActionKind.Sell)
            {
                text += " · " + _router.Price(source.Item, target.Kind == ItemActionKind.Buy);
            }
            if (target.Kind == ItemActionKind.Equip)
            {
                text += " · " + Text("equipment.slot." + EquipmentSlots.GetKey(target.Slot));
            }
            return text;
        }

        string Text(string key) { return _locale.GetString(new LocalizedMessage("ui", key)); }
        void OnSourceChanged() { if (!_executing) { Refresh(); } }
        void OnSuspending() { ++_generation; Cancel(); CloseMenu(false); }
        void OnLocaleChanged(string code) { CloseMenu(false); Refresh(); }
        void OnSuspensionChanged(InputSuspensionReason reasons) { if (reasons != InputSuspensionReason.None) { OnSuspending(); } }
        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device == _device && (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)) { OnSuspending(); }
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            OnSuspending();
            _overlay.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            _overlay.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            _overlay.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            _overlay.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            _overlay.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut, TrickleDown.TrickleDown);
            _overlay.UnregisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            _overlay.UnregisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
            _actions.clicked -= OpenSelectedMenu;
            _discard.clicked -= DiscardSelected;
            _buyZone.clicked -= BuySelected;
            _sellZone.clicked -= SellSelected;
            _input.RearrangePerformed -= OnTakeOrPlace;
            _workspace.Suspending -= OnSuspending;
            _workspace.CraftingItemChanged -= OnSourceChanged;
            _locale.LocaleChanged -= OnLocaleChanged;
            _applicationInput.SuspensionChanged -= OnSuspensionChanged;
            foreach (VisualElement window in _windows.Values) { window.UnregisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged); }
            InputSystem.onDeviceChange -= OnDeviceChange;
            foreach (IUnRegister registration in _registrations) { registration.UnRegister(); }
            _registrations.Clear();
        }
    }
}
