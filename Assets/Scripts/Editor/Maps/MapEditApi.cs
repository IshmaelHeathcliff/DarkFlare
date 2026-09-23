using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace DarkFlare.Editor
{
    // CLI / MCP 可直接调用这些静态入口，不需要窗口或第二个进程。
    public static class MapEditApi
    {
        public static string DescribeSchema() { return MapEditService.DescribeSchema().ToString(); }

        public static string ReadMap(string path)
        {
            return Run(() =>
            {
                MapEditSession session = MapEditSessions.Open(path);
                CompiledMapDocument map = session.Current;
                var header = JObject.Parse(map.CanonicalJson);
                header.Remove("chunks"); header.Remove("objects"); header.Remove("regions"); header.Remove("variantOverrides"); header.Remove("entries"); header.Remove("bossSpawns");
                header["hash"] = map.SourceHash; header["dirty"] = session.IsDirty; header["sourcePath"] = session.SourcePath;
                header["notice"] = session.Notice;
                return header;
            });
        }

        public static string ReadRegion(string path, int x, int y, int width, int height, int offset = 0, int count = 256)
        {
            return Run(() =>
            {
                CompiledMapDocument map = MapEditSessions.Open(path).Current;
                if (width < 1 || height < 1 || (long)width * height > 4096 || x < 0 || y < 0 || (long)x + width > map.Width || (long)y + height > map.Height || offset < 0 || count < 1 || count > 256)
                { throw new MapEditException("BudgetExceeded", "$read", "读取范围最多 4096 格，记录每页 256 条，范围必须在地图内"); }
                var cells = new JArray();
                for (int row = y; row < y + height; row++)
                {
                    for (int col = x; col < x + width; col++) { cells.Add(new JObject { ["x"] = col, ["y"] = row, ["terrainId"] = map.GetTerrain(col, row) }); }
                }
                JObject dto = JObject.Parse(map.CanonicalJson);
                // 记录按稳定 ID 分页，不遗漏跨区域对象 / 多边形；格子窗口单独限定。
                return new JObject
                {
                    ["revision"] = map.Revision, ["hash"] = map.SourceHash, ["cells"] = cells,
                    ["objects"] = new JArray(dto["objects"].Skip(offset).Take(count)), ["regions"] = new JArray(dto["regions"].Skip(offset).Take(count)),
                    ["variantOverrides"] = new JArray(dto["variantOverrides"].Skip(offset).Take(count)),
                    ["entries"] = new JArray(dto["entries"].Skip(offset).Take(count)), ["entriesTotal"] = dto["entries"].Count(),
                    ["bossSpawns"] = new JArray(dto["bossSpawns"].Skip(offset).Take(count)), ["bossSpawnsTotal"] = dto["bossSpawns"].Count(),
                    ["nextOffset"] = offset + count, ["objectsTotal"] = dto["objects"].Count(), ["regionsTotal"] = dto["regions"].Count(), ["overridesTotal"] = dto["variantOverrides"].Count()
                };
            });
        }

        public static string ListDefinitions(string filter = "", int offset = 0, int count = 256)
        {
            return Run(() =>
            {
                if (offset < 0 || count < 1 || count > 256) { throw new MapEditException("BudgetExceeded", "$read", "目录每页 1..256 条"); }
                var assets = MapEditorDefinitionIndex.LoadDefinitions().Where(asset => asset.name.Contains(filter ?? "", StringComparison.OrdinalIgnoreCase) || ((IContentDefinition)asset).Id.Contains(filter ?? "", StringComparison.OrdinalIgnoreCase)).ToArray();
                return new JObject
                {
                    ["total"] = assets.Length, ["nextOffset"] = offset + count,
                    ["definitions"] = new JArray(assets.Skip(offset).Take(count).Select(asset => new JObject
                    {
                        ["id"] = ContentDefinitionRegistry.TryGet(asset.GetType(), out ContentDefinitionMetadata metadata) ? new ContentId(metadata.Namespace, ((IContentDefinition)asset).Id).ToString() : ((IContentDefinition)asset).Id,
                        ["type"] = asset.GetType().Name, ["path"] = AssetDatabase.GetAssetPath(asset),
                        ["variants"] = asset is TerrainSetDefinition set ? new JArray(set.Variants.Select(v => v.Id)) : asset is MapObjectDefinition obj ? new JArray(obj.AppearanceVariants) : null,
                        ["configuration"] = JObject.Parse(EditorJsonUtility.ToJson(asset))
                    }))
                };
            });
        }

        public static string PreviewPatch(string path, string json)
        {
            return Run(() =>
            {
                MapEditSession session = MapEditSessions.Open(path);
                CompiledMapDocument candidate = session.Preview(MapEditService.ReadPatch(json));
                return Diff(session.Current, candidate);
            });
        }

        public static JObject Diff(CompiledMapDocument before, CompiledMapDocument after)
        {
            int cells = 0;
            int minX = after.Width, minY = after.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < Math.Max(before.Height, after.Height); y++)
            {
                for (int x = 0; x < Math.Max(before.Width, after.Width); x++)
                {
                    if (before.GetTerrain(x, y) == after.GetTerrain(x, y) && x < before.Width && x < after.Width && y < before.Height && y < after.Height) { continue; }
                    cells++; minX = Math.Min(x, minX); minY = Math.Min(y, minY); maxX = Math.Max(x, maxX); maxY = Math.Max(y, maxY);
                }
            }
            JObject a = JObject.Parse(before.CanonicalJson), b = JObject.Parse(after.CanonicalJson);
            JArray Changes(string field, string key)
            {
                var old = a[field].ToDictionary(item => key == null ? item["x"] + "," + item["y"] : (string)item[key], item => item);
                var next = b[field].ToDictionary(item => key == null ? item["x"] + "," + item["y"] : (string)item[key], item => item);
                return new JArray(old.Keys.Union(next.Keys).Where(id => !old.ContainsKey(id) || !next.ContainsKey(id) || !JToken.DeepEquals(old[id], next[id])).Select(id => new JObject
                { ["id"] = id, ["change"] = !old.ContainsKey(id) ? "added" : !next.ContainsKey(id) ? "removed" : "modified" }));
            }
            return new JObject
            {
                ["code"] = "Ok", ["revision"] = after.Revision, ["hash"] = after.SourceHash, ["changedCells"] = cells,
                ["affectedDisplay"] = cells == 0 ? null : new JObject { ["minX"] = minX - 1, ["minY"] = minY - 1, ["maxX"] = maxX, ["maxY"] = maxY },
                ["objects"] = Changes("objects", "objectId"), ["regions"] = Changes("regions", "regionId"), ["overrides"] = Changes("variantOverrides", null),
                ["entries"] = Changes("entries", "entryId"), ["bossSpawns"] = Changes("bossSpawns", "spawnId")
            };
        }

        public static string ApplyPatch(string path, string json)
        {
            return Run(() => MapEditSessions.Open(path).Apply(MapEditService.ReadPatch(json)));
        }

        public static string Save(string path)
        {
            return Run(() => { MapEditSession session = MapEditSessions.Open(path); session.Save(); return new JObject { ["code"] = "Ok", ["revision"] = session.Current.Revision, ["hash"] = session.Current.SourceHash }; });
        }

        public static string Export(string path, string destination)
        {
            return Run(() =>
            {
                if (Path.GetExtension(destination) != ".json" || File.Exists(destination)) { throw new MapEditException("Conflict", "$file", "AI 导出需要不存在的 .json 路径"); }
                MapEditFiles.Write(destination, MapEditSessions.Open(path).Current.CanonicalJson, null);
                return new JObject { ["code"] = "Ok", ["path"] = destination };
            });
        }

        public static string Import(string path, string json, int baseRevision, string baseHash)
        {
            return Run(() =>
            {
                MapEditSession session = MapEditSessions.Open(path);
                if (session.Current.Revision != baseRevision || session.Current.SourceHash != baseHash) { throw new MapEditException("Conflict", "$", "导入基线已过期"); }
                session.Import(json);
                return new JObject { ["code"] = "Ok", ["revision"] = session.Current.Revision, ["hash"] = session.Current.SourceHash };
            });
        }

        public static string Diagnose(string path, double startX, double startY, double endX, double endY, double radius = 0.36, int maxVisits = 262144)
        {
            return Run(() =>
            {
                CompiledMapDocument map = MapEditSessions.Open(path).Current;
                MapGeometrySnapshot geometry = MapGeometryCompiler.Compile(MapGeometryInput.Capture(map, MapEditorDefinitionIndex.Capture()));
                string code = MapEditConnectivity.Check(geometry, new MapGeometryPoint(startX, startY), new MapGeometryPoint(endX, endY), new MapGroundProfile(radius), maxVisits);
                return new JObject { ["code"] = code, ["hash"] = map.SourceHash, ["geometryHash"] = geometry.GeometryHash };
            });
        }

        static string Run(Func<JObject> action)
        {
            try { return action().ToString(); }
            catch (Exception error)
            {
                string code = error is MapEditException edit ? edit.Code : error is MapGeometryCapacityException ? "GeometryUnavailable" : error is OperationCanceledException ? "Cancelled" : error is IOException || error is UnauthorizedAccessException ? "IoFailure" : "InvalidDocument";
                return new JObject
                {
                    ["code"] = code, ["path"] = error is MapEditException e ? e.Path : error is MapDocumentException d ? d.Path : "$",
                    ["operationIndex"] = error is MapEditException operation ? operation.OperationIndex : -1,
                    ["message"] = error.Message, ["suggestion"] = code == "Conflict" ? "重新 ReadMap，检查磁盘变化后再生成请求" : "核对字段路径、目录和预算；工作副本不会半批提交"
                }.ToString();
            }
        }
    }

    public static class MapEditConnectivity
    {
        public static string Check(MapGeometrySnapshot geometry, MapGeometryPoint start, MapGeometryPoint end, MapGroundProfile profile, int maxVisits = 262144)
        {
            if (maxVisits < 1 || maxVisits > 262144) { return "BudgetExceeded"; }
            if (geometry.CheckPoint(start, profile) != MapGeometryResult.Valid || geometry.CheckPoint(end, profile) != MapGeometryResult.Valid) { return "NoLegalEndpoint"; }
            var queue = new Queue<(int X, int Y)>();
            var seen = new HashSet<(int X, int Y)>();
            int sx = (int)((start.X - geometry.Bounds.MinX) / geometry.NodeSize), sy = (int)((start.Y - geometry.Bounds.MinY) / geometry.NodeSize);
            int ex = (int)((end.X - geometry.Bounds.MinX) / geometry.NodeSize), ey = (int)((end.Y - geometry.Bounds.MinY) / geometry.NodeSize);
            if (geometry.CheckSegment(start, geometry.GetNode(sx, sy), profile) != MapGeometryResult.Valid || geometry.CheckSegment(end, geometry.GetNode(ex, ey), profile) != MapGeometryResult.Valid) { return "NoLegalEndpoint"; }
            queue.Enqueue((sx, sy)); seen.Add((sx, sy));
            int visits = 0;
            while (queue.Count > 0)
            {
                if (++visits > maxVisits) { return "BudgetExceeded"; }
                var cell = queue.Dequeue();
                if (cell == (ex, ey)) { return "Connected"; }
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var next = (cell.X + dx, cell.Y + dy);
                        if (seen.Contains(next) || (dx == 0 && dy == 0)) { continue; }
                        MapGeometryResult result = geometry.CheckEdge(cell.X, cell.Y, next.Item1, next.Item2, profile, out _);
                        if (result == MapGeometryResult.BudgetExceeded) { return "BudgetExceeded"; }
                        if (result == MapGeometryResult.Valid) { seen.Add(next); queue.Enqueue(next); }
                    }
                }
            }
            return "Disconnected";
        }
    }
}
