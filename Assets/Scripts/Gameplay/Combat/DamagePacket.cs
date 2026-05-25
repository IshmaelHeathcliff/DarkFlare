namespace DarkFlare
{
    public readonly struct DamagePacket
    {
        public DamageType DamageType { get; }

        public float Amount { get; }

        public TagSet Tags { get; }

        public DamagePacket(DamageType damageType, float amount, TagSet tags)
        {
            DamageType = damageType;
            Amount = amount;
            Tags = tags ?? TagSet.Empty;
        }

        public DamagePacket WithAmount(float amount)
        {
            return new DamagePacket(DamageType, amount, Tags);
        }
    }
}

