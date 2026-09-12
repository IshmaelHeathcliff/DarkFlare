using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed class ItemWorkspace : IDisposable
    {
        readonly Func<GameMenuPage, bool> _isVisible;
        readonly Action<GameMenuPage> _activate;
        readonly ItemTooltipView _tooltip;
        readonly VisualElement _windowArea;
        readonly ItemTooltipView[] _comparisons = new ItemTooltipView[2];
        readonly Func<InventorySnapshot> _getInventory;
        readonly ApplicationInputService _input;
        readonly LocalizationService _locale;
        ItemInstance _previewItem;
        string _previewContext;
        VisualElement _highlight;
        string _highlightClass;
        GameMenuPage _previewOwner;
        Func<bool> _cancelDrag;
        Action<Vector2> _navigateDrag;
        bool _disposed;
        bool _previewSuppressed;

        public ItemWorkspace(VisualElement root, Func<GameMenuPage, bool> isVisible, Action<GameMenuPage> activate,
            Func<InventorySnapshot> getInventory)
        {
            _getInventory = getInventory;
            _isVisible = isVisible;
            _activate = activate;
            _windowArea = root.Q("game-menu-content");
            _tooltip = new ItemTooltipView(root.Q("item-tooltip"), root.Q("item-tooltip-layer"),
                root.Q("game-menu-panel"), root.Q<Label>("item-tooltip-context"));
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                _input = host.Input;
                _locale = host.Localization;
                _input.CompareChanged += OnCompareChanged;
                _tooltip.SetLocalizationService(_locale);
            }
            for (int i = 0; i < _comparisons.Length; i++)
            {
                VisualElement comparison = root.Q($"item-comparison-{i}");
                _comparisons[i] = new ItemTooltipView(comparison, root.Q("item-tooltip-layer"),
                    _windowArea, comparison?.Q<Label>("item-tooltip-context"));
                _comparisons[i].SetLocalizationService(_locale);
            }
        }

        public GameMenuPage ActiveWindow { get; private set; }
        public ItemInteractionSession Interactions { get; internal set; }
        public event Action Suspending;
        public ItemInstance SelectedInventoryItem { get; private set; }
        public ItemInstance CraftingItem { get; private set; }
        public bool IsDragging => _cancelDrag != null;
        public event Action<ItemInstance> InventorySelectionChanged;
        public event Action CraftingItemChanged;

        public void Activate(GameMenuPage page)
        {
            if (_disposed || !_isVisible(page)) { return; }
            if (ActiveWindow != page)
            {
                ClearHighlight();
                ClearPreview();
            }
            ActiveWindow = page;
            _activate(page);
        }

        public void SelectInventoryItem(ItemInstance item)
        {
            SelectedInventoryItem = item;
            InventorySelectionChanged?.Invoke(item);
        }

        public void SetCraftingItem(ItemInstance item)
        {
            if (CraftingItem == item) { return; }
            CraftingItem = item;
            CraftingItemChanged?.Invoke();
        }

        public void Highlight(GameMenuPage owner, VisualElement element, string className, bool selected)
        {
            bool active = !_disposed && _isVisible(owner) && ActiveWindow == owner;
            if (selected && active && !IsDragging)
            {
                if (_highlight != element) { ClearHighlight(); }
                _highlight = element;
                _highlightClass = className;
                element.AddToClassList(className);
            }
            else
            {
                element.RemoveFromClassList(className);
                if (_highlight == element) { _highlight = null; }
            }
        }

        public void Preview(GameMenuPage owner, VisualElement anchor, ItemInstance item, string context)
        {
            if (_disposed || IsDragging || !_isVisible(owner) || ActiveWindow != owner) { return; }
            _previewOwner = owner;
            _previewItem = item;
            _previewContext = context;
            _tooltip.SetAnchor(_windowArea ?? anchor);
            RefreshPreview();
        }

        public void HidePreview(GameMenuPage owner)
        {
            if (_previewOwner == owner) { ClearPreview(); }
        }

        public void BeginDrag(Func<bool> cancel, Action<Vector2> navigate)
        {
            CancelDrag();
            _cancelDrag = cancel;
            _navigateDrag = navigate;
            ClearHighlight();
            ClearPreview();
        }

        public void EndDrag()
        {
            bool hadDrag = _cancelDrag != null;
            _cancelDrag = null;
            _navigateDrag = null;
            ClearPreview();
            if (hadDrag) { _previewSuppressed = true; }
        }

        public void AllowPreview() { _previewSuppressed = false; }
        public void SuppressPreview() { _previewSuppressed = true; ClearPreview(); }

        public bool CancelDrag()
        {
            Func<bool> cancel = _cancelDrag;
            EndDrag();
            return cancel?.Invoke() ?? false;
        }

        public void NavigateDrag(Vector2 direction)
        {
            _navigateDrag?.Invoke(direction);
        }

        public void Suspend()
        {
            Suspending?.Invoke();
            CancelDrag();
            ClearHighlight();
            ClearPreview();
        }

        public void Dispose()
        {
            if (_input != null) { _input.CompareChanged -= OnCompareChanged; }
            Interactions?.Dispose();
            Interactions = null;
            Suspend();
            _disposed = true;
            SelectedInventoryItem = null;
            CraftingItem = null;
            InventorySelectionChanged = null;
            CraftingItemChanged = null;
            Suspending = null;
        }

        void OnCompareChanged(bool held)
        {
            if (held && !IsDragging && Interactions?.IsMenuOpen != true) { _previewSuppressed = false; }
            RefreshPreview();
        }

        public void RefreshPreview()
        {
            foreach (ItemTooltipView comparison in _comparisons) { comparison.Hide(); }
            if (_disposed || _previewItem == null || IsDragging || _previewSuppressed
                || !_isVisible(_previewOwner)) { _tooltip.Hide(); return; }
            List<EquipmentSlotSnapshot> equipped = new List<EquipmentSlotSnapshot>();
            bool held = _input != null && _input.IsUiEnabled && !_input.HasUiContextOverride
                && _input.ActionAsset.FindAction("UI/Compare", true).IsPressed();
            if (held && _previewItem.BaseDefinition != null)
            {
                foreach (EquipmentSlotSnapshot slot in _getInventory().EquipmentSlots)
                {
                    if (slot.Item != null && slot.Item != _previewItem
                        && _previewItem.BaseDefinition.CanEquipTo(slot.Slot)) { equipped.Add(slot); }
                }
            }
            int count = Mathf.Min(equipped.Count, _comparisons.Length);
            _tooltip.SetComparisonGroup(0, count + 1);
            string hint = _input != null ? _input.GetBindingDisplayString(new InputBindingTarget(
                RebindableInputAction.UiCompare, InputBindingPart.Primary,
                _input.DisplayDeviceFamily)) : string.Empty;
            string context = _previewContext;
            if (!held && _previewItem.BaseDefinition != null
                && _previewItem.BaseDefinition.AllowedEquipmentSlots != EquipmentSlotMask.None)
            {
                context += "\n" + _locale?.GetString("ui", "item.compare.hint", new object[] { hint });
            }
            _tooltip.Show(_previewItem, context, ItemTooltipSide.Right);
            for (int i = 0; i < count; i++)
            {
                _comparisons[i].SetComparisonGroup(i + 1, count + 1);
                string slotName = _locale?.GetString("ui", "equipment.slot." + EquipmentSlots.GetKey(equipped[i].Slot));
                _comparisons[i].Show(equipped[i].Item,
                    _locale?.GetString("ui", "item.compare.equipped", new object[] { slotName }), ItemTooltipSide.Right);
            }
        }

        void ClearPreview()
        {
            _previewItem = null;
            _tooltip.Hide();
            foreach (ItemTooltipView comparison in _comparisons) { comparison.Hide(); }
        }

        void ClearHighlight()
        {
            if (_highlight != null) { _highlight.RemoveFromClassList(_highlightClass); }
            _highlight = null;
        }
    }
}
