using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/Data/Monsters/Monster Affix Definition", fileName = "MonsterAffixDefinition")]
    public class MonsterAffixDefinition : ScriptableObject
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [LabelText("中文名")]
        string _displayName = string.Empty;

        [SerializeField]
        [LabelText("互斥组")]
        string _groupId = string.Empty;

        [SerializeField]
        [MinValue(1)]
        [LabelText("生成权重")]
        int _weight = 100;

        [SerializeField]
        [LabelText("显示颜色")]
        Color _displayColor = Color.white;

        [SerializeField]
        [LabelText("修改器")]
        List<StatModifierDefinition> _modifiers = new List<StatModifierDefinition>();

        public string Id => _id;

        public string DisplayName => _displayName;

        public string GroupId => _groupId;

        public int Weight => _weight;

        public Color DisplayColor => _displayColor;

        public IReadOnlyList<StatModifierDefinition> Modifiers => _modifiers;

        public MonsterAffixInstance CreateInstance(int valueSeed)
        {
            System.Random random = new System.Random(valueSeed);
            List<ModifierInstance> modifiers = new List<ModifierInstance>(_modifiers.Count);

            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i] != null)
                {
                    modifiers.Add(_modifiers[i].CreateInstance(random));
                }
            }

            return new MonsterAffixInstance(this, valueSeed, modifiers);
        }

        void OnValidate()
        {
            List<string> issues = MonsterAffixConfigurationValidator.Validate(this);

            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogWarning($"[MonsterAffixDefinition] {name}: {issues[i]}", this);
            }
        }
    }
}
