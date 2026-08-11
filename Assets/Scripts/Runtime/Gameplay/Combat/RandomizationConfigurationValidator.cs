using System.Collections.Generic;

namespace DarkFlare
{
    public static class RandomizationConfigurationValidator
    {
        public static List<string> Validate(MonsterDefinition definition)
        {
            List<string> issues = new List<string>();

            if (definition == null)
            {
                issues.Add("怪物定义为空");
                return issues;
            }

            if (definition.HealthMultiplierRange.x <= 0f || definition.HealthMultiplierRange.y <= 0f)
            {
                issues.Add("生命倍率必须大于 0");
            }

            if (definition.HealthMultiplierRange.x > definition.HealthMultiplierRange.y)
            {
                issues.Add("生命倍率范围上下限倒置");
            }

            ValidateDamageRolls(definition.ContactDamages, "碰撞伤害", issues, true);
            ValidateCombatStats(
                definition.Accuracy,
                definition.Evasion,
                definition.CriticalChance,
                definition.CriticalDamage,
                definition.Armor,
                issues);
            return issues;
        }

        public static List<string> Validate(ProjectileSkillDefinition definition)
        {
            List<string> issues = new List<string>();

            if (definition == null)
            {
                issues.Add("投射物技能定义为空");
                return issues;
            }

            bool requiresSkillDamage = definition.DamageSource == ProjectileDamageSource.Skill;
            ValidateDamageRolls(definition.BaseDamages, "基础伤害", issues, requiresSkillDamage);

            if (definition.DamageSource == ProjectileDamageSource.EquippedWeapon
                && definition.BaseDamages.Count > 0)
            {
                issues.Add("武器来源技能不能配置技能基础伤害");
            }

            return issues;
        }

        public static List<string> Validate(CharacterDefinition definition)
        {
            List<string> issues = new List<string>();

            if (definition == null)
            {
                issues.Add("角色定义为空");
                return issues;
            }

            ValidateCombatStats(
                definition.Accuracy,
                definition.Evasion,
                definition.CriticalChance,
                definition.CriticalDamage,
                definition.Armor,
                issues);
            return issues;
        }

        public static List<string> Validate(LootTableDefinition definition)
        {
            List<string> issues = new List<string>();

            if (definition == null)
            {
                issues.Add("掉落表定义为空");
                return issues;
            }

            if (definition.DropChance < 0f || definition.DropChance > 1f)
            {
                issues.Add("掉落概率必须位于 0 到 1 之间");
            }

            if (definition.Entries.Count == 0)
            {
                issues.Add("掉落池不能为空");
                return issues;
            }

            bool hasPositiveWeight = false;

            for (int i = 0; i < definition.Entries.Count; i++)
            {
                LootTableEntry entry = definition.Entries[i];

                if (entry == null)
                {
                    issues.Add($"掉落条目 {i} 为空");
                    continue;
                }

                if (entry.Weight < 0)
                {
                    issues.Add($"掉落条目 {i} 的权重不能为负数");
                }

                if (entry.Item != null && entry.Weight > 0)
                {
                    hasPositiveWeight = true;
                }
            }

            if (!hasPositiveWeight)
            {
                issues.Add("掉落池没有可用的正权重物品");
            }

            return issues;
        }

        public static void ValidateDamageRolls(
            IReadOnlyList<DamageRollDefinition> damages,
            string label,
            List<string> issues,
            bool requireNonEmpty)
        {
            if (damages == null || damages.Count == 0)
            {
                if (requireNonEmpty)
                {
                    issues.Add($"{label}池不能为空");
                }

                return;
            }

            for (int i = 0; i < damages.Count; i++)
            {
                DamageRollDefinition damage = damages[i];

                if (damage == null)
                {
                    issues.Add($"{label} {i} 为空");
                    continue;
                }

                if (damage.AmountRange.x < 0f || damage.AmountRange.y < 0f)
                {
                    issues.Add($"{label} {i} 不能包含负伤害");
                }

                if (damage.AmountRange.x > damage.AmountRange.y)
                {
                    issues.Add($"{label} {i} 的范围上下限倒置");
                }
            }
        }

        static void ValidateCombatStats(
            float accuracy,
            float evasion,
            float criticalChance,
            float criticalDamage,
            float armor,
            List<string> issues)
        {
            if (accuracy <= 0f)
            {
                issues.Add("命中值必须大于 0");
            }

            if (evasion < 0f)
            {
                issues.Add("闪避值不能为负数");
            }

            if (criticalChance < 0f || criticalChance > 100f)
            {
                issues.Add("暴击率必须位于 0 到 100 之间");
            }

            if (criticalDamage < 0f)
            {
                issues.Add("暴击伤害不能为负数");
            }

            if (armor < 0f)
            {
                issues.Add("护甲不能为负数");
            }
        }
    }
}
