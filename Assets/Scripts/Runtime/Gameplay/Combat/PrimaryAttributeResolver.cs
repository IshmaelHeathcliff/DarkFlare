namespace DarkFlare
{
    public static class PrimaryAttributeResolver
    {
        public const float MaxHealthPerStrength = 2f;
        public const float AccuracyPerDexterity = 1f;
        public const float EvasionPerDexterity = 1f;
        public const float MaxManaPerIntelligence = 2f;

        public static StatBlock Apply(StatBlock stats, System.Collections.Generic.List<StatCalculationStep> steps = null)
        {
            StatBlock result = stats != null ? stats.Clone() : new StatBlock();
            float strength = result.GetValue(StatIds.Strength);
            float dexterity = result.GetValue(StatIds.Dexterity);
            float intelligence = result.GetValue(StatIds.Intelligence);

            Add(StatIds.MaxHealth, strength * MaxHealthPerStrength, StatIds.Strength);
            Add(StatIds.Accuracy, dexterity * AccuracyPerDexterity, StatIds.Dexterity);
            Add(StatIds.Evasion, dexterity * EvasionPerDexterity, StatIds.Dexterity);
            Add(StatIds.Mana, intelligence * MaxManaPerIntelligence, StatIds.Intelligence);
            return result;

            void Add(string id, float amount, string source)
            {
                result.AddValue(id, amount);
                steps?.Add(new StatCalculationStep(id, ModifierOperation.Flat, amount, result.GetValue(id), derivedFrom: source));
            }
        }
    }
}
