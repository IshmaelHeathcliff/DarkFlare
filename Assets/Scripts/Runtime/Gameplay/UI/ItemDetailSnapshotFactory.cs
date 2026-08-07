using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public static class ItemDetailSnapshotFactory
    {
        const string MissingPropertyName = "未配置属性";

        static readonly IReadOnlyList<DamageDetailSnapshot> EmptyDamages = new List<DamageDetailSnapshot>();
        static readonly IReadOnlyList<ModifierDetailSnapshot> EmptyModifiers = new List<ModifierDetailSnapshot>();
        static readonly IReadOnlyList<AffixDetailSnapshot> EmptyAffixes = new List<AffixDetailSnapshot>();

        public static ItemDetailSnapshot Create(ItemInstance item)
        {
            if (item == null)
            {
                return new ItemDetailSnapshot(
                    null,
                    string.Empty,
                    "未选择物品",
                    string.Empty,
                    default,
                    default,
                    0,
                    Vector2Int.one,
                    0f,
                    0,
                    0,
                    EmptyDamages,
                    EmptyModifiers,
                    EmptyAffixes,
                    EmptyAffixes);
            }

            ItemBaseDefinition definition = item.BaseDefinition;
            string displayName = definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : item.InstanceId;
            return new ItemDetailSnapshot(
                item,
                item.InstanceId,
                displayName,
                definition != null && definition.Icon != null ? definition.Icon.AssetGUID : string.Empty,
                definition != null ? definition.ItemType : default,
                item.Rarity,
                item.ItemLevel,
                definition != null ? definition.GridSize : Vector2Int.one,
                definition != null ? definition.Weight : 0f,
                definition != null ? definition.BaseValue : 0,
                ItemValueCalculator.GetValue(item),
                CreateDamages(definition),
                CreateModifiers(
                    item.ImplicitModifiers,
                    definition != null ? definition.ImplicitModifiers : null,
                    displayName,
                    "固有属性"),
                CreateAffixes(item.Prefixes, displayName),
                CreateAffixes(item.Suffixes, displayName));
        }

        static IReadOnlyList<DamageDetailSnapshot> CreateDamages(ItemBaseDefinition definition)
        {
            if (definition == null || definition.BaseDamages.Count == 0)
            {
                return EmptyDamages;
            }

            List<DamageDetailSnapshot> damages = new List<DamageDetailSnapshot>(definition.BaseDamages.Count);

            for (int i = 0; i < definition.BaseDamages.Count; i++)
            {
                DamageRollDefinition damage = definition.BaseDamages[i];
                string tagSummary = FormatTags(damage.Tags);
                damages.Add(new DamageDetailSnapshot(
                    damage.DamageType,
                    damage.AmountRange.x,
                    damage.AmountRange.y,
                    tagSummary,
                    ItemDetailFormatter.FormatDamage(
                        damage.DamageType,
                        damage.AmountRange.x,
                        damage.AmountRange.y)));
            }

            return damages;
        }

        static IReadOnlyList<AffixDetailSnapshot> CreateAffixes(
            IReadOnlyList<AffixInstance> affixes,
            string itemName)
        {
            if (affixes == null || affixes.Count == 0)
            {
                return EmptyAffixes;
            }

            List<AffixDetailSnapshot> snapshots = new List<AffixDetailSnapshot>(affixes.Count);

            for (int i = 0; i < affixes.Count; i++)
            {
                AffixInstance affix = affixes[i];
                AffixDefinition definition = affix != null ? affix.Definition : null;
                string displayName = definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? definition.DisplayName
                    : "未命名词缀";
                IReadOnlyList<ModifierDetailSnapshot> modifiers = CreateModifiers(
                    affix != null ? affix.Modifiers : null,
                    definition != null ? definition.Modifiers : null,
                    itemName,
                    displayName);
                snapshots.Add(new AffixDetailSnapshot(
                    affix,
                    definition != null ? definition.AffixType : default,
                    displayName,
                    modifiers,
                    JoinModifierText(modifiers),
                    SumModifierValues(modifiers)));
            }

            return snapshots;
        }

        static IReadOnlyList<ModifierDetailSnapshot> CreateModifiers(
            IReadOnlyList<ModifierInstance> modifiers,
            IReadOnlyList<StatModifierDefinition> definitions,
            string itemName,
            string groupName)
        {
            if (modifiers == null || modifiers.Count == 0)
            {
                return EmptyModifiers;
            }

            if (definitions == null || definitions.Count != modifiers.Count)
            {
                int definitionCount = definitions != null ? definitions.Count : 0;
                Debug.LogWarning(
                    $"[ItemDetailSnapshotFactory] {itemName} / {groupName} 的修改器定义数量({definitionCount})与实例数量({modifiers.Count})不一致");
            }

            List<ModifierDetailSnapshot> snapshots = new List<ModifierDetailSnapshot>(modifiers.Count);

            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierInstance modifier = modifiers[i];
                StatDefinition stat = definitions != null && i < definitions.Count && definitions[i] != null
                    ? definitions[i].Stat
                    : null;
                string statName = stat != null && !string.IsNullOrWhiteSpace(stat.DisplayName)
                    ? stat.DisplayName
                    : MissingPropertyName;
                snapshots.Add(new ModifierDetailSnapshot(
                    modifier,
                    statName,
                    ItemDetailFormatter.FormatModifier(modifier, statName, stat != null && stat.IsPercent)));
            }

            return snapshots;
        }

        static string JoinModifierText(IReadOnlyList<ModifierDetailSnapshot> modifiers)
        {
            if (modifiers.Count == 0)
            {
                return "无数值修改";
            }

            List<string> texts = new List<string>(modifiers.Count);

            for (int i = 0; i < modifiers.Count; i++)
            {
                texts.Add(modifiers[i].DisplayText);
            }

            return string.Join(" · ", texts);
        }

        static float SumModifierValues(IReadOnlyList<ModifierDetailSnapshot> modifiers)
        {
            float total = 0f;

            for (int i = 0; i < modifiers.Count; i++)
            {
                total += modifiers[i].Value;
            }

            return total;
        }

        static string FormatTags(IReadOnlyList<TagDefinition> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return string.Empty;
            }

            List<string> names = new List<string>(tags.Count);

            for (int i = 0; i < tags.Count; i++)
            {
                TagDefinition tag = tags[i];

                if (tag == null)
                {
                    continue;
                }

                names.Add(!string.IsNullOrWhiteSpace(tag.DisplayName) ? tag.DisplayName : tag.Id);
            }

            return string.Join("、", names);
        }
    }
}
