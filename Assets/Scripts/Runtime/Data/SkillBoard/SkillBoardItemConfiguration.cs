using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [Serializable]
    public sealed class SkillBoardItemConfiguration
    {
        [SerializeField, LabelText("柴薪生成模板")]
        FirewoodGenerationDefinition _firewood;
        [SerializeField, LabelText("天赋修改器候选（每节点抽取一项）")]
        List<StatModifierDefinition> _talentModifiers = new List<StatModifierDefinition>();
        [SerializeField, MinValue(0), LabelText("天赋范围（0 为全局）")]
        int _talentRadius;
        [SerializeField, LabelText("火焰技能")]
        ProjectileSkillDefinition _flameSkill;
        [SerializeField, MinValue(1), LabelText("余烬影响距离")]
        int _emberRadius = 3;
        [SerializeField, MinValue(1), LabelText("能量瓶容量")]
        int _energyCapacity = 1000;

        public FirewoodGenerationDefinition Firewood => _firewood;
        public IReadOnlyList<StatModifierDefinition> TalentModifiers => _talentModifiers;
        public int TalentRadius => _talentRadius;
        public ProjectileSkillDefinition FlameSkill => _flameSkill;
        public int EmberRadius => _emberRadius;
        public int EnergyCapacity => _energyCapacity;

        public SkillBoardItemState CreateState(ItemType type, int seed)
        {
            if (type == ItemType.Firewood)
            {
                if (_firewood == null || _talentModifiers == null || _talentModifiers.Count == 0)
                {
                    throw new InvalidOperationException("柴薪缺少生成模板或天赋候选");
                }
                // 独立子流，不消耗全局随机通道，也不影响隐式词条的抽取。
                int layoutSeed = unchecked(seed ^ (int)0xA511E9B3);
                var generated = _firewood.Generate(layoutSeed);
                if (!generated.Success) { throw new InvalidOperationException("柴薪生成模板不合法"); }
                var random = new System.Random(unchecked(seed ^ 0x63D83595));
                var talents = new List<SkillBoardTalentState>();
                foreach (FirewoodNode node in generated.Layout.Nodes)
                {
                    if (node.Kind != FirewoodNodeKind.Talent) { continue; }
                    StatModifierDefinition definition = _talentModifiers[random.Next(_talentModifiers.Count)];
                    if (definition == null) { throw new InvalidOperationException("柴薪天赋候选为空"); }
                    talents.Add(new SkillBoardTalentState(node.Id, _talentRadius,
                        new[] { definition.CreateInstance(random) }));
                }
                return new SkillBoardItemState(generated.Layout, talents);
            }
            return new SkillBoardItemState(flameSkill: type == ItemType.Flame ? _flameSkill : null,
                emberRadius: type == ItemType.Ember ? _emberRadius : 0);
        }

        public static List<string> Validate(ItemBaseDefinition definition)
        {
            var issues = new List<string>();
            if (definition == null || !definition.IsSkillBoardItem) { return issues; }
            SkillBoardItemConfiguration config = definition.SkillBoard;
            Vector2Int size = definition.ItemType == ItemType.EnergyBottle ? new Vector2Int(2, 1) : Vector2Int.one;
            if (definition.GridSize != size) { issues.Add($"棋盘道具背包尺寸应为 {size}"); }
            if (config == null) { issues.Add("缺少棋盘道具配置"); return issues; }
            if (definition.AllowedEquipmentSlots != EquipmentSlotMask.None || definition.UseDefinition != null
                || definition.ProvidedStatus != null || definition.IsConsumable)
            {
                issues.Add("棋盘道具不能配置战斗装备槽、主动使用或装备状态；充能由棋盘事务消费");
            }
            if (definition.ItemType == ItemType.Firewood)
            {
                if (config.Firewood == null || config.Firewood.ValidateConfiguration().Count > 0)
                {
                    issues.Add("柴薪需要合法生成模板");
                }
                if (config._talentModifiers == null || config._talentModifiers.Count == 0
                    || config._talentModifiers.Exists(m => m == null) || config.TalentRadius < 0)
                {
                    issues.Add("柴薪需要合法天赋候选和范围");
                }
                else
                {
                    foreach (StatModifierDefinition modifier in config._talentModifiers)
                    {
                        if (!Enum.IsDefined(typeof(ModifierOperation), modifier.Operation)
                            || !Enum.IsDefined(typeof(ModifierScope), modifier.Scope)
                            || modifier.Stat == null || !StatIds.IsSupported(modifier.Stat.Id)
                            || !float.IsFinite(modifier.ValueRange.x) || !float.IsFinite(modifier.ValueRange.y)
                            || modifier.ValueRange.y < modifier.ValueRange.x)
                        { issues.Add("天赋修改器的属性、计算方式、范围或数值非法"); }
                    }
                }
            }
            if (definition.ItemType == ItemType.Flame && config.FlameSkill == null) { issues.Add("火焰缺少技能"); }
            if (definition.ItemType == ItemType.Ember && (config.EmberRadius < 1 || definition.ImplicitModifiers.Count == 0))
            {
                issues.Add("余烬需要范围和强化修改器");
            }
            if (definition.ItemType == ItemType.EnergyBottle && config.EnergyCapacity < 1) { issues.Add("容量必须大于零"); }
            return issues;
        }
    }
}
