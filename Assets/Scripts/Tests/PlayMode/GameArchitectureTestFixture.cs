using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    internal sealed class GameArchitectureTestFixture
    {
        LifecycleScope _standaloneScope;

        public IArchitecture Architecture { get; private set; }

        public IEnumerator Restart()
        {
            yield return StopCurrent();

            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                LifecycleResult createResult = host.CreatePendingSession();
                Assert.IsTrue(createResult.IsSuccess, createResult.Message);
            }
            else
            {
                _standaloneScope = LifecycleScope.CreateRoot("PlayModeTestSession");
                GameArchitectureProvider.StartSession(_standaloneScope);
            }

            Architecture = GameArchitectureProvider.RequireCurrent();
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
        }
    }
}
