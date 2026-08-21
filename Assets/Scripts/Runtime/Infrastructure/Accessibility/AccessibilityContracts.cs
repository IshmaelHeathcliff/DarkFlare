using System;

namespace DarkFlare
{
    public readonly struct MotionProfile : IEquatable<MotionProfile>
    {
        const float ReducedDurationScale = 0.2f;

        public bool ReduceMotion { get; }

        public float DurationScale { get; }

        public bool AllowContinuousMotion { get; }

        public MotionProfile(
            bool reduceMotion,
            float durationScale,
            bool allowContinuousMotion)
        {
            if (float.IsNaN(durationScale)
                || float.IsInfinity(durationScale)
                || durationScale < 0f
                || durationScale > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationScale),
                    durationScale,
                    "动态效果时长倍率必须在 0 到 1 之间");
            }

            ReduceMotion = reduceMotion;
            DurationScale = durationScale;
            AllowContinuousMotion = allowContinuousMotion;
        }

        public static MotionProfile Default => new MotionProfile(false, 1f, true);

        public static MotionProfile Reduced => new MotionProfile(
            true,
            ReducedDurationScale,
            false);

        public static MotionProfile FromReduceMotion(bool reduceMotion)
        {
            return reduceMotion ? Reduced : Default;
        }

        public bool Equals(MotionProfile other)
        {
            return ReduceMotion == other.ReduceMotion
                && DurationScale.Equals(other.DurationScale)
                && AllowContinuousMotion == other.AllowContinuousMotion;
        }

        public override bool Equals(object obj)
        {
            return obj is MotionProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ReduceMotion.GetHashCode();
                hash = (hash * 397) ^ DurationScale.GetHashCode();
                hash = (hash * 397) ^ AllowContinuousMotion.GetHashCode();
                return hash;
            }
        }
    }
}
