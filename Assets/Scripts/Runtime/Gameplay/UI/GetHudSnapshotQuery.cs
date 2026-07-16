using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct HudSnapshot
    {
        public bool HasPlayer { get; }

        public float CurrentHealth { get; }

        public float MaxHealth { get; }

        public int Gold { get; }

        public string WeaponSummary { get; }

        public float HealthNormalized => HasPlayer && MaxHealth > 0f
            ? Mathf.Clamp01(CurrentHealth / MaxHealth)
            : 0f;

        public HudSnapshot(
            bool hasPlayer,
            float currentHealth,
            float maxHealth,
            int gold,
            string weaponSummary)
        {
            HasPlayer = hasPlayer;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            Gold = gold;
            WeaponSummary = weaponSummary;
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
                return new HudSnapshot(false, 0f, 0f, gold, "未装备");
            }

            ItemInstance weapon = this.GetModel<EquipmentModel>().GetWeapon(player);
            return new HudSnapshot(
                true,
                player.CurrentHealth,
                player.MaxHealth,
                gold,
                DescribeWeapon(weapon));
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

        static string DescribeWeapon(ItemInstance weapon)
        {
            if (weapon == null)
            {
                return "未装备";
            }

            string displayName = weapon.BaseDefinition != null && !string.IsNullOrWhiteSpace(weapon.BaseDefinition.DisplayName)
                ? weapon.BaseDefinition.DisplayName
                : weapon.InstanceId;
            int affixCount = weapon.Prefixes.Count + weapon.Suffixes.Count;
            return $"{displayName} · {weapon.Rarity} · {affixCount} 条词缀";
        }
    }
}
