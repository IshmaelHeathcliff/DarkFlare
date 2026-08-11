using System.Collections.Generic;

namespace DarkFlare
{
    public static class AttackSnapshotFactory
    {
        public static AttackSnapshot CreateProjectile(
            CombatActor attacker,
            ProjectileSkillDefinition skill,
            EquipmentModel equipment,
            int randomSeed)
        {
            if (attacker == null || skill == null)
            {
                return null;
            }

            ItemInstance sourceWeapon = null;
            List<DamagePacket> baseDamages;
            TagSet sourceItemTags = TagSet.Empty;
            List<ModifierInstance> modifiers = new List<ModifierInstance>(attacker.Modifiers);

            if (skill.DamageSource == ProjectileDamageSource.EquippedWeapon && equipment != null)
            {
                ItemInstance weapon = equipment.GetItem(attacker, EquipmentSlot.Weapon);

                if (weapon != null
                    && weapon.BaseDefinition != null
                    && weapon.BaseDefinition.CanEquipTo(EquipmentSlot.Weapon)
                    && weapon.BaseDefinition.BaseDamages.Count > 0)
                {
                    sourceWeapon = weapon;
                    baseDamages = weapon.CreateBaseDamagePackets(randomSeed);
                    sourceItemTags = weapon.Tags;
                    modifiers.AddRange(EquipmentEffectResolver.CollectLocalWeaponModifiers(weapon));
                }
                else
                {
                    baseDamages = skill.CreateDamagePackets(randomSeed);
                }
            }
            else
            {
                baseDamages = skill.CreateDamagePackets(randomSeed);
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
