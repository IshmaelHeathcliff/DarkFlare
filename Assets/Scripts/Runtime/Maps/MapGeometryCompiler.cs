using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace DarkFlare
{
    public static class MapGeometryCompiler
    {
        public const string Revision = "partition-pixel-rect-ground-v1";
        public const int MaxTemplates = 1024;
        sealed class Template
        {
            internal readonly TerrainPartitionRaster Raster;
            internal readonly List<MapGeometryRect> Rectangles;
            internal Template(TerrainPartitionRaster raster, List<MapGeometryRect> rectangles) { Raster = raster; Rectangles = rectangles; }
        }

        public static MapGeometrySnapshot Compile(MapGeometryInput input, CancellationToken token = default)
        {
            if (input == null) { throw new ArgumentNullException(nameof(input)); }
            token.ThrowIfCancellationRequested();
            CompiledMapDocument map = input.Map;
            MapGeometrySettings settings = input.Settings;
            var bounds = new MapGeometryRect(map.OriginX, map.OriginY, map.OriginX + map.Width * map.CellSize, map.OriginY + map.Height * map.CellSize);
            var templates = new Dictionary<TerrainWangKey, Template>();
            var rectangles = new List<MapGeometryRect>();
            int stride = map.Width + 1;
            var cells = new Template[stride * (map.Height + 1)];
            for (int y = -1; y < map.Height; y++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = -1; x < map.Width; x++)
                {
                    TerrainWangKey key = DualGridCoordinates.Sample(map, x, y);
                    if (!templates.TryGetValue(key, out Template template))
                    {
                        RequireCapacity(templates.Count, MaxTemplates, "分区模板");
                        TerrainPartitionRaster raster = TerrainPartition.Rasterize(key, MapGeometrySettings.RasterSize, token);
                        template = new Template(raster, CreateRectangles(raster, input));
                        templates.Add(key, template);
                    }
                    cells[x + 1 + (y + 1) * stride] = template;
                    foreach (MapGeometryRect rect in template.Rectangles)
                    {
                        double minX = Math.Max(bounds.MinX, map.OriginX + (x + 0.5 + rect.MinX / 64) * map.CellSize);
                        double minY = Math.Max(bounds.MinY, map.OriginY + (y + 0.5 + rect.MinY / 64) * map.CellSize);
                        double maxX = Math.Min(bounds.MaxX, map.OriginX + (x + 0.5 + rect.MaxX / 64) * map.CellSize);
                        double maxY = Math.Min(bounds.MaxY, map.OriginY + (y + 0.5 + rect.MaxY / 64) * map.CellSize);
                        if (minX >= maxX || minY >= maxY) { continue; }
                        RequireCapacity(rectangles.Count, settings.MaxRectangles, "地表矩形");
                        rectangles.Add(new MapGeometryRect(minX, minY, maxX, maxY));
                    }
                }
            }
            // 合并精确共边的同宽 / 同高矩形，不跨越空隙，不改变孔洞或对角接触。
            rectangles = Merge(Merge(rectangles, true, token), false, token);
            foreach (MapGeometryRect obstacle in input.Obstacles)
            {
                RequireCapacity(rectangles.Count, settings.MaxRectangles, "障碍矩形");
                rectangles.Add(obstacle);
            }
            int width = map.Width * settings.Subdivisions, height = map.Height * settings.Subdivisions;
            var costs = new ushort[checked(width * height)];
            for (int y = 0; y < height; y++)
            {
                token.ThrowIfCancellationRequested();
                double logicalY = (y + 0.5) / settings.Subdivisions;
                int displayY = (int)Math.Floor(logicalY - 0.5);
                int py = Math.Clamp((int)((logicalY - displayY - 0.5) * 64), 0, 63);
                for (int x = 0; x < width; x++)
                {
                    double logicalX = (x + 0.5) / settings.Subdivisions;
                    int displayX = (int)Math.Floor(logicalX - 0.5);
                    int px = Math.Clamp((int)((logicalX - displayX - 0.5) * 64), 0, 63);
                    ContentId id = cells[displayX + 1 + (displayY + 1) * stride].Raster.GetTerrain(px, py);
                    costs[x + y * width] = (ushort)input.Terrains[id].Cost;
                }
            }
            token.ThrowIfCancellationRequested();
            return new MapGeometrySnapshot(input, rectangles.ToArray(), costs, bounds, token);
        }

        static List<MapGeometryRect> CreateRectangles(TerrainPartitionRaster raster, MapGeometryInput input)
        {
            var rects = new List<MapGeometryRect>();
            var previous = new Dictionary<(int Start, int End), int>();
            for (int y = 0; y < raster.Size; y++)
            {
                var current = new Dictionary<(int Start, int End), int>();
                int x = 0;
                while (x < raster.Size)
                {
                    if (input.Terrains[raster.GetTerrain(x, y)].Walkable) { x++; continue; }
                    int start = x++;
                    while (x < raster.Size && !input.Terrains[raster.GetTerrain(x, y)].Walkable) { x++; }
                    if (previous.TryGetValue((start, x), out int index))
                    {
                        MapGeometryRect old = rects[index];
                        rects[index] = new MapGeometryRect(start, old.MinY, x, y + 1);
                    }
                    else { index = rects.Count; rects.Add(new MapGeometryRect(start, y, x, y + 1)); }
                    current.Add((start, x), index);
                }
                previous = current;
            }
            return rects;
        }

        static List<MapGeometryRect> Merge(List<MapGeometryRect> source, bool horizontal, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            source.Sort((a, b) =>
            {
                int comparison = (horizontal ? a.MinY : a.MinX).CompareTo(horizontal ? b.MinY : b.MinX);
                if (comparison != 0) { return comparison; }
                comparison = (horizontal ? a.MaxY : a.MaxX).CompareTo(horizontal ? b.MaxY : b.MaxX);
                return comparison != 0 ? comparison : (horizontal ? a.MinX : a.MinY).CompareTo(horizontal ? b.MinX : b.MinY);
            });
            var result = new List<MapGeometryRect>();
            for (int i = 0; i < source.Count; i++)
            {
                if ((i & 1023) == 0) { token.ThrowIfCancellationRequested(); }
                MapGeometryRect b = source[i];
                if (result.Count > 0)
                {
                    MapGeometryRect a = result[result.Count - 1];
                    if (horizontal ? a.MinY == b.MinY && a.MaxY == b.MaxY && a.MaxX == b.MinX
                        : a.MinX == b.MinX && a.MaxX == b.MaxX && a.MaxY == b.MinY)
                    {
                        result[result.Count - 1] = new MapGeometryRect(a.MinX, a.MinY, b.MaxX, b.MaxY);
                        continue;
                    }
                }
                result.Add(b);
            }
            return result;
        }

        internal static void RequireCapacity(int count, int maximum, string name)
        {
            if (count >= maximum) { throw new MapGeometryCapacityException(name + "超出容量 " + maximum); }
        }

        internal static string Hash(MapGeometrySnapshot geometry, MapGeometryInput input, ushort[] costs, CancellationToken token)
        {
            using var hash = SHA256.Create();
            using var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
            using var writer = new BinaryWriter(stream);
            writer.Write(Revision); writer.Write(TerrainPartition.Revision);
            var terrainIds = new List<ContentId>(input.Terrains.Keys);
            terrainIds.Sort();
            writer.Write(terrainIds.Count);
            foreach (ContentId id in terrainIds)
            {
                writer.Write(id.ToString()); writer.Write(input.Terrains[id].Walkable); writer.Write(input.Terrains[id].Cost);
            }
            writer.Write(geometry.MapId); writer.Write(geometry.CellSize); writer.Write(geometry.Subdivisions);
            writer.Write(geometry.Width); writer.Write(geometry.Height);
            writer.Write(geometry.Bounds.MinX); writer.Write(geometry.Bounds.MinY);
            writer.Write(geometry.Rectangles.Count);
            foreach (MapGeometryRect rect in geometry.Rectangles)
            {
                token.ThrowIfCancellationRequested();
                writer.Write(rect.MinX); writer.Write(rect.MinY); writer.Write(rect.MaxX); writer.Write(rect.MaxY);
            }
            for (int i = 0; i < costs.Length; i++)
            {
                if ((i & 1023) == 0) { token.ThrowIfCancellationRequested(); }
                writer.Write(costs[i]);
            }
            writer.Write(geometry.Regions.Count);
            foreach (MapGeometryRegion region in geometry.Regions)
            {
                writer.Write(region.Id); writer.Write((int)region.Kind); writer.Write(region.Priority); writer.Write(region.SpawnDefinitionId ?? "");
                writer.Write(region.Points.Count);
                foreach (MapGeometryPoint point in region.Points) { writer.Write(point.X); writer.Write(point.Y); }
            }
            writer.Flush(); stream.FlushFinalBlock();
            return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
