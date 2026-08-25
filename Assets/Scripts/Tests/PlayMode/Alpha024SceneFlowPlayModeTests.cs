using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha024SceneFlowPlayModeTests
    {
        readonly SceneFlowPlayModeFixture _fixture = new SceneFlowPlayModeFixture();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.EnterFrontEnd();
            ApplicationHost host = ApplicationHost.Current;

            SceneFlowResult frontEndResult = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.EnterFrontEnd())
                .ToCoroutine(result => frontEndResult = result);
            Assert.IsTrue(frontEndResult.Succeeded, frontEndResult.Exception?.ToString());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host)
                && host.SceneFlow != null
                && (host.SceneFlow.State == GameFlowState.InGame
                    || host.SceneFlow.State == GameFlowState.Paused))
            {
                SceneFlowResult result = null;
                yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ReturnToFrontEnd())
                    .ToCoroutine(value => result = value);
                Assert.IsTrue(
                    result.Succeeded,
                    $"{result.ErrorCode} / {result.Phase}\n{result.Exception}");
            }

            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ThreeCycles_NewGamePauseAndReturnKeepTopologyUnique()
        {
            ApplicationHost host = ApplicationHost.Current;
            ResourceDiagnosticsSnapshot applicationBaseline = host.ResourceDiagnostics;
            Assert.AreEqual(2, applicationBaseline.ActiveOwners);
            Assert.AreEqual(1, applicationBaseline.ActiveEntries);
            Assert.AreEqual(1, applicationBaseline.ActiveLeases);
            Assert.AreEqual(0, applicationBaseline.InFlightLoads);

            for (int cycle = 0; cycle < 3; cycle++)
            {
                SceneFlowResult startResult = null;
                yield return host.SceneFlow.RequestAsync(
                        SceneFlowRequest.StartGame(GameStartIntent.NewGame))
                    .ToCoroutine(result => startResult = result);
                Assert.IsTrue(startResult.Succeeded, startResult.Exception?.ToString());
                AssertInGameTopology(host, cycle);
                ResourceDiagnosticsSnapshot sessionSnapshot = host.ResourceDiagnostics;
                Assert.GreaterOrEqual(
                    sessionSnapshot.ActiveOwners,
                    applicationBaseline.ActiveOwners + 2,
                    $"cycle={cycle}: Session 资源 owner 未建立");
                Assert.Greater(
                    sessionSnapshot.ActiveLeases,
                    applicationBaseline.ActiveLeases,
                    $"cycle={cycle}: Session 资源未持有租约");
                Assert.AreEqual(0, sessionSnapshot.InFlightLoads);

                GameSessionHost session = host.CurrentSession;
                using (host.GameTime.AcquirePause($"alpha-0.2.4-cycle-{cycle}"))
                {
                    Assert.AreEqual(GameFlowState.Paused, host.SceneFlow.State);
                    Assert.AreSame(session, host.CurrentSession);
                    Assert.AreEqual(0f, Time.timeScale);

                    SceneFlowResult returnResult = null;
                    yield return host.SceneFlow.RequestAsync(
                            SceneFlowRequest.ReturnToFrontEnd())
                        .ToCoroutine(result => returnResult = result);
                    Assert.IsTrue(
                        returnResult.Succeeded,
                        $"cycle={cycle}: {returnResult.ErrorCode} / {returnResult.Phase}\n"
                        + returnResult.Exception);
                }

                yield return null;
                AssertFrontEndTopology(host, cycle);
                ResourceDiagnosticsSnapshot released = host.ResourceDiagnostics;
                Assert.AreEqual(
                    applicationBaseline.ActiveOwners,
                    released.ActiveOwners,
                    $"cycle={cycle}: Session owner 未回到基线");
                Assert.AreEqual(
                    applicationBaseline.ActiveEntries,
                    released.ActiveEntries,
                    $"cycle={cycle}: Session entry 未回到基线");
                Assert.AreEqual(
                    applicationBaseline.ActiveLeases,
                    released.ActiveLeases,
                    $"cycle={cycle}: Session lease 未回到基线");
                Assert.AreEqual(0, released.InFlightLoads);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Continue_RestoresCommittedRunWithoutNewGameGrant()
        {
            ApplicationHost host = ApplicationHost.Current;
            SceneFlowResult startResult = null;
            yield return host.SceneFlow.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame))
                .ToCoroutine(result => startResult = result);
            Assert.IsTrue(startResult.Succeeded, startResult.Exception?.ToString());

            InventoryModel inventory = host.CurrentSession.Architecture.GetModel<InventoryModel>();
            inventory.AddGold(73);
            int savedGold = inventory.Gold;
            string savedRunId = host.CurrentSession.Architecture
                .GetUtility<IRunInstanceIdGenerator>()
                .CaptureState()
                .RunId
                .Value;
            SaveOperationResult saveResult = null;
            yield return host.SessionSaveFacade.SaveAutoAsync()
                .ToCoroutine(result => saveResult = result);
            Assert.IsTrue(saveResult.Succeeded, saveResult.Exception?.ToString());

            SceneFlowResult returnResult = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ReturnToFrontEnd())
                .ToCoroutine(result => returnResult = result);
            Assert.IsTrue(returnResult.Succeeded, returnResult.Exception?.ToString());

            while (host.SaveCoordinator.IsBusy)
            {
                yield return null;
            }

            SceneFlowResult continueResult = null;
            yield return host.SceneFlow.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.Continue))
                .ToCoroutine(result => continueResult = result);
            Assert.IsTrue(continueResult.Succeeded, continueResult.Exception?.ToString());
            Assert.AreEqual(
                savedGold,
                host.CurrentSession.Architecture.GetModel<InventoryModel>().Gold);
            Assert.AreEqual(
                savedRunId,
                host.CurrentSession.Architecture
                    .GetUtility<IRunInstanceIdGenerator>()
                    .CaptureState()
                    .RunId
                    .Value);
            Assert.AreEqual(
                1,
                Object.FindObjectsByType<PlayerController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length);
        }

        static void AssertInGameTopology(ApplicationHost host, int cycle)
        {
            Assert.AreEqual(GameFlowState.InGame, host.SceneFlow.State, $"cycle={cycle}");
            Assert.AreEqual("Main", SceneManager.GetActiveScene().name, $"cycle={cycle}");
            Assert.AreEqual(2, SceneManager.sceneCount, $"cycle={cycle}");
            Assert.IsNotNull(host.CurrentSession, $"cycle={cycle}");
            Assert.AreEqual(GameSessionState.Running, host.CurrentSession.State, $"cycle={cycle}");
            Assert.IsTrue(GameArchitectureProvider.HasCurrent, $"cycle={cycle}");
            Assert.AreEqual(
                1,
                Object.FindObjectsByType<PlayerController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                $"cycle={cycle}");
            Assert.AreEqual(
                1,
                Object.FindObjectsByType<CombatPrototypeBootstrap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                $"cycle={cycle}");
            AssertUniquePersistentInfrastructure(cycle);
        }

        static void AssertFrontEndTopology(ApplicationHost host, int cycle)
        {
            Assert.AreEqual(GameFlowState.FrontEnd, host.SceneFlow.State, $"cycle={cycle}");
            Assert.AreEqual("Bootstrap", SceneManager.GetActiveScene().name, $"cycle={cycle}");
            Assert.AreEqual(1, SceneManager.sceneCount, $"cycle={cycle}");
            Assert.IsNull(host.CurrentSession, $"cycle={cycle}");
            Assert.IsFalse(GameArchitectureProvider.HasCurrent, $"cycle={cycle}");
            Assert.AreEqual(1f, Time.timeScale, $"cycle={cycle}");
            Assert.AreEqual(
                0,
                Object.FindObjectsByType<PlayerController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                $"cycle={cycle}");
            AssertUniquePersistentInfrastructure(cycle);
        }

        static void AssertUniquePersistentInfrastructure(int cycle)
        {
            Assert.AreEqual(
                1,
                Object.FindObjectsByType<ApplicationHost>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                $"cycle={cycle}");
            Assert.AreEqual(
                1,
                Object.FindObjectsByType<ApplicationShellBootstrap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                $"cycle={cycle}");
            Assert.AreEqual(
                1,
                Object.FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                $"cycle={cycle}");
        }
    }
}
