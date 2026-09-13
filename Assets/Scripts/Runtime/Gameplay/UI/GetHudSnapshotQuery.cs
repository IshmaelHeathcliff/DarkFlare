using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct HudAttributeValue
    {
        public string StatId { get; }

        public float Value { get; }

        public bool IsPercentage { get; }

        public HudAttributeValue(string statId, float value, bool isPercentage)
        {
            StatId = statId;
            Value = value;
            IsPercentage = isPercentage;
        }
    }

    public readonly struct HudAttributeSnapshot
    {
        readonly HudAttributeValue[] _values;

        static readonly HudAttributeValue[] EmptyValues = Array.Empty<HudAttributeValue>();

        static readonly AttributeDefinition[] Definitions =
        {
            new AttributeDefinition(StatIds.MaxHealth),
            new AttributeDefinition(StatIds.Mana),
            new AttributeDefinition(StatIds.HealthRegeneration),
            new AttributeDefinition(StatIds.ManaRegeneration),
            new AttributeDefinition(StatIds.Strength),
            new AttributeDefinition(StatIds.Dexterity),
            new AttributeDefinition(StatIds.Intelligence),
            new AttributeDefinition(StatIds.Damage),
            new AttributeDefinition(StatIds.CriticalChance, true),
            new AttributeDefinition(StatIds.CriticalDamage, true),
            new AttributeDefinition(StatIds.Accuracy),
            new AttributeDefinition(StatIds.Armor),
            new AttributeDefinition(StatIds.Evasion),
            new AttributeDefinition(StatIds.FireResistance, true),
            new AttributeDefinition(StatIds.ColdResistance, true),
            new AttributeDefinition(StatIds.LightningResistance, true),
            new AttributeDefinition(StatIds.ChaosResistance, true),
            new AttributeDefinition(StatIds.MoveSpeed),
            new AttributeDefinition(StatIds.PhysicalDamage),
            new AttributeDefinition(StatIds.FireDamage),
            new AttributeDefinition(StatIds.ColdDamage),
            new AttributeDefinition(StatIds.LightningDamage),
            new AttributeDefinition(StatIds.ChaosDamage),
            new AttributeDefinition(StatIds.WeaknessResistance, true),
            new AttributeDefinition(StatIds.StunResistance, true),
            new AttributeDefinition(StatIds.BleedingResistance, true),
            new AttributeDefinition(StatIds.BurningResistance, true),
            new AttributeDefinition(StatIds.ChillResistance, true),
            new AttributeDefinition(StatIds.ShockResistance, true),
            new AttributeDefinition(StatIds.PoisonResistance, true),
        };

        public IReadOnlyList<HudAttributeValue> Values => _values ?? EmptyValues;

        public float Armor { get; }

        public float Evasion { get; }

        public float MoveSpeed { get; }

        public float CriticalChance { get; }

        public float FireResistance { get; }

        public float ColdResistance { get; }

        public float LightningResistance { get; }

        public float ChaosResistance { get; }

        public HudAttributeSnapshot(StatBlock stats)
        {
            Armor = GetNonNegative(stats, StatIds.Armor);
            Evasion = GetNonNegative(stats, StatIds.Evasion);
            MoveSpeed = stats != null ? stats.GetValue(StatIds.MoveSpeed) : 0f;
            CriticalChance = CombatStatValues.CriticalChance(stats != null ? stats.GetValue(StatIds.CriticalChance) : 0f);
            FireResistance = GetResistance(stats, StatIds.FireResistance);
            ColdResistance = GetResistance(stats, StatIds.ColdResistance);
            LightningResistance = GetResistance(stats, StatIds.LightningResistance);
            ChaosResistance = GetResistance(stats, StatIds.ChaosResistance);
            _values = new HudAttributeValue[Definitions.Length];

            for (int i = 0; i < Definitions.Length; i++)
            {
                AttributeDefinition definition = Definitions[i];
                float value = stats != null ? CombatStatValues.Effective(stats, definition.StatId) : 0f;
                _values[i] = new HudAttributeValue(
                    definition.StatId,
                    value,
                    definition.IsPercentage);
            }
        }

        static float GetNonNegative(StatBlock stats, string statId)
        {
            return stats != null ? Mathf.Max(0f, stats.GetValue(statId)) : 0f;
        }

        static float GetResistance(StatBlock stats, string statId)
        {
            return stats != null ? CombatStatValues.Resistance(stats.GetValue(statId)) : 0f;
        }

        readonly struct AttributeDefinition
        {
            public string StatId { get; }

            public bool IsPercentage { get; }

            public AttributeDefinition(string statId, bool isPercentage = false)
            {
                StatId = statId;
                IsPercentage = isPercentage;
            }
        }
    }

    public readonly struct HudSnapshot
    {
        public bool HasPlayer { get; }

        public float CurrentHealth { get; }

        public float MaxHealth { get; }

        public float CurrentMana { get; }

        public float MaxMana { get; }

        public int Gold { get; }

        public HudAttributeSnapshot Attributes { get; }

        public float HealthNormalized => HasPlayer && MaxHealth > 0f
            ? Mathf.Clamp01(CurrentHealth / MaxHealth)
            : 0f;

        public float ManaNormalized => HasPlayer && MaxMana > 0f
            ? Mathf.Clamp01(CurrentMana / MaxMana)
            : 0f;

        public HudSnapshot(
            bool hasPlayer,
            float currentHealth,
            float maxHealth,
            float currentMana,
            float maxMana,
            int gold,
            HudAttributeSnapshot attributes)
        {
            HasPlayer = hasPlayer;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            CurrentMana = currentMana;
            MaxMana = maxMana;
            Gold = gold;
            Attributes = attributes;
        }
    }

    public class GetHudSnapshotQuery : AbstractQuery<HudSnapshot>
    {
        protected override HudSnapshot OnDo()
        {
            CombatActor player = GetPlayer();
            int gold = this.GetModel<InventoryModel>().Gold;

            if (player == null)
            {
                return new HudSnapshot(false, 0f, 0f, 0f, 0f, gold, new HudAttributeSnapshot(null));
            }

            return new HudSnapshot(
                true,
                player.CurrentHealth,
                player.MaxHealth,
                player.CurrentMana,
                player.MaxMana,
                gold,
                new HudAttributeSnapshot(player.Stats));
        }

        CombatActor GetPlayer()
        {
            IReadOnlyList<CombatActor> actors = this.GetModel<CombatModel>().GetActorsByTeam(ActorTeam.Player);

            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] != null)
                {
                    return actors[i];
                }
            }

            return null;
        }
    }
}
