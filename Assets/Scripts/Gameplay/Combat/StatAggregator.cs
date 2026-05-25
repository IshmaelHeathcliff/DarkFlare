using System.Collections.Generic;

namespace DarkFlare
{
    public static class StatAggregator
    {
        public static StatBlock Build(StatBlock baseStats, IEnumerable<ModifierInstance> modifiers, TagSet contextTags)
        {
            StatBlock result = baseStats != null ? baseStats.Clone() : new StatBlock();
            Dictionary<string, float> increases = new Dictionary<string, float>();
            Dictionary<string, List<float>> moreValues = new Dictionary<string, List<float>>();

            foreach (ModifierInstance modifier in modifiers)
            {
                if (modifier == null || !modifier.Matches(contextTags))
                {
                    continue;
                }

                if (modifier.Operation == ModifierOperation.Flat)
                {
                    result.AddValue(modifier.StatId, modifier.Value);
                }
                else if (modifier.Operation == ModifierOperation.Override)
                {
                    result.SetValue(modifier.StatId, modifier.Value);
                }
                else if (modifier.Operation == ModifierOperation.Increase)
                {
                    AddValue(increases, modifier.StatId, modifier.Value);
                }
                else if (modifier.Operation == ModifierOperation.More)
                {
                    AddMoreValue(moreValues, modifier.StatId, modifier.Value);
                }
            }

            foreach (KeyValuePair<string, float> pair in increases)
            {
                float current = result.GetValue(pair.Key);
                result.SetValue(pair.Key, current * (1f + pair.Value / 100f));
            }

            foreach (KeyValuePair<string, List<float>> pair in moreValues)
            {
                float current = result.GetValue(pair.Key);

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    current *= 1f + pair.Value[i] / 100f;
                }

                result.SetValue(pair.Key, current);
            }

            return result;
        }

        static void AddValue(Dictionary<string, float> values, string statId, float value)
        {
            if (string.IsNullOrWhiteSpace(statId))
            {
                return;
            }

            if (!values.ContainsKey(statId))
            {
                values[statId] = 0f;
            }

            values[statId] += value;
        }

        static void AddMoreValue(Dictionary<string, List<float>> values, string statId, float value)
        {
            if (string.IsNullOrWhiteSpace(statId))
            {
                return;
            }

            if (!values.ContainsKey(statId))
            {
                values[statId] = new List<float>();
            }

            values[statId].Add(value);
        }
    }
}

