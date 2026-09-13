using System.Collections.Generic;

namespace DarkFlare
{
    public static class EquipmentConfigurationValidator
    {
        public static List<string> Validate(ItemBaseDefinition definition)
        {
            List<string> issues = new List<string>();

            if (definition == null)
            {
                issues.Add("物品定义为空");
                return issues;
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                issues.Add("稳定 ID 不能为空");
            }

            ValidateSlots(definition, issues);
            if (definition.IsStackable && (definition.ImplicitModifiers.Count > 0 || definition.DefaultRarity != ItemRarity.Normal))
            {
                issues.Add("可堆叠物品必须为普通且没有隐式词条");
            }
            ValidateModifiers(definition, issues);
            RandomizationConfigurationValidator.ValidateDamageRolls(
                definition.BaseDamages,
                "基础伤害",
                issues,
                definition.ItemType == ItemType.Weapon);

            if (definition.ItemType != ItemType.Weapon && definition.BaseDamages.Count > 0)
            {
                issues.Add("只有武器可以配置物品基础伤害");
            }

            return issues;
        }

        public static List<string> Validate(AffixDefinition definition)
        {
            List<string> issues = new List<string>();

            if (definition == null)
            {
                issues.Add("词条定义为空");
                return issues;
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                issues.Add("稳定 ID 不能为空");
            }

            for (int i = 0; i < definition.Modifiers.Count; i++)
            {
                StatModifierDefinition modifier = definition.Modifiers[i];

                if (modifier == null)
                {
                    issues.Add($"修改器 {i} 为空");
                    continue;
                }

                ValidateStatModifier(modifier, $"修改器 {i}", issues);
            }

            return issues;
        }

        static void ValidateSlots(ItemBaseDefinition definition, List<string> issues)
        {
            EquipmentSlotMask slots = definition.AllowedEquipmentSlots;

            if (definition.ItemType == ItemType.Weapon && slots != EquipmentSlotMask.Weapon)
            {
                issues.Add("武器必须且只能允许武器槽");
            }
            else if (definition.ItemType == ItemType.Armor
                     && (slots == EquipmentSlotMask.None || (slots & ~EquipmentSlotMask.Defenses) != 0))
            {
                issues.Add("护甲只能允许身体、头部、手部、腿部或副手槽");
            }
            else if (definition.ItemType == ItemType.Accessory
                     && (slots == EquipmentSlotMask.None || (slots & ~EquipmentSlotMask.Accessories) != 0))
            {
                issues.Add("饰品只能允许戒指、项链或腰带槽");
            }
            else if (!definition.IsEquipment
                     && slots != EquipmentSlotMask.None)
            {
                issues.Add("材料和货币不能配置装备槽");
            }
        }

        static void ValidateModifiers(ItemBaseDefinition definition, List<string> issues)
        {
            for (int i = 0; i < definition.ImplicitModifiers.Count; i++)
            {
                StatModifierDefinition modifier = definition.ImplicitModifiers[i];

                if (modifier == null)
                {
                    issues.Add($"隐式修改器 {i} 为空");
                    continue;
                }

                ValidateStatModifier(modifier, $"隐式修改器 {i}", issues);

                if (modifier.Scope == ModifierScope.LocalItem && definition.ItemType != ItemType.Weapon)
                {
                    issues.Add($"隐式修改器 {i} 的 LocalItem 作用域只能用于武器");
                }
            }
        }

        static void ValidateStatModifier(
            StatModifierDefinition modifier,
            string label,
            List<string> issues)
        {
            if (modifier != null
                && UsesStat(modifier.Operation)
                && (modifier.Stat == null || !StatIds.IsSupported(modifier.Stat.Id)))
            {
                issues.Add($"{label} 使用了未支持的属性 ID");
            }
        }

        static bool UsesStat(ModifierOperation operation)
        {
            return operation == ModifierOperation.Flat
                || operation == ModifierOperation.Increase
                || operation == ModifierOperation.More
                || operation == ModifierOperation.Override;
        }
    }
}
