using System.Collections.Generic;
using System.Linq;

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
            if (skill.OnHitStatus != null)
            {
                if (skill.OnHitStatus.ValidateConfiguration().Count != 0) { return false; }
                if (skill.OnHitStatus.CreateRules().Lifetime != StatusLifetime.Timed) { return false; }
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
            int randomSeed,
            IEnumerable<ModifierInstance> localModifiers = null,
            string flameInstanceId = null)
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
            if (localModifiers != null) { modifiers.AddRange(localModifiers); }

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

            // 每次攻击从基础值重建，范围内的暴击等属性不回写角色全局属性。
            IEnumerable<ModifierInstance> attackStatModifiers = modifiers.Where(m => !CombatStatResolver.IsDamageStat(m.StatId)
                && (m.Scope == ModifierScope.GlobalActor || m.Scope == ModifierScope.Skill || m.Scope == ModifierScope.LocalItem)
                && m.Matches(tagContext)).Select(m => new ModifierInstance(m.StatId, m.Operation, ModifierScope.GlobalActor,
                    m.Value, m.FromDamageType, m.ToDamageType, TagQuery.Empty).WithOrigin(m.Origin));
            StatBlock attackStats = CombatStatResolver.Build(attacker.CaptureBaseStats(), attackStatModifiers, contextTags: attacker.Tags);

            return new AttackSnapshot(
                attacker.CombatIdentity,
                attacker.Team,
                skill.Id,
                sourceWeapon != null ? sourceWeapon.InstanceId : string.Empty,
                randomSeed,
                baseDamages,
                tagContext,
                attackStats,
                modifiers,
                skill.OnHitStatus != null ? StatusApplication.Capture(skill.OnHitStatus,
                    new StatusSource(StatusSourceKind.Skill, string.IsNullOrEmpty(flameInstanceId) ? skill.Id : flameInstanceId + ":" + skill.Id), attackStats, modifiers,
                    new DamageSourceSnapshot(attacker.CombatIdentity, attacker.Team, skill.Id,
                        sourceWeapon != null ? sourceWeapon.InstanceId : string.Empty, tagContext),
                    unchecked(randomSeed ^ 0x37A24913)) : null, flameInstanceId);
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
                attacker != null ? attacker.CombatIdentity : "environment",
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
