using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    public enum AffixType
    {
        Prefix,
        Suffix,
        Implicit
    }

    public enum ModifierOperation
    {
        Flat,
        Increase,
        More,
        Override,
        Conversion,
        GainAsExtra,
        Chance,
        Trigger,
        Limit
    }

    public enum ModifierScope
    {
        LocalItem,
        GlobalActor,
        Skill,
        TargetTaken,
        Area,
        Temporary
    }

    public enum DamageType
    {
        Physical,
        Fire,
        Cold,
        Lightning,
        Chaos
    }

    [Serializable]
    public class StatModifierDefinition
    {
        [SerializeField]
        [ShowIf(nameof(UsesStat))]
        [LabelText("目标属性")]
        StatDefinition _stat;

        [SerializeField]
        [LabelText("计算方式")]
        ModifierOperation _operation;

        [SerializeField]
        [ShowIf(nameof(UsesScope))]
        [LabelText("作用域")]
        ModifierScope _scope = ModifierScope.GlobalActor;

        [SerializeField]
        [ShowIf(nameof(UsesValue))]
        [LabelText("数值范围")]
        Vector2 _valueRange;

        [SerializeField]
        [ShowIf(nameof(UsesDamageTypeMapping))]
        [LabelText("来源伤害类型")]
        DamageType _fromDamageType = DamageType.Physical;

        [SerializeField]
        [ShowIf(nameof(UsesDamageTypeMapping))]
        [LabelText("目标伤害类型")]
        DamageType _toDamageType = DamageType.Fire;

        [SerializeField]
        [ShowIf(nameof(UsesTags))]
        [LabelText("必须标签")]
        List<TagDefinition> _requiredTags = new List<TagDefinition>();

        [SerializeField]
        [ShowIf(nameof(UsesTags))]
        [LabelText("禁止标签")]
        List<TagDefinition> _blockedTags = new List<TagDefinition>();

        [ShowInInspector]
        [ShowIf(nameof(IsUnsupportedOperation))]
        [ReadOnly]
        [LabelText("说明")]
        string UnsupportedOperationTip => "当前伤害管线尚未实现该计算方式，仅保留枚举占位。";

        public StatDefinition Stat => _stat;

        public ModifierOperation Operation => _operation;

        public ModifierScope Scope => _scope;

        public Vector2 ValueRange => _valueRange;

        public DamageType FromDamageType => _fromDamageType;

        public DamageType ToDamageType => _toDamageType;

        public IReadOnlyList<TagDefinition> RequiredTags => _requiredTags;

        public IReadOnlyList<TagDefinition> BlockedTags => _blockedTags;

        bool UsesStat => _operation == ModifierOperation.Flat
            || _operation == ModifierOperation.Increase
            || _operation == ModifierOperation.More
            || _operation == ModifierOperation.Override;

        bool UsesScope => UsesStat || UsesDamageTypeMapping;

        bool UsesValue => UsesStat || UsesDamageTypeMapping;

        bool UsesDamageTypeMapping => _operation == ModifierOperation.Conversion
            || _operation == ModifierOperation.GainAsExtra;

        bool UsesTags => UsesValue;

        bool IsUnsupportedOperation => _operation == ModifierOperation.Chance
            || _operation == ModifierOperation.Trigger
            || _operation == ModifierOperation.Limit;

        public ModifierInstance CreateInstance(System.Random random)
        {
            float value = _valueRange.x;

            if (Math.Abs(_valueRange.x - _valueRange.y) > float.Epsilon)
            {
                value = _valueRange.x + (float)random.NextDouble() * (_valueRange.y - _valueRange.x);
            }

            string statId = _stat != null ? _stat.Id : string.Empty;

            return new ModifierInstance(
                statId,
                _operation,
                _scope,
                value,
                _fromDamageType,
                _toDamageType,
                TagSet.FromDefinitions(_requiredTags),
                TagSet.FromDefinitions(_blockedTags));
        }
    }

    [CreateAssetMenu(menuName = "DarkFlare/Data/Affixes/Affix Definition", fileName = "AffixDefinition")]
    public class AffixDefinition : ScriptableObject
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [LabelText("中文名")]
        string _displayName = string.Empty;

        [SerializeField]
        [LabelText("词条类型")]
        AffixType _affixType;

        [SerializeField]
        [LabelText("词条组")]
        string _groupId = string.Empty;

        [SerializeField]
        [MinValue(1)]
        [LabelText("最低物品等级")]
        int _minItemLevel = 1;

        [SerializeField]
        [MinValue(0)]
        [LabelText("权重")]
        int _weight = 100;

        [SerializeField]
        [LabelText("可出现物品标签")]
        List<TagDefinition> _allowedItemTags = new List<TagDefinition>();

        [SerializeField]
        [LabelText("禁止物品标签")]
        List<TagDefinition> _blockedItemTags = new List<TagDefinition>();

        [SerializeField]
        [LabelText("修改器")]
        List<StatModifierDefinition> _modifiers = new List<StatModifierDefinition>();

        public string Id => _id;

        public string DisplayName => _displayName;

        public AffixType AffixType => _affixType;

        public string GroupId => _groupId;

        public int MinItemLevel => _minItemLevel;

        public int Weight => _weight;

        public IReadOnlyList<TagDefinition> AllowedItemTags => _allowedItemTags;

        public IReadOnlyList<TagDefinition> BlockedItemTags => _blockedItemTags;

        public IReadOnlyList<StatModifierDefinition> Modifiers => _modifiers;

        public bool CanApplyTo(TagSet itemTags, int itemLevel)
        {
            if (itemLevel < _minItemLevel)
            {
                return false;
            }

            TagSet allowedTags = TagSet.FromDefinitions(_allowedItemTags);
            if (!allowedTags.IsEmpty && !itemTags.ContainsAny(allowedTags))
            {
                return false;
            }

            TagSet blockedTags = TagSet.FromDefinitions(_blockedItemTags);
            return blockedTags.IsEmpty || !itemTags.ContainsAny(blockedTags);
        }

        public AffixInstance CreateInstance(System.Random random)
        {
            List<ModifierInstance> modifiers = new List<ModifierInstance>(_modifiers.Count);

            for (int i = 0; i < _modifiers.Count; i++)
            {
                modifiers.Add(_modifiers[i].CreateInstance(random));
            }

            return new AffixInstance(this, modifiers);
        }

        void OnValidate()
        {
            List<string> issues = EquipmentConfigurationValidator.Validate(this);

            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogWarning($"[AffixDefinition] {name}: {issues[i]}", this);
            }
        }
    }
}
