using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public enum ItemTooltipSide
    {
        Left,
        Right
    }

    public sealed class ItemTooltipView
    {
        const float EdgePadding = 8f;
        const float PanelGap = 10f;
        const int MaxPositionAttempts = 4;

        readonly VisualElement _root;
        readonly VisualElement _layer;
        readonly VisualElement _panel;
        readonly Label _contextLabel;
        readonly ItemDetailView _detailView;

        int _positionGeneration;

        public ItemTooltipView(
            VisualElement root,
            VisualElement layer,
            VisualElement panel,
            Label contextLabel)
        {
            _root = root;
            _layer = layer;
            _panel = panel;
            _contextLabel = contextLabel;
            DisablePicking(_root);
            _detailView = new ItemDetailView(root != null
                ? root.Q<VisualElement>("item-tooltip-detail")
                : null);
        }

        public bool IsValid => _root != null
            && _layer != null
            && _panel != null
            && _contextLabel != null
            && _detailView.IsValid;

        public void SetLocalizationService(LocalizationService localizationService)
        {
            _detailView.SetLocalizationService(localizationService);
        }

        public void Show(ItemInstance item, string context, ItemTooltipSide side)
        {
            if (!IsValid || item == null)
            {
                Hide();
                return;
            }

            ItemDetailSnapshot detail = ItemDetailSnapshotFactory.Create(item);
            _detailView.Show(detail, ItemVisualPresenter.GetSprite(detail.IconGuid));
            _contextLabel.text = context ?? string.Empty;
            _contextLabel.style.display = string.IsNullOrWhiteSpace(context)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _root.style.visibility = Visibility.Hidden;
            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();
            int generation = ++_positionGeneration;
            _root.schedule.Execute(() => Position(side, generation, 0));
        }

        public void Hide()
        {
            _positionGeneration++;

            if (_root != null)
            {
                _root.style.visibility = Visibility.Hidden;
                _root.style.display = DisplayStyle.None;
            }
        }

        void Position(ItemTooltipSide side, int generation, int attempt)
        {
            if (generation != _positionGeneration
                || !IsValid
                || _root.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            Rect panelBounds = _panel.worldBound;
            Rect screenBounds = _layer.panel.visualTree.worldBound;
            Rect tooltipBounds = _root.worldBound;

            bool hasValidGeometry = panelBounds.width > 1f
                && screenBounds.width > 1f
                && tooltipBounds.width > 1f
                && tooltipBounds.height > 1f;

            if (!hasValidGeometry)
            {
                if (attempt < MaxPositionAttempts)
                {
                    _root.schedule.Execute(() => Position(side, generation, attempt + 1));
                }
                else
                {
                    Hide();
                }

                return;
            }

            float tooltipWidth = Mathf.Max(1f, tooltipBounds.width);
            float preferredX = side == ItemTooltipSide.Left
                ? panelBounds.xMin - tooltipWidth - PanelGap
                : panelBounds.xMax + PanelGap;
            float oppositeX = side == ItemTooltipSide.Left
                ? panelBounds.xMax + PanelGap
                : panelBounds.xMin - tooltipWidth - PanelGap;
            float x = preferredX;

            if (!FitsHorizontally(x, tooltipWidth, screenBounds))
            {
                x = FitsHorizontally(oppositeX, tooltipWidth, screenBounds)
                    ? oppositeX
                    : preferredX;
            }

            x = Mathf.Clamp(
                x,
                screenBounds.xMin + EdgePadding,
                Mathf.Max(screenBounds.xMin + EdgePadding, screenBounds.xMax - tooltipWidth - EdgePadding));
            float tooltipHeight = Mathf.Max(1f, tooltipBounds.height);
            float y = Mathf.Clamp(
                panelBounds.yMin,
                screenBounds.yMin + EdgePadding,
                Mathf.Max(screenBounds.yMin + EdgePadding, screenBounds.yMax - tooltipHeight - EdgePadding));
            Vector2 local = _layer.WorldToLocal(new Vector2(x, y));
            _root.style.left = local.x;
            _root.style.top = local.y;
            _root.style.visibility = Visibility.Visible;
        }

        static bool FitsHorizontally(float x, float width, Rect screenBounds)
        {
            return x >= screenBounds.xMin + EdgePadding
                && x + width <= screenBounds.xMax - EdgePadding;
        }

        static void DisablePicking(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.pickingMode = PickingMode.Ignore;

            for (int i = 0; i < element.childCount; i++)
            {
                DisablePicking(element[i]);
            }
        }
    }
}
