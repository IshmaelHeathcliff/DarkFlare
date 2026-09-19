using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DarkFlare
{
    public enum NavigationMode { FlowField, AStar }
    public enum NavigationStatus { Pending, Moving, Arrived, Unreachable, TargetUnavailable, Busy, Cancelled, Stale, BudgetExceeded }

    // All map candidates share the budget, including the source retained for rollback.
    internal sealed class NavigationMemory : IDisposable
    {
        internal const long Limit = 128L * 1024 * 1024;
        static long s_bytes;
        static long s_peakBytes;
        long _bytes;
        public static long PeakBytes => Interlocked.Read(ref s_peakBytes);
        public static long Bytes => Interlocked.Read(ref s_bytes);
        internal NavigationMemory(long bytes)
        {
            if (bytes <= 0 || bytes > Limit) { throw new MapGeometryCapacityException("导航内存申请超出预算"); }
            long total = Interlocked.Add(ref s_bytes, bytes);
            if (total > Limit)
            {
                Interlocked.Add(ref s_bytes, -bytes);
                throw new MapGeometryCapacityException("导航原生内存超过 128 MiB（含切图候选）");
            }
            _bytes = bytes;
            long peak;
            do { peak = Interlocked.Read(ref s_peakBytes); } while (total > peak && Interlocked.CompareExchange(ref s_peakBytes, total, peak) != peak);
        }
        public void Dispose() { Interlocked.Add(ref s_bytes, -Interlocked.Exchange(ref _bytes, 0)); }
    }

    internal struct NavigationGeometryView
    {
        internal int Width;
        internal int Height;
        internal double NodeSize;
        internal double Radius;
        internal MapGeometryRect Bounds;
        [ReadOnly] internal NativeArray<MapGeometryRect> Rectangles;
        internal int Count => Width * Height;
        internal static int2 Offset(int direction)
        {
            switch (direction)
            {
                case 0: return new int2(1, 0);
                case 1: return new int2(0, 1);
                case 2: return new int2(-1, 0);
                case 3: return new int2(0, -1);
                case 4: return new int2(1, 1);
                case 5: return new int2(-1, 1);
                case 6: return new int2(-1, -1);
                default: return new int2(1, -1);
            }
        }
        internal int Neighbor(int index, int direction)
        {
            int2 p = new int2(index % Width, index / Width) + Offset(direction);
            return p.x < 0 || p.y < 0 || p.x >= Width || p.y >= Height ? -1 : p.x + p.y * Width;
        }
        internal MapGeometryPoint Point(int index)
        {
            return new MapGeometryPoint(Bounds.MinX + (index % Width + .5) * NodeSize, Bounds.MinY + (index / Width + .5) * NodeSize);
        }
        internal float2 Position(int index)
        {
            MapGeometryPoint p = Point(index);
            return new float2((float)p.X, (float)p.Y);
        }
        internal int Index(float2 position)
        {
            int x = (int)math.floor((position.x - Bounds.MinX) / NodeSize);
            int y = (int)math.floor((position.y - Bounds.MinY) / NodeSize);
            return x < 0 || y < 0 || x >= Width || y >= Height ? -1 : x + y * Width;
        }
        internal bool Inside(MapGeometryPoint p, double radius)
        {
            return p.X > Bounds.MinX + radius && p.X < Bounds.MaxX - radius && p.Y > Bounds.MinY + radius && p.Y < Bounds.MaxY - radius;
        }
        internal bool Segment(MapGeometryPoint a, MapGeometryPoint b, double radius)
        {
            if (!Inside(a, radius) || !Inside(b, radius)) { return false; }
            var area = new MapGeometryRect(math.min(a.X, b.X) - radius, math.min(a.Y, b.Y) - radius,
                math.max(a.X, b.X) + radius, math.max(a.Y, b.Y) + radius);
            for (int i = 0; i < Rectangles.Length; i++)
            {
                MapGeometryRect rect = Rectangles[i];
                if (rect.Intersects(area) && MapGeometryMath.SegmentRectSquared(a, b, rect) <= radius * radius) { return false; }
            }
            return true;
        }
        internal bool Segment(float2 a, float2 b, double radius)
        {
            return Segment(new MapGeometryPoint(a.x, a.y), new MapGeometryPoint(b.x, b.y), radius);
        }
        internal bool Escape(float2 from, float2 to, double radius, double tolerance)
        {
            var a = new MapGeometryPoint(from.x, from.y); var b = new MapGeometryPoint(to.x, to.y);
            double contact = math.max(.001, radius - tolerance), clearance = radius + MapGroundProfile.PhysicsMargin;
            if (!Inside(a, contact) || !Inside(b, contact)) { return false; }
            // Every contacted boundary must gain clearance; never permit a lateral shortcut through it.
            if (a.X <= Bounds.MinX + clearance && b.X <= a.X || a.X >= Bounds.MaxX - clearance && b.X >= a.X
                || a.Y <= Bounds.MinY + clearance && b.Y <= a.Y || a.Y >= Bounds.MaxY - clearance && b.Y >= a.Y) { return false; }
            if (a.X > Bounds.MinX + clearance && b.X <= Bounds.MinX + clearance || a.X < Bounds.MaxX - clearance && b.X >= Bounds.MaxX - clearance
                || a.Y > Bounds.MinY + clearance && b.Y <= Bounds.MinY + clearance || a.Y < Bounds.MaxY - clearance && b.Y >= Bounds.MaxY - clearance) { return false; }
            var area = new MapGeometryRect(math.min(a.X, b.X) - clearance, math.min(a.Y, b.Y) - clearance,
                math.max(a.X, b.X) + clearance, math.max(a.Y, b.Y) + clearance);
            for (int i = 0; i < Rectangles.Length; i++)
            {
                MapGeometryRect rectangle = Rectangles[i]; if (!rectangle.Intersects(area)) { continue; }
                double distance = MapGeometryMath.SegmentRectSquared(a, a, rectangle);
                double swept = MapGeometryMath.SegmentRectSquared(a, b, rectangle);
                if (distance <= clearance * clearance)
                {
                    if (swept < contact * contact || MapGeometryMath.SegmentRectSquared(b, b, rectangle) <= distance + 1e-10) { return false; }
                }
                else if (swept <= clearance * clearance) { return false; }
            }
            return true;
        }
    }

    internal struct NavigationGridView
    {
        internal NavigationGeometryView Geometry;
        [ReadOnly] internal NativeArray<ushort> Costs;
        [ReadOnly] internal NativeArray<ushort> Edges;
        internal int Width => Geometry.Width;
        internal int Height => Geometry.Height;
        internal int Count => Geometry.Count;
        internal double Radius => Geometry.Radius;
        internal double NodeSize => Geometry.NodeSize;
        internal MapGeometryRect Bounds => Geometry.Bounds;
        internal static int2 Offset(int direction) { return NavigationGeometryView.Offset(direction); }
        internal int Neighbor(int index, int direction) { return Geometry.Neighbor(index, direction); }
        internal MapGeometryPoint Point(int index) { return Geometry.Point(index); }
        internal float2 Position(int index) { return Geometry.Position(index); }
        internal int Index(float2 position) { return Geometry.Index(position); }
        internal bool Segment(MapGeometryPoint a, MapGeometryPoint b, double radius) { return Geometry.Segment(a, b, radius); }
        internal bool Segment(float2 a, float2 b, double radius) { return Geometry.Segment(a, b, radius); }
        internal bool Edge(int index, int direction)
        {
            return index >= 0 && index < Count && (Edges[index] & (1 << direction)) != 0;
        }
        internal ulong Cost(int destination, int direction) { return (ulong)Costs[destination] * (direction < 4 ? 10UL : 14UL); }
        internal int LegalNode(float2 requested, int rings = 8)
        {
            int root = Index(requested);
            if (root < 0) { return -1; }
            if (Edges[root] != 0) { return root; }
            int2 center = new int2(root % Width, root / Width);
            for (int r = 1; r <= rings; r++)
            {
                int best = -1; float distance = float.MaxValue;
                for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (math.max(math.abs(x), math.abs(y)) != r) { continue; }
                    int2 p = center + new int2(x, y);
                    if (p.x < 0 || p.y < 0 || p.x >= Width || p.y >= Height) { continue; }
                    int index = p.x + p.y * Width;
                    if (Edges[index] == 0) { continue; }
                    float next = math.distancesq(Position(index), requested);
                    if (next < distance) { best = index; distance = next; }
                }
                if (best >= 0) { return best; }
            }
            return -1;
        }
    }

    [BurstCompile]
    internal struct NavigationGridJob : IJobParallelFor
    {
        internal NavigationGeometryView Geometry;
        [WriteOnly] internal NativeArray<ushort> Edges;
        public void Execute(int index)
        {
            MapGeometryPoint p = Geometry.Point(index);
            double radius = Geometry.Radius;
            ushort edges = 0;
            if (Geometry.Segment(p, p, radius))
            {
                edges = 256;
                for (int d = 0; d < 8; d++)
                {
                    int next = Geometry.Neighbor(index, d);
                    if (next < 0) { continue; }
                    if (d >= 4)
                    {
                        int2 delta = NavigationGridView.Offset(d);
                        MapGeometryPoint a = Geometry.Point(index + delta.x);
                        MapGeometryPoint b = Geometry.Point(index + delta.y * Geometry.Width);
                        if (!Geometry.Segment(a, a, radius) || !Geometry.Segment(b, b, radius)) { continue; }
                    }
                    if (Geometry.Segment(p, Geometry.Point(next), radius)) { edges |= (ushort)(1 << d); }
                }
            }
            Edges[index] = edges;
        }
    }

    public sealed class NavigationGrid : IDisposable
    {
        NativeArray<MapGeometryRect> _rectangles;
        NativeArray<ushort> _costs;
        NativeArray<ushort> _edges;
        readonly NavigationMemory _memory;
        JobHandle _build;
        internal NavigationGridView View { get; private set; }
        public bool IsReady => _build.IsCompleted;
        public int NodeCount => _costs.Length;
        public long Bytes { get; }
        public NavigationGrid(MapGeometrySnapshot geometry, double radius, MapGeometryRect[] additional = null)
        {
            if (geometry == null || !double.IsFinite(radius) || radius <= 0) { throw new ArgumentException("无效导航网格"); }
            int count = checked(geometry.Width * geometry.Height);
            int rectangles = checked(geometry.Rectangles.Count + (additional?.Length ?? 0));
            Bytes = count * 4L + rectangles * 32L;
            _memory = new NavigationMemory(Bytes);
            try
            {
                _rectangles = new NativeArray<MapGeometryRect>(rectangles, Allocator.Persistent);
                _costs = new NativeArray<ushort>(count, Allocator.Persistent);
                _edges = new NativeArray<ushort>(count, Allocator.Persistent);
                for (int i = 0; i < geometry.Rectangles.Count; i++) { _rectangles[i] = geometry.Rectangles[i]; }
                for (int i = geometry.Rectangles.Count; i < rectangles; i++) { _rectangles[i] = additional[i - geometry.Rectangles.Count]; }
                for (int i = 0; i < count; i++) { _costs[i] = geometry.NodeCost(i); }
                var native = new NavigationGeometryView { Width = geometry.Width, Height = geometry.Height, NodeSize = geometry.NodeSize,
                    Radius = radius + MapGroundProfile.PhysicsMargin, Bounds = geometry.Bounds, Rectangles = _rectangles };
                _build = new NavigationGridJob { Geometry = native, Edges = _edges }.Schedule(count, 128);
                View = new NavigationGridView { Geometry = native, Costs = _costs, Edges = _edges };
            }
            catch { Dispose(); throw; }
        }
        public void Complete() { _build.Complete(); }
        public void Dispose()
        {
            _build.Complete();
            if (_edges.IsCreated) { _edges.Dispose(); }
            if (_costs.IsCreated) { _costs.Dispose(); }
            if (_rectangles.IsCreated) { _rectangles.Dispose(); }
            _memory?.Dispose();
        }
    }
}
