using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    internal sealed class GameArchitectureTestFixture
    {
        readonly SceneFlowPlayModeFixture _sceneFlowFixture = new SceneFlowPlayModeFixture();
        LifecycleScope _standaloneScope;
        ApplicationInputService _standaloneInputService;

        public IArchitecture Architecture { get; private set; }

        public IEnumerator Restart()
        {
            if (ApplicationHost.TryGetCurrent(out ApplicationHost divergentHost)
                && divergentHost.SceneFlow != null
                && (divergentHost.SceneFlow.State == GameFlowState.FatalError
                    || ((divergentHost.SceneFlow.State == GameFlowState.InGame
                            || divergentHost.SceneFlow.State == GameFlowState.Paused)
                        && divergentHost.CurrentSession == null)))
            {
                yield return _sceneFlowFixture.EnterFrontEnd();
            }

            if (ApplicationHost.TryGetCurrent(out ApplicationHost currentHost)
                && currentHost.SceneFlow != null
                && (currentHost.SceneFlow.State == GameFlowState.InGame
                    || currentHost.SceneFlow.State == GameFlowState.Paused))
            {
                SceneFlowResult returnResult = null;
                yield return currentHost.SceneFlow.RequestAsync(
                        SceneFlowRequest.ReturnToFrontEnd())
                    .ToCoroutine(result => returnResult = result);
                Assert.IsNotNull(returnResult, "Scene Flow 返回空结果");
                Assert.IsTrue(
                    returnResult.Succeeded,
                    $"{returnResult.ErrorCode} @ {returnResult.Phase}: "
                    + returnResult.Exception);
                Architecture = null;
            }

            yield return StopCurrent();

            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                LifecycleResult createResult = host.CreatePendingSession();
                Assert.IsTrue(createResult.IsSuccess, createResult.Message);
            }
            else
            {
                _standaloneScope = LifecycleScope.CreateRoot("PlayModeTestSession");
                _standaloneInputService = new ApplicationInputService();
                GameArchitectureProvider.StartSession(
                    _standaloneScope,
                    _standaloneInputService);
            }

            Architecture = GameArchitectureProvider.RequireCurrent();
        }

        public IEnumerator EnterMain(GameStartIntent intent = GameStartIntent.NewGame)
        {
            yield return _sceneFlowFixture.EnterMain(intent);
            Architecture = ApplicationHost.Current.CurrentSession.Architecture;
        }

        public IEnumerator StopCurrent()
        {
            Architecture = null;

            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                if (host.CurrentSession == null)
                {
                    yield break;
                }

                LifecycleResult stopResult = default;
                yield return host.StopCurrentSessionAsync()
                    .ToCoroutine(result => stopResult = result);
                Assert.IsTrue(stopResult.IsSuccess, stopResult.Message);
                yield break;
            }

            if (_standaloneScope == null)
            {
                yield break;
            }

            _standaloneScope.BeginStop();
            yield return _standaloneScope.StopAsync().ToCoroutine();
            GameArchitectureProvider.StopSession(_standaloneScope);
            _standaloneScope = null;
            _standaloneInputService?.Dispose();
            _standaloneInputService = null;
        }
    }
}
