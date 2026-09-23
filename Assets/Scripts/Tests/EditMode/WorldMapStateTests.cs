using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace DarkFlare.Tests
{
    public sealed class WorldMapStateTests
    {
        [Test]
        public void KnownWildernessBossAddition_UpgradesOnlyHistoricalRevision()
        {
            ContentCatalog catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            WorldMapCatalog maps = WorldMapCatalog.FromContent(catalog);
            var run = new RunSaveData { ActiveMapId = "map:wilderness", MapRevision = 1,
                Boss = new BossEncounterStateDto { Status = BossEncounterStatus.LegacyAbsent } };
            run.FrozenMaps.Add(new MapStateDto { MapId = "map:wilderness", Revision = 1,
                Boss = new BossEncounterStateDto { Status = BossEncounterStatus.LegacyAbsent } });
            maps.UpgradeKnownBossRevision(run);
            Assert.AreEqual(maps.Get("map:wilderness").Revision, run.MapRevision);
            Assert.AreEqual(run.MapRevision, run.FrozenMaps[0].Revision);
            run.MapRevision = 2;
            maps.UpgradeKnownBossRevision(run);
            Assert.AreEqual(2, run.MapRevision, "未知修订不能被宽松迁移");
        }

        [Test]
        public void FrozenClock_PreservesRemainingTimeAndTickPhaseWithoutMutatingSource()
        {
            var source = new StatusSaveDto { Time = 10, Actors = new List<StatusActorDto>
            {
                new StatusActorDto { ActorKey = "monster", Layers = new List<StatusLayerDto>
                { new StatusLayerDto { Id = 4, AppliedAt = 8, ExpiresAt = 15, NextTickAt = 10.75, TickOrdinal = 2 } } }
            } };
            StatusSaveDto result = MapStateData.AtTime(source, 50);
            StatusLayerDto layer = result.Actors[0].Layers[0];
            Assert.AreEqual(5, layer.ExpiresAt - result.Time);
            Assert.AreEqual(0.75, layer.NextTickAt - result.Time);
            Assert.AreEqual(2, layer.TickOrdinal);
            Assert.AreEqual(4, layer.Id);
            Assert.AreEqual(10, source.Time);
            Assert.AreEqual(8, source.Actors[0].Layers[0].AppliedAt);
            Assert.Throws<ArgumentOutOfRangeException>(() => MapStateData.AtTime(source, 9));
        }

        [Test]
        public void MapRandom_RestoresMapChannelsWithoutRewindingPlayerOrCrafting()
        {
            var random = new GameplayRandomSystem(); random.Configure(true, 876);
            random.RestoreMapState(random.CreateMapState("map:wilderness"));
            random.NextSeed(GameplayRandomChannel.Loot);
            GameplayRandomState frozen = random.CaptureMapState();
            int expected = random.NextSeed(GameplayRandomChannel.Loot);
            random.RestoreMapState(random.CreateMapState("map:town"));
            random.NextSeed(GameplayRandomChannel.Crafting);
            GameplayRandomState session = random.CaptureState();
            random.RestoreMapState(frozen);
            Assert.AreEqual(expected, random.NextSeed(GameplayRandomChannel.Loot));
            Assert.AreEqual(session.NextSequences[GameplayRandomChannel.Crafting], random.CaptureState().NextSequences[GameplayRandomChannel.Crafting]);
        }

        [Test]
        public void FrozenMaps_ValidateGlobalIdentityAndRoundTripThousandMonsters()
        {
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();
            RunSaveData run = document.Payload.Run;
            MonsterRunStateDto template = run.Monsters.Single();
            ContentCatalog catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            MonsterDefinition definition = catalog.GetAll<MonsterDefinition>().First(value => "monster:" + value.Id == template.DefinitionContentId);
            MonsterInstanceData instance = Enumerable.Range(1, 256).Select(seed => definition.CreateInstanceData(seed)).First(value => value.Affixes.Count > 0);
            Assert.IsNotEmpty(instance.Affixes, "容量样本必须包含正式词条掷值");
            var issues = new List<DtoMapIssue>();
            template.RandomSeeds = new MonsterRandomSeedsDto { RootSeed = instance.RandomSeeds.RootSeed, HealthSeed = instance.RandomSeeds.HealthSeed,
                AffixCountSeed = instance.RandomSeeds.AffixCountSeed, AffixSelectionSeed = instance.RandomSeeds.AffixSelectionSeed };
            template.BaseMaxHealth = instance.BaseMaxHealth;
            template.BaseStats = instance.BaseStats.Values.Select(value => new StatValueDto { StatContentId = "stat:" + value.Key, Value = value.Value }).ToList();
            template.EffectiveStats = instance.EffectiveStats.Values.Select(value => new StatValueDto { StatContentId = "stat:" + value.Key, Value = value.Value }).ToList();
            template.Modifiers = RuntimeStateMapper.MapModifiersToDto(instance.Modifiers, catalog, "sample.modifiers", issues);
            template.Affixes = instance.Affixes.Select(affix => new MonsterAffixInstanceDto
            { DefinitionContentId = "monster_affix:" + affix.Definition.Id,
                Modifiers = RuntimeStateMapper.MapModifiersToDto(affix.Modifiers, catalog, "sample.affixes", issues) }).ToList();
            template.Resources = new CombatResourceStateDto { CurrentHealth = instance.MaxHealth / 2, MaxHealth = instance.MaxHealth,
                MaxMana = instance.EffectiveStats.GetValue(StatIds.Mana), IsAlive = true };
            Assert.IsEmpty(issues);
            var map = new MapStateDto { MapId = "map:wilderness", Boss = run.Boss.Copy(), Random = MapStateData.Copy(run.Random),
                Spawner = MapStateData.Copy(run.Spawner), WorldDrops = run.WorldDrops };
            for (int i = 1; i <= 1000; i++)
            {
                MonsterRunStateDto monster = MapStateData.Copy(template);
                monster.InstanceId = run.InstanceIds.RunId + ":monster:" + i;
                map.Monsters.Add(monster);
                map.Statuses.Actors.Add(new StatusActorDto { ActorKey = monster.InstanceId, Order = i,
                    Registration = i, NextId = 2, Layers = new List<StatusLayerDto>
                    { new StatusLayerDto { Id = 1, Rules = StatusEffectMapper.Capture(new StatusRules("weakness", duration: 12, interval: 1)),
                        Effects = new StatusEffectDto(), SourceKind = StatusSourceKind.Mechanism, SourceKey = "test",
                        AppliedAt = 0, ExpiresAt = 12, NextTickAt = 1, TickOrdinal = 1 } } });
            }
            run.InstanceIds.NextMonsterSequence = 1001;
            run.ActiveMapId = "map:town"; run.Monsters.Clear(); run.WorldDrops = new List<WorldDropStateDto>();
            run.Spawner = new MonsterSpawnerStateDto(); run.FrozenMaps.Add(map);
            var serializer = new NewtonsoftSaveSerializer();
            SaveSerializationResult serialized = serializer.Serialize(document);
            Assert.IsTrue(serialized.Succeeded, string.Join("; ", serialized.ValidationIssues.Select(issue => issue.Message)));
            Assert.Less(serialized.Bytes.Length, LocalSaveFormat.MaximumDocumentBytes);
            TestContext.Out.WriteLine("1000 monsters + 1000 status layers + drop + stock: " + serialized.Bytes.Length + " bytes");
            SaveDeserializationResult restored = serializer.Deserialize(serialized.Bytes);
            Assert.IsTrue(restored.Succeeded);
            PreparedRestoreResult prepared = SaveRestorePreparer.Prepare(restored.Document, catalog);
            Assert.IsTrue(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(issue => issue.Message)));
            CollectionAssert.AreEqual(map.Monsters.Select(monster => monster.InstanceId), prepared.Value.Document.Payload.Run.FrozenMaps[0].Monsters.Select(monster => monster.InstanceId));
            run.Monsters.Add(MapStateData.Copy(map.Monsters[0]));
            Assert.IsFalse(SaveDataValidator.ValidateDocument(document).Succeeded, "冻结图也参与全局实例身份校验");
            run.Monsters.Clear(); run.InstanceIds.NextMonsterSequence = 500;
            Assert.IsFalse(SaveDataValidator.ValidateDocument(document).Succeeded, "分配器不得回退到冻结图内已用 ID");
            run.InstanceIds.NextMonsterSequence = 1001; map.Monsters.AddRange(Enumerable.Repeat(template, 25));
            Assert.IsFalse(SaveDataValidator.ValidateDocument(document).Succeeded, "每图容量超限拒绝而非截断");
        }

        [Test]
        public void RestorePreflight_RejectsUnknownMapAndBlockedPositionWithoutChangingInput()
        {
            ContentCatalog catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();
            document.Payload.Run.ActiveMapId = "map:missing";
            string before = JObject.FromObject(document).ToString();
            Assert.IsFalse(SaveRestorePreparer.Prepare(document, catalog).Succeeded);
            Assert.AreEqual(before, JObject.FromObject(document).ToString());
            document.Payload.Run.ActiveMapId = "map:wilderness";
            document.Payload.Run.Player.Position = new Vector3Dto { X = -52, Y = 48 };
            Assert.IsFalse(SaveRestorePreparer.Prepare(document, catalog).Succeeded);
        }
    }
}
