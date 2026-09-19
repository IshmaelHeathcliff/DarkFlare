using System;
using System.Collections;
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
        public IEnumerator FrozenMap_RoundTripsMonstersDropsRandomAndReleasesOwners()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            IArchitecture architecture = host.CurrentSession.Architecture;
            WorldMapRuntime maps = architecture.GetUtility<WorldMapRuntime>();
            using GamePauseLease pause = host.GameTime.AcquirePause("map-regression");
            yield return Change(host, "map:wilderness", "arrival");
            MonsterController monster = architecture.GetSystem<SpawnSystem>().SpawnMonster(maps.Active.Spawner.SpawnDefinition, 349, new Vector3(10, 10));
            Assert.IsNotNull(monster);
            maps.Active.Spawner.RegisterRestoredMonster(monster);
            CombatResourceSnapshot resources = monster.Actor.Resources;
            monster.RestoreRuntime(new CombatResourceSnapshot(resources.MaxHealth / 2, resources.MaxHealth, resources.CurrentMana, resources.MaxMana), 0.37f);
            NavigationMap navigation = maps.Active.Navigation;
            Assert.IsTrue(architecture.SendCommand(new SetNavigationIntentCommand(monster,
                new NavigationIntentDto { Mode = NavigationMode.AStar, Target = NavigationTargetKind.Point, MapId = "map:wilderness",
                    Point = new Vector3Dto { X = 15, Y = 10 }, StopDistance = .3f }, navigation.Handle(monster))));
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
                Assert.AreEqual(id, town.Payload.Run.FrozenMaps.Single(map => map.MapId == "map:wilderness").Monsters.Single().InstanceId);
                yield return Change(host, "map:wilderness", "arrival");
                MonsterController restored = UnityEngine.Object.FindObjectsByType<MonsterController>().Single();
                Assert.AreEqual(id, restored.Instance.Id.Value);
                Assert.AreEqual(health, restored.Actor.CurrentHealth);
                Assert.AreEqual(NavigationMode.AStar, restored.CaptureNavigation().Mode);
                Assert.AreEqual(15, restored.CaptureNavigation().Point.X);
                Assert.AreEqual(nativeBaseline, architecture.GetModel<NavigationModel>().NativeBytes);
                Assert.AreEqual(1, maps.Active.Navigation.AgentCount);
                MapStateDto after = snapshots.CaptureMap(maps.Active);
                Assert.AreEqual(dropId.Value, after.WorldDrops.Single().InstanceId);
                Assert.AreEqual(item.Id.Value, after.WorldDrops.Single().ItemInstanceId);
                StatusLayerDto layer = after.Statuses.Actors.Single().Layers.Single();
                StatusLayerDto original = before.Statuses.Actors.Single().Layers.Single();
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
