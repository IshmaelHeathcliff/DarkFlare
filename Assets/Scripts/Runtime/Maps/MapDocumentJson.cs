using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace DarkFlare
{
    public static class MapDocumentLimits
    {
        public const int MaxBytes = 8 * 1024 * 1024;
        public const int MaxDepth = 16;
        public const int MaxSide = 1024;
        public const int MaxCells = 1024 * 1024;
        public const int ChunkSide = 32;
        public const int ChunkCells = ChunkSide * ChunkSide;
        public const int MaxPalette = 64;
        public const int MaxObjects = 16384;
        public const int MaxRegions = 1024;
        public const int MaxPolygonPoints = 64;
        public const int MaxStringLength = 256;
        public const double MaxWorldCoordinate = 1000000;
    }

    public sealed class MapDocumentException : Exception
    {
        public string Path { get; }

        public MapDocumentException(string path, string message) : base($"{path}: {message}")
        {
            Path = path;
        }
    }

    public static class MapDocumentJson
    {
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static MapDocument Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length > MapDocumentLimits.MaxBytes)
            {
                throw new MapDocumentException("$", "文件为空或超过 8 MiB");
            }
            try
            {
                int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
                return Read(StrictUtf8.GetString(bytes, start, bytes.Length - start));
            }
            catch (DecoderFallbackException)
            {
                throw new MapDocumentException("$", "必须是有效 UTF-8");
            }
        }

        public static MapDocument Read(string json)
        {
            if (json == null || json.Length > MapDocumentLimits.MaxBytes || StrictUtf8.GetByteCount(json) > MapDocumentLimits.MaxBytes)
            {
                throw new MapDocumentException("$", "文件为空或超过 8 MiB");
            }
            try
            {
                using var reader = new JsonTextReader(new StringReader(json))
                {
                    MaxDepth = MapDocumentLimits.MaxDepth,
                    DateParseHandling = DateParseHandling.None
                };
                JToken token = JToken.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    CommentHandling = CommentHandling.Load
                });
                if (reader.Read()) { throw new MapDocumentException("$", "文档末尾存在额外内容"); }
                CheckShape(token, typeof(MapDocument), "$", false);
                return token.ToObject<MapDocument>(CreateSerializer());
            }
            catch (JsonException error)
            {
                throw new MapDocumentException("$", error.Message);
            }
        }

        // 所有属性按名称排序；有序 palette 保留顺序，语义集合按稳定身份排序。
        public static string Write(MapDocument document)
        {
            if (document == null) { throw new ArgumentNullException(nameof(document)); }
            JToken token = JToken.FromObject(document, CreateSerializer());
            CheckShape(token, typeof(MapDocument), "$", false);
            var root = (JObject)token;
            SortArray(root, "chunks", item => ((int)item["y"]).ToString("D8") + ((int)item["x"]).ToString("D8"));
            SortArray(root, "variantOverrides", item => ((int)item["y"]).ToString("D8") + ((int)item["x"]).ToString("D8"));
            SortArray(root, "objects", item => (string)item["objectId"]);
            SortArray(root, "regions", item => (string)item["regionId"]);
            using var output = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
            using var writer = new JsonTextWriter(output) { Formatting = Formatting.Indented, Indentation = 2 };
            OrderProperties(root).WriteTo(writer);
            writer.Flush();
            string json = output + "\n";
            if (StrictUtf8.GetByteCount(json) > MapDocumentLimits.MaxBytes)
            {
                throw new MapDocumentException("$", "规范导出超过 8 MiB");
            }
            return json;
        }

        public static byte[] Encode(string json)
        {
            return StrictUtf8.GetBytes(json);
        }

        // 与读取器共享 DTO 属性、枚举及集合上限；几何与内容引用仍由 Compile 校验。
        public static string CreateSchema()
        {
            JObject schema = DescribeType(typeof(MapDocument), "$", false);
            schema.AddFirst(new JProperty("$schema", "https://json-schema.org/draft/2020-12/schema"));
            schema.Add("description", "DarkFlare 地图 V1 结构合同；须继续运行 MapDocumentCompiler 校验内容引用、坐标、地表与几何语义。");
            return schema.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
        }

        static JObject DescribeType(Type type, string path, bool allowNull)
        {
            var schema = new JObject();
            string kind = type == typeof(string) || type.IsEnum ? "string" : type == typeof(int) ? "integer"
                : type == typeof(double) ? "number" : type.IsArray ? "array" : "object";
            schema["type"] = allowNull ? new JArray(kind, "null") : new JValue(kind);
            if (type == typeof(string)) { schema["maxLength"] = MapDocumentLimits.MaxStringLength; }
            else if (type == typeof(int)) { schema["minimum"] = int.MinValue; schema["maximum"] = int.MaxValue; }
            else if (type.IsEnum) { schema["enum"] = new JArray(Enum.GetNames(type)); }
            else if (type.IsArray)
            {
                schema["maxItems"] = ArrayLimit(path);
                schema["items"] = DescribeType(type.GetElementType(), path + "[]", false);
            }
            else if (kind == "object")
            {
                var properties = new JObject();
                var required = new JArray();
                foreach (PropertyInfo property in type.GetProperties())
                {
                    JsonPropertyAttribute attribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                    if (attribute == null) { continue; }
                    properties[attribute.PropertyName] = DescribeType(property.PropertyType, path + "." + attribute.PropertyName, attribute.Required == Required.AllowNull);
                    required.Add(attribute.PropertyName);
                }
                schema["properties"] = properties;
                schema["required"] = required;
                schema["additionalProperties"] = false;
            }
            return schema;
        }

        static JsonSerializer CreateSerializer()
        {
            var serializer = new JsonSerializer
            {
                MissingMemberHandling = MissingMemberHandling.Error,
                TypeNameHandling = TypeNameHandling.None,
                Culture = CultureInfo.InvariantCulture,
                MaxDepth = MapDocumentLimits.MaxDepth
            };
            serializer.Converters.Add(new StringEnumConverter { AllowIntegerValues = false });
            return serializer;
        }

        static void CheckShape(JToken token, Type type, string path, bool allowNull)
        {
            if (token.Type == JTokenType.Null && allowNull) { return; }
            if (type == typeof(string))
            {
                Require(token.Type == JTokenType.String && ((string)token).Length <= MapDocumentLimits.MaxStringLength, path, "必须是长度不超过 256 的字符串");
            }
            else if (type == typeof(int))
            {
                Require(token.Type == JTokenType.Integer && long.TryParse(token.ToString(), out long value) && value >= int.MinValue && value <= int.MaxValue, path, "必须是 Int32 整数");
            }
            else if (type == typeof(double))
            {
                Require((token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                    && double.TryParse(token.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    && !double.IsNaN(value) && !double.IsInfinity(value), path, "必须是有限数值");
            }
            else if (type.IsEnum)
            {
                Require(token.Type == JTokenType.String && Enum.GetNames(type).Contains((string)token), path, "未知枚举名称");
            }
            else if (type.IsArray)
            {
                Require(token is JArray && token.Count() <= ArrayLimit(path), path, "集合类型错误或容量超限");
                int index = 0;
                foreach (JToken child in token)
                {
                    CheckShape(child, type.GetElementType(), $"{path}[{index++}]", false);
                }
            }
            else
            {
                Require(token is JObject, path, "必须是对象");
                PropertyInfo[] properties = type.GetProperties().Where(property => property.GetCustomAttribute<JsonPropertyAttribute>() != null).ToArray();
                foreach (JToken child in token.Children())
                {
                    Require(child is JProperty, path, "不允许注释");
                    var property = (JProperty)child;
                    Require(properties.Any(info => info.GetCustomAttribute<JsonPropertyAttribute>().PropertyName == property.Name), path + "." + property.Name, "未知字段");
                }
                foreach (PropertyInfo property in properties)
                {
                    JsonPropertyAttribute attribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                    JToken child = token[attribute.PropertyName];
                    Require(child != null, path + "." + attribute.PropertyName, "缺少必填字段");
                    CheckShape(child, property.PropertyType, path + "." + attribute.PropertyName, attribute.Required == Required.AllowNull);
                }
            }
        }

        static int ArrayLimit(string path)
        {
            if (path.EndsWith(".terrainPalette", StringComparison.Ordinal)) { return MapDocumentLimits.MaxPalette; }
            if (path.EndsWith(".chunks", StringComparison.Ordinal)) { return MapDocumentLimits.MaxCells / MapDocumentLimits.ChunkCells; }
            if (path.EndsWith(".cells", StringComparison.Ordinal)) { return MapDocumentLimits.ChunkCells; }
            if (path.EndsWith(".objects", StringComparison.Ordinal)) { return MapDocumentLimits.MaxObjects; }
            if (path.EndsWith(".regions", StringComparison.Ordinal)) { return MapDocumentLimits.MaxRegions; }
            if (path.EndsWith(".points", StringComparison.Ordinal)) { return MapDocumentLimits.MaxPolygonPoints; }
            return (MapDocumentLimits.MaxSide + 1) * (MapDocumentLimits.MaxSide + 1);
        }

        static void SortArray(JObject root, string name, Func<JToken, string> key)
        {
            root[name] = new JArray(root[name].OrderBy(key, StringComparer.Ordinal));
        }

        static JToken OrderProperties(JToken token)
        {
            if (token is JObject obj)
            {
                return new JObject(obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => new JProperty(property.Name, OrderProperties(property.Value))));
            }
            if (token is JArray array) { return new JArray(array.Select(OrderProperties)); }
            return token.DeepClone();
        }

        internal static void Require(bool condition, string path, string message)
        {
            if (!condition) { throw new MapDocumentException(path, message); }
        }
    }
}
