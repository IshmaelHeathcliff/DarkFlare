using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkFlare.Tests
{
    internal sealed class SceneFlowPlayModeFixture
    {
        const float ReadyTimeoutSeconds = 30f;

        public IEnumerator ReloadBootstrap()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
        }

        public IEnumerator EnterFrontEnd()
        {
            if (ApplicationHost.TryGetCurrent(out ApplicationHost divergentHost)
                && divergentHost.SceneFlow != null
                && (divergentHost.SceneFlow.State == GameFlowState.FatalError
                    || ((divergentHost.SceneFlow.State == GameFlowState.InGame
                            || divergentHost.SceneFlow.State == GameFlowState.Paused)
                        && divergentHost.CurrentSession == null)))
            {
                Object.Destroy(divergentHost.gameObject);
                yield return null;
                GameObject hostObject = new GameObject("[ApplicationHost]");
                hostObject.AddComponent<ApplicationHost>();
                yield return null;
            }

            Scene bootstrap = SceneManager.GetSceneByName("Bootstrap");
            bool sceneFlowReady = ApplicationHost.TryGetCurrent(out ApplicationHost currentHost)
                && currentHost.SceneFlow != null;
            ApplicationShellBootstrap shell =
                Object.FindAnyObjectByType<ApplicationShellBootstrap>();

            if (!bootstrap.IsValid()
                || !bootstrap.isLoaded
                || !sceneFlowReady
                || shell == null)
            {
                yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            }

            float timeout = Time.realtimeSinceStartup + ReadyTimeoutSeconds;

            while ((!ApplicationHost.TryGetCurrent(out ApplicationHost host)
                    || host.State != ApplicationLifecycleState.Ready
                    || host.SceneFlow == null
                    || host.SceneFlow.State == GameFlowState.Boot
                    || host.SceneFlow.IsBusy)
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsTrue(ApplicationHost.TryGetCurrent(out ApplicationHost readyHost));
            Assert.AreEqual(ApplicationLifecycleState.Ready, readyHost.State);
            Assert.IsNotNull(readyHost.SceneFlow, "Scene Flow 未在超时前就绪");

            if (readyHost.SceneFlow.State == GameFlowState.InGame
                || readyHost.SceneFlow.State == GameFlowState.Paused)
            {
                SceneFlowResult returnResult = null;
                yield return readyHost.SceneFlow.RequestAsync(
                        SceneFlowRequest.ReturnToFrontEnd())
                    .ToCoroutine(result => returnResult = result);
                Assert.IsTrue(returnResult.Succeeded, returnResult.Exception?.ToString());
            }

            Assert.AreEqual(GameFlowState.FrontEnd, readyHost.SceneFlow.State);
        }

        public IEnumerator EnterMain(GameStartIntent intent = GameStartIntent.NewGame)
        {
            yield return EnterFrontEnd();
            ApplicationHost host = ApplicationHost.Current;
            SceneFlowResult startResult = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.StartGame(intent))
                .ToCoroutine(result => startResult = result);
            Assert.IsTrue(startResult.Succeeded, startResult.Exception?.ToString());
            Assert.AreEqual(GameFlowState.InGame, host.SceneFlow.State);
            Assert.IsNotNull(host.CurrentSession);
            Assert.AreEqual(GameSessionState.Running, host.CurrentSession.State);
        }
    }
}
