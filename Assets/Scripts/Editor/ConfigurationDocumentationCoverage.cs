using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DarkFlare.Editor
{
    [Serializable]
    public sealed class ConfigurationDocumentationManifest
    {
        [SerializeField]
        int schemaVersion;

        [SerializeField]
        List<ConfigurationDocumentationManifestEntry> entries = new List<ConfigurationDocumentationManifestEntry>();

        public int SchemaVersion => schemaVersion;

        public IReadOnlyList<ConfigurationDocumentationManifestEntry> Entries => entries;
    }

    [Serializable]
    public sealed class ConfigurationDocumentationManifestEntry
    {
        [SerializeField]
        string type = string.Empty;

        [SerializeField]
        string document = string.Empty;

        [SerializeField]
        string anchor = string.Empty;

        public string TypeName => type;

        public string DocumentPath => document;

        public string Anchor => anchor;
    }

    public sealed class ConfigurationDocumentationCoverageIssue
    {
        public string Code { get; }

        public string TypeName { get; }

        public string FieldName { get; }

        public string DocumentPath { get; }

        public string Message { get; }

        public ConfigurationDocumentationCoverageIssue(
            string code,
            string typeName,
            string fieldName,
            string documentPath,
            string message)
        {
            Code = code;
            TypeName = typeName ?? string.Empty;
            FieldName = fieldName ?? string.Empty;
            DocumentPath = documentPath ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public sealed class ConfigurationDocumentationCoverageReport
    {
        public int TypeCount { get; }

        public int SerializedFieldCount { get; }

        public int CoveredFieldCount { get; }

        public IReadOnlyList<ConfigurationDocumentationCoverageIssue> Issues { get; }

        public bool Passed => Issues.Count == 0 && CoveredFieldCount == SerializedFieldCount;

        public ConfigurationDocumentationCoverageReport(
            int typeCount,
            int serializedFieldCount,
            int coveredFieldCount,
            IReadOnlyList<ConfigurationDocumentationCoverageIssue> issues)
        {
            TypeCount = typeCount;
            SerializedFieldCount = serializedFieldCount;
            CoveredFieldCount = coveredFieldCount;
            Issues = issues;
        }
    }

    public static class ConfigurationDocumentationCoverage
    {
        public const string ManifestPath = "Docs/docs/config-reference/coverage-manifest.json";
        public const int CurrentSchemaVersion = 1;

        static readonly Regex FieldRowPattern = new Regex(
            @"^\|\s*`(?<field>_[A-Za-z0-9]+)`\s*\|",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static ConfigurationDocumentationCoverageReport ScanOfficial()
        {
            IReadOnlyList<Type> topLevelTypes = ConfigurationTypeDiscovery.FindTopLevelTypes();
            IReadOnlyList<Type> nestedTypes = ConfigurationTypeDiscovery.FindNestedSerializedTypes(topLevelTypes);
            Type[] expectedTypes = topLevelTypes
                .Concat(nestedTypes)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            if (!File.Exists(ManifestPath))
            {
                List<ConfigurationDocumentationCoverageIssue> issues = new List<ConfigurationDocumentationCoverageIssue>
                {
                    new ConfigurationDocumentationCoverageIssue(
                        "manifest-missing",
                        string.Empty,
                        string.Empty,
                        ManifestPath,
                        "配置文档覆盖 manifest 不存在"),
                };
                return CreateReport(expectedTypes, 0, issues);
            }

            string manifestJson = File.ReadAllText(ManifestPath, Encoding.UTF8);
            return Scan(expectedTypes, manifestJson, ReadDocument);
        }

        public static ConfigurationDocumentationCoverageReport Scan(
            IReadOnlyList<Type> expectedTypes,
            string manifestJson,
            Func<string, string> documentLoader)
        {
            Type[] types = expectedTypes == null
                ? Array.Empty<Type>()
                : expectedTypes
                    .Where(type => type != null)
                    .OrderBy(type => type.FullName, StringComparer.Ordinal)
                    .ToArray();
            List<ConfigurationDocumentationCoverageIssue> issues = new List<ConfigurationDocumentationCoverageIssue>();
            ConfigurationDocumentationManifest manifest;

            try
            {
                manifest = JsonUtility.FromJson<ConfigurationDocumentationManifest>(manifestJson ?? string.Empty);
            }
            catch (Exception exception)
            {
                issues.Add(new ConfigurationDocumentationCoverageIssue(
                    "manifest-invalid",
                    string.Empty,
                    string.Empty,
                    ManifestPath,
                    $"manifest JSON 无法解析：{exception.Message}"));
                return CreateReport(types, 0, issues);
            }

            if (manifest == null)
            {
                issues.Add(new ConfigurationDocumentationCoverageIssue(
                    "manifest-invalid",
                    string.Empty,
                    string.Empty,
                    ManifestPath,
                    "manifest JSON 为空"));
                return CreateReport(types, 0, issues);
            }

            if (manifest.SchemaVersion != CurrentSchemaVersion)
            {
                issues.Add(new ConfigurationDocumentationCoverageIssue(
                    "manifest-schema",
                    string.Empty,
                    string.Empty,
                    ManifestPath,
                    $"schemaVersion 应为 {CurrentSchemaVersion}，当前为 {manifest.SchemaVersion}"));
            }

            Dictionary<string, Type> expectedByName = types.ToDictionary(
                type => type.FullName,
                type => type,
                StringComparer.Ordinal);
            Dictionary<string, ConfigurationDocumentationManifestEntry> mappings =
                new Dictionary<string, ConfigurationDocumentationManifestEntry>(StringComparer.Ordinal);

            for (int i = 0; i < manifest.Entries.Count; i++)
            {
                ConfigurationDocumentationManifestEntry entry = manifest.Entries[i];

                if (entry == null || string.IsNullOrWhiteSpace(entry.TypeName))
                {
                    issues.Add(new ConfigurationDocumentationCoverageIssue(
                        "mapping-invalid",
                        string.Empty,
                        string.Empty,
                        ManifestPath,
                        $"manifest 条目 {i} 缺少完整类型名"));
                    continue;
                }

                if (!expectedByName.ContainsKey(entry.TypeName))
                {
                    issues.Add(new ConfigurationDocumentationCoverageIssue(
                        "mapping-orphan",
                        entry.TypeName,
                        string.Empty,
                        entry.DocumentPath,
                        "manifest 登记了当前配置发现入口不可达的孤立类型"));
                }

                if (!mappings.TryAdd(entry.TypeName, entry))
                {
                    issues.Add(new ConfigurationDocumentationCoverageIssue(
                        "mapping-duplicate",
                        entry.TypeName,
                        string.Empty,
                        entry.DocumentPath,
                        "同一类型存在重复文档映射"));
                }
            }

            int coveredFieldCount = 0;

            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                Type type = types[typeIndex];

                if (!mappings.TryGetValue(type.FullName, out ConfigurationDocumentationManifestEntry entry))
                {
                    issues.Add(new ConfigurationDocumentationCoverageIssue(
                        "mapping-missing",
                        type.FullName,
                        string.Empty,
                        ManifestPath,
                        "当前配置类型没有文档映射"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.DocumentPath)
                    || string.IsNullOrWhiteSpace(entry.Anchor))
                {
                    issues.Add(new ConfigurationDocumentationCoverageIssue(
                        "mapping-invalid",
                        type.FullName,
                        string.Empty,
                        entry.DocumentPath,
                        "类型映射必须同时声明 document 与 anchor"));
                    continue;
                }

                string markdown;

                try
                {
                    markdown = documentLoader != null ? documentLoader(entry.DocumentPath) : null;
                }
                catch (Exception exception)
                {
                    issues.Add(new ConfigurationDocumentationCoverageIssue(
                        "document-missing",
                        type.FullName,
                        string.Empty,
                        entry.DocumentPath,
                        $"无法读取映射文档：{exception.Message}"));
                    continue;
                }

                if (markdown == null)
                {
                    issues.Add(new ConfigurationDocumentationCoverageIssue(
                        "document-missing",
                        type.FullName,
                        string.Empty,
                        entry.DocumentPath,
                        "映射文档不存在"));
                    continue;
                }

                List<string> sections = FindSections(markdown, entry.Anchor);

                if (sections.Count == 0)
                {
                    issues.Add(new ConfigurationDocumentationCoverageIssue(
                        "heading-missing",
                        type.FullName,
                        string.Empty,
                        entry.DocumentPath,
                        $"找不到二级标题锚点：{entry.Anchor}"));
                    continue;
                }

                if (sections.Count > 1)
                {
                    issues.Add(new ConfigurationDocumentationCoverageIssue(
                        "heading-duplicate",
                        type.FullName,
                        string.Empty,
                        entry.DocumentPath,
                        $"二级标题锚点重复：{entry.Anchor}"));
                    continue;
                }

                IReadOnlyList<FieldInfo> fields = ConfigurationTypeDiscovery.FindSerializedFields(type);
                HashSet<string> expectedFields = new HashSet<string>(
                    fields.Select(field => field.Name),
                    StringComparer.Ordinal);
                Dictionary<string, int> documentedFields = ParseFieldRows(sections[0]);

                foreach (KeyValuePair<string, int> pair in documentedFields)
                {
                    if (pair.Value > 1)
                    {
                        issues.Add(new ConfigurationDocumentationCoverageIssue(
                            "field-duplicate",
                            type.FullName,
                            pair.Key,
                            entry.DocumentPath,
                            "同一类型章节重复登记字段"));
                    }

                    if (!expectedFields.Contains(pair.Key))
                    {
                        issues.Add(new ConfigurationDocumentationCoverageIssue(
                            "field-stale",
                            type.FullName,
                            pair.Key,
                            entry.DocumentPath,
                            "文档字段已不在当前 Unity 序列化结构中"));
                    }
                }

                for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    string fieldName = fields[fieldIndex].Name;

                    if (!documentedFields.ContainsKey(fieldName))
                    {
                        issues.Add(new ConfigurationDocumentationCoverageIssue(
                            "field-missing",
                            type.FullName,
                            fieldName,
                            entry.DocumentPath,
                            "当前 Unity 序列化字段缺少独立表格行"));
                        continue;
                    }

                    coveredFieldCount++;
                }
            }

            return CreateReport(types, coveredFieldCount, issues);
        }

        static ConfigurationDocumentationCoverageReport CreateReport(
            IReadOnlyList<Type> types,
            int coveredFieldCount,
            List<ConfigurationDocumentationCoverageIssue> issues)
        {
            int fieldCount = ConfigurationTypeDiscovery.CountSerializedFields(types);
            ConfigurationDocumentationCoverageIssue[] orderedIssues = issues
                .OrderBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.TypeName, StringComparer.Ordinal)
                .ThenBy(issue => issue.FieldName, StringComparer.Ordinal)
                .ThenBy(issue => issue.DocumentPath, StringComparer.Ordinal)
                .ToArray();
            return new ConfigurationDocumentationCoverageReport(
                types.Count,
                fieldCount,
                coveredFieldCount,
                orderedIssues);
        }

        static string ReadDocument(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        static List<string> FindSections(string markdown, string expectedAnchor)
        {
            string normalized = (markdown ?? string.Empty).Replace("\r\n", "\n");
            string[] lines = normalized.Split('\n');
            List<string> result = new List<string>();

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();

                if (!line.StartsWith("## ", StringComparison.Ordinal)
                    || !string.Equals(ToAnchor(line.Substring(3)), expectedAnchor, StringComparison.Ordinal))
                {
                    continue;
                }

                StringBuilder section = new StringBuilder();

                for (int sectionLine = lineIndex + 1; sectionLine < lines.Length; sectionLine++)
                {
                    if (lines[sectionLine].TrimStart().StartsWith("## ", StringComparison.Ordinal))
                    {
                        break;
                    }

                    section.AppendLine(lines[sectionLine]);
                }

                result.Add(section.ToString());
            }

            return result;
        }

        static Dictionary<string, int> ParseFieldRows(string section)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] lines = (section ?? string.Empty).Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                Match match = FieldRowPattern.Match(lines[i]);

                if (!match.Success)
                {
                    continue;
                }

                string fieldName = match.Groups["field"].Value;
                result.TryGetValue(fieldName, out int count);
                result[fieldName] = count + 1;
            }

            return result;
        }

        static string ToAnchor(string heading)
        {
            StringBuilder result = new StringBuilder();
            string value = (heading ?? string.Empty)
                .Replace("`", string.Empty)
                .Trim()
                .ToLowerInvariant();
            bool previousWasSeparator = false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (char.IsLetterOrDigit(character) || character == '_')
                {
                    result.Append(character);
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator && result.Length > 0)
                {
                    result.Append('-');
                    previousWasSeparator = true;
                }
            }

            return result.ToString().Trim('-');
        }
    }
}
