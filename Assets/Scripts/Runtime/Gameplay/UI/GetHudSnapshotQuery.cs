using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct HudAttributeValue
    {
        public string StatId { get; }

        public string DisplayName { get; }

        public float Value { get; }

        public bool IsPercentage { get; }

        public HudAttributeValue(string statId, string displayName, float value, bool isPercentage)
        {
            StatId = statId;
            DisplayName = displayName;
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
            new AttributeDefinition(StatIds.MaxHealth, "最大生命"),
            new AttributeDefinition(StatIds.Mana, "魔力"),
            new AttributeDefinition(StatIds.Strength, "力量"),
            new AttributeDefinition(StatIds.Dexterity, "敏捷"),
            new AttributeDefinition(StatIds.Intelligence, "智力"),
            new AttributeDefinition(StatIds.Damage, "伤害"),
            new AttributeDefinition(StatIds.CriticalChance, "暴击率", true),
            new AttributeDefinition(StatIds.CriticalDamage, "暴击伤害", true),
            new AttributeDefinition(StatIds.Accuracy, "命中值"),
            new AttributeDefinition(StatIds.Armor, "护甲"),
            new AttributeDefinition(StatIds.Evasion, "闪避值"),
            new AttributeDefinition(StatIds.FireResistance, "火焰抗性", true),
            new AttributeDefinition(StatIds.ColdResistance, "冰霜抗性", true),
            new AttributeDefinition(StatIds.LightningResistance, "闪电抗性", true),
            new AttributeDefinition(StatIds.ChaosResistance, "混沌抗性", true),
            new AttributeDefinition(StatIds.MoveSpeed, "移动速度"),
            new AttributeDefinition(StatIds.PhysicalDamage, "物理伤害"),
            new AttributeDefinition(StatIds.FireDamage, "火焰伤害"),
            new AttributeDefinition(StatIds.ColdDamage, "冰霜伤害"),
            new AttributeDefinition(StatIds.LightningDamage, "闪电伤害"),
            new AttributeDefinition(StatIds.ChaosDamage, "混沌伤害"),
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
            MoveSpeed = GetNonNegative(stats, StatIds.MoveSpeed);
            CriticalChance = GetNonNegative(stats, StatIds.CriticalChance);
            FireResistance = GetResistance(stats, StatIds.FireResistance);
            ColdResistance = GetResistance(stats, StatIds.ColdResistance);
            LightningResistance = GetResistance(stats, StatIds.LightningResistance);
            ChaosResistance = GetResistance(stats, StatIds.ChaosResistance);
            _values = new HudAttributeValue[Definitions.Length];

            for (int i = 0; i < Definitions.Length; i++)
            {
                AttributeDefinition definition = Definitions[i];
                float value = IsResistance(definition.StatId)
                    ? GetResistance(stats, definition.StatId)
                    : GetNonNegative(stats, definition.StatId);
                _values[i] = new HudAttributeValue(
                    definition.StatId,
                    definition.DisplayName,
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
            return stats != null ? Mathf.Clamp(stats.GetValue(statId), -100f, 75f) : 0f;
        }

        static bool IsResistance(string statId)
        {
            return statId == StatIds.FireResistance
                || statId == StatIds.ColdResistance
                || statId == StatIds.LightningResistance
                || statId == StatIds.ChaosResistance;
        }

        readonly struct AttributeDefinition
        {
            public string StatId { get; }

            public string DisplayName { get; }

            public bool IsPercentage { get; }

            public AttributeDefinition(string statId, string displayName, bool isPercentage = false)
            {
                StatId = statId;
                DisplayName = displayName;
                IsPercentage = isPercentage;
            }
        }
    }

    public readonly struct HudSnapshot
    {
        public bool HasPlayer { get; }

        public float CurrentHealth { get; }

        public float MaxHealth { get; }

        public int Gold { get; }

        public HudAttributeSnapshot Attributes { get; }

        public float HealthNormalized => HasPlayer && MaxHealth > 0f
            ? Mathf.Clamp01(CurrentHealth / MaxHealth)
            : 0f;

        public HudSnapshot(
            bool hasPlayer,
            float currentHealth,
            float maxHealth,
            int gold,
            HudAttributeSnapshot attributes)
        {
            HasPlayer = hasPlayer;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
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
                return new HudSnapshot(false, 0f, 0f, gold, new HudAttributeSnapshot(null));
            }

            return new HudSnapshot(
                true,
                player.CurrentHealth,
                player.MaxHealth,
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
