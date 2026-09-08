using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public static class EquipmentEffectResolver
    {
        public static List<ModifierInstance> CollectActorModifiers(EquipmentLoadout loadout)
        {
            List<ModifierInstance> result = new List<ModifierInstance>();

            if (loadout == null)
            {
                return result;
            }

            foreach (KeyValuePair<EquipmentSlot, ItemInstance> pair in loadout.Slots)
            {
                ItemInstance item = pair.Value;

                if (item == null)
                {
                    continue;
                }

                List<ModifierInstance> modifiers = CollectItemModifiers(item, pair.Key);

                for (int i = 0; i < modifiers.Count; i++)
                {
                    ModifierInstance modifier = modifiers[i];

                    if (modifier == null)
                    {
                        continue;
                    }

                    if (modifier.Scope == ModifierScope.LocalItem)
                    {
                        if (pair.Key != EquipmentSlot.Weapon)
                        {
                            ApplicationLog.Warning(LogEventIds.GameplayCombat,
                                $"[EquipmentEffectResolver] {item.InstanceId} 的 LocalItem 修改器位于非武器槽 {pair.Key}，已忽略");
                        }

                        continue;
                    }

                    if (modifier.Scope == ModifierScope.GlobalActor
                        || modifier.Scope == ModifierScope.Skill
                        || modifier.Scope == ModifierScope.TargetTaken)
                    {
                        result.Add(modifier);
                    }
                }
            }

            return result;
        }

        public static List<ModifierInstance> CollectLocalWeaponModifiers(ItemInstance weapon)
        {
            List<ModifierInstance> result = new List<ModifierInstance>();

            if (weapon == null)
            {
                return result;
            }

            List<ModifierInstance> modifiers = CollectItemModifiers(weapon, EquipmentSlot.Weapon);

            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierInstance modifier = modifiers[i];

                if (modifier != null && modifier.Scope == ModifierScope.LocalItem)
                {
                    result.Add(modifier);
                }
            }

            return result;
        }

        public static List<ModifierInstance> CollectItemModifiers(ItemInstance item, EquipmentSlot slot)
        {
            var result = new List<ModifierInstance>();
            if (item == null) { return result; }
            LocalizedMessage name = item.BaseDefinition?.LocalizedName?.Message ?? default;
            Add(item.ImplicitModifiers, LocalizedMessage.Ui("item.affix_type.implicit"));
            foreach (AffixInstance affix in item.Prefixes) { Add(affix.Modifiers, affix.Definition.LocalizedName.Message); }
            foreach (AffixInstance affix in item.Suffixes) { Add(affix.Modifiers, affix.Definition.LocalizedName.Message); }
            return result;

            void Add(IReadOnlyList<ModifierInstance> modifiers, LocalizedMessage affix)
            {
                foreach (ModifierInstance modifier in modifiers)
                {
                    if (modifier != null) { result.Add(modifier.WithOrigin(new ModifierOrigin(item.InstanceId, slot, name, affix))); }
                }
            }
        }
    }

    public static class CombatStatResolver
    {
        public static StatBlock Build(StatBlock baseStats, IEnumerable<ModifierInstance> modifiers,
            List<StatCalculationStep> steps = null)
        {
            List<ModifierInstance> statModifiers = new List<ModifierInstance>();

            if (modifiers != null)
            {
                foreach (ModifierInstance modifier in modifiers)
                {
                    if (!CanAggregate(modifier))
                    {
                        continue;
                    }

                    statModifiers.Add(modifier);
                }
            }

            StatBlock directStats = StatAggregator.Build(baseStats, statModifiers, TagSet.Empty, steps);
            return PrimaryAttributeResolver.Apply(directStats, steps);
        }

        public static bool IsDamageStat(string statId)
        {
            return statId == StatIds.Damage
                || statId == StatIds.PhysicalDamage
                || statId == StatIds.FireDamage
                || statId == StatIds.ColdDamage
                || statId == StatIds.LightningDamage
                || statId == StatIds.ChaosDamage;
        }

        public static bool CanAggregate(ModifierInstance modifier)
        {
            return modifier != null && modifier.Scope == ModifierScope.GlobalActor
                && !IsDamageStat(modifier.StatId) && IsStatOperation(modifier.Operation);
        }

        static bool IsStatOperation(ModifierOperation operation)
        {
            return operation == ModifierOperation.Flat
                || operation == ModifierOperation.Increase
                || operation == ModifierOperation.More
                || operation == ModifierOperation.Override;
        }
    }
}
