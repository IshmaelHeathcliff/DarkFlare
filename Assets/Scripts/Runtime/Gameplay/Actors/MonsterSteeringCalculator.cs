using UnityEngine;

namespace DarkFlare
{
    public static class MonsterSteeringCalculator
    {
        const float PositionEpsilon = 0.0001f;

        public static Vector2 GetPursuitDirection(Vector2 targetOffset, float stopDistance)
        {
            float safeStopDistance = Mathf.Max(0f, stopDistance);

            if (targetOffset.sqrMagnitude <= safeStopDistance * safeStopDistance)
            {
                return Vector2.zero;
            }

            return targetOffset.normalized;
        }

        public static Vector2 GetSeparationContribution(
            Vector2 selfPosition,
            Vector2 neighborPosition,
            float separationRadius,
            int instanceSeed)
        {
            float safeRadius = Mathf.Max(0f, separationRadius);

            if (safeRadius <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 offset = selfPosition - neighborPosition;
            float sqrDistance = offset.sqrMagnitude;

            if (sqrDistance >= safeRadius * safeRadius)
            {
                return Vector2.zero;
            }

            if (sqrDistance <= PositionEpsilon * PositionEpsilon)
            {
                return GetStableDirection(instanceSeed);
            }

            float distance = Mathf.Sqrt(sqrDistance);
            float strength = 1f - distance / safeRadius;
            return offset / distance * strength;
        }

        public static Vector2 Combine(
            Vector2 pursuitDirection,
            Vector2 separation,
            float separationWeight)
        {
            Vector2 direction = pursuitDirection + separation * Mathf.Clamp(separationWeight, 0f, 2f);
            return Vector2.ClampMagnitude(direction, 1f);
        }

        public static Vector2 GetStableDirection(int seed)
        {
            uint value = unchecked((uint)seed);
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;
            float angle = value / (float)uint.MaxValue * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
