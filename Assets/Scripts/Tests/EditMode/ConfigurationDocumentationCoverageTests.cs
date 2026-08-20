using System;
using System.Collections.Generic;
using System.IO;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class ConfigurationDocumentationCoverageTests
    {
        const string ManifestPath = "Docs/docs/config-reference/coverage-manifest.json";

        [Test]
        public void ConfigurationDocumentationManifest_Exists()
        {
            Assert.IsTrue(File.Exists(ManifestPath), ManifestPath);
        }

        [Test]
        public void OfficialConfigurationDocumentation_CoversDiscoveredTypesAndFields()
        {
            ConfigurationDocumentationCoverageReport report = ConfigurationDocumentationCoverage.ScanOfficial();
            Assert.AreEqual(19, report.TypeCount);
            Assert.AreEqual(163, report.SerializedFieldCount);
            Assert.AreEqual(report.SerializedFieldCount, report.CoveredFieldCount);
            Assert.IsTrue(report.Passed, FormatIssues(report.Issues));
        }

        [Test]
        public void Scanner_DetectsNewAndStaleFieldWithinMappedTypeSection()
        {
            string typeName = typeof(SyntheticConfiguration).FullName;
            string manifest = "{\n"
                + "  \"schemaVersion\": 1,\n"
                + "  \"entries\": [\n"
                + $"    {{ \"type\": \"{typeName}\", \"document\": \"synthetic.md\", \"anchor\": \"syntheticconfiguration\" }}\n"
                + "  ]\n"
                + "}";
            string document = @"# Synthetic

## SyntheticConfiguration

| 字段 | 说明 |
| --- | --- |
| `_knownField` | 当前字段 |
| `_staleField` | 过期字段 |
";
            ConfigurationDocumentationCoverageReport report = ConfigurationDocumentationCoverage.Scan(
                new[] { typeof(SyntheticConfiguration) },
                manifest,
                _ => document);

            Assert.IsTrue(ContainsIssue(report, "field-missing", "_newField"));
            Assert.IsTrue(ContainsIssue(report, "field-stale", "_staleField"));
        }

        [Test]
        public void Scanner_DetectsDuplicateOrphanAndWrongHeadingMappings()
        {
            string typeName = typeof(SyntheticConfiguration).FullName;
            string manifest = "{\n"
                + "  \"schemaVersion\": 1,\n"
                + "  \"entries\": [\n"
                + $"    {{ \"type\": \"{typeName}\", \"document\": \"synthetic.md\", \"anchor\": \"missing-heading\" }},\n"
                + $"    {{ \"type\": \"{typeName}\", \"document\": \"duplicate.md\", \"anchor\": \"syntheticconfiguration\" }},\n"
                + "    { \"type\": \"DarkFlare.RemovedConfiguration\", \"document\": \"removed.md\", \"anchor\": \"removedconfiguration\" }\n"
                + "  ]\n"
                + "}";
            ConfigurationDocumentationCoverageReport report = ConfigurationDocumentationCoverage.Scan(
                new[] { typeof(SyntheticConfiguration), typeof(NewUnmappedConfiguration) },
                manifest,
                _ => "# Synthetic\n\n## SyntheticConfiguration\n");

            Assert.IsTrue(ContainsIssue(report, "mapping-duplicate", string.Empty));
            Assert.IsTrue(ContainsIssue(report, "mapping-missing", string.Empty));
            Assert.IsTrue(ContainsIssue(report, "mapping-orphan", string.Empty));
            Assert.IsTrue(ContainsIssue(report, "heading-missing", string.Empty));
        }

        static bool ContainsIssue(
            ConfigurationDocumentationCoverageReport report,
            string code,
            string fieldName)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ConfigurationDocumentationCoverageIssue issue = report.Issues[i];

                if (issue.Code == code
                    && (string.IsNullOrEmpty(fieldName) || issue.FieldName == fieldName))
                {
                    return true;
                }
            }

            return false;
        }

        static string FormatIssues(IReadOnlyList<ConfigurationDocumentationCoverageIssue> issues)
        {
            List<string> lines = new List<string>(issues.Count);

            for (int i = 0; i < issues.Count; i++)
            {
                ConfigurationDocumentationCoverageIssue issue = issues[i];
                lines.Add($"{issue.Code} | {issue.TypeName} | {issue.FieldName} | {issue.DocumentPath}");
            }

            return string.Join("\n", lines);
        }

        [Serializable]
        sealed class SyntheticConfiguration
        {
            [SerializeField]
            int _knownField;

            [SerializeField]
            int _newField;
        }

        [Serializable]
        sealed class NewUnmappedConfiguration
        {
            [SerializeField]
            int _onlyField;
        }
    }
}
