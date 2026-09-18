using System;
using System.Collections.Generic;
using System.Threading;

namespace DarkFlare
{
    public enum MapGeometryResult { Valid, InvalidInput, Outside, Blocked, RegionRejected, NoCandidate, BudgetExceeded }

    public sealed class MapGeometrySnapshot
    {
        readonly MapGeometryRect[] _rectangles;
        readonly ushort[] _costs;
        readonly int[] _offsets;
        readonly int[] _indices;
        readonly int[] _rectangleMinX;
        readonly int[] _rectangleMinY;
        readonly int _logicalWidth;
        readonly int _logicalHeight;
        public string MapId { get; }
        public string SourceHash { get; }
        public string GeometryHash { get; }
        public double CellSize { get; }
        public int Subdivisions { get; }
        public int Width { get; }
        public int Height { get; }
        public double NodeSize => CellSize / Subdivisions;
        public MapGeometryRect Bounds { get; }
        public IReadOnlyList<MapGeometryRect> Rectangles { get; }
        public IReadOnlyList<MapGeometryRegion> Regions { get; }
        public long ArrayBytes => (long)_rectangles.Length * 32 + (long)_costs.Length * 2
            + ((long)_offsets.Length + _indices.Length + _rectangleMinX.Length + _rectangleMinY.Length) * 4;

        internal MapGeometrySnapshot(MapGeometryInput input, MapGeometryRect[] rectangles, ushort[] costs, MapGeometryRect bounds, CancellationToken token)
        {
            MapId = input.Map.MapId; SourceHash = input.Map.SourceHash; CellSize = input.Map.CellSize;
            Subdivisions = input.Settings.Subdivisions; _logicalWidth = input.Map.Width; _logicalHeight = input.Map.Height;
            Width = _logicalWidth * Subdivisions; Height = _logicalHeight * Subdivisions; Bounds = bounds;
            _rectangles = rectangles; _costs = costs;
            Rectangles = Array.AsReadOnly(rectangles); Regions = Array.AsReadOnly(input.Regions);
            _offsets = new int[_logicalWidth * _logicalHeight + 1];
            _rectangleMinX = new int[rectangles.Length]; _rectangleMinY = new int[rectangles.Length];
            long entries = 0;
            for (int i = 0; i < rectangles.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                MapGeometryRect rect = rectangles[i];
                int minX = CellX(rect.MinX), minY = CellY(rect.MinY), maxX = CellX(rect.MaxX), maxY = CellY(rect.MaxY);
                _rectangleMinX[i] = minX; _rectangleMinY[i] = minY;
                entries += (long)(maxX - minX + 1) * (maxY - minY + 1);
                if (entries > input.Settings.MaxIndexEntries) { throw new MapGeometryCapacityException("静态障碍索引超出容量"); }
                for (int y = minY; y <= maxY; y++)
                {
                    token.ThrowIfCancellationRequested();
                    for (int x = minX; x <= maxX; x++) { _offsets[x + y * _logicalWidth + 1]++; }
                }
            }
            for (int i = 1; i < _offsets.Length; i++) { _offsets[i] += _offsets[i - 1]; }
            _indices = new int[(int)entries];
            var cursor = (int[])_offsets.Clone();
            for (int i = 0; i < rectangles.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                MapGeometryRect rect = rectangles[i];
                int maxX = CellX(rect.MaxX), maxY = CellY(rect.MaxY);
                for (int y = _rectangleMinY[i]; y <= maxY; y++)
                {
                    token.ThrowIfCancellationRequested();
                    for (int x = _rectangleMinX[i]; x <= maxX; x++) { _indices[cursor[x + y * _logicalWidth]++] = i; }
                }
            }
            GeometryHash = MapGeometryCompiler.Hash(this, input, costs, token);
        }

        int CellX(double x) { return (int)Math.Clamp(Math.Floor((x - Bounds.MinX) / CellSize), 0, _logicalWidth - 1); }
        int CellY(double y) { return (int)Math.Clamp(Math.Floor((y - Bounds.MinY) / CellSize), 0, _logicalHeight - 1); }

        public MapGeometryPoint GetNode(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) { throw new ArgumentOutOfRangeException(nameof(x)); }
            return new MapGeometryPoint(Bounds.MinX + (x + 0.5) * NodeSize, Bounds.MinY + (y + 0.5) * NodeSize);
        }

        public MapGeometryResult CheckPoint(MapGeometryPoint p, MapGroundProfile profile, string spawnRegionId = null, CancellationToken token = default)
        {
            MapGeometryResult result = CheckSegment(p, p, profile, token: token);
            return result == MapGeometryResult.Valid ? CheckRegion(p, profile.Clearance, spawnRegionId) : result;
        }

        MapGeometryResult CheckRegion(MapGeometryPoint p, double radius, string spawnRegionId)
        {
            if (spawnRegionId == null) { return MapGeometryResult.Valid; }
            bool contained = false;
            double squared = radius * radius;
            foreach (MapGeometryRegion region in Regions)
            {
                if (region.Kind == MapRegionKind.Safe && (region.Contains(p) || region.BoundaryDistanceSquared(p) <= squared))
                { return MapGeometryResult.RegionRejected; }
                if (region.Id == spawnRegionId && region.Kind == MapRegionKind.Spawn && region.Contains(p) && region.BoundaryDistanceSquared(p) > squared)
                { contained = true; }
            }
            return contained ? MapGeometryResult.Valid : MapGeometryResult.RegionRejected;
        }

        public MapGeometryResult CheckSegment(MapGeometryPoint a, MapGeometryPoint b, MapGroundProfile profile, int maxVisits = 65536, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (!a.IsFinite || !b.IsFinite || !profile.IsValid || maxVisits <= 0) { return MapGeometryResult.InvalidInput; }
            double radius = profile.Clearance;
            if (!Inside(a, radius) || !Inside(b, radius)) { return MapGeometryResult.Outside; }
            var area = new MapGeometryRect(Math.Min(a.X, b.X) - radius, Math.Min(a.Y, b.Y) - radius,
                Math.Max(a.X, b.X) + radius, Math.Max(a.Y, b.Y) + radius);
            int minX = CellX(area.MinX), minY = CellY(area.MinY), maxX = CellX(area.MaxX), maxY = CellY(area.MaxY);
            int visits = 0;
            for (int y = minY; y <= maxY; y++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = minX; x <= maxX; x++)
                {
                    if (++visits > maxVisits) { return MapGeometryResult.BudgetExceeded; }
                    int cell = x + y * _logicalWidth;
                    for (int cursor = _offsets[cell]; cursor < _offsets[cell + 1]; cursor++)
                    {
                        if (++visits > maxVisits) { return MapGeometryResult.BudgetExceeded; }
                        int index = _indices[cursor];
                        // 一个矩形仅在与查询重叠的首桶计算距离，不分配查询 HashSet。
                        if (x != Math.Max(minX, _rectangleMinX[index]) || y != Math.Max(minY, _rectangleMinY[index])) { continue; }
                        MapGeometryRect rect = _rectangles[index];
                        if (rect.Intersects(area) && MapGeometryMath.SegmentRectSquared(a, b, rect) <= radius * radius)
                        { return MapGeometryResult.Blocked; }
                    }
                }
            }
            return MapGeometryResult.Valid;
        }

        public MapGeometryResult CheckEdge(int x, int y, int nextX, int nextY, MapGroundProfile profile, out int cost, CancellationToken token = default)
        {
            cost = 0;
            int dx = nextX - x, dy = nextY - y;
            if (x < 0 || y < 0 || x >= Width || y >= Height || nextX < 0 || nextY < 0 || nextX >= Width || nextY >= Height)
            { return MapGeometryResult.Outside; }
            if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || (dx == 0 && dy == 0)) { return MapGeometryResult.InvalidInput; }
            if (dx != 0 && dy != 0)
            {
                MapGeometryResult side = CheckPoint(GetNode(nextX, y), profile, token: token);
                if (side != MapGeometryResult.Valid) { return side; }
                side = CheckPoint(GetNode(x, nextY), profile, token: token);
                if (side != MapGeometryResult.Valid) { return side; }
            }
            MapGeometryResult result = CheckSegment(GetNode(x, y), GetNode(nextX, nextY), profile, token: token);
            if (result == MapGeometryResult.Valid) { cost = checked(_costs[nextX + nextY * Width] * (dx != 0 && dy != 0 ? 14 : 10)); }
            return result;
        }

        bool Inside(MapGeometryPoint point, double radius)
        {
            return point.X > Bounds.MinX + radius && point.X < Bounds.MaxX - radius
                && point.Y > Bounds.MinY + radius && point.Y < Bounds.MaxY - radius;
        }

        public MapGeometryResult FindPoint(MapGeometryPoint requested, MapGroundProfile profile, double searchRadius, int maxCandidates,
            out MapGeometryPoint point, string spawnRegionId = null, CancellationToken token = default)
        {
            point = default;
            token.ThrowIfCancellationRequested();
            if (!requested.IsFinite || !profile.IsValid || !double.IsFinite(searchRadius) || searchRadius < 0 || maxCandidates <= 0)
            { return MapGeometryResult.InvalidInput; }
            if (CheckPoint(requested, profile, spawnRegionId, token) == MapGeometryResult.Valid) { point = requested; return MapGeometryResult.Valid; }
            double lowX = Math.Max(Bounds.MinX, requested.X - searchRadius), lowY = Math.Max(Bounds.MinY, requested.Y - searchRadius);
            double highX = Math.Min(Bounds.MaxX, requested.X + searchRadius), highY = Math.Min(Bounds.MaxY, requested.Y + searchRadius);
            if (lowX > highX || lowY > highY) { return MapGeometryResult.NoCandidate; }
            int minX = (int)Math.Clamp(Math.Ceiling((lowX - Bounds.MinX) / NodeSize - 0.5), 0, Width - 1);
            int minY = (int)Math.Clamp(Math.Ceiling((lowY - Bounds.MinY) / NodeSize - 0.5), 0, Height - 1);
            int maxX = (int)Math.Clamp(Math.Floor((highX - Bounds.MinX) / NodeSize - 0.5), 0, Width - 1);
            int maxY = (int)Math.Clamp(Math.Floor((highY - Bounds.MinY) / NodeSize - 0.5), 0, Height - 1);
            if (minX > maxX || minY > maxY) { return MapGeometryResult.NoCandidate; }
            if ((long)(maxX - minX + 1) * (maxY - minY + 1) > maxCandidates) { return MapGeometryResult.BudgetExceeded; }
            var candidates = new List<(int X, int Y, double Distance)>();
            for (int y = minY; y <= maxY; y++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = minX; x <= maxX; x++)
                {
                    double distance = MapGeometryMath.DistanceSquared(GetNode(x, y), requested);
                    if (distance <= searchRadius * searchRadius) { candidates.Add((x, y, distance)); }
                }
            }
            candidates.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            foreach (var candidate in candidates)
            {
                token.ThrowIfCancellationRequested();
                MapGeometryPoint p = GetNode(candidate.X, candidate.Y);
                MapGeometryResult result = CheckPoint(p, profile, spawnRegionId, token);
                if (result == MapGeometryResult.BudgetExceeded) { return result; }
                if (result == MapGeometryResult.Valid) { point = p; return result; }
            }
            return MapGeometryResult.NoCandidate;
        }
    }
}
