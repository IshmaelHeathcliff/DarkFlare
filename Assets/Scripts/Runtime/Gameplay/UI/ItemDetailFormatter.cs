using System;
using System.Globalization;

namespace DarkFlare
{
    public sealed class ItemDetailFormatter
    {
        readonly Func<string, string, object[], string> _localize;

        public ItemDetailFormatter(Func<string, string, object[], string> localize = null)
        {
            _localize = localize ?? ((tableName, entryKey, arguments) =>
                $"[{tableName}.{entryKey}]");
        }

        public string GetItemTypeText(ItemType type)
        {
            string entryKey = type switch
            {
                ItemType.Weapon => "item.type.weapon",
                ItemType.Armor => "item.type.armor",
                ItemType.Accessory => "item.type.accessory",
                ItemType.Material => "item.type.material",
                ItemType.Currency => "item.type.currency",
                ItemType.Consumable => "item.type.consumable",
                ItemType.TradeGood => "item.type.trade_good",
                ItemType.QuestItem => "item.type.quest_item",
                ItemType.Firewood => "item.type.firewood",
                ItemType.Flame => "item.type.flame",
                ItemType.Ember => "item.type.ember",
                ItemType.EnergyBottle => "item.type.energy_bottle",
                _ => "item.type.unknown",
            };
            return Localize(entryKey);
        }

        public string GetRarityText(ItemRarity rarity)
        {
            string entryKey = rarity switch
            {
                ItemRarity.Normal => "item.rarity.normal",
                ItemRarity.Magic => "item.rarity.magic",
                ItemRarity.Rare => "item.rarity.rare",
                ItemRarity.Unique => "item.rarity.unique",
                _ => "item.rarity.unknown",
            };
            return Localize(entryKey);
        }

        public string GetAffixTypeText(AffixType type)
        {
            string entryKey = type switch
            {
                AffixType.Prefix => "item.affix_type.prefix",
                AffixType.Suffix => "item.affix_type.suffix",
                AffixType.Implicit => "item.affix_type.implicit",
                _ => "item.affix_type.unknown",
            };
            return Localize(entryKey);
        }

        public string GetDamageTypeText(DamageType damageType)
        {
            string entryKey = damageType switch
            {
                DamageType.Physical => "item.damage_type.physical",
                DamageType.Fire => "item.damage_type.fire",
                DamageType.Cold => "item.damage_type.cold",
                DamageType.Lightning => "item.damage_type.lightning",
                DamageType.Chaos => "item.damage_type.chaos",
                _ => "item.damage_type.unknown",
            };
            return Localize(entryKey);
        }

        public string FormatDamage(DamageType damageType, float minimum, float maximum)
        {
            string damageName = GetDamageTypeText(damageType);
            string minimumText = FormatNumber(minimum);
            string maximumText = FormatNumber(maximum);
            return NearlyEqual(minimum, maximum)
                ? Localize("item.damage.single", damageName, minimumText)
                : Localize("item.damage.range", damageName, minimumText, maximumText);
        }

        public string FormatModifier(
            ModifierInstance modifier,
            string statDisplayName,
            bool statIsPercent)
        {
            if (modifier == null)
            {
                return Localize("item.modifier.missing");
            }

            string statName = string.IsNullOrWhiteSpace(statDisplayName)
                ? Localize("item.stat.missing")
                : statDisplayName;
            string value = FormatNumber(modifier.Value);
            string percentSuffix = statIsPercent ? "%" : string.Empty;

            switch (modifier.Operation)
            {
                case ModifierOperation.Flat:
                    return Localize("item.modifier.flat", statName, value, percentSuffix);
                case ModifierOperation.Increase:
                    return Localize("item.modifier.increase", statName, value);
                case ModifierOperation.More:
                    return Localize("item.modifier.more", statName, value);
                case ModifierOperation.Override:
                    return Localize("item.modifier.override", statName, value, percentSuffix);
                case ModifierOperation.Conversion:
                    return Localize(
                        "item.modifier.conversion",
                        GetDamageTypeText(modifier.FromDamageType),
                        value,
                        GetDamageTypeText(modifier.ToDamageType));
                case ModifierOperation.GainAsExtra:
                    return Localize(
                        "item.modifier.gain_as_extra",
                        GetDamageTypeText(modifier.FromDamageType),
                        value,
                        GetDamageTypeText(modifier.ToDamageType));
                case ModifierOperation.Chance:
                    return Localize("item.modifier.chance", statName, value);
                case ModifierOperation.Trigger:
                    return Localize("item.modifier.trigger", statName, value);
                case ModifierOperation.Limit:
                    return Localize("item.modifier.limit", statName, value);
                default:
                    return Localize("item.modifier.default", statName, value);
            }
        }

        public static string FormatNumber(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public string FormatUseEffect(ItemUseEffect effect)
        {
            if (effect == null) { return Localize("item.use.configuration"); }
            string amount = FormatNumber(effect.Amount);
            switch (effect.Kind)
            {
                case ItemUseEffectKind.Heal:
                case ItemUseEffectKind.RestoreMana:
                    return Localize("item.use.effect." + (effect.Kind == ItemUseEffectKind.Heal ? "health" : "mana")
                        + (effect.RecoveryMode == ItemRecoveryMode.Percent ? "_percent" : string.Empty), amount);
                case ItemUseEffectKind.ApplyStatus:
                    if (effect.Status == null || effect.Status.ValidateConfiguration().Count > 0) { return Localize("item.use.configuration"); }
                    return Localize("item.use.effect.status", Resolve(effect.Status.LocalizedName.Message), effect.Count,
                        effect.Status.CreateRules().Duration, Resolve(effect.Status.LocalizedDescription.Message));
                case ItemUseEffectKind.Dispel:
                    string description = Localize("item.use.effect.dispel", effect.Count,
                        effect.Status != null ? Resolve(effect.Status.LocalizedName.Message) : Localize("item.use.all_ailments"));
                    if (!effect.RequiredTags.IsEmpty)
                    {
                        var names = new System.Collections.Generic.List<string>();
                        foreach (string id in effect.RequiredTags.Ids) { names.Add(_localize("items", "tag." + id + ".name", System.Array.Empty<object>())); }
                        description += " " + Localize("item.use.effect.tags", string.Join(", ", names));
                    }
                    return description;
                default: return string.Empty;
            }
        }

        string Resolve(LocalizedMessage message)
        {
            return _localize(message.TableName, message.EntryKey, System.Array.Empty<object>());
        }

        string Localize(string entryKey, params object[] arguments)
        {
            return _localize("ui", entryKey, arguments);
        }

        static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) <= 0.0001f;
        }
    }
}
