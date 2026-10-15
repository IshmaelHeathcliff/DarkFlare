using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class SaveCaptureRestorePlayModeTests
    {
        const float TimeoutSeconds = 20f;

        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();
        SaveCoordinator _testCoordinator;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.Restart();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _testCoordinator?.EmergencyClose();
            _testCoordinator = null;
            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator DeadPlayer_RestoresWithoutStatusProjectionAndRebindsOnRevive()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            GameSessionHost session = host.CurrentSession;
            PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            CombatPrototypeBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<CombatPrototypeBootstrap>();
            CombatSystem combat = session.Architecture.GetSystem<CombatSystem>();
            combat.ApplyPeriodicDamage(new DamageSourceSnapshot("death-test", ActorTeam.Monster), player.Actor,
                new[] { new DamagePacket(DamageType.Physical, player.Actor.MaxHealth * 100, TagSet.Empty) });
            Assert.IsFalse(player.Actor.IsAlive);
            var source = new SessionSnapshotSource(session, host.ContentCatalog, spawner);
            SessionSnapshotResult snapshot = source.Capture();
            Assert.IsTrue(snapshot.Succeeded, Describe(snapshot));
            Assert.IsFalse(snapshot.Payload.Run.Statuses.Actors.Any(actor => actor.ActorKey.StartsWith("player:")));
            PreparedRestoreResult prepared = SaveRestorePreparer.Prepare(CreateDocument(snapshot.Payload, host.ContentCatalog), host.ContentCatalog);
            Assert.IsTrue(prepared.Succeeded, Describe(prepared));
            bool finished = false;
            LifecycleResult completion = default;
            host.BeginSceneSessionInitialization(SceneManager.GetActiveScene(),
                new RestoreGameSessionInitializer(bootstrap.CreateSceneConfiguration(), prepared.Value),
                onCompleted: result => { completion = result; finished = true; });
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!finished && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.IsTrue(finished);
            Assert.IsTrue(completion.IsSuccess, completion.Exception?.ToString());
            player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Assert.IsFalse(player.Actor.IsAlive);
            StatusSystem statuses = host.CurrentSession.Architecture.GetSystem<StatusSystem>();
            Assert.IsFalse(statuses.GetTarget(player.Actor).IsValid);
            deadline = Time.realtimeSinceStartup + 10;
            while (!player.Actor.IsAlive && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.IsTrue(player.Actor.IsAlive, "恢复后自然复活任务必须继续完成");
            Assert.IsTrue(statuses.GetTarget(player.Actor).IsValid);
        }

        [UnityTest]
        public IEnumerator MainSession_CapturePrepareRestorePreservesStateWithoutNewGameGrant()
        {
            yield return _fixture.EnterMain();
            SceneFlowResult changed = null;
            yield return ApplicationHost.Current.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:wilderness", "arrival"))
                .ToCoroutine(result => changed = result);
            Assert.IsTrue(changed.Succeeded);
            yield return WaitForRunningSession();
            ApplicationHost host = ApplicationHost.Current;
            GameSessionHost oldSession = host.CurrentSession;
            int oldGeneration = oldSession.ArchitectureGeneration;
            IArchitecture oldArchitecture = oldSession.Architecture;
            PlayerController oldPlayer = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            CombatPrototypeBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<CombatPrototypeBootstrap>();
            Assert.IsNotNull(oldPlayer);
            Assert.IsNotNull(spawner);
            Assert.IsNotNull(bootstrap);
            InventoryModel oldInventory = oldArchitecture.GetModel<InventoryModel>();
            oldInventory.AddGold(1000);
            ItemBaseDefinition material = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemBaseDefinition>("Assets/Data/Preset/Items/锻造矿石.asset");
            ItemInstance materials = material.CreateInstance(oldArchitecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 0, ItemRarity.Normal);
            materials.TrySetQuantity(5);
            Assert.IsTrue(oldInventory.TryAddItem(materials));
            EquipmentModel equipped = oldArchitecture.GetModel<EquipmentModel>();
            EconomyModel economy = oldArchitecture.GetModel<EconomyModel>();
            GameArchitectureTestFixture.PrepareCompleteMerchant(oldArchitecture);
            foreach (EquipmentSlot slot in EquipmentSlots.All)
            {
                if (equipped.GetItem(oldPlayer.Actor, slot) != null) { continue; }
                ItemInstance candidate = economy.MerchantStock.First(item => item.BaseDefinition.CanEquipTo(slot));
                Assert.IsTrue(oldArchitecture.SendCommand(new BuyItemCommand(candidate)), slot.ToString());
                Assert.IsTrue(oldArchitecture.SendCommand(new EquipItemCommand(oldPlayer.Actor, candidate, slot)), slot.ToString());
            }
            ItemInstance discarded = oldArchitecture.GetModel<EconomyModel>().MerchantStock[0];
            Assert.IsTrue(oldArchitecture.SendCommand(new BuyItemCommand(discarded)));
            Assert.IsTrue(oldArchitecture.SendCommand(new CraftItemCommand(
                CraftOperation.UpgradeRarity, CraftingAffixScope.Any, discarded)).Succeeded);
            int expectedItemSeed = discarded.Seed;
            string[] expectedAffixes = discarded.Prefixes.Concat(discarded.Suffixes)
                .Select(affix => affix.Definition.Id).ToArray();
            float[] expectedRolls = discarded.CollectModifiers().Select(modifier => modifier.Value).ToArray();
            Assert.IsNotEmpty(expectedAffixes, "丢弃存档回归必须包含真实词缀");
            LootSystem loot = oldArchitecture.GetSystem<LootSystem>();
            var prefabField = typeof(LootSystem).GetField("_pickupPrefabReference",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            object pickupReference = prefabField.GetValue(loot);
            RectInt originalPlacement = oldInventory.Grid.Placements[discarded];
            prefabField.SetValue(loot, null);
            Assert.IsFalse(oldArchitecture.SendCommand(new DiscardItemCommand(discarded, oldPlayer.Actor)));
            Assert.AreEqual(originalPlacement, oldInventory.Grid.Placements[discarded], "生成资源缺失时物品必须保留原格");
            prefabField.SetValue(loot, pickupReference);
            Assert.IsTrue(oldArchitecture.SendCommand(new DiscardItemCommand(discarded, oldPlayer.Actor)));
            LootPickupController originalDrop = UnityEngine.Object.FindObjectsByType<LootPickupController>()
                .Single(drop => drop.Item == discarded);
            string dropId = originalDrop.Id.Value;
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsFalse(oldInventory.Grid.Placements.ContainsKey(discarded), "丢弃后不能原地立即自动拾回");
            CombatResourceSnapshot oldResources = oldPlayer.Actor.Resources;
            float expectedHealth = oldResources.MaxHealth - 13f;
            oldPlayer.Actor.RestoreResources(
                new CombatResourceSnapshot(
                    expectedHealth,
                    oldResources.MaxHealth,
                    oldResources.CurrentMana,
                    oldResources.MaxMana),
                true);
            GameplayRandomSystem oldRandom = oldArchitecture.GetSystem<GameplayRandomSystem>();
            // 棋盘与专栏共用正式 Command、统一 Items 和 Session Restore。
            ItemBaseDefinition woodDefinition = host.ContentCatalog.GetAll<ItemBaseDefinition>().Single(d => d.Id == "firewood_large");
            ItemBaseDefinition flameDefinition = host.ContentCatalog.GetAll<ItemBaseDefinition>().Single(d => d.Id == "flame_projectile");
            ItemBaseDefinition bottleDefinition = host.ContentCatalog.GetAll<ItemBaseDefinition>().Single(d => d.Id == "energy_bottle_small");
            ItemInstance boardWood = woodDefinition.CreateInstance(oldArchitecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 86, ItemRarity.Normal);
            ItemInstance boardFlame = flameDefinition.CreateInstance(oldArchitecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 86, ItemRarity.Normal);
            ItemInstance boardBottle = bottleDefinition.CreateInstance(oldArchitecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 86, ItemRarity.Normal);
            Assert.That(oldInventory.TryAddItem(boardWood), Is.True);
            Assert.That(oldInventory.TryAddItem(boardFlame), Is.True);
            Assert.That(oldInventory.TryAddItem(boardBottle), Is.True);
            foreach (SkillBoardRequest request in new[]
            {
                new SkillBoardRequest(SkillBoardOperation.Install, boardWood, new HexCoordinate(16, 0), 2),
                new SkillBoardRequest(SkillBoardOperation.Embed, boardFlame, slot: new FirewoodPortReference(boardWood.InstanceId, SkillBoardGeometry.CenterNodeId)),
                new SkillBoardRequest(SkillBoardOperation.EquipBottle, boardBottle)
            })
            {
                SkillBoardPreview preview = oldArchitecture.SendQuery(new PreviewSkillBoardQuery(oldPlayer.Id.Value, request));
                Assert.That(preview.Success, Is.True, preview.Error);
                Assert.That(oldArchitecture.SendCommand(new CommitSkillBoardCommand(preview)), Is.True);
            }
            // 使用当前地图的实际怪物与周期伤害来源，走 CombatSystem 的唯一死亡转移。
            MonsterController energyMonster = UnityEngine.Object.FindObjectsByType<MonsterController>()
                .FirstOrDefault(m => m.Actor.IsAlive);
            Assert.That(energyMonster, Is.Not.Null, "野外运行样例需要存活怪物");
            int expectedKillEnergy = MonsterEnergyReward.Calculate(energyMonster.Definition, energyMonster.Instance);
            oldArchitecture.GetSystem<CombatSystem>().ApplyPeriodicDamage(
                new DamageSourceSnapshot(oldPlayer.Actor.ActorId, ActorTeam.Player), energyMonster.Actor,
                new[] { new DamagePacket(DamageType.Physical, energyMonster.Actor.MaxHealth * 1000, TagSet.Empty) });
            Assert.That(energyMonster.Actor.IsAlive, Is.False);
            Assert.That(boardBottle.SkillBoardState.Energy, Is.EqualTo(Math.Min(expectedKillEnergy, bottleDefinition.SkillBoard.EnergyCapacity)));
            ItemInstance growthBottle = bottleDefinition.CreateInstance(oldArchitecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 87, ItemRarity.Normal);
            growthBottle.CollectEnergy(1000);
            Assert.That(oldInventory.TryAddItem(growthBottle), Is.True);
            SkillBoardPreview charge = oldArchitecture.SendQuery(new PreviewSkillBoardQuery(oldPlayer.Id.Value,
                new SkillBoardRequest(SkillBoardOperation.ConsumeBottle, growthBottle)));
            Assert.That(charge.Success, Is.True, charge.Error);
            Assert.That(oldArchitecture.SendCommand(new CommitSkillBoardCommand(charge)), Is.True);
            string firstTalent = boardWood.SkillBoardState.Layout.Edges.Where(e => e.First == "center" || e.Second == "center")
                .Select(e => e.First == "center" ? e.Second : e.First).First();
            CombatResourceSnapshot beforeGrowthResources = oldPlayer.Actor.Resources;
            foreach (PreviewPlayerGrowthQuery query in new[]
            {
                new PreviewPlayerGrowthQuery(oldPlayer.Id.Value, woodId: boardWood.InstanceId),
                new PreviewPlayerGrowthQuery(oldPlayer.Id.Value, woodId: boardWood.InstanceId, nodeId: firstTalent),
                new PreviewPlayerGrowthQuery(oldPlayer.Id.Value, StatIds.Strength, 2),
                new PreviewPlayerGrowthQuery(oldPlayer.Id.Value, StatIds.Intelligence, 1)
            })
            {
                PlayerGrowthPreview preview = oldArchitecture.SendQuery(query);
                Assert.That(preview.Success, Is.True, preview.Error);
                Assert.That(oldArchitecture.SendCommand(new CommitPlayerGrowthCommand(preview)), Is.True);
            }
            expectedHealth *= oldPlayer.Actor.MaxHealth / beforeGrowthResources.MaxHealth;
            Assert.That(oldPlayer.Actor.CurrentHealth, Is.EqualTo(expectedHealth).Within(.001f));
            PlayerGrowthDto growthBefore = RuntimeStateMapper.ToDto(oldPlayer.Id.Value, oldArchitecture.GetModel<PlayerGrowthModel>().State);
            Assert.That(growthBefore.Energy, Is.EqualTo(25));
            Assert.That(growthBefore.Activations.Count, Is.EqualTo(2));
            SkillBoardSaveDto boardBefore = RuntimeStateMapper.ToDto(oldPlayer.Id.Value,
                oldArchitecture.GetModel<SkillBoardModel>().State);
            oldRandom.NextSeed(GameplayRandomChannel.Crafting);
            oldRandom.NextSeed(GameplayRandomChannel.Crafting);
            SessionSnapshotSource source = new SessionSnapshotSource(
                oldSession,
                host.ContentCatalog,
                spawner);

            StatusSystem oldStatuses = oldArchitecture.GetSystem<StatusSystem>();
            StatusDefinition weakness = host.ContentCatalog.GetAll<StatusDefinition>().Single(value => value.Id == "weakness");
            StatusTargetId statusTarget = oldStatuses.GetTarget(oldPlayer.Actor);
            // 在已登记材料上临时挂使用能力，验证真实 Command 与历史物品 DTO；不添加正式药水资产。
            var use = ScriptableObject.CreateInstance<ItemUseDefinition>();
            var useField = typeof(ItemBaseDefinition).GetField("_useDefinition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            object originalUse = useField.GetValue(material);
            int expectedMaterialCount = materials.Quantity - 1;
            ItemUseRequest usedRequest;
            try
            {
                typeof(ItemUseDefinition).GetField("_id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(use, "save_test_use");
                typeof(ItemUseDefinition).GetField("_effects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(use, new List<ItemUseEffect> { new ItemUseEffect(ItemUseEffectKind.Heal, 5),
                        new ItemUseEffect(ItemUseEffectKind.ApplyStatus, status: weakness) });
                useField.SetValue(material, use);
                usedRequest = oldArchitecture.SendCommand(new CreateItemUseRequestCommand(oldPlayer.Actor, materials));
                using (GamePauseLease pause = host.GameTime.AcquirePause("item-use-save-test"))
                {
                    Assert.That(oldArchitecture.SendCommand(new UseItemCommand(usedRequest)).Code, Is.EqualTo(ItemUseCode.Success));
                }
                expectedHealth += 5;
                Assert.That(materials.Quantity, Is.EqualTo(expectedMaterialCount));
            }
            finally
            {
                useField.SetValue(material, originalUse);
                UnityEngine.Object.DestroyImmediate(use);
            }

            ItemBaseDefinition manaPotion = host.ContentCatalog.GetAll<ItemBaseDefinition>().Single(item => item.Id == "mana_potion");
            ItemInstance potionStack = manaPotion.CreateInstance(oldArchitecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 17, ItemRarity.Normal);
            potionStack.TrySetQuantity(2);
            Assert.IsTrue(oldInventory.TryAddItem(potionStack));
            CombatResourceSnapshot beforeManaUse = oldPlayer.Actor.Resources;
            oldPlayer.Actor.RestoreResources(new CombatResourceSnapshot(beforeManaUse.CurrentHealth, beforeManaUse.MaxHealth,
                0, beforeManaUse.MaxMana), true);
            ItemUseRequest potionRequest = oldArchitecture.SendCommand(new CreateItemUseRequestCommand(oldPlayer.Actor, potionStack));
            using (GamePauseLease pause = host.GameTime.AcquirePause("formal-potion-save-test"))
            {
                Assert.IsTrue(oldArchitecture.SendCommand(new UseItemCommand(potionRequest)).Succeeded);
            }
            float expectedMana = oldPlayer.Actor.CurrentMana;
            Assert.AreEqual(1, potionStack.Quantity);
            long beforeRefreshRevision = host.SaveCoordinator.DirtyRevision;
            Assert.IsTrue(oldArchitecture.SendCommand(new RefreshMerchantCommand()));
            Assert.Greater(host.SaveCoordinator.DirtyRevision, beforeRefreshRevision);
            ItemInstance[] savedOffers = economy.MerchantStock.ToArray();
            SessionSnapshotResult captured = source.Capture();

            Assert.IsTrue(captured.Succeeded, Describe(captured));
            int capturedGold = captured.Payload.Profile.Gold;
            int capturedMerchantCount = captured.Payload.Run.Merchant.OrderedItemInstanceIds.Count;
            GameplayRandomState capturedRandom = oldRandom.CaptureState();
            GameplayRandomSystem randomOracle = new GameplayRandomSystem();
            randomOracle.RestoreState(capturedRandom);
            int expectedNextCraftSeed = randomOracle.NextSeed(GameplayRandomChannel.Crafting);
            RunInstanceIdState capturedIds = oldArchitecture
                .GetUtility<IRunInstanceIdGenerator>()
                .CaptureState();
            RunInstanceIdGenerator idOracle = new RunInstanceIdGenerator(capturedIds);
            string expectedNextMonsterId = idOracle.NextMonsterId().Value;
            SaveDocumentDto document = CreateDocument(captured.Payload, host.ContentCatalog);
            SaveSerializationResult serialized = new NewtonsoftSaveSerializer().Serialize(document);
            Assert.IsTrue(serialized.Succeeded, serialized.Exception?.ToString());
            SaveDeserializationResult deserialized = new NewtonsoftSaveSerializer().Deserialize(
                serialized.Bytes,
                SaveSlotId.Parse("auto"),
                1);
            Assert.IsTrue(deserialized.Succeeded, deserialized.Exception?.ToString());
            PreparedRestoreResult prepared = SaveRestorePreparer.Prepare(
                deserialized.Document,
                host.ContentCatalog);
            Assert.IsTrue(prepared.Succeeded, Describe(prepared));
            bool callbackReceived = false;
            LifecycleResult callbackResult = default;
            LifecycleResult submitted = host.BeginSceneSessionInitialization(
                SceneManager.GetActiveScene(),
                new RestoreGameSessionInitializer(
                    bootstrap.CreateSceneConfiguration(),
                    prepared.Value),
                onCompleted: result =>
                {
                    callbackResult = result;
                    callbackReceived = true;
                });
            Assert.IsTrue(submitted.IsSuccess, submitted.Message);
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while ((!callbackReceived
                    || host.CurrentSession == null
                    || host.CurrentSession.State != GameSessionState.Running)
                && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsTrue(callbackReceived, "Restore 回调超时");
            Assert.IsTrue(callbackResult.IsSuccess, callbackResult.Exception?.ToString());
            Assert.Greater(host.CurrentSession.ArchitectureGeneration, oldGeneration);
            IArchitecture restoredArchitecture = host.CurrentSession.Architecture;
            SkillBoardState restoredBoard = restoredArchitecture.SendQuery(new GetSkillBoardQuery());
            PlayerGrowthState restoredGrowth = restoredArchitecture.SendQuery(new GetPlayerGrowthQuery());
            AssertGrowthEquals(growthBefore, restoredGrowth);
            SkillBoardSaveDto boardAfter = RuntimeStateMapper.ToDto(boardBefore.ActorInstanceId, restoredBoard);
            CollectionAssert.AreEqual(boardBefore.Firewoods.Select(w => (w.ItemInstanceId, w.Q, w.R, w.Rotation)),
                boardAfter.Firewoods.Select(w => (w.ItemInstanceId, w.Q, w.R, w.Rotation)));
            CollectionAssert.AreEqual(boardBefore.Embeddings.Select(e => (e.FirewoodInstanceId, e.NodeId, e.ItemInstanceId)),
                boardAfter.Embeddings.Select(e => (e.FirewoodInstanceId, e.NodeId, e.ItemInstanceId)));
            Assert.That(restoredBoard.EnergyBottle.InstanceId, Is.EqualTo(boardBottle.InstanceId));
            Assert.That(restoredBoard.EnergyBottle.SkillBoardState.Energy, Is.EqualTo(
                captured.Payload.Items.Single(i => i.InstanceId == boardBottle.InstanceId).SkillBoard.Energy));
            Assert.That(restoredArchitecture.GetModel<InventoryModel>().Grid.Placements.Keys.Any(i => i.Id == boardWood.Id || i.Id == boardFlame.Id || i.Id == boardBottle.Id), Is.False);
            PrefabAssetLoader prefabLoader = restoredArchitecture.GetUtility<PrefabAssetLoader>();

            foreach (MonsterDefinition monster in prepared.Value.SpawnDefinition.AllMonsters)
            {
                Assert.IsNotNull(
                    prefabLoader.GetPrefab(monster.Prefab),
                    $"Restore 必须预热后续刷怪所需 Prefab: {monster.Id}");
            }

            PlayerController restoredPlayer = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(restoredPlayer);
            StatusSystem restoredStatuses = restoredArchitecture.GetSystem<StatusSystem>();
            StatusTargetId restoredTarget = restoredStatuses.GetTarget(restoredPlayer.Actor);
            StatusLayerSnapshot restoredWeakness = restoredStatuses.GetStatusSnapshot(restoredTarget).Layers.Single();
            StatusLayerDto savedWeakness = captured.Payload.Run.Statuses.Actors.Single(value => value.ActorKey == statusTarget.ActorKey).Layers.Single();
            Assert.That(restoredWeakness.Rules.Id.LocalId, Is.EqualTo("weakness"));
            Assert.That(restoredWeakness.Source.Actor, Is.EqualTo(restoredTarget));
            Assert.That(restoredWeakness.Source.Kind, Is.EqualTo(StatusSourceKind.Consumable));
            Assert.That(restoredArchitecture.GetModel<InventoryModel>().Grid.Placements.Keys.Single(item => item.Id == materials.Id).Quantity,
                Is.EqualTo(expectedMaterialCount));
            Assert.That(restoredArchitecture.SendCommand(new UseItemCommand(usedRequest)).Succeeded, Is.False, "新 Session 拒绝旧使用请求");
            Assert.That(material.UseDefinition, Is.Null, "恢复旧物品不能残留测试能力或再次恢复生命");
            Assert.That(restoredWeakness.ExpiresAt, Is.EqualTo(savedWeakness.ExpiresAt));
            Assert.That(restoredWeakness.Effects.Resistance.RawResistance, Is.EqualTo(savedWeakness.Effects.Resistance.Raw));
            Assert.AreEqual(expectedHealth, restoredPlayer.Actor.CurrentHealth, 0.001f);
            Assert.AreEqual(expectedMana, restoredPlayer.Actor.CurrentMana, 0.001f, "恢复不能再次执行即时回蓝");
            ItemInstance restoredPotion = restoredArchitecture.GetModel<InventoryModel>().Grid.Placements.Keys.Single(item => item.InstanceId == potionStack.InstanceId);
            Assert.AreEqual(1, restoredPotion.Quantity);
            Assert.AreSame(manaPotion.UseDefinition, restoredPotion.BaseDefinition.UseDefinition);
            Assert.IsFalse(restoredArchitecture.SendCommand(new UseItemCommand(potionRequest)).Succeeded);
            Assert.AreEqual(capturedGold, restoredArchitecture.GetModel<InventoryModel>().Gold);
            ItemInstance[] restoredOffers = restoredArchitecture.GetModel<EconomyModel>().MerchantStock.ToArray();
            CollectionAssert.AreEqual(savedOffers.Select(item => item.InstanceId), restoredOffers.Select(item => item.InstanceId));
            for (int i = 0; i < savedOffers.Length; i++)
            {
                Assert.AreEqual(savedOffers[i].Rarity, restoredOffers[i].Rarity);
                CollectionAssert.AreEqual(savedOffers[i].Prefixes.Concat(savedOffers[i].Suffixes).Select(affix => affix.Definition.Id),
                    restoredOffers[i].Prefixes.Concat(restoredOffers[i].Suffixes).Select(affix => affix.Definition.Id));
                CollectionAssert.AreEqual(savedOffers[i].Prefixes.Concat(savedOffers[i].Suffixes).SelectMany(affix => affix.Modifiers).Select(modifier => modifier.Value),
                    restoredOffers[i].Prefixes.Concat(restoredOffers[i].Suffixes).SelectMany(affix => affix.Modifiers).Select(modifier => modifier.Value));
            }
            Assert.AreEqual(
                capturedMerchantCount,
                restoredArchitecture.GetModel<EconomyModel>().MerchantStock.Count);
            Assert.AreEqual(
                expectedNextCraftSeed,
                restoredArchitecture.GetSystem<GameplayRandomSystem>()
                    .NextSeed(GameplayRandomChannel.Crafting));
            Assert.AreEqual(
                expectedNextMonsterId,
                restoredArchitecture.GetUtility<IRunInstanceIdGenerator>()
                    .NextMonsterId()
                    .Value);
            foreach (EquipmentSlot slot in EquipmentSlots.All)
            {
                Assert.AreEqual(captured.Payload.Profile.Equipment[0].Entries.Single(entry => entry.Slot == slot).ItemInstanceId,
                    restoredArchitecture.GetModel<EquipmentModel>().GetItem(restoredPlayer.Actor, slot).Id.Value,
                    $"{slot} 保存恢复后身份不一致");
            }
            Assert.AreEqual(
                captured.Payload.Run.Spawner.IsRunning,
                UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>().IsSpawning);
            LootPickupController restoredDrop = UnityEngine.Object.FindObjectsByType<LootPickupController>()
                .Single(drop => drop.Id.Value == dropId);
            ItemInstance restoredItem = restoredDrop.Item;
            Assert.AreEqual(discarded.InstanceId, restoredItem.InstanceId);
            Assert.AreEqual(expectedItemSeed, restoredItem.Seed);
            CollectionAssert.AreEqual(expectedAffixes, restoredItem.Prefixes.Concat(restoredItem.Suffixes)
                .Select(affix => affix.Definition.Id).ToArray());
            CollectionAssert.AreEqual(expectedRolls, restoredItem.CollectModifiers().Select(modifier => modifier.Value).ToArray());
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsNotNull(restoredDrop, "继续游戏后不应在原位自动拾回丢弃物");
            InventoryModel restoredInventory = restoredArchitecture.GetModel<InventoryModel>();
            Assert.IsFalse(restoredInventory.Grid.Placements.ContainsKey(restoredItem));
            int notifications = 0;
            IUnRegister registration = restoredArchitecture.RegisterEvent<InventoryChangedEvent>(change =>
            {
                if (change.Item != restoredItem) { return; }
                notifications++;
                Assert.IsNull(restoredDrop.Item, "拾取通知发出时必须已清除世界归属");
                Assert.IsFalse(restoredArchitecture.SendCommand(new PickupLootCommand(restoredDrop, restoredPlayer.Actor)),
                    "同帧重入不能重复拾取");
            });
            Assert.IsTrue(restoredArchitecture.SendCommand(new PickupLootCommand(restoredDrop, restoredPlayer.Actor)));
            Assert.IsTrue(restoredInventory.Grid.Placements.ContainsKey(restoredItem));
            Assert.AreEqual(1, notifications);
            registration.UnRegister();
            // 棋盘是角色资产，实际切图也必须保留所有权与固定变体。
            foreach (string map in new[] { "map:town", "map:wilderness" })
            {
                SceneFlowResult travel = null;
                yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap(map, map == "map:town" ? "return" : "arrival"))
                    .ToCoroutine(result => travel = result);
                Assert.That(travel.Succeeded, Is.True, travel.Exception?.ToString());
                yield return WaitForRunningSession();
                SkillBoardState travelled = host.CurrentSession.Architecture.SendQuery(new GetSkillBoardQuery());
                PlayerGrowthState travelledGrowth = host.CurrentSession.Architecture.SendQuery(new GetPlayerGrowthQuery());
                AssertGrowthEquals(growthBefore, travelledGrowth);
                Assert.That(travelled.Firewoods.Single().Item.InstanceId, Is.EqualTo(boardWood.InstanceId));
                Assert.That(travelled.Firewoods.Single().Placement.Rotation, Is.EqualTo(2));
                Assert.That(travelled.Embeddings.Single().Item.InstanceId, Is.EqualTo(boardFlame.InstanceId));
                Assert.That(travelled.EnergyBottle.InstanceId, Is.EqualTo(boardBottle.InstanceId));
                Assert.That(travelled.EnergyBottle.SkillBoardState.Energy, Is.GreaterThanOrEqualTo(
                    captured.Payload.Items.Single(i => i.InstanceId == boardBottle.InstanceId).SkillBoard.Energy));
            }
        }

        [UnityTest]
        public IEnumerator SessionFacade_SaveIsSessionScopedAndCrossSessionMethodsAreRemoved()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            GameSessionHost session = host.CurrentSession;
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            Assert.IsNotNull(spawner);
            LocalSaveStorage storage = new LocalSaveStorage(
                new MemorySavePathProvider("C:/DarkFlare-FacadePlayModeTests"),
                new NewtonsoftSaveSerializer(),
                new MemorySaveFileOperations());
            _testCoordinator = new SaveCoordinator(
                host.ProfileScope,
                storage,
                host.ContentCatalog,
                "test");
            _testCoordinator.BindSession(
                session,
                new SessionSnapshotSource(session, host.ContentCatalog, spawner));
            SessionSaveFacade facade = new SessionSaveFacade(
                host,
                session,
                _testCoordinator,
                SceneManager.GetActiveScene());
            InventoryModel inventory = session.Architecture.GetModel<InventoryModel>();
            inventory.AddGold(41);
            int savedGold = inventory.Gold;
            SaveOperationResult saveResult = null;
            yield return facade.SaveAutoAsync().ToCoroutine(result => saveResult = result);
            Assert.IsTrue(saveResult.Succeeded, saveResult.Exception?.ToString());
            Assert.AreEqual(savedGold, inventory.Gold);
            Assert.IsTrue(facade.IsAvailable);
            Assert.IsNull(typeof(SessionSaveFacade).GetMethod("ContinueAutoAsync"));
            Assert.IsNull(typeof(SessionSaveFacade).GetMethod("StartNewGameAsync"));
        }

        [UnityTest]
        public IEnumerator GameMenu_SaveActionsArePresentFocusableAndReflectSlotProbe()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
            RuntimePanelView document = menu != null ? menu.GetComponent<RuntimePanelView>() : null;
            Assert.IsNotNull(menu);
            Assert.IsNotNull(document);
            menu.OpenPage(GameMenuPage.Inventory);
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (menu.IsSaveOperationBusy && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsFalse(menu.IsSaveOperationBusy, "自动存档探测超时");
            VisualElement root = document.Root;
            menu.TogglePause();
            yield return null;
            Button save = root.Q<Button>("game-menu-save");
            Button returnFrontEnd = root.Q<Button>("game-menu-return-front-end");
            Label status = root.Q<Label>("game-menu-save-status");
            Assert.IsNotNull(save);
            Assert.IsNotNull(returnFrontEnd);
            Assert.IsNotNull(status);
            Assert.IsTrue(save.enabledSelf);
            Assert.IsTrue(returnFrontEnd.enabledSelf);
            Assert.IsNull(root.Q<Button>("game-menu-continue"));
            Assert.IsNull(root.Q<Button>("game-menu-new-game"));
            Assert.IsNull(root.Q<DropdownField>("game-menu-language-dropdown"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(status.text));
            save.Focus();
            yield return null;
            Assert.AreSame(save, root.focusController.focusedElement);
            returnFrontEnd.Focus();
            yield return null;
            Assert.AreSame(returnFrontEnd, root.focusController.focusedElement);
        }

        static void AssertGrowthEquals(PlayerGrowthDto expected, PlayerGrowthState actual)
        {
            Assert.That((actual.Energy, actual.EarnedPoints, actual.TalentPoints, actual.AttributePoints,
                actual.Strength, actual.Dexterity, actual.Intelligence),
                Is.EqualTo((expected.Energy, expected.EarnedPoints, expected.TalentPoints, expected.AttributePoints,
                    expected.Strength, expected.Dexterity, expected.Intelligence)));
            Assert.That(actual.Rules, Is.EqualTo(new GrowthRules(expected.FirstCost, expected.CostIncrement,
                expected.FirewoodCost, expected.TalentCost, expected.AttributeValue)));
            CollectionAssert.AreEqual(expected.Activations.Select(a => (a.FirewoodId, a.NodeId, a.Cost)),
                actual.Activations.Select(a => (a.FirewoodId, a.NodeId, a.Cost)));
        }

        static SaveDocumentDto CreateDocument(SavePayloadDto payload, ContentCatalog catalog)
        {
            string now = new DateTimeOffset(
                    2026,
                    8,
                    19,
                    0,
                    0,
                    0,
                    TimeSpan.Zero)
                .ToString("O");
            return new SaveDocumentDto
            {
                Header = new SaveHeaderDto
                {
                    FormatId = LocalSaveFormat.FormatId,
                    FormatVersion = LocalSaveFormat.FormatVersion,
                    SaveSchemaVersion = SaveSchemaVersion.Current.Value,
                    GameVersion = "0.2.2-alpha",
                    CatalogId = catalog.CatalogId,
                    ContentVersion = catalog.ContentVersion,
                    SlotId = "auto",
                    CommitSequence = 1,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    PayloadSha256 = new string('0', 64),
                    Summary = new SaveSummaryDto
                    {
                        RunId = payload.Run.InstanceIds.RunId,
                        Gold = payload.Profile.Gold,
                        ItemCount = payload.Items.Count,
                        CurrentHealth = payload.Run.Player.Resources.CurrentHealth,
                        MaxHealth = payload.Run.Player.Resources.MaxHealth,
                    },
                },
                Payload = payload,
            };
        }

        static IEnumerator WaitForRunningSession()
        {
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (Time.realtimeSinceStartup < timeout)
            {
                if (ApplicationHost.TryGetCurrent(out ApplicationHost host)
                    && host.CurrentSession != null
                    && host.CurrentSession.State == GameSessionState.Running)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("等待 Running Session 超时");
        }

        static string Describe(SessionSnapshotResult result)
        {
            return string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.Code} {issue.Path}: {issue.Message}"));
        }

        static string Describe(PreparedRestoreResult result)
        {
            return string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.Code} {issue.Path}: {issue.Message}"));
        }

        sealed class MemorySavePathProvider : ISavePathProvider
        {
            public MemorySavePathProvider(string rootPath)
            {
                RootPath = rootPath;
            }

            public string RootPath { get; }
        }

        sealed class MemorySaveFileOperations : ILocalSaveFileOperations
        {
            readonly HashSet<string> _directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            public bool DirectoryExists(string path)
            {
                return _directories.Contains(Normalize(path));
            }

            public void CreateDirectory(string path)
            {
                _directories.Add(Normalize(path));
            }

            public IReadOnlyList<string> EnumerateFiles(string path)
            {
                string prefix = Normalize(path).TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                return _files.Keys
                    .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            public bool FileExists(string path)
            {
                return _files.ContainsKey(Normalize(path));
            }

            public long GetFileLength(string path)
            {
                return _files[Normalize(path)].LongLength;
            }

            public byte[] ReadAllBytes(string path)
            {
                return _files[Normalize(path)].ToArray();
            }

            public void WriteAllBytesDurable(string path, byte[] bytes)
            {
                _files.Add(Normalize(path), bytes.ToArray());
            }

            public void MoveFileNoOverwrite(string sourcePath, string destinationPath)
            {
                string source = Normalize(sourcePath);
                string destination = Normalize(destinationPath);
                _files.Add(destination, _files[source]);
                _files.Remove(source);
            }

            public void DeleteFile(string path)
            {
                _files.Remove(Normalize(path));
            }

            static string Normalize(string path)
            {
                return Path.GetFullPath(path);
            }
        }
    }
}
