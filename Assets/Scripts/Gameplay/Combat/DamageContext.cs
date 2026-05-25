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

        public TagSet ContextTags { get; }

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
        {
            AttackerId = attackerId;
            DefenderId = defenderId;
            SkillId = skillId;
            SourceItemId = sourceItemId;
            RandomSeed = randomSeed;
            _baseDamages = new List<DamagePacket>(baseDamages);
            ContextTags = contextTags ?? TagSet.Empty;
            AttackerStats = attackerStats != null ? attackerStats.Clone() : new StatBlock();
            DefenderStats = defenderStats != null ? defenderStats.Clone() : new StatBlock();
            _attackerModifiers = new List<ModifierInstance>(attackerModifiers);
            _defenderModifiers = new List<ModifierInstance>(defenderModifiers);
            IsHit = isHit;
            IsCritical = isCritical;
        }
    }
}

