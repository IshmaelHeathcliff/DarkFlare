using System.Collections.Generic;

namespace DarkFlare
{
    public static class CombatTagIds
    {
        public const string Armor = "armor";
        public const string Axe = "axe";
        public const string Chaos = "chaos";
        public const string Cold = "cold";
        public const string Damage = "damage";
        public const string Fire = "fire";
        public const string Lightning = "lightning";
        public const string Melee = "melee";
        public const string Monster = "monster";
        public const string Physical = "physical";
        public const string Projectile = "projectile";
        public const string Ring = "ring";
        public const string Sword = "sword";
        public const string Weapon = "weapon";
    }

    public static class CombatTagResolver
    {
        public static TagSet ResolveItemSpawnTags(
            ItemType itemType,
            EquipmentSlotMask equipmentSlots,
            TagSet configuredTags)
        {
            HashSet<string> ids = Copy(configuredTags);

            if (itemType == ItemType.Weapon)
            {
                ids.Add(CombatTagIds.Weapon);
            }
            else if (itemType == ItemType.Armor)
            {
                ids.Add(CombatTagIds.Armor);
            }
            else if (itemType == ItemType.Accessory && (equipmentSlots & EquipmentSlotMask.Rings) != 0)
            {
                ids.Add(CombatTagIds.Ring);
            }

            return Create(ids);
        }

        public static TagSet ResolveActorTags(ActorTeam team, TagSet configuredTags)
        {
            HashSet<string> ids = Copy(configuredTags);

            if (team == ActorTeam.Monster)
            {
                ids.Add(CombatTagIds.Monster);
            }

            return Create(ids);
        }

        public static TagSet ResolveProjectileSkillTags(TagSet configuredTags)
        {
            HashSet<string> ids = Copy(configuredTags);
            ids.Add(CombatTagIds.Projectile);
            return Create(ids);
        }

        public static TagSet ResolveDamageTags(DamagePacket packet)
        {
            HashSet<string> ids = Copy(packet.CustomTags);
            ids.Add(CombatTagIds.Damage);
            AddDamageTypeTags(ids, packet.ScalingTypes | DamagePacket.ToMask(packet.CurrentType));
            return Create(ids);
        }

        static void AddDamageTypeTags(HashSet<string> ids, DamageTypeMask types)
        {
            if ((types & DamageTypeMask.Physical) != 0)
            {
                ids.Add(CombatTagIds.Physical);
            }

            if ((types & DamageTypeMask.Fire) != 0)
            {
                ids.Add(CombatTagIds.Fire);
            }

            if ((types & DamageTypeMask.Cold) != 0)
            {
                ids.Add(CombatTagIds.Cold);
            }

            if ((types & DamageTypeMask.Lightning) != 0)
            {
                ids.Add(CombatTagIds.Lightning);
            }

            if ((types & DamageTypeMask.Chaos) != 0)
            {
                ids.Add(CombatTagIds.Chaos);
            }
        }

        static HashSet<string> Copy(TagSet tags)
        {
            return tags != null
                ? new HashSet<string>(tags.Ids)
                : new HashSet<string>();
        }

        static TagSet Create(HashSet<string> ids)
        {
            return ids.Count > 0 ? new TagSet(ids) : TagSet.Empty;
        }
    }
}
