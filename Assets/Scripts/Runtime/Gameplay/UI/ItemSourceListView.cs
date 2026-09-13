using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed class ItemSourceListView
    {
        readonly VisualElement _root;
        readonly Dictionary<ItemInstance, Button> _buttons;
        int _generation;

        public ItemSourceListView(VisualElement root, Dictionary<ItemInstance, Button> buttons)
        {
            _root = root;
            _buttons = buttons;
        }

        public void Refresh(IReadOnlyList<ItemInstance> items, Func<ItemInstance, string> title,
            Action<ItemInstance> select, Action<ItemInstance> preview, Action<ItemInstance> endPreview)
        {
            int generation = ++_generation;
            _root.Clear();
            _buttons.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance item = items[i];
                var button = new Button(() =>
                {
                    if (generation == _generation && GameMenuController.IsNavigable(_root)) { select(item); }
                })
                {
                    name = $"{_root.name}-item-{item.InstanceId}",
                    userData = item,
                };
                button.AddToClassList("item-source-row");
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("item-source-icon");
                ItemVisualPresenter.ApplyIcon(icon, ItemDetailSnapshotFactory.Create(item).IconGuid);
                button.Add(icon);
                var label = new Label(title(item) + (item.BaseDefinition.IsStackable ? $" ×{item.Quantity}" : string.Empty)) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("item-source-name");
                button.Add(label);
                button.RegisterCallback<PointerEnterEvent>(_ => { if (generation == _generation) { preview(item); } });
                button.RegisterCallback<PointerLeaveEvent>(_ => { if (generation == _generation) { endPreview(item); } });
                button.RegisterCallback<FocusInEvent>(_ => { if (generation == _generation) { preview(item); } });
                button.RegisterCallback<FocusOutEvent>(_ => { if (generation == _generation) { endPreview(item); } });
                _root.Add(button);
                _buttons.Add(item, button);
            }
        }

        public void RefreshTitles(Func<ItemInstance, string> title)
        {
            foreach (var entry in _buttons)
            {
                entry.Value.Q<Label>(className: "item-source-name").text = title(entry.Key)
                    + (entry.Key.BaseDefinition.IsStackable ? $" ×{entry.Key.Quantity}" : string.Empty);
            }
        }
    }
}
