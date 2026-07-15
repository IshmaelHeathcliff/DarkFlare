namespace DarkFlare
{
    public sealed class ModifierInstance
    {
        public string StatId { get; }

        public ModifierOperation Operation { get; }

        public ModifierScope Scope { get; }

        public float Value { get; }

        public DamageType FromDamageType { get; }

        public DamageType ToDamageType { get; }

        public TagSet RequiredTags { get; }

        public TagSet BlockedTags { get; }

        public ModifierInstance(
            string statId,
            ModifierOperation operation,
            ModifierScope scope,
            float value,
            DamageType fromDamageType,
            DamageType toDamageType,
            TagSet requiredTags,
            TagSet blockedTags)
        {
            StatId = statId;
            Operation = operation;
            Scope = scope;
            Value = value;
            FromDamageType = fromDamageType;
            ToDamageType = toDamageType;
            RequiredTags = requiredTags;
            BlockedTags = blockedTags;
        }

        public bool Matches(TagSet contextTags)
        {
            if (!RequiredTags.IsEmpty && !contextTags.ContainsAll(RequiredTags))
            {
                return false;
            }

            return BlockedTags.IsEmpty || !contextTags.ContainsAny(BlockedTags);
        }
    }
}

