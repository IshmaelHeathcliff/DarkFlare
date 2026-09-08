using System;
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
        VisualElement _highlight;
        string _highlightClass;
        GameMenuPage _previewOwner;
        Func<bool> _cancelDrag;
        Action<Vector2> _navigateDrag;
        bool _disposed;
        bool _previewSuppressed;

        public ItemWorkspace(VisualElement root, Func<GameMenuPage, bool> isVisible, Action<GameMenuPage> activate)
        {
            _isVisible = isVisible;
            _activate = activate;
            _windowArea = root.Q("game-menu-content");
            _tooltip = new ItemTooltipView(root.Q("item-tooltip"), root.Q("item-tooltip-layer"),
                root.Q("game-menu-panel"), root.Q<Label>("item-tooltip-context"));
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                _tooltip.SetLocalizationService(host.Localization);
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
                _tooltip.Hide();
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
            if (_disposed || IsDragging || _previewSuppressed || !_isVisible(owner) || ActiveWindow != owner) { return; }
            _previewOwner = owner;
            _tooltip.SetAnchor(_windowArea ?? anchor);
            _tooltip.Show(item, context, ItemTooltipSide.Right);
        }

        public void HidePreview(GameMenuPage owner)
        {
            if (_previewOwner == owner) { _tooltip.Hide(); }
        }

        public void BeginDrag(Func<bool> cancel, Action<Vector2> navigate)
        {
            CancelDrag();
            _cancelDrag = cancel;
            _navigateDrag = navigate;
            ClearHighlight();
            _tooltip.Hide();
        }

        public void EndDrag()
        {
            bool hadDrag = _cancelDrag != null;
            _cancelDrag = null;
            _navigateDrag = null;
            _tooltip.Hide();
            if (hadDrag) { _previewSuppressed = true; }
        }

        public void AllowPreview() { _previewSuppressed = false; }
        public void SuppressPreview() { _previewSuppressed = true; _tooltip.Hide(); }

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
            _tooltip.Hide();
        }

        public void Dispose()
        {
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

        void ClearHighlight()
        {
            if (_highlight != null) { _highlight.RemoveFromClassList(_highlightClass); }
            _highlight = null;
        }
    }
}
