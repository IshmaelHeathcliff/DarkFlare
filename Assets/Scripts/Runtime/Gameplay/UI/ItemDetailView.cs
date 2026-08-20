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
        const string DenseClass = "item-detail--dense";

        readonly VisualElement _root;
        readonly VisualElement _icon;
        readonly Label _nameLabel;
        readonly Label _metaLabel;
        readonly Label _baseLabel;
        readonly VisualElement _implicitList;
        readonly VisualElement _prefixList;
        readonly VisualElement _suffixList;
        readonly ItemDetailFormatter _formatter;
        LocalizationService _localizationService;

        public ItemDetailView(VisualElement root, LocalizationService localizationService = null)
        {
            _root = root;
            _localizationService = localizationService;
            _formatter = new ItemDetailFormatter(Localize);

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

        public void SetLocalizationService(LocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

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
            _root.EnableInClassList(DenseClass, detail.AffixCount >= 4);
            _icon.EnableInClassList("item-icon--missing", detail.Item != null && icon == null);
            _icon.style.backgroundImage = icon != null
                ? new StyleBackground(icon)
                : new StyleBackground(StyleKeyword.None);
            _nameLabel.text = detail.Item != null
                ? Resolve(detail.Name, detail.InstanceId)
                : Localize("ui", "item.detail.none_name");
            _metaLabel.text = detail.Item != null
                ? Localize(
                    "ui",
                    "item.detail.meta",
                    _formatter.GetItemTypeText(detail.Type),
                    _formatter.GetRarityText(detail.Rarity),
                    detail.ItemLevel)
                : Localize("ui", "item.detail.meta_empty");
            _baseLabel.text = detail.Item != null
                ? BuildBaseSummary(detail)
                : Localize("ui", "item.detail.base_empty");
            BindModifiers(
                _implicitList,
                detail.ImplicitModifiers,
                Localize("ui", "item.detail.no_implicit"));
            BindAffixes(
                _prefixList,
                detail.Prefixes,
                Localize("ui", "item.detail.no_prefix"));
            BindAffixes(
                _suffixList,
                detail.Suffixes,
                Localize("ui", "item.detail.no_suffix"));
        }

        public void Clear()
        {
            Show(ItemDetailSnapshotFactory.Create(null));
        }

        string BuildBaseSummary(ItemDetailSnapshot detail)
        {
            List<string> lines = new List<string>();

            for (int i = 0; i < detail.Damages.Count; i++)
            {
                DamageDetailSnapshot damage = detail.Damages[i];
                string line = _formatter.FormatDamage(
                    damage.DamageType,
                    damage.Minimum,
                    damage.Maximum);

                string tagSummary = FormatTags(damage.Tags);

                if (!string.IsNullOrWhiteSpace(tagSummary))
                {
                    line += $" · {tagSummary}";
                }

                lines.Add(line);
            }

            if (lines.Count == 0)
            {
                lines.Add(Localize("ui", "item.detail.no_base_damage"));
            }

            lines.Add(Localize(
                "ui",
                "item.detail.size_weight",
                detail.GridSize.x,
                detail.GridSize.y,
                ItemDetailFormatter.FormatNumber(detail.Weight)));
            lines.Add(Localize(
                "ui",
                "item.detail.values",
                detail.BaseValue,
                detail.CalculatedValue));
            return string.Join("\n", lines);
        }

        void BindModifiers(
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
                AddLine(
                    container,
                    FormatModifier(modifiers[i]),
                    "item-detail-modifier");
            }
        }

        void BindAffixes(
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
                VisualElement entry = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                };
                entry.AddToClassList("item-detail-affix");
                container.Add(entry);

                for (int j = 0; j < affix.Modifiers.Count; j++)
                {
                    AddLine(
                        entry,
                        FormatModifier(affix.Modifiers[j]),
                        "item-detail-affix-content");
                }

                Label name = AddLine(
                    entry,
                    Localize(
                        "ui",
                        "item.detail.affix_name",
                        _formatter.GetAffixTypeText(affix.Type),
                        Resolve(affix.Name, string.Empty)),
                    "item-detail-affix-name");
                name.tooltip = Resolve(affix.Name, string.Empty);
            }
        }

        static Label AddLine(VisualElement container, string text, string className)
        {
            Label label = new Label(text);
            label.AddToClassList(className);
            container.Add(label);
            return label;
        }

        string FormatModifier(ModifierDetailSnapshot detail)
        {
            string statDisplayName = !string.IsNullOrWhiteSpace(detail.StatId)
                && StatIds.IsSupported(detail.StatId)
                    ? Localize("stats", detail.StatId)
                    : detail.Modifier?.StatId;
            return _formatter.FormatModifier(
                detail.Modifier,
                statDisplayName,
                detail.StatIsPercent);
        }

        string FormatTags(IReadOnlyList<LocalizedMessage> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return string.Empty;
            }

            List<string> names = new List<string>(tags.Count);

            for (int i = 0; i < tags.Count; i++)
            {
                names.Add(Resolve(tags[i], string.Empty));
            }

            return string.Join(", ", names);
        }

        string Resolve(LocalizedMessage message, string fallback)
        {
            return message.IsEmpty
                ? fallback
                : _localizationService?.GetString(message)
                    ?? $"[{message.TableName}.{message.EntryKey}]";
        }

        string Localize(string tableName, string entryKey, params object[] arguments)
        {
            return _localizationService?.GetString(tableName, entryKey, arguments)
                ?? $"[{tableName}.{entryKey}]";
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
