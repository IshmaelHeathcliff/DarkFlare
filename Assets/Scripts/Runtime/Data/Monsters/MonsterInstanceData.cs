namespace DarkFlare
{
    public sealed class MonsterInstanceData
    {
        public int Seed { get; }

        public float MaxHealth { get; }

        public StatBlock Stats { get; }

        public MonsterInstanceData(int seed, float maxHealth, StatBlock stats)
        {
            Seed = seed;
            MaxHealth = maxHealth;
            Stats = stats;
        }
    }
}
