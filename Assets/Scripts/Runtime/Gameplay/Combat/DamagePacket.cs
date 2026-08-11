using System;

namespace DarkFlare
{
    [Flags]
    public enum DamageTypeMask
    {
        None = 0,
        Physical = 1 << 0,
        Fire = 1 << 1,
        Cold = 1 << 2,
        Lightning = 1 << 3,
        Chaos = 1 << 4
    }

    public readonly struct DamagePacket
    {
        public DamageType CurrentType { get; }

        public DamageType DamageType => CurrentType;

        public float Amount { get; }

        public DamageTypeMask ScalingTypes { get; }

        public TagSet CustomTags { get; }

        public TagSet Tags => CustomTags;

        public DamagePacket(DamageType damageType, float amount, TagSet tags)
            : this(damageType, amount, ToMask(damageType), tags)
        {
        }

        public DamagePacket(
            DamageType currentType,
            float amount,
            DamageTypeMask scalingTypes,
            TagSet customTags)
        {
            CurrentType = currentType;
            Amount = amount;
            ScalingTypes = scalingTypes | ToMask(currentType);
            CustomTags = customTags != null ? new TagSet(customTags.Ids) : TagSet.Empty;
        }

        public DamagePacket WithAmount(float amount)
        {
            return new DamagePacket(CurrentType, amount, ScalingTypes, CustomTags);
        }

        public DamagePacket WithCurrentType(DamageType currentType, float amount)
        {
            DamageTypeMask lineage = ScalingTypes | ToMask(CurrentType) | ToMask(currentType);
            return new DamagePacket(currentType, amount, lineage, CustomTags);
        }

        public static DamageTypeMask ToMask(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Physical => DamageTypeMask.Physical,
                DamageType.Fire => DamageTypeMask.Fire,
                DamageType.Cold => DamageTypeMask.Cold,
                DamageType.Lightning => DamageTypeMask.Lightning,
                DamageType.Chaos => DamageTypeMask.Chaos,
                _ => DamageTypeMask.None,
            };
        }
    }
}
