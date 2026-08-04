using System.Globalization;

namespace DarkFlare
{
    public static class ItemDetailFormatter
    {
        public static string GetItemTypeText(ItemType type)
        {
            switch (type)
            {
                case ItemType.Weapon:
                    return "武器";
                case ItemType.Armor:
                    return "护甲";
                case ItemType.Accessory:
                    return "饰品";
                case ItemType.Material:
                    return "材料";
                case ItemType.Currency:
                    return "货币";
                default:
                    return "未知类型";
            }
        }

        public static string GetRarityText(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Normal:
                    return "普通";
                case ItemRarity.Magic:
                    return "魔法";
                case ItemRarity.Rare:
                    return "稀有";
                case ItemRarity.Unique:
                    return "传奇";
                default:
                    return "未知稀有度";
            }
        }

        public static string GetAffixTypeText(AffixType type)
        {
            switch (type)
            {
                case AffixType.Prefix:
                    return "前缀";
                case AffixType.Suffix:
                    return "后缀";
                case AffixType.Implicit:
                    return "固有";
                default:
                    return "词缀";
            }
        }

        public static string GetDamageTypeText(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Physical:
                    return "物理";
                case DamageType.Fire:
                    return "火焰";
                case DamageType.Cold:
                    return "冰霜";
                case DamageType.Lightning:
                    return "闪电";
                case DamageType.Chaos:
                    return "混沌";
                default:
                    return "未知";
            }
        }

        public static string FormatDamage(DamageType damageType, float minimum, float maximum)
        {
            string damageName = GetDamageTypeText(damageType);
            string minimumText = FormatNumber(minimum);
            string maximumText = FormatNumber(maximum);
            return NearlyEqual(minimum, maximum)
                ? $"{damageName}伤害 {minimumText}"
                : $"{damageName}伤害 {minimumText}–{maximumText}";
        }

        public static string FormatModifier(
            ModifierInstance modifier,
            string statDisplayName,
            bool statIsPercent)
        {
            if (modifier == null)
            {
                return "未配置修改器";
            }

            string statName = string.IsNullOrWhiteSpace(statDisplayName)
                ? "未配置属性"
                : statDisplayName;
            string value = FormatNumber(modifier.Value);

            switch (modifier.Operation)
            {
                case ModifierOperation.Flat:
                    return $"{statName} +{value}{(statIsPercent ? "%" : string.Empty)}";
                case ModifierOperation.Increase:
                    return $"{statName}提高 {value}%";
                case ModifierOperation.More:
                    return $"{statName}总增 {value}%";
                case ModifierOperation.Override:
                    return $"{statName}设为 {value}{(statIsPercent ? "%" : string.Empty)}";
                case ModifierOperation.Conversion:
                    return $"{GetDamageTypeText(modifier.FromDamageType)}伤害的 {value}% 转化为{GetDamageTypeText(modifier.ToDamageType)}伤害";
                case ModifierOperation.GainAsExtra:
                    return $"获得等同于{GetDamageTypeText(modifier.FromDamageType)}伤害 {value}% 的额外{GetDamageTypeText(modifier.ToDamageType)}伤害";
                case ModifierOperation.Chance:
                    return $"{statName}触发概率 {value}%（暂未生效）";
                case ModifierOperation.Trigger:
                    return $"{statName}触发 {value}（暂未生效）";
                case ModifierOperation.Limit:
                    return $"{statName}上限 {value}（暂未生效）";
                default:
                    return $"{statName} {value}";
            }
        }

        public static string FormatNumber(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static bool NearlyEqual(float left, float right)
        {
            return System.Math.Abs(left - right) <= 0.0001f;
        }
    }
}
