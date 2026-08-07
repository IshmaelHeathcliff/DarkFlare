using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed class ItemDetailView
    {
        const string NormalClass = "item-detail--normal";
        const string MagicClass = "item-detail--magic";
        const string RareClass = "item-detail--rare";
        const string UniqueClass = "item-detail--unique";

        readonly VisualElement _root;
        readonly VisualElement _icon;
        readonly Label _nameLabel;
        readonly Label _metaLabel;
        readonly Label _baseLabel;
        readonly VisualElement _implicitList;
        readonly VisualElement _prefixList;
        readonly VisualElement _suffixList;

        public ItemDetailView(VisualElement root)
        {
            _root = root;

            if (_root == null)
            {
                return;
            }

            _nameLabel = _root.Q<Label>("item-detail-name");
            _icon = _root.Q<VisualElement>("item-detail-icon");
            _metaLabel = _root.Q<Label>("item-detail-meta");
            _baseLabel = _root.Q<Label>("item-detail-base");
            _implicitList = _root.Q<VisualElement>("item-detail-implicit-list");
            _prefixList = _root.Q<VisualElement>("item-detail-prefix-list");
            _suffixList = _root.Q<VisualElement>("item-detail-suffix-list");
        }

        public bool IsValid => _root != null
            && _icon != null
            && _nameLabel != null
            && _metaLabel != null
            && _baseLabel != null
            && _implicitList != null
            && _prefixList != null
            && _suffixList != null;

        public void Show(ItemDetailSnapshot detail)
        {
            Show(detail, ItemVisualPresenter.GetSprite(detail.IconGuid));
        }

        public void Show(ItemDetailSnapshot detail, Sprite icon)
        {
            if (!IsValid)
            {
                return;
            }

            ApplyRarityClass(detail.Rarity);
            _icon.EnableInClassList("item-icon--missing", detail.Item != null && icon == null);
            _icon.style.backgroundImage = icon != null
                ? new StyleBackground(icon)
                : new StyleBackground(StyleKeyword.None);
            _nameLabel.text = detail.Item != null ? detail.DisplayName : "未选择物品";
            _metaLabel.text = detail.Item != null
                ? $"{ItemDetailFormatter.GetItemTypeText(detail.Type)} · {ItemDetailFormatter.GetRarityText(detail.Rarity)} · 等级 {detail.ItemLevel}"
                : "类型与稀有度：-";
            _baseLabel.text = detail.Item != null ? BuildBaseSummary(detail) : "基础属性：-";
            BindModifiers(_implicitList, detail.ImplicitModifiers, "无固有属性");
            BindAffixes(_prefixList, detail.Prefixes, "无前缀");
            BindAffixes(_suffixList, detail.Suffixes, "无后缀");
        }

        public void Clear()
        {
            Show(ItemDetailSnapshotFactory.Create(null));
        }

        static string BuildBaseSummary(ItemDetailSnapshot detail)
        {
            List<string> lines = new List<string>();

            for (int i = 0; i < detail.Damages.Count; i++)
            {
                DamageDetailSnapshot damage = detail.Damages[i];
                string line = damage.DisplayText;

                if (!string.IsNullOrWhiteSpace(damage.TagSummary))
                {
                    line += $" · {damage.TagSummary}";
                }

                lines.Add(line);
            }

            if (lines.Count == 0)
            {
                lines.Add("无基础伤害");
            }

            lines.Add($"占用 {detail.GridSize.x}×{detail.GridSize.y} · 重量 {ItemDetailFormatter.FormatNumber(detail.Weight)}");
            lines.Add($"基础价值 {detail.BaseValue} · 当前价值 {detail.CalculatedValue}");
            return string.Join("\n", lines);
        }

        static void BindModifiers(
            VisualElement container,
            IReadOnlyList<ModifierDetailSnapshot> modifiers,
            string emptyText)
        {
            container.Clear();

            if (modifiers.Count == 0)
            {
                AddLine(container, emptyText, "item-detail-empty");
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                AddLine(container, modifiers[i].DisplayText, "item-detail-modifier");
            }
        }

        static void BindAffixes(
            VisualElement container,
            IReadOnlyList<AffixDetailSnapshot> affixes,
            string emptyText)
        {
            container.Clear();

            if (affixes.Count == 0)
            {
                AddLine(container, emptyText, "item-detail-empty");
                return;
            }

            for (int i = 0; i < affixes.Count; i++)
            {
                AffixDetailSnapshot affix = affixes[i];
                Label title = AddLine(
                    container,
                    $"{ItemDetailFormatter.GetAffixTypeText(affix.Type)} · {affix.DisplayName}",
                    "item-detail-affix-name");
                title.tooltip = affix.DisplayName;

                for (int j = 0; j < affix.Modifiers.Count; j++)
                {
                    AddLine(container, affix.Modifiers[j].DisplayText, "item-detail-modifier");
                }
            }
        }

        static Label AddLine(VisualElement container, string text, string className)
        {
            Label label = new Label(text);
            label.AddToClassList(className);
            container.Add(label);
            return label;
        }

        void ApplyRarityClass(ItemRarity rarity)
        {
            _root.RemoveFromClassList(NormalClass);
            _root.RemoveFromClassList(MagicClass);
            _root.RemoveFromClassList(RareClass);
            _root.RemoveFromClassList(UniqueClass);

            switch (rarity)
            {
                case ItemRarity.Magic:
                    _root.AddToClassList(MagicClass);
                    break;
                case ItemRarity.Rare:
                    _root.AddToClassList(RareClass);
                    break;
                case ItemRarity.Unique:
                    _root.AddToClassList(UniqueClass);
                    break;
                default:
                    _root.AddToClassList(NormalClass);
                    break;
            }
        }
    }
}
