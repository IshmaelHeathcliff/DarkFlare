using System.Collections.Generic;

namespace DarkFlare
{
    public static class StatAggregator
    {
        public static StatBlock Build(StatBlock baseStats, IEnumerable<ModifierInstance> modifiers, TagSet contextTags,
            List<StatCalculationStep> steps = null)
        {
            StatBlock result = baseStats != null ? baseStats.Clone() : new StatBlock();
            var increases = new Dictionary<string, List<ModifierInstance>>();
            var moreValues = new Dictionary<string, List<ModifierInstance>>();

            foreach (ModifierInstance modifier in modifiers)
            {
                if (modifier == null || !modifier.Matches(contextTags))
                {
                    continue;
                }

                if (modifier.Operation == ModifierOperation.Flat)
                {
                    result.AddValue(modifier.StatId, modifier.Value);
                    steps?.Add(new StatCalculationStep(modifier.StatId, modifier.Operation, modifier.Value, result.GetValue(modifier.StatId), modifier.Origin));
                }
                else if (modifier.Operation == ModifierOperation.Override)
                {
                    result.SetValue(modifier.StatId, modifier.Value);
                    steps?.Add(new StatCalculationStep(modifier.StatId, modifier.Operation, modifier.Value, result.GetValue(modifier.StatId), modifier.Origin));
                }
                else if (modifier.Operation == ModifierOperation.Increase)
                {
                    AddModifier(increases, modifier);
                }
                else if (modifier.Operation == ModifierOperation.More)
                {
                    AddModifier(moreValues, modifier);
                }
            }

            foreach (KeyValuePair<string, List<ModifierInstance>> pair in increases)
            {
                float current = result.GetValue(pair.Key);
                float total = 0;
                foreach (ModifierInstance modifier in pair.Value)
                {
                    total += modifier.Value;
                    steps?.Add(new StatCalculationStep(pair.Key, ModifierOperation.Increase, modifier.Value,
                        current * (1f + total / 100f), modifier.Origin));
                }
                result.SetValue(pair.Key, current * (1f + total / 100f));
            }

            foreach (KeyValuePair<string, List<ModifierInstance>> pair in moreValues)
            {
                float current = result.GetValue(pair.Key);

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    current *= 1f + pair.Value[i].Value / 100f;
                    steps?.Add(new StatCalculationStep(pair.Key, ModifierOperation.More, pair.Value[i].Value, current, pair.Value[i].Origin));
                }

                result.SetValue(pair.Key, current);
            }

            return result;
        }

        static void AddModifier(Dictionary<string, List<ModifierInstance>> values, ModifierInstance modifier)
        {
            string statId = modifier.StatId;
            if (string.IsNullOrWhiteSpace(statId))
            {
                return;
            }

            if (!values.ContainsKey(statId))
            {
                values[statId] = new List<ModifierInstance>();
            }

            values[statId].Add(modifier);
        }
    }
}
