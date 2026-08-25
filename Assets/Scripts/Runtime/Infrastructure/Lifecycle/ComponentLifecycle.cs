using System;
using System.Threading;
using UnityEngine;

namespace DarkFlare
{
    public static class ComponentLifecycle
    {
        public static LifecycleScope CreateScope(
            MonoBehaviour owner,
            string operationName,
            CancellationToken externalToken = default)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            string scopeName = $"{owner.GetType().Name}:{operationName}:{owner.GetInstanceID()}";

            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                LifecycleScope sceneScope = host.CurrentSceneScope;

                if (sceneScope != null && sceneScope.State == LifecycleScopeState.Active)
                {
                    return sceneScope.CreateChild(scopeName, externalToken);
                }

                throw new InvalidOperationException(
                    $"Application 当前状态 {host.State}，Scene 作用域不可用，无法启动组件任务 {scopeName}");
            }

            if (GameArchitectureProvider.TryGetOwnerScope(out LifecycleScope sessionScope)
                && sessionScope.State == LifecycleScopeState.Active)
            {
                return sessionScope.CreateChild($"TestComponent:{scopeName}", externalToken);
            }

            return LifecycleScope.CreateRoot(
                $"StandaloneComponent:{scopeName}",
                failure => ApplicationLog.Exception(LogEventIds.InfrastructureLifecycle, failure.Exception, owner),
                externalToken);
        }
    }
}
