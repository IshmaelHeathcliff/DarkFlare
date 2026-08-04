using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct DamageDetailSnapshot
    {
        public DamageType DamageType { get; }

        public float Minimum { get; }

        public float Maximum { get; }

        public string TagSummary { get; }

        public string DisplayText { get; }

        public DamageDetailSnapshot(
            DamageType damageType,
            float minimum,
            float maximum,
            string tagSummary,
            string displayText)
        {
            DamageType = damageType;
            Minimum = minimum;
            Maximum = maximum;
            TagSummary = tagSummary;
            DisplayText = displayText;
        }
    }

    public readonly struct ModifierDetailSnapshot
    {
        public ModifierInstance Modifier { get; }

        public string StatDisplayName { get; }

        public ModifierOperation Operation { get; }

        public ModifierScope Scope { get; }

        public float Value { get; }

        public string DisplayText { get; }

        public ModifierDetailSnapshot(
            ModifierInstance modifier,
            string statDisplayName,
            string displayText)
        {
            Modifier = modifier;
            StatDisplayName = statDisplayName;
            Operation = modifier != null ? modifier.Operation : default;
            Scope = modifier != null ? modifier.Scope : default;
            Value = modifier != null ? modifier.Value : 0f;
            DisplayText = displayText;
        }
    }

    public readonly struct AffixDetailSnapshot
    {
        public AffixInstance Affix { get; }

        public AffixType Type { get; }

        public string DisplayName { get; }

        public IReadOnlyList<ModifierDetailSnapshot> Modifiers { get; }

        public string ModifierSummary { get; }

        public float TotalValue { get; }

        public AffixDetailSnapshot(
            AffixInstance affix,
            AffixType type,
            string displayName,
            IReadOnlyList<ModifierDetailSnapshot> modifiers,
            string modifierSummary,
            float totalValue)
        {
            Affix = affix;
            Type = type;
            DisplayName = displayName;
            Modifiers = modifiers;
            ModifierSummary = modifierSummary;
            TotalValue = totalValue;
        }
    }

    public readonly struct ItemDetailSnapshot
    {
        public ItemInstance Item { get; }

        public string InstanceId { get; }

        public string DisplayName { get; }

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
            string displayName,
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
            DisplayName = displayName;
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
