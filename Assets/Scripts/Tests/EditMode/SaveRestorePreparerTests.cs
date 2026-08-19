using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace DarkFlare.Tests
{
    public sealed class SaveRestorePreparerTests
    {
        ContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            ContentCatalogDefinition definition = AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset");
            ContentCatalogBuildResult build = ContentCatalog.Build(definition);
            Assert.IsTrue(
                build.Succeeded,
                string.Join("\n", build.Issues.Select(issue => issue.Message)));
            _catalog = build.Catalog;
        }

        [Test]
        public void Prepare_ResolvesClosedGraphAndPreservesExactContinuationState()
        {
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();

            PreparedRestoreResult result = SaveRestorePreparer.Prepare(document, _catalog);

            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.AreEqual("player", result.Value.PlayerDefinition.Id);
            Assert.AreEqual("basic_projectile", result.Value.PlayerSkill.Id);
            Assert.AreEqual("main", result.Value.SpawnDefinition.Id);
            Assert.AreEqual(4, result.Value.RuntimeState.Items.Count);
            Assert.AreEqual(1, result.Value.RuntimeState.Inventory.Placements.Count);
            Assert.AreEqual(
                "22222222222222222222222222222222",
                result.Value.PlayerEquipment.Get(EquipmentSlot.Weapon).Id.Value);
            Assert.AreEqual(
                "33333333333333333333333333333333",
                result.Value.RuntimeState.MerchantStock[0].Id.Value);
            Assert.AreSame(
                result.Value.RuntimeState.Items[ItemInstanceId.Parse(
                    "44444444444444444444444444444444")],
                result.Value.WorldDrops[0].Item);
            Assert.AreEqual(3, result.Value.InstanceIdState.NextMonsterSequence);
            Assert.AreEqual(5, result.Value.InstanceIdState.NextWorldDropSequence);
            Assert.AreEqual(
                document.Payload.Run.Random.Channels.Count,
                result.Value.RandomState.NextSequences.Count);
            Assert.AreEqual(
                document.Payload.Run.Monsters[0].RandomSeeds.RootSeed,
                result.Value.Monsters[0].Instance.RandomSeeds.RootSeed);
            Assert.AreEqual(
                30f,
                result.Value.Monsters[0].Instance.EffectiveStats.GetValue(StatIds.MaxHealth));
        }

        [Test]
        public void Prepare_RejectsCatalogMismatchAndDerivedMonsterStateMismatchBeforeCommit()
        {
            SaveDocumentDto catalogMismatch = SaveDataContractTests.CreateValidDocument();
            catalogMismatch.Header.ContentVersion++;

            PreparedRestoreResult incompatible = SaveRestorePreparer.Prepare(
                catalogMismatch,
                _catalog);

            Assert.IsFalse(incompatible.Succeeded);
            Assert.That(
                incompatible.Issues.Select(issue => issue.Code),
                Has.Some.EqualTo(DtoMapIssueCode.MissingContent));

            SaveDocumentDto statMismatch = SaveDataContractTests.CreateValidDocument();
            statMismatch.Payload.Run.Monsters[0].EffectiveStats[0].Value = 31f;

            PreparedRestoreResult invalid = SaveRestorePreparer.Prepare(statMismatch, _catalog);

            Assert.IsFalse(invalid.Succeeded);
            Assert.IsTrue(invalid.Issues.Any(issue =>
                issue.Code == DtoMapIssueCode.InvalidValue
                && issue.Path.EndsWith("effectiveStats", StringComparison.Ordinal)));
        }

        static string Describe(PreparedRestoreResult result)
        {
            return string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.Code} {issue.Path}: {issue.Message}"));
        }
    }
}
