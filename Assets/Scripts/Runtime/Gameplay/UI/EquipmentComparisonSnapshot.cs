using System.Collections.Generic;

namespace DarkFlare
{
    public readonly struct EquipmentComparisonSnapshot
    {
        public EquipmentSlot Slot { get; }

        public ItemDetailSnapshot Current { get; }

        public ItemDetailSnapshot Candidate { get; }

        public IReadOnlyList<string> Lines { get; }

        public EquipmentComparisonSnapshot(
            EquipmentSlot slot,
            ItemDetailSnapshot current,
            ItemDetailSnapshot candidate,
            IReadOnlyList<string> lines)
        {
            Slot = slot;
            Current = current;
            Candidate = candidate;
            Lines = lines;
        }
    }

    public static class EquipmentComparisonFactory
    {
        public static EquipmentComparisonSnapshot Create(
            EquipmentSlot slot,
            ItemInstance current,
            ItemInstance candidate)
        {
            ItemDetailSnapshot currentDetail = ItemDetailSnapshotFactory.Create(current);
            ItemDetailSnapshot candidateDetail = ItemDetailSnapshotFactory.Create(candidate);
            List<string> lines = new List<string>();

            AddDamageComparison(currentDetail, candidateDetail, lines);
            AddModifierComparisons(currentDetail, candidateDetail, lines);

            if (lines.Count == 0)
            {
                lines.Add("没有可直接比较的数值；条件或转换词条请查看详情原文");
            }

            return new EquipmentComparisonSnapshot(slot, currentDetail, candidateDetail, lines);
        }

        static void AddDamageComparison(
            ItemDetailSnapshot current,
            ItemDetailSnapshot candidate,
            List<string> lines)
        {
            if (current.Damages.Count == 0 && candidate.Damages.Count == 0)
            {
                return;
            }

            GetDamageRange(current.Damages, out float currentMinimum, out float currentMaximum);
            GetDamageRange(candidate.Damages, out float candidateMinimum, out float candidateMaximum);
            float minimumDifference = candidateMinimum - currentMinimum;
            float maximumDifference = candidateMaximum - currentMaximum;
            lines.Add(
                $"基础伤害 {currentMinimum:0.#}–{currentMaximum:0.#} → {candidateMinimum:0.#}–{candidateMaximum:0.#} "
                + $"({FormatDifference(minimumDifference)} / {FormatDifference(maximumDifference)})");
        }

        static void AddModifierComparisons(
            ItemDetailSnapshot current,
            ItemDetailSnapshot candidate,
            List<string> lines)
        {
            Dictionary<string, ModifierValue> currentValues = CollectComparableModifiers(current);
            Dictionary<string, ModifierValue> candidateValues = CollectComparableModifiers(candidate);
            HashSet<string> keys = new HashSet<string>(currentValues.Keys);
            keys.UnionWith(candidateValues.Keys);

            foreach (string key in keys)
            {
                currentValues.TryGetValue(key, out ModifierValue currentValue);
                candidateValues.TryGetValue(key, out ModifierValue candidateValue);
                ModifierValue display = !string.IsNullOrEmpty(candidateValue.DisplayName) ? candidateValue : currentValue;
                float difference = candidateValue.Value - currentValue.Value;
                lines.Add(
                    $"{display.DisplayName}{GetOperationLabel(display.Operation)} "
                    + $"{FormatModifierValue(currentValue.Value, display.Operation)} → "
                    + $"{FormatModifierValue(candidateValue.Value, display.Operation)} "
                    + $"({FormatModifierValue(difference, display.Operation, true)})");
            }
        }

        static Dictionary<string, ModifierValue> CollectComparableModifiers(ItemDetailSnapshot detail)
        {
            Dictionary<string, ModifierValue> values = new Dictionary<string, ModifierValue>();
            AddModifiers(detail.ImplicitModifiers, values);

            for (int i = 0; i < detail.Prefixes.Count; i++)
            {
                AddModifiers(detail.Prefixes[i].Modifiers, values);
            }

            for (int i = 0; i < detail.Suffixes.Count; i++)
            {
                AddModifiers(detail.Suffixes[i].Modifiers, values);
            }

            return values;
        }

        static void AddModifiers(
            IReadOnlyList<ModifierDetailSnapshot> modifiers,
            Dictionary<string, ModifierValue> values)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierDetailSnapshot detail = modifiers[i];
                ModifierInstance modifier = detail.Modifier;

                if (modifier == null
                    || modifier.Query.HasConditions
                    || !IsComparableOperation(modifier.Operation))
                {
                    continue;
                }

                string key = $"{modifier.Scope}:{modifier.Operation}:{modifier.StatId}";
                values.TryGetValue(key, out ModifierValue current);
                values[key] = new ModifierValue(
                    detail.StatDisplayName,
                    modifier.Operation,
                    current.Value + modifier.Value);
            }
        }

        static void GetDamageRange(
            IReadOnlyList<DamageDetailSnapshot> damages,
            out float minimum,
            out float maximum)
        {
            minimum = 0f;
            maximum = 0f;

            for (int i = 0; i < damages.Count; i++)
            {
                minimum += damages[i].Minimum;
                maximum += damages[i].Maximum;
            }
        }

        static bool IsComparableOperation(ModifierOperation operation)
        {
            return operation == ModifierOperation.Flat
                || operation == ModifierOperation.Increase
                || operation == ModifierOperation.More;
        }

        static string FormatDifference(float difference)
        {
            return difference > 0f ? $"+{difference:0.#}" : difference.ToString("0.#");
        }

        static string GetOperationLabel(ModifierOperation operation)
        {
            return operation switch
            {
                ModifierOperation.Increase => "提高",
                ModifierOperation.More => "总增",
                _ => string.Empty,
            };
        }

        static string FormatModifierValue(
            float value,
            ModifierOperation operation,
            bool showPositiveSign = false)
        {
            string number = showPositiveSign && value > 0f
                ? $"+{value:0.#}"
                : value.ToString("0.#");
            return operation == ModifierOperation.Increase || operation == ModifierOperation.More
                ? $"{number}%"
                : number;
        }

        readonly struct ModifierValue
        {
            public string DisplayName { get; }

            public ModifierOperation Operation { get; }

            public float Value { get; }

            public ModifierValue(string displayName, ModifierOperation operation, float value)
            {
                DisplayName = displayName;
                Operation = operation;
                Value = value;
            }
        }
    }
}
