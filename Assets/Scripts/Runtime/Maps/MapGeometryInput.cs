using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed class MapGeometrySettings
    {
        public int Subdivisions { get; }
        public int MaxNodes { get; }
        public int MaxRectangles { get; }
        public int MaxIndexEntries { get; }
        public const int RasterSize = 64;
        public MapGeometrySettings(int subdivisions = 4, int maxNodes = 4194304, int maxRectangles = 262144, int maxIndexEntries = 4194304)
        {
            if ((subdivisions != 2 && subdivisions != 4 && subdivisions != 8) || maxNodes <= 0 || maxRectangles <= 0 || maxIndexEntries <= 0)
            { throw new ArgumentOutOfRangeException(nameof(subdivisions)); }
            Subdivisions = subdivisions; MaxNodes = maxNodes; MaxRectangles = maxRectangles; MaxIndexEntries = maxIndexEntries;
        }
    }

    public readonly struct MapGroundProfile
    {
        // Polygon 物理形状约 0.01 的 skin + 投影精度余量；0 仅用于纯几何诊断。
        public const double PhysicsMargin = 0.0125;
        public double Radius { get; }
        public double Margin { get; }
        public double Clearance => Radius + Margin;
        public bool SupportsPhysics => Margin >= PhysicsMargin;
        public MapGroundProfile(double radius, double margin = PhysicsMargin)
        {
            if (!double.IsFinite(radius) || !double.IsFinite(margin) || radius <= 0 || margin < 0 || !double.IsFinite(radius + margin))
            { throw new ArgumentOutOfRangeException(nameof(radius)); }
            Radius = radius; Margin = margin;
        }
        internal bool IsValid => Radius > 0 && double.IsFinite(Clearance) && Margin >= 0;
    }

    public sealed class MapGeometryCapacityException : Exception
    {
        public MapGeometryCapacityException(string message) : base(message) { }
    }

    public sealed class MapGeometryRegion
    {
        readonly MapGeometryPoint[] _points;
        public string Id { get; }
        public MapRegionKind Kind { get; }
        public int Priority { get; }
        public string SpawnDefinitionId { get; }
        public IReadOnlyList<MapGeometryPoint> Points { get; }
        internal MapGeometryRegion(MapRegionRecord record, MapRegionKind kind, CompiledMapDocument map)
        {
            Id = record.RegionId; Kind = kind; Priority = record.Priority; SpawnDefinitionId = record.SpawnDefinitionId;
            MapPoint[] points = record.Points;
            if (record.Shape == MapRegionShape.Rectangle)
            {
                points = new[] { points[0], new MapPoint { X = points[1].X, Y = points[0].Y }, points[1], new MapPoint { X = points[0].X, Y = points[1].Y } };
            }
            _points = points.Select(p => new MapGeometryPoint(map.OriginX + p.X * map.CellSize, map.OriginY + p.Y * map.CellSize)).ToArray();
            Points = Array.AsReadOnly(_points);
        }
        public bool Contains(MapGeometryPoint p)
        {
            bool inside = false;
            for (int i = 0, j = _points.Length - 1; i < _points.Length; j = i++)
            {
                MapGeometryPoint a = _points[j], b = _points[i];
                if (MapGeometryMath.PointSegmentSquared(p, a, b) == 0) { return true; }
                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) { inside = !inside; }
            }
            return inside;
        }
        public double BoundaryDistanceSquared(MapGeometryPoint p)
        {
            double distance = double.PositiveInfinity;
            for (int i = 0; i < _points.Length; i++)
            { distance = Math.Min(distance, MapGeometryMath.PointSegmentSquared(p, _points[i], _points[(i + 1) % _points.Length])); }
            return distance;
        }
    }

    // 仅 Capture 访问 SO；Compile 可在后台运行，不持有定义或场景对象。
    public sealed class MapGeometryInput
    {
        internal readonly Dictionary<ContentId, (bool Walkable, int Cost)> Terrains;
        internal readonly MapGeometryRect[] Obstacles;
        internal readonly MapGeometryRegion[] Regions;
        public CompiledMapDocument Map { get; }
        public MapGeometrySettings Settings { get; }
        MapGeometryInput(CompiledMapDocument map, MapGeometrySettings settings,
            Dictionary<ContentId, (bool Walkable, int Cost)> terrains, MapGeometryRect[] obstacles, MapGeometryRegion[] regions)
        {
            Map = map; Settings = settings; Terrains = terrains; Obstacles = obstacles; Regions = regions;
        }
        public static MapGeometryInput Capture(CompiledMapDocument map, IMapDefinitionLookup definitions, MapGeometrySettings settings = null)
        {
            if (map == null || definitions == null) { throw new ArgumentNullException(map == null ? nameof(map) : nameof(definitions)); }
            settings ??= new MapGeometrySettings();
            if ((long)map.Width * map.Height * settings.Subdivisions * settings.Subdivisions > settings.MaxNodes)
            { throw new MapGeometryCapacityException("导航节点超出容量，需降低细分或缩小地图"); }
            MapDocument dto = map.CreateEditableCopy();
            MapDocumentCompiler.Compile(dto, definitions); // 重新验证自源导入后可能改变的定义。
            var terrains = new Dictionary<ContentId, (bool Walkable, int Cost)>();
            foreach (ContentId id in map.TerrainPalette)
            {
                TerrainDefinition terrain = definitions.Resolve<TerrainDefinition>(id);
                terrains.Add(id, (terrain.Walkable, terrain.MovementCost));
            }
            var obstacles = new List<MapGeometryRect>();
            foreach (MapObjectRecord record in dto.Objects)
            {
                MapObjectDefinition definition = definitions.Resolve<MapObjectDefinition>(ContentId.Parse(record.DefinitionId));
                if ((definition.Capabilities & MapObjectCapabilities.Obstacle) == 0) { continue; }
                double x = map.OriginX + record.Position.X, y = map.OriginY + record.Position.Y;
                double w = (record.Rotation % 180 == 0 ? definition.Footprint.x : definition.Footprint.y) / 2.0;
                double h = (record.Rotation % 180 == 0 ? definition.Footprint.y : definition.Footprint.x) / 2.0;
                obstacles.Add(new MapGeometryRect(x - w, y - h, x + w, y + h));
            }
            MapGeometryRegion[] regions = dto.Regions.Select(record => new MapGeometryRegion(record,
                definitions.Resolve<MapRegionDefinition>(ContentId.Parse(record.DefinitionId)).Kind, map))
                .OrderByDescending(r => r.Priority).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray();
            return new MapGeometryInput(map, settings, terrains, obstacles.ToArray(), regions);
        }
    }
}
