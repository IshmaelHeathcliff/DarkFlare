using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class WorldMapPlayModeTests
    {
        readonly SceneFlowPlayModeFixture _fixture = new SceneFlowPlayModeFixture();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host) && host.CurrentSession != null)
            {
                host.CurrentSession.Architecture.GetUtility<WorldMapRuntime>().FaultInjection = null;
                host.SaveCoordinator.ExclusiveSaveOverride = null;
                if (host.SceneFlow.State == GameFlowState.FatalError)
                {
                    LifecycleResult stopped = default;
                    yield return host.StopCurrentSessionAsync().ToCoroutine(value => stopped = value);
                    Assert.IsTrue(stopped.IsSuccess, stopped.Exception?.ToString());
                }
            }
            DismissFailure();
            yield return _fixture.EnterFrontEnd();
        }

        [UnityTest]
        public IEnumerator FormalInteractions_ReceivePhysicsFocusAndWallContactsCanRestore()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            foreach (WorldInteractionTarget target in UnityEngine.Object.FindObjectsByType<WorldInteractionTarget>())
            {
                Vector3 point = target.transform.position + Vector3.down;
                player.transform.position = point; body.position = point;
                Physics2D.SyncTransforms();
                yield return new WaitForSeconds(.15f);
                Assert.AreSame(target, player.GetComponent<PlayerInteractionController>().FocusedTarget,
                    target.MapBinding.Kind + " player=" + body.position + " target=" + target.transform.position);
            }
            player.enabled = false;
            body.position = new Vector2(-30, 0);
            for (int i = 0; i < 60; i++) { body.linearVelocity = Vector2.left * 5; yield return new WaitForFixedUpdate(); }
            body.linearVelocity = Vector2.zero;
            player.enabled = true;
            Assert.Greater(body.position.x, -32, "物理边界不能穿透");
            yield return _fixture.EnterFrontEnd();
            yield return _fixture.EnterMain(GameStartIntent.Continue);
            Assert.Less(UnityEngine.Object.FindAnyObjectByType<PlayerController>().transform.position.x, -31, "接触点应原样恢复");
        }

        [UnityTest]
        public IEnumerator MapSwitch_RollsBackEveryCommitBoundaryAndKeepsSessionPlayer()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            GameSessionHost session = host.CurrentSession;
            WorldMapRuntime maps = session.Architecture.GetUtility<WorldMapRuntime>();
            PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Assert.AreEqual("map:town", maps.Active.Map.MapId);
            Assert.IsNull(maps.Active.Spawner.SpawnDefinition);
            CompositeCollider2D geometry = maps.Active.Root.GetComponentInChildren<CompositeCollider2D>();
            Assert.Greater(geometry.pathCount, 0);
            Assert.IsTrue(geometry.OverlapPoint(new Vector2(-20, 16)), "正式地表水区必须存在物理阻挡");
            Assert.IsFalse(geometry.OverlapPoint(Vector2.zero), "默认入口必须可通行");
            foreach (string boundary in new[] { "prepared", "source-detached", "target-restored", "before-commit" })
            {
                MapScope source = maps.Active;
                maps.FaultInjection = phase => { if (phase == boundary) { throw new InvalidOperationException("map-test-" + boundary); } };
                SceneFlowResult failed = null;
                yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:wilderness", "arrival"))
                    .ToCoroutine(value => failed = value);
                Assert.IsFalse(failed.Succeeded, boundary);
                Assert.AreEqual(failed.FromState, failed.TargetState, boundary);
                Assert.AreSame(source, maps.Active, boundary);
                Assert.IsTrue(source.Root.activeInHierarchy, boundary);
                Assert.AreSame(session, host.CurrentSession);
                Assert.AreSame(player, UnityEngine.Object.FindAnyObjectByType<PlayerController>());
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PlayerController>().Length);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<MonsterSpawner>().Length);
                DismissFailure();
            }
            maps.FaultInjection = null;
            yield return Change(host, "map:wilderness", "arrival");
            Assert.IsNotNull(maps.Active.Spawner.SpawnDefinition);
            yield return Change(host, "map:town", "return");
            Assert.AreSame(session, host.CurrentSession);
            Assert.AreSame(player, UnityEngine.Object.FindAnyObjectByType<PlayerController>());
        }

        [UnityTest]
        public IEnumerator FiniteBatches_TriggerOnceAfterPauseAndFastCrossing()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            WorldMapRuntime maps = host.CurrentSession.Architecture.GetUtility<WorldMapRuntime>();
            yield return Change(host, "map:wilderness", "arrival");
            MonsterSpawner spawner = maps.Active.Spawner;
            Assert.AreEqual(100, spawner.PlannedCount);
            Assert.AreEqual(80, spawner.PendingCount);
            Assert.AreEqual(20, spawner.AliveCount);
            Assert.AreEqual(21, UnityEngine.Object.FindObjectsByType<MonsterController>().Length, "Boss 独立于普通名额");
            yield return null;
            using GamePauseLease pause = host.GameTime.AcquirePause("finite-batch-test");
            PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Vector3 destination = new Vector3(36, 32);
            player.transform.position = destination;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            body.position = destination;
            Physics2D.SyncTransforms();
            yield return null;
            Assert.AreEqual(80, spawner.PendingCount, "暂停时不能激活范围批次");
            pause.Dispose();
            for (int i = 0; i < 60 && spawner.PendingCount > 72; i++) { yield return null; }
            Assert.AreEqual(72, spawner.PendingCount, spawner.LastSpawnFailure);
            Assert.AreEqual(28, spawner.AliveCount + spawner.DefeatedCount);
            for (int i = 0; i < 10; i++) { yield return null; }
            Assert.AreEqual(100, spawner.PlannedCount, "重复进入范围不能追加名额");
            Assert.AreEqual(72, spawner.PendingCount);
            MonsterController victim = UnityEngine.Object.FindObjectsByType<MonsterController>()
                .First(monster => monster.Definition.BossBehavior == null && monster.Actor.IsAlive);
            int defeatedBefore = spawner.DefeatedCount;
            float fatalDamage = victim.Actor.MaxHealth + 100f;
            Assert.IsTrue(victim.Actor.ReceiveDamage(new DamageResult(true, false,
                new Dictionary<DamageType, float> { [DamageType.Physical] = fatalDamage },
                new Dictionary<DamageType, float> { [DamageType.Physical] = fatalDamage })));
            yield return null;
            Assert.AreEqual(defeatedBefore + 1, spawner.DefeatedCount);
            Assert.AreEqual(72, spawner.PendingCount, "击杀后不能补怪");
            Assert.AreEqual(100, spawner.PlannedCount);
            MonsterSpawnerStateDto before = spawner.CaptureState();
            yield return Change(host, "map:town", "return");
            yield return Change(host, "map:wilderness", "arrival");
            MonsterSpawnerStateDto restored = maps.Active.Spawner.CaptureState();
            CollectionAssert.AreEqual(before.Slots.Select(slot => (slot.SlotId, slot.Status, slot.InstanceId)),
                restored.Slots.Select(slot => (slot.SlotId, slot.Status, slot.InstanceId)),
                "切图后必须恢复同一批次的名额和怪物身份");
            Assert.IsTrue(restored.Batches.Single(batch => batch.BatchId == "east_patrol").IsActivated);
        }

        [UnityTest]
        public IEnumerator ManualReset_ReplacesOnlyTargetInstanceAndPersistsAcrossContinue()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            WorldMapRuntime maps = host.CurrentSession.Architecture.GetUtility<WorldMapRuntime>();
            using GamePauseLease pause = host.GameTime.AcquirePause("map-reset-test");
            yield return Change(host, "map:wilderness", "arrival");
            MonsterController boss = UnityEngine.Object.FindObjectsByType<MonsterController>()
                .Single(monster => monster.Definition.BossBehavior != null);
            string oldBossId = boss.Instance.Id.Value;
            MonsterController victim = UnityEngine.Object.FindObjectsByType<MonsterController>()
                .First(monster => monster.Definition.BossBehavior == null);
            float fatal = victim.Actor.MaxHealth + 100f;
            var damage = new Dictionary<DamageType, float> { [DamageType.Physical] = fatal };
            Assert.IsTrue(victim.Actor.ReceiveDamage(new DamageResult(true, false, damage, damage)));
            yield return null;
            Assert.AreEqual(1, maps.Active.Spawner.DefeatedCount);
            ItemBaseDefinition material = host.ContentCatalog.GetAll<ItemBaseDefinition>().First(item => item.Id == "forging_ore");
            ItemInstance dropItem = material.CreateInstance(host.CurrentSession.Architecture.GetUtility<IItemInstanceIdGenerator>().Next(),
                1, 53, ItemRarity.Normal);
            WorldDropId dropId = host.CurrentSession.Architecture.GetUtility<IRunInstanceIdGenerator>().NextWorldDropId();
            Assert.IsNotNull(host.CurrentSession.Architecture.GetSystem<LootSystem>()
                .SpawnRestoredPickup(dropId, dropItem, new Vector3(-10, -10)));
            yield return Change(host, "map:town", "return");
            for (int i = 0; i < 120 && host.SaveCoordinator.IsBusy; i++) { yield return null; }
            Assert.IsFalse(host.SaveCoordinator.IsBusy);
            var snapshots = new SessionSnapshotSource(host.CurrentSession, host.ContentCatalog, maps.Active.Spawner);
            SessionSnapshotResult before = snapshots.Capture();
            Assert.IsTrue(before.Succeeded, string.Join("; ", before.Issues.Select(issue => issue.Message)));
            MapStateDto oldMap = before.Payload.Run.FrozenMaps.Single(map => map.MapId == "map:wilderness");
            Assert.AreEqual(0, oldMap.InstanceEpoch);
            Assert.AreEqual(1, oldMap.Spawner.Slots.Count(slot => slot.Status == SpawnSlotStatus.Defeated));
            Assert.AreEqual(dropId.Value, oldMap.WorldDrops.Single().InstanceId);

            SceneFlowResult reset = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ResetAndChangeMap("map:wilderness", "arrival"))
                .ToCoroutine(value => reset = value);
            Assert.IsTrue(reset.Succeeded, reset.Exception?.ToString());
            Assert.AreEqual(1, maps.ActiveMapEpoch);
            Assert.AreEqual("map:wilderness", maps.LastResetMapId);
            Assert.AreEqual(MapResetPhase.Completed, maps.LastResetPhase);
            Assert.AreEqual(100, maps.Active.Spawner.PlannedCount);
            Assert.AreEqual(20, maps.Active.Spawner.AliveCount);
            Assert.AreEqual(80, maps.Active.Spawner.PendingCount);
            Assert.AreEqual(0, maps.Active.Spawner.DefeatedCount);
            Assert.AreNotEqual(oldBossId, UnityEngine.Object.FindObjectsByType<MonsterController>()
                .Single(monster => monster.Definition.BossBehavior != null).Instance.Id.Value);
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<LootPickupController>().Any(drop => drop.Id == dropId));
            SessionSnapshotResult after = snapshots.Capture();
            Assert.IsTrue(after.Succeeded, string.Join("; ", after.Issues.Select(issue => issue.Message)));
            Assert.IsFalse(after.Payload.Items.Any(item => item.InstanceId == dropItem.Id.Value));
            Assert.AreEqual(before.Payload.Profile.Gold, after.Payload.Profile.Gold);
            CollectionAssert.AreEqual(before.Payload.Profile.Inventory.Placements.Select(item => item.ItemInstanceId),
                after.Payload.Profile.Inventory.Placements.Select(item => item.ItemInstanceId));
            CollectionAssert.AreEqual(before.Payload.Profile.Equipment.SelectMany(loadout => loadout.Entries)
                    .Select(item => item.ItemInstanceId),
                after.Payload.Profile.Equipment.SelectMany(loadout => loadout.Entries)
                    .Select(item => item.ItemInstanceId));
            Assert.AreEqual(0, after.Payload.Run.FrozenMaps.Single(map => map.MapId == "map:town").InstanceEpoch);

            pause.Dispose();
            yield return _fixture.EnterFrontEnd();
            yield return _fixture.EnterMain(GameStartIntent.Continue);
            maps = ApplicationHost.Current.CurrentSession.Architecture.GetUtility<WorldMapRuntime>();
            Assert.AreEqual("map:wilderness", maps.Active.Map.MapId);
            Assert.AreEqual(1, maps.ActiveMapEpoch);
            Assert.AreEqual(0, maps.Active.Spawner.DefeatedCount);
            Assert.AreEqual(100, maps.Active.Spawner.PlannedCount);
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<LootPickupController>().Any(drop => drop.Id == dropId));
        }

        [UnityTest]
        public IEnumerator ManualReset_PreparationAndSaveFailuresKeepOldMap()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            WorldMapRuntime maps = host.CurrentSession.Architecture.GetUtility<WorldMapRuntime>();
            yield return Change(host, "map:wilderness", "arrival");
            yield return Change(host, "map:town", "return");
            for (int i = 0; i < 120 && host.SaveCoordinator.IsBusy; i++) { yield return null; }
            Assert.IsFalse(host.SaveCoordinator.IsBusy);
            var snapshots = new SessionSnapshotSource(host.CurrentSession, host.ContentCatalog, maps.Active.Spawner);
            MapStateDto previous = snapshots.Capture().Payload.Run.FrozenMaps.Single(map => map.MapId == "map:wilderness");
            foreach (bool saveFailure in new[] { false, true })
            {
                if (saveFailure)
                {
                    maps.FaultInjection = null;
                    host.SaveCoordinator.ExclusiveSaveOverride = _ => SaveOperationResult.Failure(
                        SaveOperation.Save, SaveCoordinator.AutoSlot, SaveErrorCode.CommitFailed);
                }
                else { maps.FaultInjection = phase => { if (phase == "before-save") { throw new InvalidOperationException("reset-test"); } }; }
                SceneFlowResult failed = null;
                yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ResetAndChangeMap("map:wilderness", "arrival"))
                    .ToCoroutine(value => failed = value);
                Assert.IsFalse(failed.Succeeded);
                if (saveFailure) { Assert.AreEqual(SceneFlowErrorCode.MapResetSaveFailed, failed.ErrorCode); }
                Assert.AreEqual("map:town", maps.Active.Map.MapId);
                Assert.AreEqual(0, maps.ActiveMapEpoch);
                Assert.IsFalse(maps.IsTransitioning);
                Assert.AreEqual(MapResetPhase.RolledBack, maps.LastResetPhase);
                MapStateDto retained = snapshots.Capture().Payload.Run.FrozenMaps.Single(map => map.MapId == "map:wilderness");
                Assert.AreEqual(previous.InstanceEpoch, retained.InstanceEpoch);
                CollectionAssert.AreEqual(previous.Spawner.Slots.Select(slot => (slot.SlotId, slot.Status, slot.InstanceId)),
                    retained.Spawner.Slots.Select(slot => (slot.SlotId, slot.Status, slot.InstanceId)));
                PrepareContinueResult persisted = null;
                yield return host.SaveCoordinator.PrepareContinueAsync(SaveCoordinator.AutoSlot)
                    .ToCoroutine(value => persisted = value);
                Assert.IsTrue(persisted.Succeeded, persisted.OperationResult.Exception?.ToString());
                Assert.AreEqual("map:town", persisted.PreparedRestore.Document.Payload.Run.ActiveMapId);
                MapStateDto savedMap = persisted.PreparedRestore.Document.Payload.Run.FrozenMaps
                    .Single(map => map.MapId == "map:wilderness");
                Assert.AreEqual(previous.InstanceEpoch, savedMap.InstanceEpoch);
                CollectionAssert.AreEqual(previous.Spawner.Slots.Select(slot => (slot.SlotId, slot.Status, slot.InstanceId)),
                    savedMap.Spawner.Slots.Select(slot => (slot.SlotId, slot.Status, slot.InstanceId)));
                DismissFailure();
            }
        }

        [UnityTest]
        public IEnumerator MapPortalChoice_OffersResetConfirmationAndCancelKeepsMap()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            WorldMapRuntime maps = host.CurrentSession.Architecture.GetUtility<WorldMapRuntime>();
            WorldInteractionTarget portal = UnityEngine.Object.FindObjectsByType<WorldInteractionTarget>()
                .Single(target => target.MapBinding?.Kind == MapInteractionKind.Portal);
            Assert.IsTrue(maps.CanReset(portal.MapBinding.TargetMapId));
            Assert.IsTrue(host.CurrentSession.Architecture.SendCommand(new OpenGameMenuCommand(portal)));
            VisualElement root = UnityEngine.Object.FindAnyObjectByType<ApplicationShellBootstrap>()
                .GetComponent<RuntimePanelView>().Root;
            Button enter = root.Q<Button>("application-modal-retry");
            Button reset = root.Q<Button>("application-modal-cancel");
            Assert.IsTrue(host.ApplicationShell.BlocksGameplay);
            Assert.IsFalse(string.IsNullOrWhiteSpace(enter.text));
            Assert.IsFalse(string.IsNullOrWhiteSpace(reset.text));
            Assert.AreSame(enter, root.panel.focusController.focusedElement);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = reset;
                reset.SendEvent(submit);
            }
            Assert.IsTrue(host.ApplicationShell.BlocksGameplay, "重置应进入二次确认");
            Assert.AreSame(enter, root.panel.focusController.focusedElement);
            DismissFailure();
            Assert.AreEqual("map:town", maps.Active.Map.MapId);
            Assert.AreEqual(0, maps.ActiveMapEpoch);
            Assert.IsFalse(host.ApplicationShell.BlocksGameplay);

            Assert.IsTrue(host.CurrentSession.Architecture.SendCommand(new OpenGameMenuCommand(portal)));
            using (NavigationSubmitEvent chooseReset = NavigationSubmitEvent.GetPooled())
            {
                chooseReset.target = reset;
                reset.SendEvent(chooseReset);
            }
            using (NavigationSubmitEvent confirm = NavigationSubmitEvent.GetPooled())
            {
                confirm.target = enter;
                enter.SendEvent(confirm);
            }
            float timeout = Time.realtimeSinceStartup + 15f;
            while (maps.Active.Map.MapId == "map:town" && Time.realtimeSinceStartup < timeout) { yield return null; }
            while (host.SceneFlow.IsBusy && Time.realtimeSinceStartup < timeout) { yield return null; }
            Assert.AreEqual("map:wilderness", maps.Active.Map.MapId, "传送门确认按钮应实际发起重置切图");
            Assert.AreEqual(1, maps.ActiveMapEpoch);
            Assert.AreEqual(MapResetPhase.Completed, maps.LastResetPhase);
            Assert.AreEqual(20, maps.Active.Spawner.AliveCount);
            Assert.IsFalse(host.ApplicationShell.BlocksGameplay);
        }

        [UnityTest]
        public IEnumerator ManualReset_PostCommitCleanupFailureKeepsDurableNewEpoch()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            WorldMapRuntime maps = host.CurrentSession.Architecture.GetUtility<WorldMapRuntime>();
            yield return Change(host, "map:wilderness", "arrival");
            yield return Change(host, "map:town", "return");
            while (host.SaveCoordinator.IsBusy) { yield return null; }
            maps.FaultInjection = phase =>
            {
                if (phase == "source-released") { throw new InvalidOperationException("reset-post-commit-test"); }
            };
            SceneFlowResult failed = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ResetAndChangeMap("map:wilderness", "arrival"))
                .ToCoroutine(value => failed = value);
            Assert.AreEqual(SceneFlowErrorCode.FatalCleanupFailed, failed.ErrorCode);
            Assert.AreEqual(GameFlowState.FatalError, host.SceneFlow.State);
            Assert.AreEqual(MapResetPhase.Fatal, maps.LastResetPhase);
            Assert.AreEqual(1, maps.ActiveMapEpoch);

            PrepareContinueResult saved = null;
            yield return host.SaveCoordinator.PrepareContinueAsync(SaveCoordinator.AutoSlot)
                .ToCoroutine(value => saved = value);
            Assert.IsTrue(saved.Succeeded, saved.OperationResult.Exception?.ToString());
            Assert.AreEqual("map:wilderness", saved.PreparedRestore.Document.Payload.Run.ActiveMapId);
            Assert.AreEqual(1, saved.PreparedRestore.Document.Payload.Run.ActiveMapEpoch);
            Assert.AreEqual(100, saved.PreparedRestore.Document.Payload.Run.Spawner.Slots.Count);
            Assert.AreEqual(20, saved.PreparedRestore.Document.Payload.Run.Spawner.Slots.Count(
                slot => slot.Status == SpawnSlotStatus.Alive));
        }

        [UnityTest]
        public IEnumerator ManualReset_TwentyCyclesReleaseMapOwners()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            IArchitecture architecture = host.CurrentSession.Architecture;
            WorldMapRuntime maps = architecture.GetUtility<WorldMapRuntime>();
            SessionObjectRegistry registry = architecture.GetUtility<SessionObjectRegistry>();
            int townObjects = registry.Count;
            using GamePauseLease pause = host.GameTime.AcquirePause("map-reset-cycles");
            long nativeBytes = -1;
            ResourceDiagnosticsSnapshot releasedBaseline = default;
            for (int i = 1; i <= 20; i++)
            {
                for (int frame = 0; frame < 120 && host.SaveCoordinator.IsBusy; frame++) { yield return null; }
                Assert.IsFalse(host.SaveCoordinator.IsBusy);
                SceneFlowResult result = null;
                yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ResetAndChangeMap("map:wilderness", "arrival"))
                    .ToCoroutine(value => result = value);
                Assert.IsTrue(result.Succeeded, $"cycle {i}: {result.Exception}");
                Assert.AreEqual(i, maps.ActiveMapEpoch);
                Assert.AreEqual(100, maps.Active.Spawner.PlannedCount);
                Assert.AreEqual(20, maps.Active.Spawner.AliveCount);
                Assert.AreEqual(21, UnityEngine.Object.FindObjectsByType<MonsterController>().Length);
                long currentNative = architecture.GetModel<NavigationModel>().NativeBytes;
                if (nativeBytes < 0) { nativeBytes = currentNative; }
                else { Assert.AreEqual(nativeBytes, currentNative, "导航容量不应逐轮增加"); }
                MapScope old = maps.Active;
                yield return Change(host, "map:town", "return");
                Assert.IsEmpty(registry.CaptureOwner(old));
                Assert.AreEqual(townObjects, registry.Count, "旧图对象不能逐轮残留");
                ResourceDiagnosticsSnapshot released = host.ResourceDiagnostics;
                Assert.AreEqual(0, released.InFlightLoads, $"cycle {i}: 旧图加载不能继续运行");
                if (i == 1)
                {
                    releasedBaseline = released;
                }
                else
                {
                    Assert.AreEqual(releasedBaseline.ActiveOwners, released.ActiveOwners,
                        $"cycle {i}: Addressables owner 不应逐轮增加");
                    Assert.AreEqual(releasedBaseline.ActiveEntries, released.ActiveEntries,
                        $"cycle {i}: Addressables entry 不应逐轮增加");
                    Assert.AreEqual(releasedBaseline.ActiveLeases, released.ActiveLeases,
                        $"cycle {i}: Addressables lease 不应逐轮增加");
                }
            }
        }

        [UnityTest]
        public IEnumerator AllRangeBatches_ActivateAndSpawnTheirReservedMonsters()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            WorldMapRuntime maps = host.CurrentSession.Architecture.GetUtility<WorldMapRuntime>();
            yield return Change(host, "map:wilderness", "arrival");
            CompiledMapDocument map = maps.Active.Map;
            MonsterSpawner spawner = maps.Active.Spawner;
            MapSpawnBatchRecord[] triggers = map.CreateEditableCopy().SpawnBatches
                .Where(batch => batch.Activation == MapSpawnActivation.PlayerRange).ToArray();
            Assert.GreaterOrEqual(triggers.Length, 10);
            PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            foreach (MapSpawnBatchRecord batch in triggers)
            {
                Vector2 center = new Vector2((float)(map.OriginX + batch.TriggerCenter.X),
                    (float)(map.OriginY + batch.TriggerCenter.Y));
                player.transform.position = center;
                body.position = center;
                Physics2D.SyncTransforms();
                yield return null;
                Assert.IsTrue(spawner.CaptureState().Batches.Single(state => state.BatchId == batch.BatchId).IsActivated,
                    "触发点未激活：" + batch.BatchId);
            }
            player.transform.position = Vector2.zero;
            body.position = Vector2.zero;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 120 && spawner.PendingCount > 0; i++) { yield return null; }
            Assert.AreEqual(0, spawner.PendingCount, spawner.LastSpawnFailure);
            Assert.AreEqual(100, spawner.AliveCount + spawner.DefeatedCount);
        }

        [UnityTest]
        public IEnumerator FrozenMap_RoundTripsMonstersDropsRandomAndReleasesOwners()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            IArchitecture architecture = host.CurrentSession.Architecture;
            WorldMapRuntime maps = architecture.GetUtility<WorldMapRuntime>();
            using GamePauseLease pause = host.GameTime.AcquirePause("map-regression");
            yield return Change(host, "map:wilderness", "arrival");
            MonsterController boss = UnityEngine.Object.FindObjectsByType<MonsterController>()
                .Single(value => value.Definition.BossBehavior != null);
            string bossId = boss.Instance.Id.Value;
            MonsterController monster = UnityEngine.Object.FindObjectsByType<MonsterController>()
                .First(value => value.Definition.BossBehavior == null);
            int expectedMonsterCount = UnityEngine.Object.FindObjectsByType<MonsterController>().Length;
            CombatResourceSnapshot resources = monster.Actor.Resources;
            monster.RestoreRuntime(new CombatResourceSnapshot(resources.MaxHealth / 2, resources.MaxHealth, resources.CurrentMana, resources.MaxMana), 0.37f);
            NavigationMap navigation = maps.Active.Navigation;
            Assert.IsTrue(architecture.SendCommand(new SetNavigationIntentCommand(monster,
                new NavigationIntentDto { Mode = NavigationMode.AStar, Target = NavigationTargetKind.Point, MapId = "map:wilderness",
                    Point = new Vector3Dto { X = 15, Y = 10 }, StopDistance = .3f }, navigation.Handle(monster), takeBehaviorControl: true)));
            long nativeBaseline = architecture.GetModel<NavigationModel>().NativeBytes;
            string id = monster.Instance.Id.Value;
            float health = monster.Actor.CurrentHealth;
            StatusSystem statuses = architecture.GetSystem<StatusSystem>();
            statuses.ApplyStatus(statuses.GetTarget(monster.Actor), StatusMutation.Apply(
                new StatusRules("weakness", duration: 12, interval: 1), new StatusSource(StatusSourceKind.Mechanism, "map-regression")));
            ItemBaseDefinition material = host.ContentCatalog.GetAll<ItemBaseDefinition>().First(item => item.Id == "forging_ore");
            ItemInstance item = material.CreateInstance(architecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 53, ItemRarity.Normal);
            WorldDropId dropId = architecture.GetUtility<IRunInstanceIdGenerator>().NextWorldDropId();
            Assert.IsNotNull(architecture.GetSystem<LootSystem>().SpawnRestoredPickup(dropId, item, new Vector3(-10, -10)));
            var snapshots = new SessionSnapshotSource(host.CurrentSession, host.ContentCatalog, maps.Active.Spawner);
            MapStateDto before = snapshots.CaptureMap(maps.Active);
            SessionObjectRegistry registry = architecture.GetUtility<SessionObjectRegistry>();
            int stableCount = registry.Count;
            for (int i = 0; i < 20; i++)
            {
                MapScope old = maps.Active;
                yield return Change(host, "map:town", "return");
                Assert.IsEmpty(registry.CaptureOwner(old));
                Assert.IsEmpty(UnityEngine.Object.FindObjectsByType<MonsterController>());
                SessionSnapshotResult town = snapshots.Capture();
                Assert.IsTrue(town.Succeeded, string.Join("; ", town.Issues.Select(issue => issue.Message)));
                MapStateDto frozen = town.Payload.Run.FrozenMaps.Single(map => map.MapId == "map:wilderness");
                Assert.AreEqual(id, frozen.Monsters.Single(value => value.InstanceId == id).InstanceId);
                Assert.AreEqual(bossId, frozen.Boss.InstanceId);
                yield return Change(host, "map:wilderness", "arrival");
                MonsterController restored = UnityEngine.Object.FindObjectsByType<MonsterController>()
                    .Single(value => value.Instance.Id.Value == id);
                Assert.AreEqual(bossId, UnityEngine.Object.FindObjectsByType<MonsterController>()
                    .Single(value => value.Definition.BossBehavior != null).Instance.Id.Value);
                Assert.AreEqual(id, restored.Instance.Id.Value);
                Assert.AreEqual(health, restored.Actor.CurrentHealth);
                Assert.AreEqual(NavigationTargetKind.None, restored.CaptureNavigation().Target, "恢复行为是导航意图的权威来源，调试接管不跨图延续");
                Assert.AreEqual(before.Monsters.Single(value => value.InstanceId == id).Behavior.Anchor.X, restored.CaptureBehavior().Anchor.X);
                Assert.AreEqual(before.Monsters.Single(value => value.InstanceId == id).Behavior.RandomSequence, restored.CaptureBehavior().RandomSequence);
                Assert.AreEqual(.37f, restored.ContactDamageCooldownRemainingSeconds, .001f);
                Assert.AreEqual(expectedMonsterCount, maps.Active.Behavior.Count);
                Assert.AreEqual(expectedMonsterCount, architecture.GetModel<BehaviorModel>().Count);
                Assert.AreEqual(nativeBaseline, architecture.GetModel<NavigationModel>().NativeBytes);
                Assert.AreEqual(expectedMonsterCount, maps.Active.Navigation.AgentCount);
                MapStateDto after = snapshots.CaptureMap(maps.Active);
                Assert.AreEqual(dropId.Value, after.WorldDrops.Single().InstanceId);
                Assert.AreEqual(item.Id.Value, after.WorldDrops.Single().ItemInstanceId);
                StatusLayerDto layer = after.Statuses.Actors.Single(value => value.ActorKey == id).Layers.Single();
                StatusLayerDto original = before.Statuses.Actors.Single(value => value.ActorKey == id).Layers.Single();
                Assert.AreEqual(original.ExpiresAt - before.Statuses.Time, layer.ExpiresAt - after.Statuses.Time);
                Assert.AreEqual(original.NextTickAt - before.Statuses.Time, layer.NextTickAt - after.Statuses.Time);
                Assert.AreEqual(original.TickOrdinal, layer.TickOrdinal);
                Assert.AreEqual(before.Random.RootSeed, after.Random.RootSeed);
                CollectionAssert.AreEqual(before.Random.Channels.Select(channel => (channel.Channel, channel.NextSequence)),
                    after.Random.Channels.Select(channel => (channel.Channel, channel.NextSequence)));
                Assert.AreEqual(stableCount, registry.Count);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<MonsterSpawner>().Length);
            }
            pause.Dispose();
            yield return _fixture.EnterFrontEnd();
            yield return _fixture.EnterMain(GameStartIntent.Continue);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<MonsterController>().Count(value => value.Instance.Id.Value == id));
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<LootPickupController>().Count(value => value.Id == dropId));
            yield return Change(host, "map:town", "return");
            yield return _fixture.EnterFrontEnd();
            yield return _fixture.EnterMain(GameStartIntent.Continue);
            yield return Change(host, "map:wilderness", "arrival");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<MonsterController>().Count(value => value.Instance.Id.Value == id));
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<LootPickupController>().Count(value => value.Id == dropId));
        }

        [UnityTest]
        public IEnumerator GuardianBoss_FightsAndPersists()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            yield return Change(host, "map:wilderness", "arrival");
            IArchitecture architecture = host.CurrentSession.Architecture;
            WorldMapRuntime maps = architecture.GetUtility<WorldMapRuntime>();
            maps.Active.Spawner.StopSpawning();
            MonsterController boss = UnityEngine.Object.FindObjectsByType<MonsterController>()
                .Single(value => value.Definition.BossBehavior != null);
            Assert.IsNotNull(boss.BossRuntime);
            Assert.AreEqual(new Vector2(20, 12), boss.Body.position);
            int bossAttacks = 0;
            IUnRegister attackObserver = architecture.RegisterEvent<ActorAttackedEvent>(attack =>
            {
                if (attack.Actor == boss.Actor) { bossAttacks++; }
            });
            PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            player.enabled = false;
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            void MovePlayer(Vector2 point)
            {
                player.transform.position = point; playerBody.position = point; playerBody.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
            MovePlayer(new Vector2(19, 12));
            float deadline = Time.realtimeSinceStartup + 4;
            while (boss.BossRuntime.Action != BossActionPhase.SwipeWarning && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.AreEqual(BossActionPhase.SwipeWarning, boss.BossRuntime.Action);
            LineRenderer warning = boss.GetComponentInChildren<LineRenderer>();
            yield return null;
            Assert.IsNotNull(warning);
            Assert.Greater(warning.positionCount, 0);
            deadline = Time.realtimeSinceStartup + 4;
            while (boss.BossRuntime.Action != BossActionPhase.SwipeRecovery && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.AreEqual(BossActionPhase.SwipeRecovery, boss.BossRuntime.Action);
            Assert.AreEqual(1, bossAttacks, "近战命中只能由 Boss 发起一次");

            MovePlayer(new Vector2(17, 12));
            deadline = Time.realtimeSinceStartup + 5;
            while (boss.BossRuntime.Action != BossActionPhase.ChargeWarning && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.AreEqual(BossActionPhase.ChargeWarning, boss.BossRuntime.Action);
            var snapshots = new SessionSnapshotSource(host.CurrentSession, host.ContentCatalog, maps.Active.Spawner);
            MapStateDto savedWarning = snapshots.CaptureMap(maps.Active);
            Assert.AreEqual(BossEncounterStatus.Alive, savedWarning.Boss.Status);
            Assert.AreEqual(BossActionPhase.ChargeWarning, savedWarning.Monsters.Single(value => value.Boss != null).Boss.Action);
            float health = player.Actor.CurrentHealth;
            MovePlayer(new Vector2(17, 15));
            deadline = Time.realtimeSinceStartup + 5;
            while (boss.BossRuntime.Action != BossActionPhase.ChargeRecovery && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.AreEqual(BossActionPhase.ChargeRecovery, boss.BossRuntime.Action);
            Assert.GreaterOrEqual(player.Actor.CurrentHealth, health, "躲开锁向轨迹后不能被冲锋命中");
            Assert.IsFalse(boss.CaptureBoss().HitResolved, "冲锋挥空不能消耗 Boss 的命中许可");
            Assert.AreEqual(1, bossAttacks, "冲锋挥空不能发出 Boss 攻击事件");
            attackObserver.UnRegister();

            CombatResourceSnapshot resources = boss.Actor.Resources;
            boss.Actor.RestoreResources(new CombatResourceSnapshot(resources.MaxHealth * .4f, resources.MaxHealth,
                resources.CurrentMana, resources.MaxMana), true);
            deadline = Time.realtimeSinceStartup + 5;
            while (!boss.CaptureBoss().Empowered && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.IsTrue(boss.CaptureBoss().Empowered);
            long phaseAction = boss.CaptureBoss().ActionId;
            yield return new WaitForSeconds(.3f);
            Assert.IsTrue(boss.CaptureBoss().Empowered);
            Assert.GreaterOrEqual(boss.CaptureBoss().ActionId, phaseAction);

            string bossId = boss.Instance.Id.Value;
            float fatal = boss.Actor.CurrentHealth + 10;
            var damage = new Dictionary<DamageType, float> { [DamageType.Physical] = fatal };
            Assert.IsTrue(boss.Actor.ReceiveDamage(new DamageResult(true, false, damage, damage)));
            yield return new WaitForSeconds(.8f);
            Assert.AreEqual(BossEncounterStatus.Defeated, snapshots.CaptureMap(maps.Active).Boss.Status);
            yield return Change(host, "map:town", "return");
            yield return Change(host, "map:wilderness", "arrival");
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<MonsterController>()
                .Any(value => value.Definition.BossBehavior != null));
            Assert.AreEqual(BossEncounterStatus.Defeated, snapshots.CaptureMap(maps.Active).Boss.Status);
            Assert.AreEqual(bossId, snapshots.CaptureMap(maps.Active).Boss.InstanceId);
            player.enabled = true;
        }

        [UnityTest]
        public IEnumerator Requests_CancelSupersedeAndRejectStaleTargetsWithoutPublishingCandidates()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            WorldMapRuntime maps = host.CurrentSession.Architecture.GetUtility<WorldMapRuntime>();
            MapScope source = maps.Active;
            foreach (SceneFlowRequest request in new[] { SceneFlowRequest.ChangeMap("map:missing", "arrival"), SceneFlowRequest.ChangeMap("map:wilderness", "missing") })
            {
                SceneFlowResult rejected = null;
                yield return host.SceneFlow.RequestAsync(request).ToCoroutine(value => rejected = value);
                Assert.IsFalse(rejected.Succeeded);
                Assert.AreSame(source, maps.Active);
                DismissFailure();
            }
            using (var cancellation = new CancellationTokenSource())
            {
                maps.FaultInjection = phase => { if (phase == "prepared") { cancellation.Cancel(); } };
                SceneFlowResult cancelled = null;
                yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:wilderness", "arrival"), cancellation.Token)
                    .ToCoroutine(value => cancelled = value);
                Assert.AreEqual(SceneFlowErrorCode.Cancelled, cancelled.ErrorCode);
                Assert.AreSame(source, maps.Active);
                Assert.IsFalse(maps.IsTransitioning);
            }
            maps.FaultInjection = null;
            UniTask<SceneFlowResult> first = host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:wilderness", "arrival"));
            UniTask<SceneFlowResult> second = host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:town", "return"));
            UniTask<SceneFlowResult> last = host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:wilderness", "arrival"));
            SceneFlowResult a = null, b = null, c = null;
            yield return first.ToCoroutine(value => a = value);
            yield return second.ToCoroutine(value => b = value);
            yield return last.ToCoroutine(value => c = value);
            Assert.AreEqual(SceneFlowErrorCode.Superseded, a.ErrorCode);
            Assert.AreEqual(SceneFlowErrorCode.Superseded, b.ErrorCode);
            Assert.IsTrue(c.Succeeded, c.Exception?.ToString());
            Assert.AreEqual("map:wilderness", maps.Active.Map.MapId);
            WorldInteractionTarget portal = UnityEngine.Object.FindObjectsByType<WorldInteractionTarget>().Single();
            portal.ConfigureMap(portal.MapBinding, maps.Active.Generation - 1);
            Assert.IsFalse(host.CurrentSession.Architecture.SendCommand(new OpenGameMenuCommand(portal)));
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PlayerController>().Length);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<MonsterSpawner>().Length);
        }

        [UnityTest]
        public IEnumerator CommittedCleanupFailure_EntersFatalWithoutPretendingRollback()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            WorldMapRuntime maps = host.CurrentSession.Architecture.GetUtility<WorldMapRuntime>();
            maps.FaultInjection = phase => { if (phase == "source-released") { throw new InvalidOperationException("cleanup-test"); } };
            SceneFlowResult result = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:wilderness", "arrival"))
                .ToCoroutine(value => result = value);
            Assert.AreEqual(SceneFlowErrorCode.FatalCleanupFailed, result.ErrorCode);
            Assert.AreEqual(GameFlowState.FatalError, host.SceneFlow.State);
            Assert.AreEqual("map:wilderness", maps.Active.Map.MapId);
            Assert.IsFalse(maps.IsTransitioning);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PlayerController>().Length);
        }

        static void DismissFailure()
        {
            ApplicationShellBootstrap shell = UnityEngine.Object.FindAnyObjectByType<ApplicationShellBootstrap>();
            Button cancel = shell?.GetComponent<RuntimePanelView>().Root?.Q<Button>("application-modal-cancel");
            if (cancel == null) { return; }
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            submit.target = cancel;
            cancel.SendEvent(submit);
        }

        static IEnumerator Change(ApplicationHost host, string mapId, string entry)
        {
            SceneFlowResult result = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap(mapId, entry)).ToCoroutine(value => result = value);
            Assert.IsTrue(result.Succeeded, result.Exception?.ToString() ?? result.ErrorCode.ToString());
        }
    }
}
