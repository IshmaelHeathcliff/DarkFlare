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
        public void KnownWildernessRevisions_UpgradeOnlyHistoricalLayouts()
        {
            ContentCatalog catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            WorldMapCatalog maps = WorldMapCatalog.FromContent(catalog);
            var run = new RunSaveData { ActiveMapId = "map:wilderness", MapRevision = 1,
                Boss = new BossEncounterStateDto { Status = BossEncounterStatus.LegacyAbsent } };
            run.FrozenMaps.Add(new MapStateDto { MapId = "map:wilderness", Revision = 1,
                Boss = new BossEncounterStateDto { Status = BossEncounterStatus.LegacyAbsent } });
            maps.UpgradeKnownWildernessRevision(run);
            Assert.AreEqual(maps.Get("map:wilderness").Revision, run.MapRevision);
            Assert.AreEqual(run.MapRevision, run.FrozenMaps[0].Revision);
            run.MapRevision = 4;
            run.FrozenMaps[0].Revision = 4;
            maps.UpgradeKnownWildernessRevision(run);
            Assert.AreEqual(maps.Get("map:wilderness").Revision, run.MapRevision);
            Assert.AreEqual(run.MapRevision, run.FrozenMaps[0].Revision);
            run.MapRevision = 7;
            run.FrozenMaps[0].Revision = 7;
            maps.UpgradeKnownWildernessRevision(run);
            Assert.AreEqual(maps.Get("map:wilderness").Revision, run.MapRevision);
            Assert.AreEqual(run.MapRevision, run.FrozenMaps[0].Revision);
            run.MapRevision = 2;
            maps.UpgradeKnownWildernessRevision(run);
            Assert.AreEqual(2, run.MapRevision, "未知修订不能被宽松迁移");
        }

        [Test]
        public void WildernessFinitePlan_HasStableReservedSlotsAndSeparateBoss()
        {
            ContentCatalog catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            WorldMapCatalog maps = WorldMapCatalog.FromContent(catalog);
            CompiledMapDocument map = maps.Get("map:wilderness");
            MonsterSpawnerStateDto first = FiniteSpawnPlan.Create(map, maps.Definitions, 49123);
            MonsterSpawnerStateDto second = FiniteSpawnPlan.Create(map, maps.Definitions, 49123);
            Assert.AreEqual(JObject.FromObject(first).ToString(), JObject.FromObject(second).ToString());
            Assert.AreEqual(FiniteSpawnMode.Finite, first.Mode);
            Assert.AreEqual(100, first.Slots.Count);
            Assert.AreEqual(20, first.Slots.Count(slot => first.Batches.Single(batch => batch.BatchId == slot.BatchId).IsActivated));
            MapSpawnBatchRecord[] triggers = map.CreateEditableCopy().SpawnBatches
                .Where(batch => batch.Activation == MapSpawnActivation.PlayerRange).ToArray();
            Assert.GreaterOrEqual(triggers.Length, 10);
            Assert.AreEqual(80, triggers.Sum(batch => batch.Count));
            Assert.Greater(triggers.Max(batch => batch.TriggerCenter.X) - triggers.Min(batch => batch.TriggerCenter.X), 80);
            Assert.Greater(triggers.Max(batch => batch.TriggerCenter.Y) - triggers.Min(batch => batch.TriggerCenter.Y), 80);
            for (int i = 0; i < triggers.Length; i++)
            {
                for (int j = i + 1; j < triggers.Length; j++)
                {
                    double dx = triggers[i].TriggerCenter.X - triggers[j].TriggerCenter.X;
                    double dy = triggers[i].TriggerCenter.Y - triggers[j].TriggerCenter.Y;
                    Assert.Greater(Math.Sqrt(dx * dx + dy * dy), 16, "触发点不应堆叠");
                }
            }
            Assert.IsTrue(first.Slots.All(slot => slot.Status == SpawnSlotStatus.Pending && string.IsNullOrEmpty(slot.InstanceId)));
            Assert.AreEqual(1, map.CreateEditableCopy().BossSpawns.Length);
            Assert.IsTrue(first.Slots.All(slot => catalog.GetAll<MonsterDefinition>().Single(monster =>
                "monster:" + monster.Id == slot.MonsterDefinitionId).BossBehavior == null));
        }

        [Test]
        public void RevisionSevenFinitePlan_ExpandsWithoutChangingExistingSlots()
        {
            ContentCatalog catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            WorldMapCatalog maps = WorldMapCatalog.FromContent(catalog);
            MapDocument source = maps.Get("map:wilderness").CreateEditableCopy();
            source.Revision = 7;
            source.Regions = source.Regions.Where(region => region.RegionId is "entry_safe" or "wild_spawn").ToArray();
            source.SpawnBatches = new[]
            {
                new MapSpawnBatchRecord { BatchId = "east_patrol", RegionId = "wild_spawn", Count = 6,
                    Activation = MapSpawnActivation.PlayerRange, TriggerCenter = new MapPoint { X = 100, Y = 96 }, TriggerRadius = 12 },
                new MapSpawnBatchRecord { BatchId = "initial", RegionId = "wild_spawn", Count = 6,
                    Activation = MapSpawnActivation.OnEnter }
            };
            MonsterSpawnerStateDto old = FiniteSpawnPlan.Create(MapDocumentCompiler.Compile(source, maps.Definitions), maps.Definitions, 49123);
            old.Batches.Single(batch => batch.BatchId == "east_patrol").IsActivated = true;
            old.Slots[0].Status = SpawnSlotStatus.Defeated;
            old.Slots[1].Status = SpawnSlotStatus.Alive;
            old.Slots[1].InstanceId = "preserved-monster";
            Dictionary<string, string> oldSlots = old.Slots.ToDictionary(slot => slot.SlotId,
                slot => JObject.FromObject(slot).ToString(), StringComparer.Ordinal);
            var run = new RunSaveData { ActiveMapId = "map:wilderness", MapRevision = 7, Spawner = old };
            maps.UpgradeKnownWildernessRevision(run);
            Assert.AreEqual(100, run.Spawner.Slots.Count);
            Assert.AreEqual(20, run.Spawner.Slots.Count(slot => slot.BatchId == "initial"));
            Assert.IsTrue(run.Spawner.Batches.Single(batch => batch.BatchId == "east_patrol").IsActivated);
            foreach (SpawnSlotStateDto slot in run.Spawner.Slots.Where(slot => oldSlots.ContainsKey(slot.SlotId)))
            {
                Assert.AreEqual(oldSlots[slot.SlotId], JObject.FromObject(slot).ToString(),
                    "旧名额的怪物类型、种子、身份和击败进度必须保持");
            }
        }

        [Test]
        public void WildernessBatchCompiler_RejectsInvalidQuotaRegionAndTrigger()
        {
            ContentCatalog catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            WorldMapCatalog maps = WorldMapCatalog.FromContent(catalog);
            MapDocument source = maps.Get("map:wilderness").CreateEditableCopy();
            source.SpawnBatches[0].Count = 0;
            StringAssert.Contains("$.spawnBatches[0].count", Assert.Throws<MapDocumentException>(() =>
                MapDocumentCompiler.Compile(source, maps.Definitions)).Message);
            source = maps.Get("map:wilderness").CreateEditableCopy();
            source.SpawnBatches[0].RegionId = "missing";
            StringAssert.Contains("$.spawnBatches[0].regionId", Assert.Throws<MapDocumentException>(() =>
                MapDocumentCompiler.Compile(source, maps.Definitions)).Message);
            source = maps.Get("map:wilderness").CreateEditableCopy();
            source.SpawnBatches[0].TriggerRadius = -1;
            StringAssert.Contains("$.spawnBatches[0].triggerRadius", Assert.Throws<MapDocumentException>(() =>
                MapDocumentCompiler.Compile(source, maps.Definitions)).Message);
        }

        [Test]
        public void RestorePreflight_RejectsMissingFinitePlanOnVisitedWilderness()
        {
            ContentCatalog catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();
            Assert.IsTrue(SaveRestorePreparer.Prepare(document, catalog).Succeeded);
            document.Payload.Run.Spawner.Mode = FiniteSpawnMode.None;
            Assert.IsFalse(SaveRestorePreparer.Prepare(document, catalog).Succeeded);
        }

        [Test]
        public void SchemaSevenMigration_PreservesVisitedMonstersAndStopsRefill()
        {
            JObject legacy = JObject.Parse("{\"payload\":{\"run\":{\"spawner\":{\"isRunning\":true},"
                + "\"monsters\":[{\"instanceId\":\"old-monster\"}],\"frozenMaps\":[{\"spawner\":{\"isRunning\":true},"
                + "\"monsters\":[{\"instanceId\":\"frozen-monster\"}]}]}}}");
            JToken activeMonsters = legacy["payload"]["run"]["monsters"].DeepClone();
            JToken frozenMonsters = legacy["payload"]["run"]["frozenMaps"][0]["monsters"].DeepClone();
            new FiniteSpawnSaveMigration().Apply(legacy);
            JObject run = (JObject)legacy["payload"]["run"];
            Assert.AreEqual((int)FiniteSpawnMode.LegacyPreserved, (int)run["spawner"]["mode"]);
            Assert.IsFalse((bool)run["spawner"]["isRunning"]);
            Assert.IsFalse((bool)run["frozenMaps"][0]["spawner"]["isRunning"]);
            Assert.IsTrue(JToken.DeepEquals(activeMonsters, run["monsters"]));
            Assert.IsTrue(JToken.DeepEquals(frozenMonsters, run["frozenMaps"][0]["monsters"]));
            Assert.IsEmpty((JArray)run["spawner"]["slots"]);
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
