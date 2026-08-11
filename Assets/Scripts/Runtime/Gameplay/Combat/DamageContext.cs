using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class DamageContext
    {
        readonly List<DamagePacket> _baseDamages;
        readonly List<ModifierInstance> _attackerModifiers;
        readonly List<ModifierInstance> _defenderModifiers;

        public string AttackerId { get; }

        public string DefenderId { get; }

        public string SkillId { get; }

        public string SourceItemId { get; }

        public int RandomSeed { get; }

        public CombatTagContext TagContext { get; }

        public TagSet ContextTags => TagContext.LegacyTags;

        public StatBlock AttackerStats { get; }

        public StatBlock DefenderStats { get; }

        public HitOutcome Outcome { get; }

        public bool IsHit => Outcome == HitOutcome.Hit || Outcome == HitOutcome.NoDamage;

        public bool IsCritical { get; }

        public float HitChance { get; }

        public float HitRoll { get; }

        public float CriticalChance { get; }

        public float CriticalRoll { get; }

        public IReadOnlyList<DamagePacket> BaseDamages => _baseDamages;

        public IReadOnlyList<ModifierInstance> AttackerModifiers => _attackerModifiers;

        public IReadOnlyList<ModifierInstance> DefenderModifiers => _defenderModifiers;

        public DamageContext(
            string attackerId,
            string defenderId,
            string skillId,
            string sourceItemId,
            int randomSeed,
            IEnumerable<DamagePacket> baseDamages,
            TagSet contextTags,
            StatBlock attackerStats,
            StatBlock defenderStats,
            IEnumerable<ModifierInstance> attackerModifiers,
            IEnumerable<ModifierInstance> defenderModifiers,
            bool isHit = true,
            bool isCritical = false)
            : this(
                attackerId,
                defenderId,
                skillId,
                sourceItemId,
                randomSeed,
                baseDamages,
                new CombatTagContext(attackTags: contextTags, legacyTags: contextTags),
                attackerStats,
                defenderStats,
                attackerModifiers,
                defenderModifiers,
                isHit ? HitOutcome.Hit : HitOutcome.Missed,
                isCritical,
                isHit ? 1f : 0f,
                0f,
                isCritical ? 1f : 0f,
                0f)
        {
        }

        public DamageContext(
            string attackerId,
            string defenderId,
            string skillId,
            string sourceItemId,
            int randomSeed,
            IEnumerable<DamagePacket> baseDamages,
            CombatTagContext tagContext,
            StatBlock attackerStats,
            StatBlock defenderStats,
            IEnumerable<ModifierInstance> attackerModifiers,
            IEnumerable<ModifierInstance> defenderModifiers,
            bool isHit = true,
            bool isCritical = false)
            : this(
                attackerId,
                defenderId,
                skillId,
                sourceItemId,
                randomSeed,
                baseDamages,
                tagContext,
                attackerStats,
                defenderStats,
                attackerModifiers,
                defenderModifiers,
                isHit ? HitOutcome.Hit : HitOutcome.Missed,
                isCritical,
                isHit ? 1f : 0f,
                0f,
                isCritical ? 1f : 0f,
                0f)
        {
        }

        public DamageContext(
            string attackerId,
            string defenderId,
            string skillId,
            string sourceItemId,
            int randomSeed,
            IEnumerable<DamagePacket> baseDamages,
            CombatTagContext tagContext,
            StatBlock attackerStats,
            StatBlock defenderStats,
            IEnumerable<ModifierInstance> attackerModifiers,
            IEnumerable<ModifierInstance> defenderModifiers,
            HitResolution resolution)
            : this(
                attackerId,
                defenderId,
                skillId,
                sourceItemId,
                randomSeed,
                baseDamages,
                tagContext,
                attackerStats,
                defenderStats,
                attackerModifiers,
                defenderModifiers,
                resolution.Outcome,
                resolution.IsCritical,
                resolution.HitChance,
                resolution.HitRoll,
                resolution.CriticalChance,
                resolution.CriticalRoll)
        {
        }

        DamageContext(
            string attackerId,
            string defenderId,
            string skillId,
            string sourceItemId,
            int randomSeed,
            IEnumerable<DamagePacket> baseDamages,
            CombatTagContext tagContext,
            StatBlock attackerStats,
            StatBlock defenderStats,
            IEnumerable<ModifierInstance> attackerModifiers,
            IEnumerable<ModifierInstance> defenderModifiers,
            HitOutcome outcome,
            bool isCritical,
            float hitChance,
            float hitRoll,
            float criticalChance,
            float criticalRoll)
        {
            AttackerId = attackerId;
            DefenderId = defenderId;
            SkillId = skillId;
            SourceItemId = sourceItemId;
            RandomSeed = randomSeed;
            _baseDamages = baseDamages != null
                ? new List<DamagePacket>(baseDamages)
                : new List<DamagePacket>();
            TagContext = tagContext != null
                ? new CombatTagContext(
                    tagContext.SourceActorTags,
                    tagContext.TargetActorTags,
                    tagContext.SkillTags,
                    tagContext.SourceItemTags,
                    tagContext.AttackTags,
                    tagContext.DamageTags,
                    tagContext.LegacyTags)
                : CombatTagContext.Empty;
            AttackerStats = attackerStats != null ? attackerStats.Clone() : new StatBlock();
            DefenderStats = defenderStats != null ? defenderStats.Clone() : new StatBlock();
            _attackerModifiers = attackerModifiers != null
                ? new List<ModifierInstance>(attackerModifiers)
                : new List<ModifierInstance>();
            _defenderModifiers = defenderModifiers != null
                ? new List<ModifierInstance>(defenderModifiers)
                : new List<ModifierInstance>();
            Outcome = outcome;
            IsCritical = isCritical && IsHit;
            HitChance = hitChance;
            HitRoll = hitRoll;
            CriticalChance = criticalChance;
            CriticalRoll = criticalRoll;
        }
    }
}
