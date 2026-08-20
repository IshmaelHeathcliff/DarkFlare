using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    public enum StatCategory
    {
        Survival,
        Offense,
        Defense,
        Utility,
        Economy,
        Crafting
    }

    [ContentDefinition(ContentNamespaces.Stat)]
    [CreateAssetMenu(menuName = "DarkFlare/Data/Stats/Stat Definition", fileName = "StatDefinition")]
    public class StatDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [LabelText("中文名")]
        string _displayName = string.Empty;

        [SerializeField]
        [LabelText("本地化名称")]
        LocalizedContentReference _localizedName = new LocalizedContentReference("stats", string.Empty);

        [SerializeField]
        [LabelText("类别")]
        StatCategory _category;

        [SerializeField]
        [LabelText("默认值")]
        float _defaultValue;

        [SerializeField]
        [LabelText("最小值")]
        float _minValue;

        [SerializeField]
        [LabelText("最大值")]
        float _maxValue = 999999f;

        [SerializeField]
        [LabelText("百分比属性")]
        bool _isPercent;

        [SerializeField]
        [TextArea]
        [LabelText("说明")]
        string _description = string.Empty;

        public string Id => _id;

        public string DisplayName => _displayName;

        public LocalizedContentReference LocalizedName => _localizedName;

        public StatCategory Category => _category;

        public float DefaultValue => _defaultValue;

        public float MinValue => _minValue;

        public float MaxValue => _maxValue;

        public bool IsPercent => _isPercent;

        public string Description => _description;

        void OnValidate()
        {
            List<string> issues = StatConfigurationValidator.Validate(this);

            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogWarning($"[StatDefinition] {name}: {issues[i]}", this);
            }
        }
    }
}
