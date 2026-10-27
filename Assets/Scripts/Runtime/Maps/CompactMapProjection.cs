using System;
using System.Collections.Generic;

namespace DarkFlare
{
    // 只转换候选DTO中的空间点。正式单位最大导航足迹为0.75，预留0.05；激活前仍校验真实Prefab足迹。
    sealed class CompactMapProjection
    {
        const double ActorRadius = 0.8;
        readonly CompiledMapDocument _old;
        readonly CompiledMapDocument _current;
        readonly MapGeometrySnapshot _geometry;
        readonly List<MapGeometryPoint> _actors = new();

        internal CompactMapProjection(CompiledMapDocument old, CompiledMapDocument current, IMapDefinitionLookup definitions)
        {
            _old = old; _current = current;
            _geometry = MapGeometryCompiler.Compile(MapGeometryInput.Capture(current, definitions, new MapGeometrySettings()));
        }

        internal void Actor(Vector3Dto value, bool monster) { Move(value, ActorRadius, true, monster); }
        internal void Point(Vector3Dto value) { Move(value, ActorRadius, false, false); }
        internal void Drop(Vector3Dto value) { Move(value, 0.3, false, false); }

        void Move(Vector3Dto value, double radius, bool reserve, bool excludeSafe)
        {
            double x = _current.OriginX + (value.X - _old.OriginX) * _current.Width / _old.Width;
            double y = _current.OriginY + (value.Y - _old.OriginY) * _current.Height / _old.Height;
            var requested = new MapGeometryPoint(x, y);
            MapGeometryPoint chosen = requested;
            if (!Valid(chosen))
            {
                bool found = false;
                // 固定网格环与同距顺序，无随机数、无实例重建；总候选数受地图尺寸限制。
                double step = _geometry.NodeSize * 2;
                int rings = (int)Math.Ceiling(Math.Max(_geometry.Bounds.MaxX - _geometry.Bounds.MinX, _geometry.Bounds.MaxY - _geometry.Bounds.MinY) / step);
                for (int ring = 1; ring <= rings && !found; ring++)
                {
                    for (int dy = -ring; dy <= ring && !found; dy++)
                    for (int dx = -ring; dx <= ring; dx += Math.Abs(dy) == ring ? 1 : ring * 2)
                    {
                        chosen = new MapGeometryPoint(x + dx * step, y + dy * step);
                        if (!Valid(chosen)) { continue; }
                        found = true; break;
                    }
                }
                if (!found) { throw new InvalidOperationException("紧凑地图无法容纳存档对象：" + _current.MapId); }
            }
            value.X = (float)chosen.X; value.Y = (float)chosen.Y;
            if (reserve) { _actors.Add(chosen); }

            bool Valid(MapGeometryPoint candidate)
            {
                if (_geometry.CheckPoint(candidate, new MapGroundProfile(radius, 0)) != MapGeometryResult.Valid) { return false; }
                if (excludeSafe)
                {
                    foreach (MapGeometryRegion region in _geometry.Regions)
                    {
                        if (region.Kind == MapRegionKind.Safe && (region.Contains(candidate) || region.BoundaryDistanceSquared(candidate) <= radius * radius)) { return false; }
                    }
                }
                if (reserve)
                {
                    foreach (MapGeometryPoint actor in _actors)
                    {
                        double dx = candidate.X - actor.X, dy = candidate.Y - actor.Y;
                        if (dx * dx + dy * dy < ActorRadius * ActorRadius * 4) { return false; }
                    }
                }
                return true;
            }
        }
    }
}
