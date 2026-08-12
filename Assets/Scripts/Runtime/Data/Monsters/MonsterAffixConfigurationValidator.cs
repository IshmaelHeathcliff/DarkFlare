using System.Collections.Generic;

namespace DarkFlare
{
    public static class MonsterAffixConfigurationValidator
    {
        public static List<string> Validate(MonsterAffixDefinition definition)
        {
            List<string> issues = new List<string>();

            if (definition == null)
            {
                issues.Add("怪物词条定义为空");
                return issues;
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                issues.Add("稳定ID不能为空");
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                issues.Add("中文名不能为空");
            }

            if (string.IsNullOrWhiteSpace(definition.GroupId))
            {
                issues.Add("互斥组不能为空");
            }

            if (definition.Weight <= 0)
            {
                issues.Add("生成权重必须大于 0");
            }

            if (definition.Modifiers == null || definition.Modifiers.Count == 0)
            {
                issues.Add("修改器不能为空");
                return issues;
            }

            for (int i = 0; i < definition.Modifiers.Count; i++)
            {
                ValidateModifier(definition.Modifiers[i], i, issues);
            }

            return issues;
        }

        public static List<string> ValidatePool(
            IReadOnlyList<MonsterAffixDefinition> pool,
            int minimumCount,
            int maximumCount)
        {
            List<string> issues = new List<string>();

            if (minimumCount < 0 || maximumCount < 0)
            {
                issues.Add("怪物词条数量不能为负数");
            }

            if (minimumCount > maximumCount)
            {
                issues.Add("怪物词条数量范围上下限倒置");
            }

            if (pool == null || pool.Count == 0)
            {
                if (maximumCount > 0)
                {
                    issues.Add("启用怪物词条时词条池不能为空");
                }

                return issues;
            }

            HashSet<MonsterAffixDefinition> definitions = new HashSet<MonsterAffixDefinition>();
            HashSet<string> ids = new HashSet<string>();
            HashSet<string> groups = new HashSet<string>();

            for (int i = 0; i < pool.Count; i++)
            {
                MonsterAffixDefinition definition = pool[i];

                if (definition == null)
                {
                    issues.Add($"怪物词条池条目 {i} 为空");
                    continue;
                }

                if (!definitions.Add(definition))
                {
                    issues.Add($"怪物词条池重复引用 {definition.Id}");
                }

                if (!string.IsNullOrWhiteSpace(definition.Id) && !ids.Add(definition.Id))
                {
                    issues.Add($"怪物词条池存在重复ID {definition.Id}");
                }

                if (!string.IsNullOrWhiteSpace(definition.GroupId))
                {
                    groups.Add(definition.GroupId);
                }
            }

            if (maximumCount > groups.Count)
            {
                issues.Add("怪物词条数量上限超过可用互斥组数量");
            }

            return issues;
        }

        static void ValidateModifier(
            StatModifierDefinition modifier,
            int index,
            List<string> issues)
        {
            if (modifier == null)
            {
                issues.Add($"修改器 {index} 为空");
                return;
            }

            if (modifier.Scope != ModifierScope.GlobalActor)
            {
                issues.Add($"修改器 {index} 只能使用 GlobalActor 作用域");
            }

            if (modifier.ValueRange.x > modifier.ValueRange.y)
            {
                issues.Add($"修改器 {index} 的数值范围上下限倒置");
            }

            if (modifier.Operation == ModifierOperation.GainAsExtra)
            {
                if (modifier.FromDamageType != DamageType.Physical
                    || (modifier.ToDamageType != DamageType.Fire
                        && modifier.ToDamageType != DamageType.Cold
                        && modifier.ToDamageType != DamageType.Lightning))
                {
                    issues.Add($"修改器 {index} 的元素附伤必须由物理伤害转为火焰、冰霜或闪电伤害");
                }

                return;
            }

            if (modifier.Operation != ModifierOperation.Flat
                && modifier.Operation != ModifierOperation.Increase
                && modifier.Operation != ModifierOperation.More)
            {
                issues.Add($"修改器 {index} 使用了不支持的计算方式 {modifier.Operation}");
                return;
            }

            if (modifier.Stat == null || !StatIds.IsSupported(modifier.Stat.Id))
            {
                issues.Add($"修改器 {index} 没有可用的属性定义");
            }
            else if (CombatStatResolver.IsDamageStat(modifier.Stat.Id))
            {
                issues.Add($"修改器 {index} 的伤害增益应使用元素附伤，而不是伤害属性修改器");
            }
        }
    }
}
