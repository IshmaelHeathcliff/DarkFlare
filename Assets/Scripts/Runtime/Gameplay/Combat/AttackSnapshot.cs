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

        public int RandomSeed { get; }

        public TagSet ContextTags { get; }

        public bool IsHit { get; }

        public bool IsCritical { get; }

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
            IEnumerable<ModifierInstance> attackerModifiers,
            bool isHit = true,
            bool isCritical = false)
        {
            AttackerId = string.IsNullOrWhiteSpace(attackerId) ? "environment" : attackerId;
            AttackerTeam = attackerTeam;
            SkillId = skillId ?? string.Empty;
            SourceItemId = sourceItemId ?? string.Empty;
            RandomSeed = randomSeed;
            _baseDamages = CloneDamagePackets(baseDamages).AsReadOnly();
            ContextTags = contextTags != null ? new TagSet(contextTags.Ids) : TagSet.Empty;
            _attackerStats = attackerStats != null ? attackerStats.Clone() : new StatBlock();
            List<ModifierInstance> modifiers = attackerModifiers != null
                ? new List<ModifierInstance>(attackerModifiers)
                : new List<ModifierInstance>();
            _attackerModifiers = modifiers.AsReadOnly();
            IsHit = isHit;
            IsCritical = isCritical;
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
                TagSet tags = packet.Tags != null ? new TagSet(packet.Tags.Ids) : TagSet.Empty;
                result.Add(new DamagePacket(packet.DamageType, packet.Amount, tags));
            }

            return result;
        }
    }
}
