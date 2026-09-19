using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DarkFlare
{
    internal struct NavigationSample
    {
        internal float2 Position;
        internal float2 Desired;
        internal float2 AttachOffset;
        internal float Speed;
        internal float Radius;
        internal float SeparationRadius;
        internal float SeparationWeight;
        internal ulong Identity;
        internal bool CanMove;
    }
    internal struct NavigationNeighbor : IComparable<NavigationNeighbor>
    {
        internal long Cell;
        internal ulong Identity;
        internal int Index;
        internal static long Key(int2 cell) { return ((long)cell.x << 32) | (uint)cell.y; }
        public int CompareTo(NavigationNeighbor other)
        {
            int key = Cell.CompareTo(other.Cell);
            return key != 0 ? key : Identity.CompareTo(other.Identity);
        }
    }
    [BurstCompile]
    internal struct NavigationHashJob : IJob
    {
        [ReadOnly] internal NativeArray<NavigationSample> Samples;
        internal NativeArray<NavigationNeighbor> Sorted;
        public void Execute()
        {
            for (int i = 0; i < Samples.Length; i++)
            {
                NavigationSample sample = Samples[i];
                Sorted[i] = new NavigationNeighbor { Cell = NavigationNeighbor.Key((int2)math.floor(sample.Position)), Identity = sample.Identity, Index = i };
            }
            Sorted.Sort();
        }
    }
    [BurstCompile]
    internal struct NavigationSteeringJob : IJobParallelFor
    {
        internal NavigationGeometryView Geometry;
        [ReadOnly] internal NativeArray<NavigationSample> Samples;
        [ReadOnly] internal NativeArray<NavigationNeighbor> Sorted;
        [WriteOnly] internal NativeArray<float2> Velocities;
        [WriteOnly] internal NativeArray<int> Visits;
        internal float DeltaTime;
        internal float ContactTolerance;
        internal uint Tick;
        public void Execute(int index)
        {
            NavigationSample self = Samples[index];
            if (!self.CanMove) { Velocities[index] = float2.zero; Visits[index] = 0; return; }
            float2 desired = math.normalizesafe(self.Desired);
            int2 cell = (int2)math.floor(self.Position);
            int rings = (int)math.ceil(self.SeparationRadius);
            float2 separation = float2.zero;
            int visited = 0, neighbors = 0;
            for (int ring = 0; ring <= rings && visited < 96 && neighbors < 24; ring++)
            for (int y = -ring; y <= ring && visited < 96 && neighbors < 24; y++)
            for (int x = -ring; x <= ring && visited < 96 && neighbors < 24; x++)
            {
                if (math.max(math.abs(x), math.abs(y)) != ring) { continue; }
                long key = NavigationNeighbor.Key(cell + new int2(x, y));
                int first = LowerBound(key), end = UpperBound(key);
                int count = end - first;
                if (count == 0) { continue; }
                int offset = (int)((self.Identity + Tick) % (ulong)count);
                for (int n = 0; n < count && visited < 96 && neighbors < 24; n++)
                {
                    visited++;
                    int otherIndex = Sorted[first + (offset + n) % count].Index;
                    if (otherIndex == index) { continue; }
                    NavigationSample other = Samples[otherIndex];
                    float2 delta = self.Position - other.Position;
                    float squared = math.lengthsq(delta);
                    if (squared >= self.SeparationRadius * self.SeparationRadius) { continue; }
                    neighbors++;
                    if (squared < .00000001f)
                    {
                        ulong a = math.min(self.Identity, other.Identity), b = math.max(self.Identity, other.Identity);
                        uint hash = math.hash(new uint4((uint)a, (uint)(a >> 32), (uint)b, (uint)(b >> 32)));
                        float angle = hash * (math.PI * 2 / uint.MaxValue);
                        separation += new float2(math.cos(angle), math.sin(angle)) * (self.Identity < other.Identity ? 1 : -1);
                    }
                    else
                    {
                        float distance = math.sqrt(squared);
                        separation += delta / distance * (1 - distance / self.SeparationRadius);
                    }
                }
            }
            float2 direction = desired + separation * self.SeparationWeight;
            float length = math.length(direction);
            if (length > 1) { direction /= length; }
            float2 velocity = direction * self.Speed;
            double radius = self.Radius + MapGroundProfile.PhysicsMargin;
            if (!Geometry.Segment(self.Position, self.Position + velocity * DeltaTime, radius))
            {
                // Try the path direction without crowd pressure, then stop. Never slide through a thin wall.
                velocity = desired * self.Speed;
                if (!Geometry.Segment(self.Position, self.Position + velocity * DeltaTime, radius))
                {
                    velocity = math.normalizesafe(self.AttachOffset) * self.Speed;
                    if (!Geometry.Escape(self.Position, self.Position + velocity * DeltaTime, self.Radius, ContactTolerance)) { velocity = float2.zero; }
                }
            }
            Velocities[index] = math.all(math.isfinite(velocity)) ? velocity : float2.zero;
            Visits[index] = visited;
        }
        int LowerBound(long key)
        {
            int low = 0, high = Sorted.Length;
            while (low < high) { int mid = (low + high) / 2; if (Sorted[mid].Cell < key) { low = mid + 1; } else { high = mid; } }
            return low;
        }
        int UpperBound(long key)
        {
            int low = 0, high = Sorted.Length;
            while (low < high) { int mid = (low + high) / 2; if (Sorted[mid].Cell <= key) { low = mid + 1; } else { high = mid; } }
            return low;
        }
    }
}
