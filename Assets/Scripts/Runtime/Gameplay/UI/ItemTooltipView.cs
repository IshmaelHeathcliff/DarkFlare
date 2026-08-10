using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed class ItemTooltipView
    {
        const float EdgePadding = 8f;
        const float AnchorGap = 10f;

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

        public void Show(ItemInstance item, VisualElement anchor, string context)
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
            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();
            int generation = ++_positionGeneration;
            _root.schedule.Execute(() => Position(anchor, generation));
        }

        public void Hide()
        {
            _positionGeneration++;

            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }
        }

        void Position(VisualElement anchor, int generation)
        {
            if (generation != _positionGeneration
                || !IsValid
                || _root.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            Rect panelBounds = _panel.worldBound;
            Rect screenBounds = _layer.panel.visualTree.worldBound;
            float tooltipWidth = Mathf.Max(1f, _root.resolvedStyle.width);
            Rect anchorBounds = anchor != null && anchor.panel != null
                ? anchor.worldBound
                : new Rect(panelBounds.center.x, panelBounds.yMin + EdgePadding, 1f, 1f);
            float x = panelBounds.xMin - tooltipWidth - AnchorGap;

            if (x < screenBounds.xMin + EdgePadding)
            {
                float exteriorRight = panelBounds.xMax + AnchorGap;
                x = exteriorRight + tooltipWidth <= screenBounds.xMax - EdgePadding
                    ? exteriorRight
                    : anchorBounds.xMin - tooltipWidth - AnchorGap;
            }

            x = Mathf.Clamp(
                x,
                screenBounds.xMin + EdgePadding,
                Mathf.Max(screenBounds.xMin + EdgePadding, screenBounds.xMax - tooltipWidth - EdgePadding));
            float y = screenBounds.yMin + EdgePadding;
            Vector2 local = _layer.WorldToLocal(new Vector2(x, y));
            _root.style.left = local.x;
            _root.style.top = local.y;
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
