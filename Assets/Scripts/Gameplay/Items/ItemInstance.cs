using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class ItemInstance
    {
        readonly List<ModifierInstance> _implicitModifiers;
        readonly List<AffixInstance> _prefixes;
        readonly List<AffixInstance> _suffixes;

        public string InstanceId { get; }

        public ItemBaseDefinition BaseDefinition { get; }

        public ItemRarity Rarity { get; }

        public int ItemLevel { get; }

        public int Seed { get; }

        public int Quality { get; set; }

        public float Durability { get; set; } = 1f;

        public IReadOnlyList<ModifierInstance> ImplicitModifiers => _implicitModifiers;

        public IReadOnlyList<AffixInstance> Prefixes => _prefixes;

        public IReadOnlyList<AffixInstance> Suffixes => _suffixes;

        public TagSet Tags => BaseDefinition != null ? BaseDefinition.RuntimeTags : TagSet.Empty;

        public ItemInstance(
            string instanceId,
            ItemBaseDefinition baseDefinition,
            ItemRarity rarity,
            int itemLevel,
            int seed,
            IEnumerable<ModifierInstance> implicitModifiers)
        {
            InstanceId = instanceId;
            BaseDefinition = baseDefinition;
            Rarity = rarity;
            ItemLevel = itemLevel;
            Seed = seed;
            _implicitModifiers = new List<ModifierInstance>(implicitModifiers);
            _prefixes = new List<AffixInstance>();
            _suffixes = new List<AffixInstance>();
        }

        public bool TryAddAffix(AffixInstance affix)
        {
            if (affix == null || affix.Definition == null || BaseDefinition == null)
            {
                return false;
            }

            if (!affix.Definition.CanApplyTo(Tags, ItemLevel))
            {
                return false;
            }

            if (HasGroup(affix.Definition.GroupId))
            {
                return false;
            }

            if (affix.Definition.AffixType == AffixType.Prefix)
            {
                if (_prefixes.Count >= BaseDefinition.MaxPrefixCount)
                {
                    return false;
                }

                _prefixes.Add(affix);
                return true;
            }

            if (affix.Definition.AffixType == AffixType.Suffix)
            {
                if (_suffixes.Count >= BaseDefinition.MaxSuffixCount)
                {
                    return false;
                }

                _suffixes.Add(affix);
                return true;
            }

            return false;
        }

        public List<ModifierInstance> CollectModifiers()
        {
            List<ModifierInstance> modifiers = new List<ModifierInstance>(_implicitModifiers);

            AddAffixModifiers(_prefixes, modifiers);
            AddAffixModifiers(_suffixes, modifiers);

            return modifiers;
        }

        public List<DamagePacket> CreateBaseDamagePackets()
        {
            List<DamagePacket> packets = new List<DamagePacket>();

            if (BaseDefinition == null)
            {
                return packets;
            }

            System.Random random = new System.Random(Seed);

            for (int i = 0; i < BaseDefinition.BaseDamages.Count; i++)
            {
                packets.Add(BaseDefinition.BaseDamages[i].CreatePacket(random));
            }

            return packets;
        }

        bool HasGroup(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return false;
            }

            for (int i = 0; i < _prefixes.Count; i++)
            {
                if (_prefixes[i].Definition != null && _prefixes[i].Definition.GroupId == groupId)
                {
                    return true;
                }
            }

            for (int i = 0; i < _suffixes.Count; i++)
            {
                if (_suffixes[i].Definition != null && _suffixes[i].Definition.GroupId == groupId)
                {
                    return true;
                }
            }

            return false;
        }

        static void AddAffixModifiers(IEnumerable<AffixInstance> affixes, List<ModifierInstance> modifiers)
        {
            foreach (AffixInstance affix in affixes)
            {
                modifiers.AddRange(affix.Modifiers);
            }
        }
    }
}
