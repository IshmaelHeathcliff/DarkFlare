using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class MigrationPipelineTests
    {
        sealed class DelegateMigrationStep : IJsonMigrationStep
        {
            readonly Action<JObject> _apply;

            public string Id { get; }

            public MigrationDataDomain Domain { get; }

            public int FromVersion { get; }

            public int ToVersion { get; }

            public DelegateMigrationStep(
                string id,
                MigrationDataDomain domain,
                int fromVersion,
                int toVersion,
                Action<JObject> apply)
            {
                Id = id;
                Domain = domain;
                FromVersion = fromVersion;
                ToVersion = toVersion;
                _apply = apply;
            }

            public void Apply(JObject document)
            {
                _apply(document);
            }
        }

        [Test]
        public void VersionDomains_AreIndependentStrongTypes()
        {
            GameVersion game = new GameVersion("0.2.1-alpha");
            ContentVersion content = new ContentVersion(3);
            SaveSchemaVersion save = new SaveSchemaVersion(3);
            SettingsSchemaVersion settings = new SettingsSchemaVersion(3);

            Assert.AreEqual("0.2.1-alpha", game.Value);
            Assert.AreEqual(3, content.Value);
            Assert.AreEqual(3, save.Value);
            Assert.AreEqual(3, settings.Value);
            Assert.AreNotEqual(typeof(ContentVersion), typeof(SaveSchemaVersion));
            Assert.AreNotEqual(typeof(SaveSchemaVersion), typeof(SettingsSchemaVersion));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContentVersion(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveSchemaVersion(-1));
            Assert.AreEqual(
                VersionCompatibilityCode.MigrationRequired,
                VersionCompatibility.Evaluate(1, 2).Code);
            Assert.AreEqual(
                VersionCompatibilityCode.FutureVersionUnsupported,
                VersionCompatibility.Evaluate(3, 2).Code);
        }

        [Test]
        public void HistoricalMigration_DeepCopiesNormalizesContentIdAndIsDeterministic()
        {
            JsonMigrationRegistry registry = BuildRegistry(
                1,
                new LegacySaveV0ToV1Migration());
            JsonMigrationPipeline pipeline = new JsonMigrationPipeline(registry);
            JObject input = JObject.Parse(
                "{\"schemaVersion\":0,\"items\":[{\"baseItemId\":\"great_sword\"}]}");
            JObject original = (JObject)input.DeepClone();

            JsonMigrationResult first = pipeline.Migrate(input, 0);
            JsonMigrationResult replay = pipeline.Migrate(input, 0);

            Assert.IsTrue(first.Succeeded);
            Assert.AreEqual(1, first.FinalVersion);
            CollectionAssert.AreEqual(
                new[] { "save_0_to_1_content_ids" },
                first.CompletedStepIds);
            Assert.AreEqual("item:great_sword", first.Document["items"]?[0]?["baseContentId"]?.Value<string>());
            Assert.IsNull(first.Document["items"]?[0]?["baseItemId"]);
            Assert.IsTrue(JToken.DeepEquals(original, input), "迁移不得原地修改调用方输入");
            Assert.IsTrue(JToken.DeepEquals(first.Document, replay.Document));
            Assert.AreNotSame(input, first.Document);
        }

        [Test]
        public void SaveDocumentMigration_UpdatesNestedHeaderAndPayloadItems()
        {
            JsonMigrationPipeline pipeline = new JsonMigrationPipeline(
                BuildRegistry(1, new LegacySaveV0ToV1Migration()),
                "header.saveSchemaVersion");
            JObject input = JObject.Parse(
                "{\"header\":{\"saveSchemaVersion\":0},\"payload\":{\"items\":[{\"baseItemId\":\"great_sword\"}]}}");

            JsonMigrationResult result = pipeline.Migrate(input, 0);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, result.Document["header"]?["saveSchemaVersion"]?.Value<int>());
            Assert.AreEqual(
                "item:great_sword",
                result.Document["payload"]?["items"]?[0]?["baseContentId"]?.Value<string>());
            Assert.IsNull(result.Document["payload"]?["items"]?[0]?["baseItemId"]);
            Assert.AreEqual(0, input["header"]?["saveSchemaVersion"]?.Value<int>());
        }

        [Test]
        public void CurrentVersion_IsIdempotentSuccessButStillReturnsDetachedCopy()
        {
            JsonMigrationPipeline pipeline = new JsonMigrationPipeline(BuildRegistry(
                1,
                new LegacySaveV0ToV1Migration()));
            JObject input = JObject.Parse("{\"schemaVersion\":1,\"value\":5}");

            JsonMigrationResult result = pipeline.Migrate(input, 1);

            Assert.IsTrue(result.Succeeded);
            Assert.IsEmpty(result.CompletedStepIds);
            Assert.IsTrue(JToken.DeepEquals(input, result.Document));
            Assert.AreNotSame(input, result.Document);
        }

        [Test]
        public void FutureVersion_IsRejectedWithoutDocument()
        {
            JsonMigrationPipeline pipeline = new JsonMigrationPipeline(BuildRegistry(
                1,
                new LegacySaveV0ToV1Migration()));

            JsonMigrationResult result = pipeline.Migrate(new JObject(), 2);

            Assert.AreEqual(MigrationResultCode.FutureVersionUnsupported, result.Code);
            Assert.IsNull(result.Document);
            Assert.IsEmpty(result.CompletedStepIds);
        }

        [Test]
        public void Registry_RejectsDuplicateHoleJumpAndDomainMismatch()
        {
            IJsonMigrationStep duplicate = new DelegateMigrationStep(
                "duplicate_start",
                MigrationDataDomain.Save,
                0,
                1,
                _ => { });
            IJsonMigrationStep jump = new DelegateMigrationStep(
                "jump_step",
                MigrationDataDomain.Save,
                1,
                3,
                _ => { });
            IJsonMigrationStep wrongDomain = new DelegateMigrationStep(
                "wrong_domain",
                MigrationDataDomain.Settings,
                2,
                3,
                _ => { });

            JsonMigrationRegistryBuildResult result = JsonMigrationRegistry.Build(
                MigrationDataDomain.Save,
                3,
                new IJsonMigrationStep[]
                {
                    new LegacySaveV0ToV1Migration(),
                    duplicate,
                    jump,
                    wrongDomain,
                });

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Registry);
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    MigrationRegistrationIssueCode.DuplicateFromVersion,
                    MigrationRegistrationIssueCode.StepMustBeIncremental,
                    MigrationRegistrationIssueCode.DomainMismatch,
                    MigrationRegistrationIssueCode.MigrationPathMissing,
                },
                result.Issues.Select(issue => issue.Code).ToArray());
        }

        [Test]
        public void Registry_RejectsStepsBeyondCurrentVersion()
        {
            JsonMigrationRegistryBuildResult result = JsonMigrationRegistry.Build(
                MigrationDataDomain.Save,
                1,
                new IJsonMigrationStep[]
                {
                    new LegacySaveV0ToV1Migration(),
                    new DelegateMigrationStep(
                        "save_1_to_2_future",
                        MigrationDataDomain.Save,
                        1,
                        2,
                        _ => { }),
                });

            Assert.IsFalse(result.Succeeded);
            Assert.That(
                result.Issues.Select(issue => issue.Code),
                Has.Some.EqualTo(MigrationRegistrationIssueCode.VersionInvalid));
        }

        [Test]
        public void StepFailure_ReturnsStructuredReportAndLeavesInputUnchanged()
        {
            IJsonMigrationStep first = new DelegateMigrationStep(
                "save_0_to_1",
                MigrationDataDomain.Save,
                0,
                1,
                document => document["first"] = true);
            IJsonMigrationStep failing = new DelegateMigrationStep(
                "save_1_to_2_failing",
                MigrationDataDomain.Save,
                1,
                2,
                document =>
                {
                    document["partial"] = true;
                    throw new InvalidOperationException("fixture failure");
                });
            JsonMigrationPipeline pipeline = new JsonMigrationPipeline(BuildRegistry(2, first, failing));
            JObject input = JObject.Parse("{\"schemaVersion\":0,\"keep\":\"original\"}");
            JObject original = (JObject)input.DeepClone();

            JsonMigrationResult result = pipeline.Migrate(input, 0);

            Assert.AreEqual(MigrationResultCode.StepFailed, result.Code);
            Assert.AreEqual("save_1_to_2_failing", result.FailedStepId);
            Assert.AreEqual(1, result.FinalVersion);
            Assert.IsInstanceOf<InvalidOperationException>(result.Exception);
            CollectionAssert.AreEqual(new[] { "save_0_to_1" }, result.CompletedStepIds);
            Assert.IsNull(result.Document, "失败不得暴露可提交的部分文档");
            Assert.IsTrue(JToken.DeepEquals(original, input));
        }

        static JsonMigrationRegistry BuildRegistry(
            int currentVersion,
            params IJsonMigrationStep[] steps)
        {
            JsonMigrationRegistryBuildResult result = JsonMigrationRegistry.Build(
                MigrationDataDomain.Save,
                currentVersion,
                steps);
            Assert.IsTrue(
                result.Succeeded,
                string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
            return result.Registry;
        }
    }
}
