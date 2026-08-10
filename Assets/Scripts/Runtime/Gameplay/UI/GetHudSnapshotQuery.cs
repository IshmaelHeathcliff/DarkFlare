using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct HudAttributeSnapshot
    {
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
        }

        static float GetNonNegative(StatBlock stats, string statId)
        {
            return stats != null ? Mathf.Max(0f, stats.GetValue(statId)) : 0f;
        }

        static float GetResistance(StatBlock stats, string statId)
        {
            return stats != null ? Mathf.Clamp(stats.GetValue(statId), -100f, 75f) : 0f;
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
                return new HudSnapshot(false, 0f, 0f, gold, default);
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
