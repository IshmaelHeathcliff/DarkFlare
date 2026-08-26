using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class Alpha027AcceptanceContractTests
    {
        const string ManifestPath =
            "Assets/Scripts/Tests/EditMode/Alpha027AcceptanceCoverage.json";
        const string ExpectedVersion = "0.2.7-alpha";

        [Test]
        public void ProjectVersion_UsesAlpha027ReleaseIdentity()
        {
            Assert.AreEqual(ExpectedVersion, PlayerSettings.bundleVersion);
            Assert.AreEqual(ExpectedVersion, Application.version);
        }

        [Test]
        public void CoverageManifest_TracksCurrentEvidencePlansAndKnownIgnores()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("无法解析项目根目录");
            string manifestAbsolutePath = GetAbsolutePath(projectRoot, ManifestPath);
            string json = File.ReadAllText(manifestAbsolutePath, Encoding.UTF8);
            AcceptanceCoverageManifest manifest =
                JsonUtility.FromJson<AcceptanceCoverageManifest>(json);

            Assert.IsNotNull(manifest);
            Assert.AreEqual(1, manifest.SchemaVersion);
            Assert.AreEqual("complete", manifest.PhaseStatus);
            StringAssert.IsMatch("^[0-9a-f]{7,40}$", manifest.BaselineCommit);
            Assert.AreEqual(Application.unityVersion, manifest.UnityVersion);

            ValidateRequirements(projectRoot, manifest);
            ValidateKnownIgnores(projectRoot, manifest);
        }

        static void ValidateRequirements(
            string projectRoot,
            AcceptanceCoverageManifest manifest)
        {
            Assert.IsNotNull(manifest.Requirements);
            Assert.IsNotEmpty(manifest.Requirements);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            bool isComplete = string.Equals(
                manifest.PhaseStatus,
                "complete",
                StringComparison.Ordinal);

            for (int i = 0; i < manifest.Requirements.Count; i++)
            {
                AcceptanceRequirement requirement = manifest.Requirements[i];
                Assert.IsNotNull(requirement, $"requirements[{i}]");
                StringAssert.IsMatch(
                    "^[a-z0-9]+(?:[.-][a-z0-9]+)*$",
                    requirement.Id,
                    $"requirements[{i}].id");
                Assert.IsTrue(ids.Add(requirement.Id), $"重复 requirement id：{requirement.Id}");

                if (string.Equals(requirement.Status, "covered", StringComparison.Ordinal))
                {
                    Assert.IsNotNull(requirement.Evidence, requirement.Id);
                    Assert.IsNotEmpty(requirement.Evidence, requirement.Id);

                    for (int evidenceIndex = 0;
                         evidenceIndex < requirement.Evidence.Count;
                         evidenceIndex++)
                    {
                        ValidateEvidence(
                            projectRoot,
                            requirement.Id,
                            requirement.Evidence[evidenceIndex]);
                    }

                    continue;
                }

                Assert.AreEqual("planned", requirement.Status, requirement.Id);
                Assert.IsFalse(isComplete, $"完成态不能保留 planned：{requirement.Id}");
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(requirement.PlannedSlice),
                    requirement.Id);
            }
        }

        static void ValidateKnownIgnores(
            string projectRoot,
            AcceptanceCoverageManifest manifest)
        {
            Assert.IsNotNull(manifest.AllowedIgnoredTests);
            Assert.AreEqual(2, manifest.AllowedIgnoredTests.Count);
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < manifest.AllowedIgnoredTests.Count; i++)
            {
                AcceptanceIgnoredTest ignored = manifest.AllowedIgnoredTests[i];
                Assert.IsNotNull(ignored, $"allowedIgnoredTests[{i}]");
                StringAssert.Contains("1252825", ignored.Reason);
                ValidateProjectRelativePath(projectRoot, ignored.Path);
                Assert.IsTrue(
                    keys.Add(ignored.Path + "|" + ignored.TestName),
                    $"重复 Ignore：{ignored.TestName}");
                AssertSourceContainsTest(projectRoot, ignored.Path, ignored.TestName);
            }
        }

        static void ValidateEvidence(
            string projectRoot,
            string requirementId,
            AcceptanceEvidence evidence)
        {
            Assert.IsNotNull(evidence, requirementId);
            ValidateProjectRelativePath(projectRoot, evidence.Path);
            Assert.IsFalse(string.IsNullOrWhiteSpace(evidence.TestName), requirementId);
            AssertSourceContainsTest(projectRoot, evidence.Path, evidence.TestName);
        }

        static void ValidateProjectRelativePath(string projectRoot, string path)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(path));
            Assert.IsFalse(Path.IsPathRooted(path), path);
            Assert.AreEqual(path.Replace('\\', '/'), path, path);
            Assert.IsFalse(path.Contains("../"), path);
            Assert.IsTrue(File.Exists(GetAbsolutePath(projectRoot, path)), path);
        }

        static void AssertSourceContainsTest(
            string projectRoot,
            string path,
            string testName)
        {
            string source = File.ReadAllText(
                GetAbsolutePath(projectRoot, path),
                Encoding.UTF8);
            string pattern = @"\b" + Regex.Escape(testName) + @"\s*\(";
            Assert.IsTrue(Regex.IsMatch(source, pattern), $"{path} 未找到 {testName}");
        }

        static string GetAbsolutePath(string projectRoot, string projectPath)
        {
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                projectPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        [Serializable]
        sealed class AcceptanceCoverageManifest
        {
            [SerializeField]
            int schemaVersion;

            [SerializeField]
            string phaseStatus;

            [SerializeField]
            string baselineCommit;

            [SerializeField]
            string unityVersion;

            [SerializeField]
            List<AcceptanceRequirement> requirements = new List<AcceptanceRequirement>();

            [SerializeField]
            List<AcceptanceIgnoredTest> allowedIgnoredTests =
                new List<AcceptanceIgnoredTest>();

            public int SchemaVersion => schemaVersion;

            public string PhaseStatus => phaseStatus;

            public string BaselineCommit => baselineCommit;

            public string UnityVersion => unityVersion;

            public IReadOnlyList<AcceptanceRequirement> Requirements => requirements;

            public IReadOnlyList<AcceptanceIgnoredTest> AllowedIgnoredTests =>
                allowedIgnoredTests;
        }

        [Serializable]
        sealed class AcceptanceRequirement
        {
            [SerializeField]
            string id;

            [SerializeField]
            string status;

            [SerializeField]
            string plannedSlice;

            [SerializeField]
            List<AcceptanceEvidence> evidence = new List<AcceptanceEvidence>();

            public string Id => id;

            public string Status => status;

            public string PlannedSlice => plannedSlice;

            public IReadOnlyList<AcceptanceEvidence> Evidence => evidence;
        }

        [Serializable]
        sealed class AcceptanceEvidence
        {
            [SerializeField]
            string path;

            [SerializeField]
            string testName;

            public string Path => path;

            public string TestName => testName;
        }

        [Serializable]
        sealed class AcceptanceIgnoredTest
        {
            [SerializeField]
            string path;

            [SerializeField]
            string testName;

            [SerializeField]
            string reason;

            public string Path => path;

            public string TestName => testName;

            public string Reason => reason;
        }
    }
}
