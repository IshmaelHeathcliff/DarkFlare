using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct DamageDetailSnapshot
    {
        public DamageType DamageType { get; }

        public float Minimum { get; }

        public float Maximum { get; }

        public IReadOnlyList<LocalizedMessage> Tags { get; }

        public DamageDetailSnapshot(
            DamageType damageType,
            float minimum,
            float maximum,
            IReadOnlyList<LocalizedMessage> tags)
        {
            DamageType = damageType;
            Minimum = minimum;
            Maximum = maximum;
            Tags = tags;
        }
    }

    public readonly struct ModifierDetailSnapshot
    {
        public ModifierInstance Modifier { get; }

        public string StatId { get; }

        public bool StatIsPercent { get; }

        public ModifierOperation Operation { get; }

        public ModifierScope Scope { get; }

        public float Value { get; }

        public ModifierDetailSnapshot(
            ModifierInstance modifier,
            string statId,
            bool statIsPercent)
        {
            Modifier = modifier;
            StatId = statId ?? string.Empty;
            StatIsPercent = statIsPercent;
            Operation = modifier != null ? modifier.Operation : default;
            Scope = modifier != null ? modifier.Scope : default;
            Value = modifier != null ? modifier.Value : 0f;
        }
    }

    public readonly struct AffixDetailSnapshot
    {
        public AffixInstance Affix { get; }

        public AffixType Type { get; }

        public LocalizedMessage Name { get; }

        public IReadOnlyList<ModifierDetailSnapshot> Modifiers { get; }

        public float TotalValue { get; }

        public AffixDetailSnapshot(
            AffixInstance affix,
            AffixType type,
            LocalizedMessage name,
            IReadOnlyList<ModifierDetailSnapshot> modifiers,
            float totalValue)
        {
            Affix = affix;
            Type = type;
            Name = name;
            Modifiers = modifiers;
            TotalValue = totalValue;
        }
    }

    public readonly struct ItemDetailSnapshot
    {
        public ItemInstance Item { get; }

        public string InstanceId { get; }

        public LocalizedMessage Name { get; }

        public string IconGuid { get; }

        public ItemType Type { get; }

        public ItemRarity Rarity { get; }

        public int ItemLevel { get; }

        public Vector2Int GridSize { get; }

        public float Weight { get; }

        public int BaseValue { get; }

        public int CalculatedValue { get; }

        public IReadOnlyList<DamageDetailSnapshot> Damages { get; }

        public IReadOnlyList<ModifierDetailSnapshot> ImplicitModifiers { get; }

        public IReadOnlyList<AffixDetailSnapshot> Prefixes { get; }

        public IReadOnlyList<AffixDetailSnapshot> Suffixes { get; }

        public int AffixCount => Prefixes.Count + Suffixes.Count;

        public ItemDetailSnapshot(
            ItemInstance item,
            string instanceId,
            LocalizedMessage name,
            string iconGuid,
            ItemType type,
            ItemRarity rarity,
            int itemLevel,
            Vector2Int gridSize,
            float weight,
            int baseValue,
            int calculatedValue,
            IReadOnlyList<DamageDetailSnapshot> damages,
            IReadOnlyList<ModifierDetailSnapshot> implicitModifiers,
            IReadOnlyList<AffixDetailSnapshot> prefixes,
            IReadOnlyList<AffixDetailSnapshot> suffixes)
        {
            Item = item;
            InstanceId = instanceId;
            Name = name;
            IconGuid = iconGuid;
            Type = type;
            Rarity = rarity;
            ItemLevel = itemLevel;
            GridSize = gridSize;
            Weight = weight;
            BaseValue = baseValue;
            CalculatedValue = calculatedValue;
            Damages = damages;
            ImplicitModifiers = implicitModifiers;
            Prefixes = prefixes;
            Suffixes = suffixes;
        }
    }
}
