using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public static class ItemDetailSnapshotFactory
    {
        static readonly IReadOnlyList<DamageDetailSnapshot> EmptyDamages = new List<DamageDetailSnapshot>();
        static readonly IReadOnlyList<ModifierDetailSnapshot> EmptyModifiers = new List<ModifierDetailSnapshot>();
        static readonly IReadOnlyList<AffixDetailSnapshot> EmptyAffixes = new List<AffixDetailSnapshot>();
        static readonly IReadOnlyList<LocalizedMessage> EmptyNames = new List<LocalizedMessage>();

        public static ItemDetailSnapshot Create(ItemInstance item)
        {
            if (item == null)
            {
                return new ItemDetailSnapshot(
                    null,
                    string.Empty,
                    default,
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
            LocalizedMessage name = definition?.LocalizedName?.Message ?? default;
            return new ItemDetailSnapshot(
                item,
                item.InstanceId,
                name,
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
                    definition != null ? definition.Id : item.InstanceId,
                    "implicit"),
                CreateAffixes(item.Prefixes, definition != null ? definition.Id : item.InstanceId),
                CreateAffixes(item.Suffixes, definition != null ? definition.Id : item.InstanceId));
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
                damages.Add(new DamageDetailSnapshot(
                    damage.DamageType,
                    damage.AmountRange.x,
                    damage.AmountRange.y,
                    CreateTagNames(damage.Tags)));
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
                LocalizedMessage name = definition?.LocalizedName?.Message ?? default;
                IReadOnlyList<ModifierDetailSnapshot> modifiers = CreateModifiers(
                    affix != null ? affix.Modifiers : null,
                    definition != null ? definition.Modifiers : null,
                    itemName,
                    definition != null ? definition.Id : string.Empty);
                snapshots.Add(new AffixDetailSnapshot(
                    affix,
                    definition != null ? definition.AffixType : default,
                    name,
                    modifiers,
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
                ApplicationLog.Warning(LogEventIds.GameplayUi,
                    $"[ItemDetailSnapshotFactory] {itemName} / {groupName} modifier definition count "
                    + $"({definitionCount}) does not match instance count ({modifiers.Count})");
            }

            List<ModifierDetailSnapshot> snapshots = new List<ModifierDetailSnapshot>(modifiers.Count);

            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierInstance modifier = modifiers[i];
                StatDefinition stat = definitions != null && i < definitions.Count && definitions[i] != null
                    ? definitions[i].Stat
                    : null;
                snapshots.Add(new ModifierDetailSnapshot(
                    modifier,
                    stat != null ? stat.Id : modifier?.StatId ?? string.Empty,
                    stat != null && stat.IsPercent));
            }

            return snapshots;
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

        static IReadOnlyList<LocalizedMessage> CreateTagNames(IReadOnlyList<TagDefinition> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return EmptyNames;
            }

            List<LocalizedMessage> names = new List<LocalizedMessage>(tags.Count);

            for (int i = 0; i < tags.Count; i++)
            {
                TagDefinition tag = tags[i];

                if (tag == null)
                {
                    continue;
                }

                if (tag.LocalizedName != null && tag.LocalizedName.IsValid)
                {
                    names.Add(tag.LocalizedName.Message);
                }
            }

            return names;
        }
    }
}
