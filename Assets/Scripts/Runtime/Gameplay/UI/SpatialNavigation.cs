using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public static class SpatialNavigation
    {
        public static int FindNeighbor(Rect source, IReadOnlyList<Rect> candidates, Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) { return -1; }
            bool horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
            float sign = horizontal ? Mathf.Sign(direction.x) : -Mathf.Sign(direction.y);
            int best = -1;
            bool bestAligned = false;
            float bestDistance = float.MaxValue;
            float bestOffset = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                Rect target = candidates[i];
                float forward = horizontal ? target.center.x - source.center.x : target.center.y - source.center.y;
                if (target.width <= 0f || target.height <= 0f || forward * sign <= 0.5f) { continue; }
                float gap = horizontal
                    ? sign > 0f ? target.xMin - source.xMax : source.xMin - target.xMax
                    : sign > 0f ? target.yMin - source.yMax : source.yMin - target.yMax;
                float crossGap = horizontal
                    ? Mathf.Max(source.yMin - target.yMax, target.yMin - source.yMax)
                    : Mathf.Max(source.xMin - target.xMax, target.xMin - source.xMax);
                bool aligned = crossGap <= 0f;
                float distance = Mathf.Max(0f, gap) + Mathf.Max(0f, crossGap);
                float offset = horizontal ? Mathf.Abs(source.center.y - target.center.y) : Mathf.Abs(source.center.x - target.center.x);
                if (!aligned && forward * sign < offset) { continue; }
                if (best < 0 || aligned && !bestAligned
                    || aligned == bestAligned && (distance < bestDistance - 0.5f
                        || Mathf.Abs(distance - bestDistance) <= 0.5f && offset < bestOffset - 0.5f))
                {
                    best = i;
                    bestAligned = aligned;
                    bestDistance = distance;
                    bestOffset = offset;
                }
            }
            return best;
        }
    }
}
