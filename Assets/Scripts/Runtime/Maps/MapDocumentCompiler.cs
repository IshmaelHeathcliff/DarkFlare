using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DarkFlare.MapDocumentJson;

namespace DarkFlare
{
    public static class MapDocumentCompiler
    {
        public static CompiledMapDocument Compile(MapDocument source, IMapDefinitionLookup definitions)
        {
            if (definitions == null) { throw new ArgumentNullException(nameof(definitions)); }
            // 脱离调用者的可变数据，同时校验程序创建 DTO 的字段、容量及数值类型。
            string canonical = Write(source);
            MapDocument document = Read(canonical);
            Require(document.FormatVersion == 1, "$.formatVersion", "仅支持格式版本 1");
            Require(document.Revision >= 0, "$.revision", "修订不能为负数");
            TypedId(document.MapId, ContentNamespaces.Map, "$.mapId");
            Require(document.Width > 0 && document.Width <= MapDocumentLimits.MaxSide, "$.width", "尺寸必须为 1..1024");
            Require(document.Height > 0 && document.Height <= MapDocumentLimits.MaxSide, "$.height", "尺寸必须为 1..1024");
            Require((long)document.Width * document.Height <= MapDocumentLimits.MaxCells, "$", "地图容量超限");
            Require(document.CellSize > 0 && document.CellSize <= 1024, "$.cellSize", "格宽必须在 (0, 1024] 内");
            CheckCoordinate(document.Origin.X, "$.origin.x");
            CheckCoordinate(document.Origin.Y, "$.origin.y");
            CheckCoordinate(document.Origin.X + document.Width * document.CellSize, "$.width");
            CheckCoordinate(document.Origin.Y + document.Height * document.CellSize, "$.height");

            TerrainSetDefinition set = Resolve<TerrainSetDefinition>(definitions, document.TerrainSetId, ContentNamespaces.TerrainSet, "$.terrainSetId");
            ValidateReference(set, definitions, "$.terrainSetId");
            Require(document.TerrainPalette.Length > 0, "$.terrainPalette", "至少需要一种地表");
            var palette = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < document.TerrainPalette.Length; i++)
            {
                string path = $"$.terrainPalette[{i}]";
                TerrainDefinition terrain = Resolve<TerrainDefinition>(definitions, document.TerrainPalette[i], ContentNamespaces.Terrain, path);
                ValidateReference(terrain, definitions, path);
                Require(palette.Add(document.TerrainPalette[i]), path, "地表重复");
                Require(set.Terrains.Contains(terrain), path, "地表不在所选地表集中");
            }
            int defaultIndex = Array.IndexOf(document.TerrainPalette, document.DefaultTerrainId);
            Require(defaultIndex >= 0, "$.defaultTerrainId", "默认地表必须在 palette 中");
            int[] cells = new int[document.Width * document.Height];
            if (defaultIndex != 0) { Array.Fill(cells, defaultIndex); }
            var chunks = new HashSet<Vector2Int>();
            for (int i = 0; i < document.Chunks.Length; i++)
            {
                MapChunk chunk = document.Chunks[i];
                string path = $"$.chunks[{i}]";
                Require(chunk.X >= 0 && chunk.Y >= 0 && chunk.X < (document.Width + 31) / 32 && chunk.Y < (document.Height + 31) / 32, path, "块坐标越界");
                Require(chunks.Add(new Vector2Int(chunk.X, chunk.Y)), path, "重复块坐标");
                Require(chunk.Cells.Length == MapDocumentLimits.ChunkCells, path + ".cells", "每块必须包含 1024 个槽位");
                for (int j = 0; j < chunk.Cells.Length; j++)
                {
                    int index = chunk.Cells[j];
                    Require(index >= 0 && index < document.TerrainPalette.Length, $"{path}.cells[{j}]", "地表索引越界");
                    int x = chunk.X * 32 + j % 32;
                    int y = chunk.Y * 32 + j / 32;
                    if (x >= document.Width || y >= document.Height)
                    {
                        Require(index == defaultIndex, $"{path}.cells[{j}]", "地图外的填充槽只能使用默认地表");
                    }
                    else { cells[x + y * document.Width] = index; }
                }
            }
            var compiled = new CompiledMapDocument(document, cells, defaultIndex, canonical);
            var positions = new HashSet<Vector2Int>();
            for (int i = 0; i < document.VariantOverrides.Length; i++)
            {
                MapVariantOverride entry = document.VariantOverrides[i];
                string path = $"$.variantOverrides[{i}]";
                Require(entry.X >= -1 && entry.Y >= -1 && entry.X < document.Width && entry.Y < document.Height, path, "显示格坐标越界");
                Require(positions.Add(new Vector2Int(entry.X, entry.Y)), path, "重复变体覆盖");
                TerrainTileVariant variant = set.Variants.SingleOrDefault(candidate => candidate.Id == entry.VariantId);
                Require(variant != null, path + ".variantId", "未知变体");
                Require(Matches(variant.SouthWest, compiled.GetTerrain(entry.X, entry.Y))
                    && Matches(variant.SouthEast, compiled.GetTerrain(entry.X + 1, entry.Y))
                    && Matches(variant.NorthWest, compiled.GetTerrain(entry.X, entry.Y + 1))
                    && Matches(variant.NorthEast, compiled.GetTerrain(entry.X + 1, entry.Y + 1)), path, "变体四角与地表不兼容");
            }
            ValidateObjects(document, definitions);
            ValidateRegions(document, definitions);
            return compiled;
        }

        public static void ValidateDefinition(ScriptableObject asset, IMapDefinitionLookup definitions)
        {
            Require(asset is IContentDefinition && ContentDefinitionRegistry.TryGet(asset.GetType(), out _), "$definition", "不是内容定义");
            var content = (IContentDefinition)asset;
            Require(ContentId.IsValidSegment(content.Id), "$definition.id", "ID 必须是小写 snake_case");
            if (asset is TerrainDefinition terrain)
            {
                Require(terrain.MovementCost >= 1 && terrain.MovementCost <= 10000, terrain.Id, "移动代价必须为 1..10000");
            }
            else if (asset is TerrainSetDefinition set)
            {
                Require(set.Terrains.Count > 0 && set.Terrains.Count <= MapDocumentLimits.MaxPalette, set.Id, "地表集容量必须为 1..64");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (TerrainDefinition entry in set.Terrains)
                {
                    Require(entry != null && ids.Add(entry.Id), set.Id, "地表为空或重复");
                    Require(Resolve<TerrainDefinition>(definitions, "terrain:" + entry.Id, ContentNamespaces.Terrain, set.Id) == entry, set.Id, "地表引用冲突");
                    ValidateDefinition(entry, definitions);
                }
                ids.Clear();
                foreach (TerrainTileVariant variant in set.Variants)
                {
                    Require(variant != null && ContentId.IsValidSegment(variant.Id) && ids.Add(variant.Id), set.Id, "变体 ID 非法或重复");
                    Require(set.Terrains.Contains(variant.SouthWest) && set.Terrains.Contains(variant.SouthEast)
                        && set.Terrains.Contains(variant.NorthWest) && set.Terrains.Contains(variant.NorthEast), set.Id, "变体四角必须来自地表集");
                }
            }
            else if (asset is MapObjectDefinition obj)
            {
                Require(obj.Capabilities != MapObjectCapabilities.None && (obj.Capabilities & ~(MapObjectCapabilities.Obstacle | MapObjectCapabilities.Decoration | MapObjectCapabilities.Interaction)) == 0, obj.Id, "未知或空对象能力");
                bool interactive = (obj.Capabilities & MapObjectCapabilities.Interaction) != 0;
                Require(Enum.IsDefined(typeof(MapInteractionKind), obj.InteractionKind) && interactive == (obj.InteractionKind != MapInteractionKind.None), obj.Id, "交互能力与类型不一致");
                if ((obj.Capabilities & MapObjectCapabilities.Obstacle) != 0)
                {
                    Require(IsFinitePositive(obj.Footprint.x) && IsFinitePositive(obj.Footprint.y), obj.Id, "障碍尺寸必须是正有限数值");
                }
                var variants = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in obj.AppearanceVariants)
                {
                    Require(ContentId.IsValidSegment(id) && variants.Add(id), obj.Id, "外观变体 ID 非法或重复");
                }
            }
            else if (asset is MapRegionDefinition region)
            {
                Require(Enum.IsDefined(typeof(MapRegionKind), region.Kind), region.Id, "未知区域语义");
            }
        }

        static void ValidateObjects(MapDocument document, IMapDefinitionLookup definitions)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < document.Objects.Length; i++)
            {
                MapObjectRecord obj = document.Objects[i];
                string path = $"$.objects[{i}]";
                Require(ContentId.IsValidSegment(obj.ObjectId) && ids.Add(obj.ObjectId), path + ".objectId", "对象 ID 非法或重复");
                MapObjectDefinition definition = Resolve<MapObjectDefinition>(definitions, obj.DefinitionId, ContentNamespaces.MapObject, path + ".definitionId");
                ValidateReference(definition, definitions, path + ".definitionId");
                InBounds(obj.Position, document.Width * document.CellSize, document.Height * document.CellSize, path + ".position");
                Require(obj.Rotation == 0 || obj.Rotation == 90 || obj.Rotation == 180 || obj.Rotation == 270, path + ".rotation", "旋转必须是 0/90/180/270");
                Require(obj.AppearanceVariant == null || definition.AppearanceVariants.Contains(obj.AppearanceVariant), path + ".appearanceVariant", "未知外观变体");
                if ((definition.Capabilities & MapObjectCapabilities.Obstacle) != 0)
                {
                    double halfX = (obj.Rotation % 180 == 0 ? definition.Footprint.x : definition.Footprint.y) / 2.0;
                    double halfY = (obj.Rotation % 180 == 0 ? definition.Footprint.y : definition.Footprint.x) / 2.0;
                    Require(obj.Position.X >= halfX && obj.Position.X + halfX <= document.Width * document.CellSize
                        && obj.Position.Y >= halfY && obj.Position.Y + halfY <= document.Height * document.CellSize, path + ".position", "障碍足迹超出地图");
                }
                if (definition.InteractionKind == MapInteractionKind.None)
                {
                    Require(obj.Interaction == null, path + ".interaction", "此对象不允许交互绑定");
                }
                else
                {
                    Require(obj.Interaction != null && obj.Interaction.Kind == definition.InteractionKind, path + ".interaction", "缺少或不兼容的交互绑定");
                    if (obj.Interaction.Kind == MapInteractionKind.Trader)
                    {
                        Resolve<TraderDefinition>(definitions, obj.Interaction.ContentId, ContentNamespaces.Trader, path + ".interaction.contentId");
                    }
                    else
                    {
                        Resolve<CraftingDefinition>(definitions, obj.Interaction.ContentId, ContentNamespaces.Crafting, path + ".interaction.contentId");
                    }
                }
            }
        }

        static void ValidateRegions(MapDocument document, IMapDefinitionLookup definitions)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < document.Regions.Length; i++)
            {
                MapRegionRecord region = document.Regions[i];
                string path = $"$.regions[{i}]";
                Require(ContentId.IsValidSegment(region.RegionId) && ids.Add(region.RegionId), path + ".regionId", "区域 ID 非法或重复");
                MapRegionDefinition definition = Resolve<MapRegionDefinition>(definitions, region.DefinitionId, ContentNamespaces.MapRegion, path + ".definitionId");
                ValidateReference(definition, definitions, path + ".definitionId");
                if (definition.Kind == MapRegionKind.Spawn)
                {
                    Resolve<MonsterSpawnDefinition>(definitions, region.SpawnDefinitionId, ContentNamespaces.Spawn, path + ".spawnDefinitionId");
                }
                else { Require(region.SpawnDefinitionId == null, path + ".spawnDefinitionId", "非刷怪区不允许刷怪参数"); }
                foreach (MapPoint point in region.Points) { InBounds(point, document.Width, document.Height, path + ".points"); }
                if (region.Shape == MapRegionShape.Rectangle)
                {
                    Require(region.Points.Length == 2, path + ".points", "矩形需要左下、右上两个点");
                    Require(region.Points[0].X < region.Points[1].X && region.Points[0].Y < region.Points[1].Y, path + ".points", "矩形必须有正面积");
                }
                else { ValidatePolygon(region.Points, path + ".points"); }
            }
        }

        static void ValidatePolygon(MapPoint[] points, string path)
        {
            Require(points.Length >= 3, path, "多边形至少需要 3 点");
            double area = 0;
            for (int i = 0; i < points.Length; i++)
            {
                MapPoint a = points[i];
                MapPoint b = points[(i + 1) % points.Length];
                Require(a.X != b.X || a.Y != b.Y, path, "不允许重复相邻点");
                area += a.X * b.Y - b.X * a.Y;
                MapPoint previous = points[(i + points.Length - 1) % points.Length];
                Require(Cross(previous, a, b) != 0, path, "相邻边不能重叠或冗余共线");
                for (int j = i + 1; j < points.Length; j++)
                {
                    if (j == i + 1 || (i == 0 && j == points.Length - 1)) { continue; }
                    MapPoint c = points[j];
                    MapPoint d = points[(j + 1) % points.Length];
                    bool intersects = Math.Max(a.X, b.X) >= Math.Min(c.X, d.X) && Math.Max(c.X, d.X) >= Math.Min(a.X, b.X)
                        && Math.Max(a.Y, b.Y) >= Math.Min(c.Y, d.Y) && Math.Max(c.Y, d.Y) >= Math.Min(a.Y, b.Y)
                        && Cross(a, b, c) * Cross(a, b, d) <= 0 && Cross(c, d, a) * Cross(c, d, b) <= 0;
                    Require(!intersects, path, "多边形不能自交或自接触");
                }
            }
            Require(Math.Abs(area) > 0.00000001, path, "多边形面积为零");
        }

        static double Cross(MapPoint a, MapPoint b, MapPoint c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }

        static bool Matches(TerrainDefinition terrain, string id)
        {
            return "terrain:" + terrain.Id == id;
        }

        static void ValidateReference(ScriptableObject asset, IMapDefinitionLookup definitions, string path)
        {
            try { ValidateDefinition(asset, definitions); }
            catch (MapDocumentException error) { throw new MapDocumentException(path, error.Message); }
        }

        static bool IsFinitePositive(double value)
        {
            return value > 0 && value <= MapDocumentLimits.MaxWorldCoordinate && !double.IsInfinity(value);
        }

        static void CheckCoordinate(double value, string path)
        {
            Require(!double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) <= MapDocumentLimits.MaxWorldCoordinate, path, "世界坐标非有限或超过 ±1000000");
        }

        static void InBounds(MapPoint point, double width, double height, string path)
        {
            Require(point.X >= 0 && point.Y >= 0 && point.X <= width && point.Y <= height, path, "局部坐标超出地图");
        }

        static ContentId TypedId(string value, string contentNamespace, string path)
        {
            Require(ContentId.TryParse(value, out ContentId id) && id.Namespace == contentNamespace, path, $"需要 {contentNamespace}:local_id");
            return id;
        }

        static T Resolve<T>(IMapDefinitionLookup definitions, string value, string contentNamespace, string path) where T : ScriptableObject, IContentDefinition
        {
            ContentId id = TypedId(value, contentNamespace, path);
            try { return definitions.Resolve<T>(id); }
            catch (MapDocumentException error) { throw new MapDocumentException(path, error.Message); }
        }
    }
}
