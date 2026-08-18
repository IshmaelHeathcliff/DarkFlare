using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class AbandonedGenerationIsolationTests
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2d);

        [UnityTest]
        public IEnumerator EmergencyStop_LateContinuationsCannotLeaveAbandonedOrTouchNewGeneration()
        {
            return VerifyLateContinuationsAreIsolatedAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator LifecycleTaskGroup_LateFailureAfterTimeoutDoesNotInvokeScopeCallbacks()
        {
            return VerifyLateFailureCallbacksAreSuppressedAsync().ToCoroutine();
        }

        static async UniTask VerifyLateContinuationsAreIsolatedAsync()
        {
            ResetProvider();
            LifecycleScope profileScope = LifecycleScope.CreateRoot(
                "Abandoned-Generation-Profile",
                taskStopTimeout: Timeout);
            LifecycleScope replacementOwner = null;
            IgnoringCancellationInitializer initializer = new IgnoringCancellationInitializer();
            GameSessionHost session = null;

            try
            {
                session = CreateSession(profileScope);
                int abandonedGeneration = GameArchitectureProvider.Generation;
                UniTask<LifecycleResult> initialization = session.InitializeAsync(initializer);
                UniTask<LifecycleResult> normalStop = session.StopAsync();

                LifecycleResult emergencyResult = InvokeEmergencyStop(session);

                Assert.IsFalse(emergencyResult.IsSuccess);
                Assert.AreEqual(GameSessionState.Abandoned, session.State);

                ResetProvider();
                Assert.AreEqual(
                    abandonedGeneration,
                    GameArchitectureProvider.Generation,
                    "静态重置不得让代际回退并形成 ABA");

                replacementOwner = LifecycleScope.CreateRoot("Replacement-Generation-Owner");
                IArchitecture replacementArchitecture =
                    GameArchitectureProvider.StartSession(replacementOwner);
                int replacementGeneration = GameArchitectureProvider.Generation;

                Assert.Greater(replacementGeneration, abandonedGeneration);

                initializer.Release();
                await initialization.Timeout(Timeout, DelayType.Realtime);
                await normalStop.Timeout(Timeout, DelayType.Realtime);
                await UniTask.WaitUntil(
                        () => session.SessionScope.State != LifecycleScopeState.Stopping)
                    .Timeout(Timeout, DelayType.Realtime);
                await UniTask.Yield();

                Assert.AreEqual(GameSessionState.Abandoned, session.State);
                Assert.AreEqual(0, initializer.RollbackCount);
                Assert.AreEqual(replacementGeneration, GameArchitectureProvider.Generation);
                Assert.AreSame(
                    replacementArchitecture,
                    GameArchitectureProvider.RequireCurrent());
            }
            finally
            {
                initializer.Release();

                if (replacementOwner != null
                    && GameArchitectureProvider.TryGetOwnerScope(out LifecycleScope currentOwner)
                    && ReferenceEquals(currentOwner, replacementOwner))
                {
                    await replacementOwner.StopAsync().Timeout(Timeout, DelayType.Realtime);
                    GameArchitectureProvider.StopSession(replacementOwner);
                }

                await profileScope.StopAsync().Timeout(Timeout, DelayType.Realtime);
                ResetProvider();
            }
        }

        static async UniTask VerifyLateFailureCallbacksAreSuppressedAsync()
        {
            List<LifecycleTaskFailure> reported = new List<LifecycleTaskFailure>();
            LifecycleScope scope = LifecycleScope.CreateRoot(
                "Late-Failure-Isolation",
                reported.Add,
                taskStopTimeout: TimeSpan.FromMilliseconds(50d));
            UniTaskCompletionSource release = new UniTaskCompletionSource();
            InvalidOperationException lateException = new InvalidOperationException(
                "late operation failure");
            LifecycleTaskHandle handle = scope.Tasks.Run(
                "late-failure",
                async token =>
                {
                    await release.Task;
                    throw lateException;
                },
                failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);

            await scope.StopAsync().Timeout(Timeout, DelayType.Realtime);

            Assert.AreEqual(LifecycleScopeState.Abandoned, scope.State);
            Assert.AreEqual(1, reported.Count);
            Assert.AreEqual("stop-timeout", reported[0].OperationName);

            release.TrySetResult();
            await handle.WaitAsync().Timeout(Timeout, DelayType.Realtime);

            Assert.AreEqual(1, reported.Count, "迟到失败不得再次触发已隔离作用域的回调");
            Assert.AreEqual(2, scope.Tasks.Failures.Count);
            Assert.AreSame(lateException, scope.Tasks.Failures[1].Exception);
            Assert.AreEqual(LifecycleScopeState.Abandoned, scope.State);
        }

        static GameSessionHost CreateSession(LifecycleScope profileScope)
        {
            ConstructorInfo constructor = typeof(GameSessionHost).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(LifecycleScope),
                    typeof(int),
                    typeof(Action<GameSessionHost, string>),
                    typeof(IItemInstanceIdGenerator)
                },
                null);

            Assert.IsNotNull(constructor);
            return (GameSessionHost)constructor.Invoke(new object[]
            {
                profileScope,
                1,
                null,
                null
            });
        }

        static LifecycleResult InvokeEmergencyStop(GameSessionHost session)
        {
            MethodInfo method = typeof(GameSessionHost).GetMethod(
                "EmergencyStop",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method);
            return (LifecycleResult)method.Invoke(session, Array.Empty<object>());
        }

        static void ResetProvider()
        {
            MethodInfo method = typeof(GameArchitectureProvider).GetMethod(
                "ResetStaticState",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method);
            method.Invoke(null, Array.Empty<object>());
        }

        sealed class IgnoringCancellationInitializer : IGameSessionInitializer
        {
            readonly UniTaskCompletionSource _release = new UniTaskCompletionSource();

            public string Name => "ignores-cancellation";

            public int RollbackCount { get; private set; }

            public async UniTask InitializeAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                await _release.Task;
            }

            public UniTask RollbackAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                RollbackCount++;
                return UniTask.CompletedTask;
            }

            public void Release()
            {
                _release.TrySetResult();
            }
        }
    }
}
