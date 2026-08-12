using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class MonsterInstanceData
    {
        readonly IReadOnlyList<MonsterAffixInstance> _affixes;
        readonly IReadOnlyList<ModifierInstance> _modifiers;

        public int Seed => RandomSeeds.RootSeed;

        public MonsterInstanceRandomSeeds RandomSeeds { get; }

        public float BaseMaxHealth { get; }

        public float MaxHealth => EffectiveStats.GetValue(StatIds.MaxHealth);

        public StatBlock BaseStats { get; }

        public StatBlock EffectiveStats { get; }

        public StatBlock Stats => EffectiveStats;

        public IReadOnlyList<MonsterAffixInstance> Affixes => _affixes;

        public IReadOnlyList<ModifierInstance> Modifiers => _modifiers;

        public MonsterInstanceData(int seed, float maxHealth, StatBlock stats)
            : this(
                new MonsterInstanceRandomSeeds(seed),
                maxHealth,
                stats,
                stats,
                new List<MonsterAffixInstance>(),
                new List<ModifierInstance>())
        {
        }

        public MonsterInstanceData(
            MonsterInstanceRandomSeeds randomSeeds,
            float baseMaxHealth,
            StatBlock baseStats,
            StatBlock effectiveStats,
            IEnumerable<MonsterAffixInstance> affixes,
            IEnumerable<ModifierInstance> modifiers)
        {
            RandomSeeds = randomSeeds;
            BaseMaxHealth = baseMaxHealth;
            BaseStats = baseStats != null ? baseStats.Clone() : new StatBlock();
            EffectiveStats = effectiveStats != null ? effectiveStats.Clone() : BaseStats.Clone();
            List<MonsterAffixInstance> copiedAffixes = affixes != null
                ? new List<MonsterAffixInstance>(affixes)
                : new List<MonsterAffixInstance>();
            List<ModifierInstance> copiedModifiers = modifiers != null
                ? new List<ModifierInstance>(modifiers)
                : new List<ModifierInstance>();
            _affixes = copiedAffixes.AsReadOnly();
            _modifiers = copiedModifiers.AsReadOnly();
        }
    }
}
