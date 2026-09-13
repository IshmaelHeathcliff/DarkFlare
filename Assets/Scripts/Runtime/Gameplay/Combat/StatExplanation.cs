using System;

namespace DarkFlare
{
    public enum ModifierOriginKind { Legacy, Equipment, Monster, Status }

    public readonly struct ModifierOrigin
    {
        public ModifierOriginKind Kind { get; }
        public string StatusId { get; }
        public long LayerId { get; }
        public int StackCount { get; }
        public string ItemId { get; }
        public EquipmentSlot Slot { get; }
        public LocalizedMessage ItemName { get; }
        public LocalizedMessage AffixName { get; }

        public ModifierOrigin(string itemId, EquipmentSlot slot, LocalizedMessage itemName, LocalizedMessage affixName)
        {
            Kind = ModifierOriginKind.Equipment;
            StatusId = null; LayerId = 0; StackCount = 0;
            ItemId = itemId;
            Slot = slot;
            ItemName = itemName;
            AffixName = affixName;
        }

        public ModifierOrigin(ModifierOriginKind kind, string statusId = null, long layerId = 0, int stackCount = 1)
        {
            Kind = kind; StatusId = statusId; LayerId = layerId; StackCount = stackCount;
            ItemId = null; Slot = default; ItemName = default; AffixName = default;
        }
    }

    public readonly struct StatCalculationStep
    {
        public string StatId { get; }
        public ModifierOperation Operation { get; }
        public float Operand { get; }
        public float Result { get; }
        public ModifierOrigin Origin { get; }
        public string DerivedFrom { get; }

        public StatCalculationStep(string statId, ModifierOperation operation, float operand, float result, ModifierOrigin origin = default, string derivedFrom = null)
        {
            StatId = statId;
            Operation = operation;
            Operand = operand;
            Result = result;
            Origin = origin;
            DerivedFrom = derivedFrom;
        }
    }

    public static class CombatStatValues
    {
        public static bool IsResistance(string id)
        {
            return id == StatIds.FireResistance || id == StatIds.ColdResistance
                || id == StatIds.LightningResistance || id == StatIds.ChaosResistance;
        }

        public static bool IsPercentage(string id)
        {
            return IsResistance(id) || id == StatIds.CriticalChance || id == StatIds.CriticalDamage;
        }

        public static float CriticalChance(float value)
        {
            return Math.Clamp(value, 0f, 100f);
        }

        public static float CriticalMultiplier(float value)
        {
            return 1f + value / 100f;
        }

        public static float Resistance(float value)
        {
            return Math.Clamp(value, -100f, 75f);
        }

        public static float Effective(StatBlock stats, string id)
        {
            float value = stats.GetValue(id);
            if (IsResistance(id)) { return Resistance(value); }
            if (id == StatIds.CriticalChance) { return CriticalChance(value); }
            if (id == StatIds.ManaRegeneration)
            {
                return ResourceRegenerationSystem.CalculateManaRegenerationPerSecond(stats.GetValue(StatIds.Mana), value);
            }
            if (id == StatIds.MaxHealth || id == StatIds.Mana || id == StatIds.HealthRegeneration
                || id == StatIds.Armor || id == StatIds.Evasion || id == StatIds.Accuracy)
            {
                return Math.Max(0f, value);
            }
            return value;
        }
    }
}
