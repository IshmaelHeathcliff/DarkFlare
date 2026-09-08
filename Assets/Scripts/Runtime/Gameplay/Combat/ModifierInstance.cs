namespace DarkFlare
{
    public sealed class ModifierInstance
    {
        public ModifierOrigin Origin { get; private set; }

        public ModifierInstance WithOrigin(ModifierOrigin origin)
        {
            return new ModifierInstance(StatId, Operation, Scope, Value, FromDamageType,
                ToDamageType, Query, UsesLegacyTagMatching) { Origin = origin };
        }

        public string StatId { get; }

        public ModifierOperation Operation { get; }

        public ModifierScope Scope { get; }

        public float Value { get; }

        public DamageType FromDamageType { get; }

        public DamageType ToDamageType { get; }

        public TagQuery Query { get; }

        public bool UsesLegacyTagMatching { get; }

        public TagSet RequiredTags => Query.RequiredAll;

        public TagSet RequiredAnyTags => Query.RequiredAny;

        public TagSet BlockedTags => Query.BlockedAny;

        public ModifierInstance(
            string statId,
            ModifierOperation operation,
            ModifierScope scope,
            float value,
            DamageType fromDamageType,
            DamageType toDamageType,
            TagSet requiredTags,
            TagSet blockedTags)
            : this(
                statId,
                operation,
                scope,
                value,
                fromDamageType,
                toDamageType,
                new TagQuery(
                    CombatTagScope.All,
                    requiredTags,
                    TagSet.Empty,
                    blockedTags),
                true)
        {
        }

        public ModifierInstance(
            string statId,
            ModifierOperation operation,
            ModifierScope scope,
            float value,
            DamageType fromDamageType,
            DamageType toDamageType,
            TagQuery query)
            : this(
                statId,
                operation,
                scope,
                value,
                fromDamageType,
                toDamageType,
                query,
                false)
        {
        }

        ModifierInstance(
            string statId,
            ModifierOperation operation,
            ModifierScope scope,
            float value,
            DamageType fromDamageType,
            DamageType toDamageType,
            TagQuery query,
            bool usesLegacyTagMatching)
        {
            StatId = statId;
            Operation = operation;
            Scope = scope;
            Value = value;
            FromDamageType = fromDamageType;
            ToDamageType = toDamageType;
            Query = query ?? TagQuery.Empty;
            UsesLegacyTagMatching = usesLegacyTagMatching;
        }

        public bool Matches(TagSet contextTags)
        {
            return Query.Matches(contextTags);
        }

        public bool Matches(CombatTagContext context)
        {
            if (UsesLegacyTagMatching)
            {
                return Query.Matches(context != null ? context.LegacyTags : TagSet.Empty);
            }

            return Query.Matches(context);
        }
    }
}
