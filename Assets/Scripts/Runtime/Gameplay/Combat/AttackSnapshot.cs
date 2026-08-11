using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class AttackSnapshot
    {
        readonly IReadOnlyList<DamagePacket> _baseDamages;
        readonly IReadOnlyList<ModifierInstance> _attackerModifiers;
        readonly StatBlock _attackerStats;

        public string AttackerId { get; }

        public ActorTeam AttackerTeam { get; }

        public string SkillId { get; }

        public string SourceItemId { get; }

        public int RandomSeed => RandomRolls.RootSeed;

        public AttackRandomRolls RandomRolls { get; }

        public int BaseDamageSeed => RandomRolls.BaseDamageSeed;

        public float HitRoll => RandomRolls.HitRoll;

        public float CriticalRoll => RandomRolls.CriticalRoll;

        public CombatTagContext TagContext { get; }

        public TagSet ContextTags => TagContext.LegacyTags;

        public IReadOnlyList<DamagePacket> BaseDamages => _baseDamages;

        public IReadOnlyList<ModifierInstance> AttackerModifiers => _attackerModifiers;

        public StatBlock AttackerStats => _attackerStats.Clone();

        public AttackSnapshot(
            string attackerId,
            ActorTeam attackerTeam,
            string skillId,
            string sourceItemId,
            int randomSeed,
            IEnumerable<DamagePacket> baseDamages,
            TagSet contextTags,
            StatBlock attackerStats,
            IEnumerable<ModifierInstance> attackerModifiers)
            : this(
                attackerId,
                attackerTeam,
                skillId,
                sourceItemId,
                randomSeed,
                baseDamages,
                new CombatTagContext(attackTags: contextTags, legacyTags: contextTags),
                attackerStats,
                attackerModifiers)
        {
        }

        public AttackSnapshot(
            string attackerId,
            ActorTeam attackerTeam,
            string skillId,
            string sourceItemId,
            int randomSeed,
            IEnumerable<DamagePacket> baseDamages,
            CombatTagContext tagContext,
            StatBlock attackerStats,
            IEnumerable<ModifierInstance> attackerModifiers)
        {
            AttackerId = string.IsNullOrWhiteSpace(attackerId) ? "environment" : attackerId;
            AttackerTeam = attackerTeam;
            SkillId = skillId ?? string.Empty;
            SourceItemId = sourceItemId ?? string.Empty;
            RandomRolls = AttackRandomRolls.FromRootSeed(randomSeed);
            _baseDamages = CloneDamagePackets(baseDamages).AsReadOnly();
            TagContext = CloneTagContext(tagContext);
            _attackerStats = attackerStats != null ? attackerStats.Clone() : new StatBlock();
            List<ModifierInstance> modifiers = attackerModifiers != null
                ? new List<ModifierInstance>(attackerModifiers)
                : new List<ModifierInstance>();
            _attackerModifiers = modifiers.AsReadOnly();
        }

        static List<DamagePacket> CloneDamagePackets(IEnumerable<DamagePacket> packets)
        {
            List<DamagePacket> result = new List<DamagePacket>();

            if (packets == null)
            {
                return result;
            }

            foreach (DamagePacket packet in packets)
            {
                result.Add(new DamagePacket(
                    packet.CurrentType,
                    packet.Amount,
                    packet.ScalingTypes,
                    packet.CustomTags));
            }

            return result;
        }

        static CombatTagContext CloneTagContext(CombatTagContext context)
        {
            if (context == null)
            {
                return CombatTagContext.Empty;
            }

            return new CombatTagContext(
                context.SourceActorTags,
                context.TargetActorTags,
                context.SkillTags,
                context.SourceItemTags,
                context.AttackTags,
                context.DamageTags,
                context.LegacyTags);
        }
    }
}
