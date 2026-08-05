using System.Collections.Generic;

namespace DarkFlare
{
    public static class StatIds
    {
        public const string Armor = "armor";
        public const string Accuracy = "accuracy";
        public const string ChaosResistance = "chaos_resistance";
        public const string ChaosDamage = "chaos_damage";
        public const string ColdResistance = "cold_resistance";
        public const string ColdDamage = "cold_damage";
        public const string CriticalDamage = "critical_damage";
        public const string CriticalChance = "critical_chance";
        public const string Damage = "damage";
        public const string Dexterity = "dexterity";
        public const string Evasion = "evasion";
        public const string FireResistance = "fire_resistance";
        public const string FireDamage = "fire_damage";
        public const string Intelligence = "intelligence";
        public const string LightningResistance = "lightning_resistance";
        public const string LightningDamage = "lightning_damage";
        public const string Mana = "mana";
        public const string MaxHealth = "max_health";
        public const string MoveSpeed = "move_speed";
        public const string PhysicalDamage = "physical_damage";
        public const string Strength = "strength";

        static readonly string[] AllIds =
        {
            MaxHealth,
            Mana,
            Strength,
            Dexterity,
            Intelligence,
            Damage,
            CriticalChance,
            CriticalDamage,
            Accuracy,
            Armor,
            Evasion,
            FireResistance,
            ColdResistance,
            LightningResistance,
            ChaosResistance,
            MoveSpeed,
            PhysicalDamage,
            FireDamage,
            ColdDamage,
            LightningDamage,
            ChaosDamage,
        };

        public static IReadOnlyList<string> All => AllIds;

        public static bool IsSupported(string statId)
        {
            for (int i = 0; i < AllIds.Length; i++)
            {
                if (AllIds[i] == statId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
