using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class StatBlock
    {
        readonly Dictionary<string, float> _values = new Dictionary<string, float>();

        public IReadOnlyDictionary<string, float> Values => _values;

        public float GetValue(string statId)
        {
            return !string.IsNullOrWhiteSpace(statId) && _values.TryGetValue(statId, out float value) ? value : 0f;
        }

        public void SetValue(string statId, float value)
        {
            if (!string.IsNullOrWhiteSpace(statId))
            {
                _values[statId] = value;
            }
        }

        public void AddValue(string statId, float value)
        {
            if (string.IsNullOrWhiteSpace(statId))
            {
                return;
            }

            _values[statId] = GetValue(statId) + value;
        }

        public StatBlock Clone()
        {
            StatBlock clone = new StatBlock();

            foreach (KeyValuePair<string, float> pair in _values)
            {
                clone.SetValue(pair.Key, pair.Value);
            }

            return clone;
        }
    }
}

