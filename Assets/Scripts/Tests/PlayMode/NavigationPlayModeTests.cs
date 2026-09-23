using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class NavigationPlayModeTests
    {
        readonly SceneFlowPlayModeFixture _fixture = new();
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _fixture.EnterFrontEnd();
            Assert.AreEqual(0, NavigationMemory.Bytes, "Session 结束必须释放地图及所有搜索容器");
        }
        [UnityTest]
        public IEnumerator MixedStrategies_DetourDynamicWall_RespectControlAndInvalidateHandles()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            SceneFlowResult switched = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:wilderness", "arrival")).ToCoroutine(value => switched = value);
            Assert.IsTrue(switched.Succeeded, switched.Exception?.ToString());
            IArchitecture architecture = host.CurrentSession.Architecture;
            MapScope scope = architecture.GetUtility<WorldMapRuntime>().Active;
            scope.Spawner.StopSpawning();
            var monsters = Object.FindObjectsByType<MonsterController>()
                .Where(value => value.Definition.BossBehavior == null).Take(2).ToArray();
            foreach (MonsterController existing in Object.FindObjectsByType<MonsterController>())
            { if (!monsters.Contains(existing)) { Object.Destroy(existing.gameObject); } }
            yield return null;
            NavigationMap navigation = scope.Navigation;
            NavigationStampTicket first = architecture.SendCommand(new ChangeNavigationStampCommand(scope.Generation, "wall-a", new MapGeometryRect(11, 8, 12, 12)));
            NavigationStampTicket congested = architecture.SendCommand(new ChangeNavigationStampCommand(scope.Generation, "wall-busy", new MapGeometryRect(13, 8, 14, 12)));
            Assert.AreEqual(NavigationStampStatus.Capacity, congested.Status, "无法在下一物理边界处理的变更必须立即拒绝，不能静默积压");
            yield return WaitTicket(first);
            Assert.AreEqual(NavigationStampStatus.Applied, first.Status);
            while (!navigation.IsReady) { yield return null; }
            for (int i = 0; i < monsters.Length; i++)
            {
                monsters[i].transform.position = new Vector3(9, 9 + i * 2);
                monsters[i].Body.position = monsters[i].transform.position;
                Assert.IsNotNull(monsters[i]);
                Assert.IsTrue(architecture.SendCommand(new SetNavigationIntentCommand(monsters[i],
                    new NavigationIntentDto
                    {
                        Mode = i == 0 ? NavigationMode.FlowField : NavigationMode.AStar,
                        Target = NavigationTargetKind.Point,
                        MapId = "map:wilderness",
                        Point = new Vector3Dto { X = 15, Y = 10 },
                        StopDistance = .3f
                    }, navigation.Handle(monsters[i]), takeBehaviorControl: true)));
            }
            float deadline = Time.realtimeSinceStartup + 12;
            while (Time.realtimeSinceStartup < deadline && (monsters[0].transform.position.x < 13 || monsters[1].transform.position.x < 13))
            {
                foreach (MonsterController monster in monsters)
                { Assert.IsFalse(monster.transform.position.x > 10.8f && monster.transform.position.x < 12.2f && monster.transform.position.y > 8 && monster.transform.position.y < 12, "不能穿过新增墙"); }
                yield return new WaitForFixedUpdate();
            }
            foreach (MonsterController monster in monsters)
            { Assert.Greater(monster.transform.position.x, 13, navigation.Status(monster) + " " + monster.transform.position); }
            NavigationAgentHandle handle = navigation.Handle(monsters[0]);
            StatusSystem statuses = architecture.GetSystem<StatusSystem>();
            statuses.ApplyStatus(statuses.GetTarget(monsters[0].Actor), StatusMutation.Apply(new StatusRules("nav_control"),
                new StatusSource(StatusSourceKind.Mechanism, "nav-test"), new StatusEffectSnapshot(blockedActions: StatusActionBlock.Move)));
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(Vector2.zero, monsters[0].Body.linearVelocity);
            monsters[0].gameObject.SetActive(false); monsters[0].gameObject.SetActive(true);
            Assert.IsFalse(architecture.SendCommand(new SetNavigationIntentCommand(monsters[0], new NavigationIntentDto(), handle)), "旧注册句柄不能控制重新注册的单位");
            Assert.IsTrue(architecture.SendCommand(new SetNavigationIntentCommand(monsters[1],
                new NavigationIntentDto { Target = NavigationTargetKind.None }, navigation.Handle(monsters[1]))));
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(NavigationStatus.TargetUnavailable, navigation.Status(monsters[1]));
            Assert.AreEqual(Vector2.zero, monsters[1].Body.linearVelocity);

            NavigationStampTicket overlap = architecture.SendCommand(new ChangeNavigationStampCommand(scope.Generation, "wall-b", new MapGeometryRect(11, 8, 12, 12)));
            yield return WaitTicket(overlap);
            NavigationStampTicket remove = architecture.SendCommand(new ChangeNavigationStampCommand(scope.Generation, "wall-a", default, true));
            yield return WaitTicket(remove);
            Assert.IsFalse(navigation.HasLineOfSight(new Vector2(10, 10), new Vector2(13, 10)), "删掉一个重叠 stamp 不能打通另一个");
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            player.enabled = false; player.GetComponent<Rigidbody2D>().position = new Vector2(13, 10);
            monsters[0].Body.position = new Vector2(9, 10);
            MonsterDefinition definition = monsters[0].Definition;
            MonsterDefinition longReach = Object.Instantiate(definition);
            typeof(MonsterDefinition).GetField("_contactDamageRadius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(longReach, 5f);
            GameplayRandomSystem random = architecture.GetSystem<GameplayRandomSystem>();
            GameplayRandomState before = random.CaptureState();
            int attacks = 0;
            IUnRegister observer = architecture.RegisterEvent<ActorAttackedEvent>(_ => attacks++);
            try
            {
                monsters[0].Configure(longReach, monsters[0].Instance);
                monsters[0].RestoreRuntime(monsters[0].Actor.Resources, 0);
                Assert.IsFalse(architecture.SendCommand(new ContactAttackCommand(monsters[0], player.Actor)));
                Assert.AreEqual(0, monsters[0].ContactDamageCooldownRemainingSeconds);
                Assert.AreEqual(0, attacks);
                CollectionAssert.AreEquivalent(before.NextSequences, random.CaptureState().NextSequences);
            }
            finally { observer.UnRegister(); monsters[0].Configure(definition, monsters[0].Instance); Object.Destroy(longReach); }
            Vector3 occupied = monsters[1].transform.position;
            NavigationStampTicket rejected = architecture.SendCommand(new ChangeNavigationStampCommand(scope.Generation, "occupied",
                new MapGeometryRect(occupied.x - .2, occupied.y - .2, occupied.x + .2, occupied.y + .2)));
            yield return WaitTicket(rejected); Assert.AreEqual(NavigationStampStatus.Occupied, rejected.Status);
        }
        [UnityTest]
        public IEnumerator MovingPlayer_KeepsFreshCompletedGuidance_AndPublishesWhileMoving()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            SceneFlowResult switched = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:wilderness", "arrival")).ToCoroutine(value => switched = value);
            Assert.IsTrue(switched.Succeeded);
            IArchitecture architecture = host.CurrentSession.Architecture;
            MapScope scope = architecture.GetUtility<WorldMapRuntime>().Active;
            scope.Spawner.StopSpawning();
            MonsterController monster = Object.FindObjectsByType<MonsterController>()
                .First(value => value.Definition.BossBehavior == null);
            foreach (MonsterController existing in Object.FindObjectsByType<MonsterController>())
            { if (existing != monster) { Object.Destroy(existing.gameObject); } }
            yield return null;
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            player.enabled = false;
            Rigidbody2D target = player.GetComponent<Rigidbody2D>();
            target.position = new Vector2(15, 10);
            monster.transform.position = new Vector3(9, 10);
            monster.Body.position = monster.transform.position;
            monster.Body.simulated = false;
            Assert.IsTrue(architecture.SendCommand(new SetNavigationIntentCommand(monster,
                new NavigationIntentDto { Mode = NavigationMode.FlowField }, scope.Navigation.Handle(monster), takeBehaviorControl: true)));
            float deadline = Time.realtimeSinceStartup + 5;
            while (scope.Navigation.Status(monster) != NavigationStatus.Moving && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.AreEqual(NavigationStatus.Moving, scope.Navigation.Status(monster));
            yield return new WaitForSecondsRealtime(1);
            target.position = new Vector2(15.5f, 10);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(NavigationStatus.Moving, scope.Navigation.Status(monster), "滞后窗口从目标变化开始，不能从旧场创建时间开始");
            NavigationModel metrics = architecture.GetModel<NavigationModel>();
            int published = metrics.ResultSequence;
            for (int i = 0; i < 40; i++)
            {
                target.position = new Vector2(15.5f + i * .025f, 10);
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(metrics.ResultSequence, published, "目标持续跨格移动不能饿死已接纳搜索");
            Assert.IsTrue(architecture.SendCommand(new SetNavigationIntentCommand(monster,
                new NavigationIntentDto { Mode = NavigationMode.AStar, Target = NavigationTargetKind.None }, scope.Navigation.Handle(monster))));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            long retained = NavigationMemory.Bytes;
            for (int i = 0; i < 8; i++)
            {
                NavigationMode mode = i % 2 == 0 ? NavigationMode.FlowField : NavigationMode.AStar;
                Assert.IsTrue(architecture.SendCommand(new SetNavigationIntentCommand(monster,
                    new NavigationIntentDto { Mode = mode, Target = NavigationTargetKind.None }, scope.Navigation.Handle(monster))));
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.AreEqual(retained, NavigationMemory.Bytes, "模式切换保留有界路径缓冲，不反复释放和申请");
                Assert.AreEqual(Vector2.zero, monster.Body.linearVelocity, "旧路线不能在等待态复用");
            }
            Object.Destroy(monster.gameObject);
            yield return null;
            Assert.Less(NavigationMemory.Bytes, retained, "注销归还实例路径缓冲");
        }
        static IEnumerator WaitTicket(NavigationStampTicket ticket)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (ticket.Status == NavigationStampStatus.Queued && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.AreNotEqual(NavigationStampStatus.Queued, ticket.Status);
        }
    }
}
