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

        public bool IsHit { get; }

        public bool IsCritical { get; }

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
                isHit,
                isCritical)
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
            IsHit = isHit;
            IsCritical = isCritical;
        }
    }
}
