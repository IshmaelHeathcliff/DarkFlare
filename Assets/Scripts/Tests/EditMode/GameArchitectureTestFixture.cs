using System;
using System.Threading;

namespace DarkFlare.Tests
{
    sealed class GameArchitectureTestFixture
    {
        static int s_sequence;

        LifecycleScope _ownerScope;
        IArchitecture _architecture;

        public IArchitecture Architecture => _architecture
            ?? throw new InvalidOperationException("测试 Session 尚未启动");

        public IArchitecture Start()
        {
            if (_ownerScope != null)
            {
                throw new InvalidOperationException("测试 Session 已经启动");
            }

            int sequence = Interlocked.Increment(ref s_sequence);
            LifecycleScope ownerScope = LifecycleScope.CreateRoot($"EditMode-TestSession-{sequence}");

            try
            {
                IArchitecture architecture = GameArchitectureProvider.StartSession(ownerScope);
                _ownerScope = ownerScope;
                _architecture = architecture;
                return architecture;
            }
            catch
            {
                ownerScope.BeginStop();
                ownerScope.StopAsync().GetAwaiter().GetResult();
                throw;
            }
        }

        public IArchitecture Restart()
        {
            Stop();
            return Start();
        }

        public void Stop()
        {
            if (_ownerScope == null)
            {
                return;
            }

            LifecycleScope ownerScope = _ownerScope;

            try
            {
                ownerScope.BeginStop();
                ownerScope.StopAsync().GetAwaiter().GetResult();
                GameArchitectureProvider.StopSession(ownerScope);
            }
            finally
            {
                _ownerScope = null;
                _architecture = null;
            }
        }
    }
}
