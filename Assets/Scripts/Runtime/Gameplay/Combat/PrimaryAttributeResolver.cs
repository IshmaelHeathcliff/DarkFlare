namespace DarkFlare
{
    public static class PrimaryAttributeResolver
    {
        public const float MaxHealthPerStrength = 2f;
        public const float AccuracyPerDexterity = 1f;
        public const float EvasionPerDexterity = 1f;
        public const float MaxManaPerIntelligence = 2f;

        public static StatBlock Apply(StatBlock stats)
        {
            StatBlock result = stats != null ? stats.Clone() : new StatBlock();
            float strength = result.GetValue(StatIds.Strength);
            float dexterity = result.GetValue(StatIds.Dexterity);
            float intelligence = result.GetValue(StatIds.Intelligence);

            result.AddValue(StatIds.MaxHealth, strength * MaxHealthPerStrength);
            result.AddValue(StatIds.Accuracy, dexterity * AccuracyPerDexterity);
            result.AddValue(StatIds.Evasion, dexterity * EvasionPerDexterity);
            result.AddValue(StatIds.Mana, intelligence * MaxManaPerIntelligence);
            return result;
        }
    }
}
