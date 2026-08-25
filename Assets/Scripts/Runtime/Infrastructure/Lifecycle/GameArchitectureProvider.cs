using System;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct GameArchitectureSessionLease
    {
        internal GameArchitectureSessionLease(
            IArchitecture architecture,
            LifecycleScope ownerScope,
            int generation)
        {
            Architecture = architecture;
            OwnerScope = ownerScope;
            Generation = generation;
        }

        public IArchitecture Architecture { get; }

        public int Generation { get; }

        internal LifecycleScope OwnerScope { get; }

        public bool IsValid => Architecture != null
            && OwnerScope != null
            && Generation > 0;
    }

    public static class GameArchitectureProvider
    {
        static IArchitecture s_current;
        static LifecycleScope s_ownerScope;
        static int s_generation;
        static int s_currentGeneration;

        public static bool HasCurrent => s_current != null;

        public static int Generation => s_generation;

        public static IArchitecture StartSession(
            LifecycleScope ownerScope,
            ApplicationInputService inputService)
        {
            return StartOwnedSession(ownerScope, inputService).Architecture;
        }

        public static GameArchitectureSessionLease StartOwnedSession(
            LifecycleScope ownerScope,
            ApplicationInputService inputService)
        {
            if (ownerScope == null)
            {
                throw new ArgumentNullException(nameof(ownerScope));
            }

            if (inputService == null)
            {
                throw new ArgumentNullException(nameof(inputService));
            }

            if (inputService.IsClosed)
            {
                throw new ObjectDisposedException(nameof(inputService));
            }

            if (ownerScope.State != LifecycleScopeState.Active)
            {
                throw new InvalidOperationException($"Session 作用域 {ownerScope.Name} 不是 Active 状态");
            }

            if (s_current != null)
            {
                throw new InvalidOperationException(
                    $"GameArchitecture 已由 {s_ownerScope?.Name ?? "未知作用域"} 持有");
            }

            int generation = AdvanceGeneration();
            s_ownerScope = ownerScope;
            s_currentGeneration = generation;
            GameInput gameInput = null;

            try
            {
                IArchitecture architecture = GameArchitecture.Interface;
                gameInput = new GameInput(inputService);
                architecture.RegisterUtility(gameInput);
                s_current = architecture;
                return new GameArchitectureSessionLease(
                    s_current,
                    ownerScope,
                    generation);
            }
            catch
            {
                gameInput?.Dispose();
                s_ownerScope = null;
                s_current = null;
                s_currentGeneration = 0;
                throw;
            }
        }

        public static IArchitecture RequireCurrent()
        {
            if (s_current == null)
            {
                string applicationState = ApplicationHost.TryGetCurrent(out ApplicationHost host)
                    ? host.State.ToString()
                    : "Unavailable";
                string sessionState = host != null && host.CurrentSession != null
                    ? host.CurrentSession.State.ToString()
                    : GameSessionState.None.ToString();
                throw new InvalidOperationException(
                    "当前没有可用的 GameArchitecture。"
                    + $"Application={applicationState}, Session={sessionState}。"
                    + "请先由 ApplicationHost 创建 Session。");
            }

            return s_current;
        }

        public static bool TryGetCurrent(out IArchitecture architecture)
        {
            architecture = s_current;
            return architecture != null;
        }

        public static bool TryGetOwnerScope(out LifecycleScope ownerScope)
        {
            ownerScope = s_ownerScope;
            return ownerScope != null;
        }

        public static bool IsCurrent(GameArchitectureSessionLease lease)
        {
            return lease.IsValid
                && ReferenceEquals(s_current, lease.Architecture)
                && ReferenceEquals(s_ownerScope, lease.OwnerScope)
                && s_currentGeneration == lease.Generation;
        }

        public static bool TryGetCurrentLease(out GameArchitectureSessionLease lease)
        {
            if (s_current == null || s_ownerScope == null || s_currentGeneration <= 0)
            {
                lease = default;
                return false;
            }

            lease = new GameArchitectureSessionLease(
                s_current,
                s_ownerScope,
                s_currentGeneration);
            return true;
        }

        public static void StopSession(LifecycleScope ownerScope)
        {
            if (s_current == null)
            {
                return;
            }

            if (!ReferenceEquals(s_ownerScope, ownerScope))
            {
                throw new InvalidOperationException(
                    $"只有当前 Session 作用域 {s_ownerScope?.Name ?? "未知"} 可以停止 GameArchitecture");
            }

            StopOwnedSession(new GameArchitectureSessionLease(
                s_current,
                s_ownerScope,
                s_currentGeneration));
        }

        public static void StopOwnedSession(GameArchitectureSessionLease lease)
        {
            ValidateCurrentLease(lease, "停止");

            LifecycleScope ownerScope = lease.OwnerScope;

            if (ownerScope.State != LifecycleScopeState.Stopped)
            {
                throw new InvalidOperationException(
                    $"Session 作用域 {ownerScope.Name} 必须完全停止后才能正常停止 GameArchitecture，"
                    + $"当前状态为 {ownerScope.State}");
            }

            StopCurrentArchitecture(lease);
        }

        internal static void EmergencyStopSession(LifecycleScope ownerScope)
        {
            if (s_current == null)
            {
                return;
            }

            if (!ReferenceEquals(s_ownerScope, ownerScope))
            {
                throw new InvalidOperationException(
                    $"只有当前 Session 作用域 {s_ownerScope?.Name ?? "未知"} 可以应急停止 GameArchitecture");
            }

            EmergencyStopOwnedSession(new GameArchitectureSessionLease(
                s_current,
                s_ownerScope,
                s_currentGeneration));
        }

        internal static void EmergencyStopOwnedSession(GameArchitectureSessionLease lease)
        {
            ValidateCurrentLease(lease, "应急停止");

            // 应急路径只发出取消，不等待作用域收敛；调用方必须隔离迟到的异步 continuation。
            lease.OwnerScope.BeginStop();
            StopCurrentArchitecture(lease);
        }

        static void StopCurrentArchitecture(GameArchitectureSessionLease lease)
        {
            ValidateCurrentLease(lease, "释放");

            try
            {
                lease.Architecture.Deinit();
            }
            finally
            {
                if (IsCurrent(lease))
                {
                    s_current = null;
                    s_ownerScope = null;
                    s_currentGeneration = 0;
                }
            }
        }

        static void ValidateCurrentLease(
            GameArchitectureSessionLease lease,
            string operationName)
        {
            if (!lease.IsValid)
            {
                throw new InvalidOperationException(
                    $"GameArchitecture {operationName}需要有效的 Session 所有权租约");
            }

            if (!IsCurrent(lease))
            {
                throw new InvalidOperationException(
                    $"GameArchitecture {operationName}租约已失效。"
                    + $"租约代际={lease.Generation}，当前代际={s_currentGeneration}，"
                    + $"租约作用域={lease.OwnerScope.Name}，"
                    + $"当前作用域={s_ownerScope?.Name ?? "无"}");
            }
        }

        static int AdvanceGeneration()
        {
            if (s_generation == int.MaxValue)
            {
                throw new InvalidOperationException("GameArchitecture 代际计数已耗尽");
            }

            s_generation++;
            return s_generation;
        }

        internal static void ResetStaticState()
        {
            if (!TryGetCurrentLease(out GameArchitectureSessionLease lease))
            {
                s_ownerScope = null;
                s_current = null;
                s_currentGeneration = 0;
                return;
            }

            try
            {
                EmergencyStopOwnedSession(lease);
            }
            catch (Exception exception)
            {
                ApplicationLog.Exception(LogEventIds.InfrastructureLifecycle, exception);
            }
            finally
            {
                s_current = null;
                s_ownerScope = null;
                s_currentGeneration = 0;
            }
        }
    }
}
