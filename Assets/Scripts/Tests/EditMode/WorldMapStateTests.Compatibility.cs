using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed partial class WorldMapStateTests
    {
        static SaveDocumentDto ReadHistoricalMapSave(string name)
        {
            TextAsset fixture = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/Tests/Fixtures/Migration/save-core14-" + name + ".json");
            SaveDeserializationResult result = new NewtonsoftSaveSerializer().Deserialize(fixture.bytes);
            Assert.IsTrue(result.Succeeded);
            return result.Document;
        }

        [TestCase("town")]
        [TestCase("wilderness")]
        public void HistoricalMapSave_PreservesProgressAndIsIdempotent(string name)
        {
            ContentCatalog content = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            SaveDocumentDto source = ReadHistoricalMapSave(name);
            string before = JObject.FromObject(source).ToString();
            PreparedRestoreResult prepared = SaveRestorePreparer.Prepare(source, content);
            Assert.IsTrue(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(i => i.Message)));
            Assert.AreEqual(before, JObject.FromObject(source).ToString(), "预检不能修改调用者");
            SaveDocumentDto actual = prepared.Value.Document;
            Assert.AreEqual(content.ContentVersion, actual.Header.ContentVersion);
            RunSaveData run = actual.Payload.Run;
            MapStateDto oldWild = source.Payload.Run.FrozenMaps.FirstOrDefault(m => m.MapId == "map:wilderness");
            MonsterSpawnerStateDto oldPlan = oldWild?.Spawner ?? source.Payload.Run.Spawner;
            MapStateDto wild = run.FrozenMaps.FirstOrDefault(m => m.MapId == "map:wilderness");
            MonsterSpawnerStateDto plan = wild?.Spawner ?? run.Spawner;
            Assert.AreEqual(100, plan.Slots.Count, "已访问旧实例不能补发200个名额");
            Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(oldPlan), JObject.FromObject(plan)));
            Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(source.Payload.Run.Random), JObject.FromObject(run.Random)));
            Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(source.Payload.Run.InstanceIds), JObject.FromObject(run.InstanceIds)));
            Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(source.Payload.Profile), JObject.FromObject(actual.Payload.Profile)));
            Assert.IsTrue(JToken.DeepEquals(JArray.FromObject(source.Payload.Items), JArray.FromObject(actual.Payload.Items)));
            WorldMapCatalog maps = WorldMapCatalog.FromContent(content);
            string once = JObject.FromObject(actual).ToString();
            maps.UpgradeKnownWildernessRevision(run);
            maps.ValidateSave(run);
            Assert.AreEqual(once, JObject.FromObject(actual).ToString());
            SaveSerializationResult saved = new NewtonsoftSaveSerializer().Serialize(actual);
            Assert.IsTrue(saved.Succeeded);
            Assert.IsTrue(SaveRestorePreparer.Prepare(new NewtonsoftSaveSerializer().Deserialize(saved.Bytes).Document, content).Succeeded);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HistoricalMapMigration_RejectsUnknownOrTruncatedPlanAtomically(bool truncated)
        {
            ContentCatalog content = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            RunSaveData run = ReadHistoricalMapSave("town").Payload.Run;
            MapStateDto wild = run.FrozenMaps.Single();
            if (truncated) { wild.Spawner.Slots.RemoveAt(wild.Spawner.Slots.Count - 1); }
            else { wild.Revision = 999; }
            string before = JObject.FromObject(run).ToString();
            Assert.Throws<InvalidOperationException>(() => WorldMapCatalog.FromContent(content).UpgradeKnownWildernessRevision(run));
            Assert.AreEqual(before, JObject.FromObject(run).ToString());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CurrentFinitePlan_RejectsTruncatedOrMixedSeeds(bool mixed)
        {
            ContentCatalog content = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            WorldMapCatalog maps = WorldMapCatalog.FromContent(content);
            RunSaveData run = ReadHistoricalMapSave("wilderness").Payload.Run;
            maps.UpgradeKnownWildernessRevision(run);
            run.Spawner = FiniteSpawnPlan.Create(maps.Get(run.ActiveMapId), maps.Definitions, 49123);
            run.Spawner.SpawnContentId = "spawn:main";
            run.Monsters.Clear();
            if (mixed) { run.Spawner.Slots[0].MonsterSeed++; }
            else { run.Spawner.Slots.RemoveAt(run.Spawner.Slots.Count - 1); }
            Assert.Throws<InvalidOperationException>(() => maps.ValidateSave(run));
        }

        [Test]
        public void HistoricalMapMigration_LegacyPreservedDoesNotInventSlots()
        {
            ContentCatalog content = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            RunSaveData run = ReadHistoricalMapSave("wilderness").Payload.Run;
            run.MapRevision = 0;
            run.Spawner.Mode = FiniteSpawnMode.LegacyPreserved;
            run.Spawner.Slots.Clear(); run.Spawner.Batches.Clear();
            string[] ids = run.Monsters.Select(m => m.InstanceId).ToArray();
            WorldMapCatalog maps = WorldMapCatalog.FromContent(content);
            maps.UpgradeKnownWildernessRevision(run);
            maps.ValidateSave(run);
            Assert.IsEmpty(run.Spawner.Slots);
            CollectionAssert.AreEqual(ids, run.Monsters.Select(m => m.InstanceId));
        }
    }
}
