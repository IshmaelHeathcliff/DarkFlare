using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkFlare.Editor
{
    public sealed class MapEditException : Exception
    {
        public string Code { get; }
        public string Path { get; }
        public int OperationIndex { get; }

        public MapEditException(string code, string path, string message, int operationIndex = -1) : base(message)
        {
            Code = code;
            Path = path;
            OperationIndex = operationIndex;
        }
    }

    // 无窗口、文件或 Undo 依赖；所有调用方在候选校验成功后才发布。
    public static class MapEditService
    {
        public const int MaxPatchBytes = 1048576;
        public const int MaxOperations = 256;
        public const int MaxVisits = 262144;
        static readonly Dictionary<string, string[]> Fields = new()
        {
            ["PaintCells"] = new[] { "cells", "terrainId" },
            ["FillRect"] = new[] { "x", "y", "width", "height", "terrainId" },
            ["PaintLine"] = new[] { "x", "y", "endX", "endY", "terrainId" },
            ["FillRegion"] = new[] { "x", "y", "terrainId" },
            ["SetVariant"] = new[] { "x", "y", "variantId" },
            ["UpsertObject"] = new[] { "record" }, ["RemoveObject"] = new[] { "id" },
            ["UpsertRegion"] = new[] { "record" }, ["RemoveRegion"] = new[] { "id" },
            ["SetMapMetadata"] = new[] { "values" }
        };

        public static JObject ReadPatch(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxPatchBytes)
            {
                throw new MapEditException("BudgetExceeded", "$", "Patch 超过 1 MiB");
            }
            try
            {
                using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 16, DateParseHandling = DateParseHandling.None };
                var patch = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) { throw new JsonReaderException("多余 JSON 内容"); }
                RequireFields(patch, new[] { "patchVersion", "mapId", "baseRevision", "baseHash", "operationId", "operations", "clearAffectedOverrides" });
                if (Integer(patch, "patchVersion") != 1) { throw Invalid("$.patchVersion", "不支持的 Patch 版本"); }
                Integer(patch, "baseRevision");
                Text(patch, "mapId"); Text(patch, "baseHash"); Text(patch, "operationId");
                if (patch["clearAffectedOverrides"]?.Type != JTokenType.Boolean) { throw Invalid("$.clearAffectedOverrides", "需要显式布尔值"); }
                if (patch["operations"] is not JArray operations || operations.Count > MaxOperations)
                {
                    throw new MapEditException("BudgetExceeded", "$.operations", "需要操作数组，最多 256 项");
                }
                foreach (JToken item in operations)
                {
                    if (item is not JObject operation) { throw Invalid(item.Path, "操作必须是对象"); }
                    string type = Text(operation, "type");
                    if (!Fields.TryGetValue(type, out string[] fields)) { throw Invalid(operation.Path, "未知操作 " + type); }
                    RequireFields(operation, fields.Concat(new[] { "type" }).ToArray());
                }
                return patch;
            }
            catch (JsonException error) { throw Invalid("$", error.Message); }
        }

        public static JObject Patch(CompiledMapDocument map, params JObject[] operations)
        {
            return new JObject
            {
                ["patchVersion"] = 1, ["mapId"] = map.MapId, ["baseRevision"] = map.Revision,
                ["baseHash"] = map.SourceHash, ["operationId"] = Guid.NewGuid().ToString("N"),
                ["clearAffectedOverrides"] = false, ["operations"] = new JArray(operations)
            };
        }

        public static CompiledMapDocument Preview(CompiledMapDocument current, JObject request, IMapDefinitionLookup definitions, CancellationToken token = default)
        {
            JObject patch = ReadPatch(request.ToString(Formatting.None));
            if (Text(patch, "mapId") != current.MapId || Integer(patch, "baseRevision") != current.Revision || Text(patch, "baseHash") != current.SourceHash)
            {
                throw new MapEditException("Conflict", "$", "工作副本已改变，请重新读取地图");
            }
            MapDocument document = current.CreateEditableCopy();
            var chunks = document.Chunks.ToDictionary(chunk => (chunk.X, chunk.Y));
            var overrides = document.VariantOverrides.ToDictionary(item => (item.X, item.Y));
            int visits = 0;
            int index = -1;
            bool finalValidation = false;
            void Visit()
            {
                token.ThrowIfCancellationRequested();
                if (++visits > MaxVisits) { throw new MapEditException("BudgetExceeded", "$.operations", "访问预算耗尽，拆分批次", index); }
            }
            void CheckCell(int x, int y)
            {
                if (x < 0 || y < 0 || x >= document.Width || y >= document.Height) { throw Invalid("$.operations", $"逻辑格 ({x},{y}) 越界"); }
            }
            int Get(int x, int y)
            {
                return chunks.TryGetValue((x / 32, y / 32), out MapChunk chunk) ? chunk.Cells[x % 32 + y % 32 * 32] : Array.IndexOf(document.TerrainPalette, document.DefaultTerrainId);
            }
            void Paint(int x, int y, int terrain)
            {
                Visit(); CheckCell(x, y);
                if (Get(x, y) == terrain) { return; }
                if (!chunks.TryGetValue((x / 32, y / 32), out MapChunk chunk))
                {
                    chunk = new MapChunk { X = x / 32, Y = y / 32, Cells = Enumerable.Repeat(Array.IndexOf(document.TerrainPalette, document.DefaultTerrainId), 1024).ToArray() };
                    chunks.Add((chunk.X, chunk.Y), chunk);
                }
                chunk.Cells[x % 32 + y % 32 * 32] = terrain;
                if ((bool)patch["clearAffectedOverrides"])
                {
                    for (int dy = -1; dy <= 0; dy++)
                    {
                        for (int dx = -1; dx <= 0; dx++) { overrides.Remove((x + dx, y + dy)); }
                    }
                }
            }
            try
            {
                foreach (JObject op in (JArray)patch["operations"])
                {
                    index++; Visit();
                    string type = Text(op, "type");
                    int terrain = -1;
                    if (op.ContainsKey("terrainId"))
                    {
                        terrain = Array.IndexOf(document.TerrainPalette, Text(op, "terrainId"));
                        if (terrain < 0) { throw Invalid(op.Path + ".terrainId", "地表不在 palette 中"); }
                    }
                    switch (type)
                    {
                        case "PaintCells":
                            if (op["cells"] is not JArray cells) { throw Invalid(op.Path, "cells 必须是数组"); }
                            foreach (JToken cell in cells)
                            {
                                if (cell is not JObject point) { throw Invalid(cell.Path, "格子需要 x / y"); }
                                RequireFields(point, new[] { "x", "y" });
                                Paint(Integer(point, "x"), Integer(point, "y"), terrain);
                            }
                            break;
                        case "FillRect":
                            int x = Integer(op, "x"), y = Integer(op, "y"), width = Integer(op, "width"), height = Integer(op, "height");
                            if (width < 1 || height < 1 || (long)width * height > MaxVisits - visits) { throw new MapEditException("BudgetExceeded", op.Path, "矩形尺寸或访问预算无效", index); }
                            CheckCell(x, y); CheckCell(checked(x + width - 1), checked(y + height - 1));
                            for (int row = y; row < y + height; row++)
                            {
                                for (int col = x; col < x + width; col++) { Paint(col, row, terrain); }
                            }
                            break;
                        case "PaintLine":
                            int ax = Integer(op, "x"), ay = Integer(op, "y"), bx = Integer(op, "endX"), by = Integer(op, "endY");
                            CheckCell(ax, ay); CheckCell(bx, by);
                            foreach (var cell in Line(ax, ay, bx, by)) { Paint(cell.X, cell.Y, terrain); }
                            break;
                        case "FillRegion":
                            int fx = Integer(op, "x"), fy = Integer(op, "y");
                            CheckCell(fx, fy);
                            int original = Get(fx, fy);
                            if (original == terrain) { break; }
                            var queue = new Queue<(int X, int Y)>();
                            var seen = new HashSet<(int X, int Y)>();
                            queue.Enqueue((fx, fy)); seen.Add((fx, fy));
                            while (queue.Count > 0)
                            {
                                var cell = queue.Dequeue();
                                Paint(cell.X, cell.Y, terrain);
                                foreach (var next in new[] { (cell.X - 1, cell.Y), (cell.X + 1, cell.Y), (cell.X, cell.Y - 1), (cell.X, cell.Y + 1) })
                                {
                                    if (next.Item1 >= 0 && next.Item2 >= 0 && next.Item1 < document.Width && next.Item2 < document.Height && seen.Add(next))
                                    {
                                        Visit();
                                        if (Get(next.Item1, next.Item2) == original) { queue.Enqueue(next); }
                                    }
                                }
                            }
                            break;
                        case "SetVariant":
                            int vx = Integer(op, "x"), vy = Integer(op, "y");
                            if (vx < -1 || vy < -1 || vx >= document.Width || vy >= document.Height) { throw Invalid(op.Path, "显示格越界"); }
                            if (op["variantId"].Type == JTokenType.Null) { overrides.Remove((vx, vy)); }
                            else { overrides[(vx, vy)] = new MapVariantOverride { X = vx, Y = vy, VariantId = Text(op, "variantId") }; }
                            break;
                        case "UpsertObject":
                            MapObjectRecord obj = ReadRecord<MapObjectRecord>(document, "objects", op["record"]);
                            document.Objects = document.Objects.Where(item => item.ObjectId != obj.ObjectId).Append(obj).ToArray();
                            break;
                        case "RemoveObject": document.Objects = document.Objects.Where(item => item.ObjectId != Text(op, "id")).ToArray(); break;
                        case "UpsertRegion":
                            MapRegionRecord region = ReadRecord<MapRegionRecord>(document, "regions", op["record"]);
                            document.Regions = document.Regions.Where(item => item.RegionId != region.RegionId).Append(region).ToArray();
                            break;
                        case "RemoveRegion": document.Regions = document.Regions.Where(item => item.RegionId != Text(op, "id")).ToArray(); break;
                        case "SetMapMetadata":
                            if (op["values"] is not JObject values) { throw Invalid(op.Path, "values 必须是对象"); }
                            string[] allowed = { "seed", "origin", "cellSize", "width", "height" };
                            if (values.Properties().Any(property => !allowed.Contains(property.Name))) { throw Invalid(op.Path, "元数据只允许 seed / origin / cellSize / width / height；缩图不裁剪数据"); }
                            JObject raw = JObject.Parse(MapDocumentJson.Write(document));
                            foreach (JProperty property in values.Properties()) { raw[property.Name] = property.Value.DeepClone(); }
                            MapDocument header = MapDocumentJson.Read(raw.ToString());
                            document.Seed = header.Seed; document.Origin = header.Origin; document.CellSize = header.CellSize;
                            document.Width = header.Width; document.Height = header.Height;
                            break;
                    }
                }
                document.Chunks = chunks.Values.Where(chunk => chunk.Cells.Any(value => value != Array.IndexOf(document.TerrainPalette, document.DefaultTerrainId))).ToArray();
                document.VariantOverrides = overrides.Values.ToArray();
                token.ThrowIfCancellationRequested();
                finalValidation = true;
                CompiledMapDocument candidate = MapDocumentCompiler.Compile(document, definitions);
                if (SemanticHash(candidate) == SemanticHash(current)) { return current; }
                document.Revision = checked(current.Revision + 1);
                return MapDocumentCompiler.Compile(document, definitions);
            }
            catch (MapEditException error) { throw new MapEditException(error.Code, error.Path, error.Message, index); }
            catch (MapDocumentException error) { throw new MapEditException("InvalidDocument", error.Path, error.Message, finalValidation ? -1 : index); }
            catch (OverflowException) { throw Invalid("$.operations", "数值溢出或修订耗尽"); }
        }

        static T ReadRecord<T>(MapDocument document, string field, JToken record)
        {
            // 只借用 DTO 结构校验单条记录；不能为每条对象操作重复序列化整张地图。
            var header = new MapDocument
            {
                MapId = document.MapId, Width = document.Width, Height = document.Height,
                TerrainSetId = document.TerrainSetId, TerrainPalette = document.TerrainPalette,
                DefaultTerrainId = document.DefaultTerrainId
            };
            JObject raw = JObject.Parse(MapDocumentJson.Write(header));
            raw[field] = new JArray(record.DeepClone());
            MapDocument parsed = MapDocumentJson.Read(raw.ToString());
            return (T)(object)(field == "objects" ? (object)parsed.Objects[0] : parsed.Regions[0]);
        }

        public static IEnumerable<(int X, int Y)> Line(int x, int y, int endX, int endY)
        {
            int dx = Math.Abs(endX - x), dy = -Math.Abs(endY - y), sx = x < endX ? 1 : -1, sy = y < endY ? 1 : -1, error = dx + dy;
            while (true)
            {
                yield return (x, y);
                if (x == endX && y == endY) { yield break; }
                int twice = 2 * error;
                if (twice >= dy) { error += dy; x += sx; }
                if (twice <= dx) { error += dx; y += sy; }
            }
        }

        public static string SemanticHash(CompiledMapDocument map)
        {
            MapDocument copy = map.CreateEditableCopy();
            copy.Revision = 0;
            int defaultIndex = Array.IndexOf(copy.TerrainPalette, copy.DefaultTerrainId);
            copy.Chunks = copy.Chunks.Where(chunk => chunk.Cells.Any(cell => cell != defaultIndex)).ToArray();
            return Hash(MapDocumentJson.Write(copy));
        }

        public static string Hash(string text)
        {
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        public static int Integer(JObject value, string key)
        {
            if (value[key]?.Type != JTokenType.Integer || !int.TryParse(value[key].ToString(), out int result)) { throw Invalid(value.Path + "." + key, "需要 Int32 整数"); }
            return result;
        }

        public static string Text(JObject value, string key)
        {
            if (value[key]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)value[key]) || ((string)value[key]).Length > 256) { throw Invalid(value.Path + "." + key, "需要非空字符串，最多 256 字符"); }
            return (string)value[key];
        }

        static void RequireFields(JObject value, string[] fields)
        {
            if (value.Properties().Any(property => !fields.Contains(property.Name)) || fields.Any(field => !value.ContainsKey(field))) { throw Invalid(value.Path, "字段缺失或存在未知字段；请读取 DescribeSchema"); }
        }

        public static MapEditException Invalid(string path, string message)
        {
            return new MapEditException("InvalidDocument", path, message);
        }

        public static JObject DescribeSchema()
        {
            JObject documentSchema = JObject.Parse(MapDocumentJson.CreateSchema());
            JObject Number() { return new JObject { ["type"] = "integer", ["minimum"] = int.MinValue, ["maximum"] = int.MaxValue }; }
            JObject String() { return new JObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 256 }; }
            JObject Point() { return new JObject { ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JArray("x", "y"), ["properties"] = new JObject { ["x"] = Number(), ["y"] = Number() } }; }
            var variants = new JArray();
            foreach (var entry in Fields)
            {
                var fields = new JObject { ["type"] = new JObject { ["const"] = entry.Key } };
                foreach (string field in entry.Value)
                {
                    if (field == "cells") { fields[field] = new JObject { ["type"] = "array", ["maxItems"] = MaxVisits, ["items"] = Point() }; }
                    else if (field == "record") { fields[field] = documentSchema["properties"][entry.Key == "UpsertObject" ? "objects" : "regions"]["items"].DeepClone(); }
                    else if (field == "values")
                    {
                        var properties = new JObject();
                        foreach (string name in new[] { "seed", "origin", "cellSize", "width", "height" }) { properties[name] = documentSchema["properties"][name].DeepClone(); }
                        fields[field] = new JObject { ["type"] = "object", ["additionalProperties"] = false, ["properties"] = properties };
                    }
                    else if (field == "variantId") { fields[field] = new JObject { ["type"] = new JArray("string", "null"), ["minLength"] = 1, ["maxLength"] = 256 }; }
                    else { fields[field] = field == "terrainId" || field == "id" ? String() : Number(); }
                }
                variants.Add(new JObject { ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JArray(fields.Properties().Select(p => p.Name)), ["properties"] = fields });
            }
            return new JObject
            {
                ["$schema"] = "https://json-schema.org/draft/2020-12/schema", ["type"] = "object", ["additionalProperties"] = false,
                ["required"] = new JArray("patchVersion", "mapId", "baseRevision", "baseHash", "operationId", "operations", "clearAffectedOverrides"),
                ["properties"] = new JObject
                {
                    ["patchVersion"] = new JObject { ["const"] = 1 }, ["mapId"] = String(), ["baseRevision"] = Number(), ["baseHash"] = String(), ["operationId"] = String(),
                    ["clearAffectedOverrides"] = new JObject { ["type"] = "boolean" }, ["operations"] = new JObject { ["type"] = "array", ["maxItems"] = MaxOperations, ["items"] = new JObject { ["oneOf"] = variants } }
                },
                ["patchVersion"] = 1, ["maxBytes"] = MaxPatchBytes, ["maxOperations"] = MaxOperations, ["maxVisits"] = MaxVisits,
                ["requiredEnvelope"] = new JArray("patchVersion", "mapId", "baseRevision", "baseHash", "operationId", "operations", "clearAffectedOverrides"),
                ["operations"] = JObject.FromObject(Fields),
                ["recordSchema"] = documentSchema,
                ["coordinates"] = "地表：逻辑格整数；变体：显示格 -1..size-1；对象：局部世界单位 / 角度 0/90/180/270；区域：逻辑格单位；FillRegion：四邻接同材质洪水填充",
                ["metadata"] = new JArray("seed", "origin", "cellSize", "width", "height"),
                ["saving"] = "Apply 只修改工作副本；Save 显式落盘；移除不存在 ID / 相同值为 no-op"
            };
        }
    }
}
