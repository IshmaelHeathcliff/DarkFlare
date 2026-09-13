using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    [ContentDefinition(ContentNamespaces.Status)]
    [CreateAssetMenu(menuName = "DarkFlare/Data/Statuses/Status Definition", fileName = "StatusDefinition")]
    public sealed class StatusDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField, LabelText("稳定 ID")]
        string _id = string.Empty;
        [SerializeField, LabelText("本地化名称")]
        LocalizedContentReference _localizedName = new LocalizedContentReference("statuses", string.Empty);
        [SerializeField, LabelText("本地化说明")]
        LocalizedContentReference _localizedDescription = new LocalizedContentReference("statuses", string.Empty);
        [SerializeField, LabelText("状态图标")]
        AssetReferenceSprite _icon;
        [SerializeField, LabelText("分类")]
        StatusCategory _category = StatusCategory.Skill;
        [SerializeField, LabelText("状态标签")]
        List<TagDefinition> _tags = new List<TagDefinition>();
        [SerializeField, LabelText("可驱散")]
        bool _canDispel = true;
        [SerializeField, LabelText("可消耗层数")]
        bool _canConsume = true;
        [SerializeField, LabelText("数值与重复模型")]
        StatusRepeatMode _repeat;
        [SerializeField, LabelText("计时方式")]
        StatusClockMode _clock;
        [SerializeField, LabelText("未满层时刷新已有层"), ShowIf(nameof(CanConfigureLayerRefresh))]
        bool _refreshExistingLayers = true;
        [SerializeField, LabelText("持续类型")]
        StatusLifetime _lifetime;
        [SerializeField, LabelText("满层策略")]
        StatusOverflow _overflow;
        [SerializeField, LabelText("层数上限"), MinValue(1), MaxValue(4096)]
        int _maxStacks = 1;
        [SerializeField, LabelText("持续秒数"), MinValue(0)]
        double _duration = 5;
        [SerializeField, LabelText("周期间隔（0 为无周期）"), MinValue(0)]
        double _interval;
        [SerializeField, LabelText("择强比较值"), MinValue(0)]
        double _strength;
        [SerializeField, LabelText("属性修改器")]
        List<StatModifierDefinition> _modifiers = new List<StatModifierDefinition>();
        [SerializeField, LabelText("每跳基础伤害")]
        List<DamageRollDefinition> _periodicDamage = new List<DamageRollDefinition>();
        [SerializeField, LabelText("禁止行动")]
        StatusActionBlock _blockedActions;

        public string Id => _id;
        public LocalizedContentReference LocalizedName => _localizedName;
        public LocalizedContentReference LocalizedDescription => _localizedDescription;
        public AssetReferenceSprite Icon => _icon;
        bool CanConfigureLayerRefresh => _repeat == StatusRepeatMode.Uniform && _clock == StatusClockMode.PerLayer;

        public StatusRules CreateRules()
        {
            if (_tags == null || _tags.Exists(tag => tag == null || !ContentId.IsValidSegment(tag.Id)))
            {
                throw new ArgumentException("状态标签不能包含空引用或非法 ID");
            }
            return new StatusRules(_id, _repeat, _maxStacks, _duration, _interval, _lifetime, _clock,
                _overflow, _category, _canDispel, _canConsume, TagSet.FromDefinitions(_tags), _refreshExistingLayers);
        }

        public StatusEffectSnapshot CreateEffects(System.Random random)
        {
            if (random == null) { throw new ArgumentNullException(nameof(random)); }
            if (_modifiers == null || _periodicDamage == null) { throw new ArgumentException("效果列表不能为 null"); }
            var modifiers = new List<ModifierInstance>();
            var packets = new List<DamagePacket>();
            foreach (StatModifierDefinition modifier in _modifiers)
            {
                if (modifier == null || !ValidRange(modifier.ValueRange)) { throw new ArgumentException("状态修改器范围非法"); }
                modifiers.Add(modifier.CreateInstance(random));
            }
            foreach (DamageRollDefinition damage in _periodicDamage)
            {
                if (damage == null || !ValidRange(damage.AmountRange) || damage.AmountRange.x < 0)
                {
                    throw new ArgumentException("周期伤害范围非法");
                }
                packets.Add(damage.CreatePacket(random));
            }
            if (packets.Count > 0 && _interval <= 0) { throw new ArgumentException("周期伤害必须配置正间隔"); }
            return new StatusEffectSnapshot(_strength, modifiers, packets, _blockedActions, StatusDamageStage.Base);
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var issues = new List<string>();
            try { CreateRules(); }
            catch (ArgumentException exception) { issues.Add(exception.Message); }
            if (_tags == null || _tags.Exists(tag => tag == null || !ContentId.IsValidSegment(tag.Id)))
            {
                issues.Add("状态标签不能包含空引用或非法 ID");
            }
            if (_modifiers == null || _periodicDamage == null) { issues.Add("效果列表不能为 null"); }
            else
            {
                try { CreateEffects(new System.Random(0)); }
                catch (ArgumentException exception) { issues.Add(exception.Message); }
            }
            return issues.AsReadOnly();
        }

        static bool ValidRange(Vector2 range)
        {
            return StatusRules.IsFinite(range.x) && StatusRules.IsFinite(range.y)
                && StatusRules.IsFinite(range.y - range.x) && range.x <= range.y;
        }

        [Button("校验状态规则")]
        void LogValidation()
        {
            foreach (string issue in ValidateConfiguration())
            {
                ApplicationLog.Warning(LogEventIds.DataValidation, $"[StatusDefinition] {name}: {issue}", this);
            }
        }
    }
}
