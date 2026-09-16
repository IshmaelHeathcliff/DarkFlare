using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    public enum ItemUseEffectKind
    {
        [LabelText("恢复生命")] Heal,
        [LabelText("恢复法力")] RestoreMana,
        [LabelText("临时状态")] ApplyStatus,
        [LabelText("驱散异常")] Dispel
    }

    public enum ItemRecoveryMode
    {
        [LabelText("固定数值")] Fixed,
        [LabelText("最大值百分比（0–100）")] Percent
    }

    [Serializable]
    public sealed class ItemUseEffect
    {
        [SerializeField, LabelText("效果类型")]
        ItemUseEffectKind _kind;
        [SerializeField, LabelText("恢复方式")]
        ItemRecoveryMode _recoveryMode;
        [SerializeField, LabelText("恢复量"), MinValue(0)]
        float _amount;
        [SerializeField, LabelText("状态（施加必填、驱散留空为全部异常）")]
        StatusDefinition _status;
        [SerializeField, LabelText("施加层数 / 驱散最多层数"), MinValue(1)]
        int _count = 1;
        [SerializeField, LabelText("驱散必需标签")]
        List<TagDefinition> _tags = new List<TagDefinition>();

        public ItemUseEffectKind Kind => _kind;
        public ItemRecoveryMode RecoveryMode => _recoveryMode;
        public float Amount => _amount;
        public StatusDefinition Status => _status;
        public int Count => _count;
        public TagSet RequiredTags => TagSet.FromDefinitions(_tags);

        public ItemUseEffect() { }

        public ItemUseEffect(ItemUseEffectKind kind, float amount = 0, ItemRecoveryMode recoveryMode = ItemRecoveryMode.Fixed,
            StatusDefinition status = null, int count = 1)
        {
            _kind = kind; _amount = amount; _recoveryMode = recoveryMode; _status = status; _count = count;
        }

        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(ItemUseEffectKind), _kind) || !Enum.IsDefined(typeof(ItemRecoveryMode), _recoveryMode)
                || float.IsNaN(_amount) || float.IsInfinity(_amount) || _amount < 0 || _count < 1 || _count > 4096)
            {
                throw new ArgumentException("使用效果的类型、数值或层数非法");
            }
            if (_kind == ItemUseEffectKind.ApplyStatus && (_status == null || _status.CreateRules().Lifetime != StatusLifetime.Timed))
            {
                throw new ArgumentException("物品施加的状态必须有独立时限");
            }
            if (_status != null && _status.ValidateConfiguration().Count > 0) { throw new ArgumentException("使用状态配置非法"); }
            if (_tags == null || _tags.Exists(tag => tag == null || !ContentId.IsValidSegment(tag.Id)))
            {
                throw new ArgumentException("驱散标签非法");
            }
        }
    }

    [CreateAssetMenu(menuName = "DarkFlare/Data/Items/Item Use Definition", fileName = "ItemUseDefinition")]
    [ContentDefinition(ContentNamespaces.ItemUse)]
    public sealed class ItemUseDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField, LabelText("稳定 ID")]
        string _id = string.Empty;
        [SerializeField, LabelText("每次扣除数量（0 为不消耗）"), MinValue(0)]
        int _consumeCount = 1;
        [SerializeField, LabelText("按顺序执行的效果")]
        List<ItemUseEffect> _effects = new List<ItemUseEffect>();

        public int ConsumeCount => _consumeCount;
        public string Id => _id;
        public IReadOnlyList<ItemUseEffect> Effects => _effects.AsReadOnly();

        public List<string> ValidateConfiguration(ItemBaseDefinition item)
        {
            var issues = new List<string>();
            if (!ContentId.IsValidSegment(_id)) { issues.Add("使用能力需要合法稳定 ID"); }
            if (_consumeCount < 0 || item == null || _consumeCount > 0 && (!item.IsConsumable || _consumeCount > item.MaxStackSize))
            {
                issues.Add("使用扣除数量或物品消耗资格非法");
            }
            if (_effects == null || _effects.Count == 0 || _effects.Count > 64) { issues.Add("使用效果数量必须为 1–64"); }
            else
            {
                foreach (ItemUseEffect effect in _effects)
                {
                    if (effect == null) { issues.Add("使用效果不能为空"); continue; }
                    try { effect.Validate(); }
                    catch (ArgumentException exception) { issues.Add(exception.Message); }
                }
            }
            return issues;
        }
    }
}
