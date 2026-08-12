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

        public ItemRarity Rarity { get; private set; }

        public int ItemLevel { get; }

        public int Seed { get; }

        public int Quality { get; set; }

        public float Durability { get; set; } = 1f;

        public int Revision { get; private set; }

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
                if (_prefixes.Count >= ItemRarityRules.GetLimits(Rarity).MaxPrefixCount)
                {
                    return false;
                }

                _prefixes.Add(affix);
                Revision++;
                return true;
            }

            if (affix.Definition.AffixType == AffixType.Suffix)
            {
                if (_suffixes.Count >= ItemRarityRules.GetLimits(Rarity).MaxSuffixCount)
                {
                    return false;
                }

                _suffixes.Add(affix);
                Revision++;
                return true;
            }

            return false;
        }

        public bool RemoveAffix(AffixInstance affix)
        {
            if (affix == null)
            {
                return false;
            }

            bool removed = _prefixes.Remove(affix) || _suffixes.Remove(affix);

            if (removed)
            {
                Revision++;
            }

            return removed;
        }

        public void ClearAffixes()
        {
            if (_prefixes.Count == 0 && _suffixes.Count == 0)
            {
                return;
            }

            _prefixes.Clear();
            _suffixes.Clear();
            Revision++;
        }

        internal bool TryApplyAffixState(
            int expectedRevision,
            ItemRarity rarity,
            IReadOnlyList<AffixInstance> prefixes,
            IReadOnlyList<AffixInstance> suffixes)
        {
            if (Revision != expectedRevision
                || BaseDefinition == null
                || prefixes == null
                || suffixes == null
                || !ItemRarityRules.IsWithinCapacity(rarity, prefixes.Count, suffixes.Count))
            {
                return false;
            }

            HashSet<string> groups = new HashSet<string>(System.StringComparer.Ordinal);
            HashSet<AffixDefinition> definitions = new HashSet<AffixDefinition>();

            if (!ValidateAffixes(prefixes, AffixType.Prefix, groups, definitions)
                || !ValidateAffixes(suffixes, AffixType.Suffix, groups, definitions))
            {
                return false;
            }

            Rarity = rarity;
            _prefixes.Clear();
            _prefixes.AddRange(prefixes);
            _suffixes.Clear();
            _suffixes.AddRange(suffixes);
            Revision++;
            return true;
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
            return CreateBaseDamagePackets(Seed);
        }

        public List<DamagePacket> CreateBaseDamagePackets(int seed)
        {
            List<DamagePacket> packets = new List<DamagePacket>();

            if (BaseDefinition == null)
            {
                return packets;
            }

            System.Random random = new System.Random(seed);

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

        bool ValidateAffixes(
            IReadOnlyList<AffixInstance> affixes,
            AffixType expectedType,
            HashSet<string> groups,
            HashSet<AffixDefinition> definitions)
        {
            for (int i = 0; i < affixes.Count; i++)
            {
                AffixInstance affix = affixes[i];

                if (affix == null
                    || affix.Definition == null
                    || affix.Definition.AffixType != expectedType
                    || !affix.Definition.CanApplyTo(Tags, ItemLevel)
                    || !definitions.Add(affix.Definition))
                {
                    return false;
                }

                string groupId = affix.Definition.GroupId;

                if (!string.IsNullOrWhiteSpace(groupId) && !groups.Add(groupId))
                {
                    return false;
                }
            }

            return true;
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
