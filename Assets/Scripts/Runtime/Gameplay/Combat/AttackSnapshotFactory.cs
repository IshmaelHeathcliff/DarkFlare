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
            TagSet contextTags = skill.RuntimeTags;
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
                    contextTags = contextTags.Union(weapon.Tags);
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

            return new AttackSnapshot(
                attacker.ActorId,
                attacker.Team,
                skill.Id,
                sourceWeapon != null ? sourceWeapon.InstanceId : string.Empty,
                randomSeed,
                baseDamages,
                contextTags,
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
            return new AttackSnapshot(
                attacker != null ? attacker.ActorId : "environment",
                attacker != null ? attacker.Team : default,
                skillId,
                sourceItemId,
                randomSeed,
                baseDamages,
                contextTags,
                attacker != null ? attacker.Stats : null,
                attacker != null ? attacker.Modifiers : null);
        }
    }
}
