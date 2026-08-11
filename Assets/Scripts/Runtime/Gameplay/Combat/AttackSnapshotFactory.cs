using System.Collections.Generic;

namespace DarkFlare
{
    public static class AttackSnapshotFactory
    {
        public static bool CanCreateProjectile(
            CombatActor attacker,
            ProjectileSkillDefinition skill,
            EquipmentModel equipment)
        {
            if (attacker == null || skill == null)
            {
                return false;
            }

            if (skill.DamageSource == ProjectileDamageSource.Skill)
            {
                return skill.BaseDamages.Count > 0;
            }

            if (equipment == null)
            {
                return false;
            }

            ItemInstance weapon = equipment.GetWeapon(attacker);
            return IsValidWeapon(weapon);
        }

        public static AttackSnapshot CreateProjectile(
            CombatActor attacker,
            ProjectileSkillDefinition skill,
            EquipmentModel equipment,
            int randomSeed)
        {
            if (!CanCreateProjectile(attacker, skill, equipment))
            {
                return null;
            }

            AttackRandomRolls rolls = AttackRandomRolls.FromRootSeed(randomSeed);
            ItemInstance sourceWeapon = null;
            List<DamagePacket> baseDamages;
            TagSet sourceItemTags = TagSet.Empty;
            List<ModifierInstance> modifiers = new List<ModifierInstance>(attacker.Modifiers);

            if (skill.DamageSource == ProjectileDamageSource.EquippedWeapon)
            {
                sourceWeapon = equipment.GetWeapon(attacker);
                baseDamages = sourceWeapon.CreateBaseDamagePackets(rolls.BaseDamageSeed);
                sourceItemTags = sourceWeapon.Tags;
                modifiers.AddRange(EquipmentEffectResolver.CollectLocalWeaponModifiers(sourceWeapon));
            }
            else
            {
                baseDamages = skill.CreateDamagePackets(rolls.BaseDamageSeed);
            }

            if (baseDamages.Count == 0)
            {
                return null;
            }

            CombatTagContext tagContext = new CombatTagContext(
                sourceActorTags: attacker.Tags,
                skillTags: skill.RuntimeTags,
                sourceItemTags: sourceItemTags,
                legacyTags: skill.RuntimeTags.Union(sourceItemTags));

            return new AttackSnapshot(
                attacker.ActorId,
                attacker.Team,
                skill.Id,
                sourceWeapon != null ? sourceWeapon.InstanceId : string.Empty,
                randomSeed,
                baseDamages,
                tagContext,
                attacker.Stats,
                modifiers);
        }

        static bool IsValidWeapon(ItemInstance weapon)
        {
            return weapon != null
                && weapon.BaseDefinition != null
                && weapon.BaseDefinition.ItemType == ItemType.Weapon
                && weapon.BaseDefinition.CanEquipTo(EquipmentSlot.Weapon)
                && weapon.BaseDefinition.BaseDamages.Count > 0;
        }

        public static AttackSnapshot CreateImmediate(
            CombatActor attacker,
            string skillId,
            string sourceItemId,
            IEnumerable<DamagePacket> baseDamages,
            TagSet contextTags,
            int randomSeed)
        {
            CombatTagContext tagContext = new CombatTagContext(
                sourceActorTags: attacker != null ? attacker.Tags : TagSet.Empty,
                attackTags: contextTags,
                legacyTags: contextTags);
            return CreateImmediate(
                attacker,
                skillId,
                sourceItemId,
                baseDamages,
                tagContext,
                randomSeed);
        }

        public static AttackSnapshot CreateImmediate(
            CombatActor attacker,
            string skillId,
            string sourceItemId,
            IEnumerable<DamagePacket> baseDamages,
            int randomSeed)
        {
            TagSet actorTags = attacker != null ? attacker.Tags : TagSet.Empty;
            CombatTagContext tagContext = new CombatTagContext(
                sourceActorTags: actorTags,
                legacyTags: actorTags);
            return CreateImmediate(
                attacker,
                skillId,
                sourceItemId,
                baseDamages,
                tagContext,
                randomSeed);
        }

        static AttackSnapshot CreateImmediate(
            CombatActor attacker,
            string skillId,
            string sourceItemId,
            IEnumerable<DamagePacket> baseDamages,
            CombatTagContext tagContext,
            int randomSeed)
        {
            return new AttackSnapshot(
                attacker != null ? attacker.ActorId : "environment",
                attacker != null ? attacker.Team : default,
                skillId,
                sourceItemId,
                randomSeed,
                baseDamages,
                tagContext,
                attacker != null ? attacker.Stats : null,
                attacker != null ? attacker.Modifiers : null);
        }
    }
}
