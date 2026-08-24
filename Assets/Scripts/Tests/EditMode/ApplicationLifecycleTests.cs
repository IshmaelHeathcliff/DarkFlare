using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class ApplicationLifecycleTests
    {
        const float TimeoutSeconds = 2f;

        [UnityTest]
        public IEnumerator LifecycleScope_StoppingParentCancelsAndWaitsForChildren()
        {
            return VerifyParentChildCancellationAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator LifecycleScope_RepeatedAndConcurrentStopCallsRemainAwaitable()
        {
            return VerifyRepeatedStopAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator LifecycleScope_TaskCanBeginItsOwnScopeStopWithoutDeadlock()
        {
            return VerifySelfStoppingTaskAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator LifecycleScope_CancellationCallbackCanReenterStopAsync()
        {
            return VerifyCancellationReentryAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator LifecycleTaskGroup_SynchronousSelfStopStillWaitsForTaskCompletion()
        {
            return VerifySynchronousSelfStopAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator LifecycleTaskGroup_ObservesAndReportsTaskFailures()
        {
            return VerifyTaskFailureObservationAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator LifecycleTaskGroup_TracksOnlyActiveSynchronousAndAsynchronousTasks()
        {
            return VerifyActiveTaskTrackingAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator LifecycleScope_StopTimeoutMarksScopeAbandoned()
        {
            return VerifyStopTimeoutIsolationAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator GameSessionHost_AbandonedComponentTaintsAncestorsAndRetainsLease()
        {
            return VerifyAbandonedComponentTaintAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator GameSessionHost_RootCancellationAndHangingRollbackStayBoundedAndIsolated()
        {
            return VerifyHangingRollbackIsolationAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator GameArchitectureProvider_EnforcesOwnershipAndSupportsRecreation()
        {
            return VerifyArchitectureProviderAsync().ToCoroutine();
        }

        static async UniTask VerifyParentChildCancellationAsync()
        {
            LifecycleScope root = LifecycleScope.CreateRoot("Lifecycle-Test-Root");
            LifecycleScope child = root.CreateChild("Lifecycle-Test-Child");
            CancellationToken rootToken = root.Token;
            CancellationToken childToken = child.Token;
            bool childObservedCancellation = false;

            try
            {
                child.Tasks.Run(
                    "observe-child-cancellation",
                    async token =>
                    {
                        try
                        {
                            await UniTask.Delay(
                                TimeSpan.FromSeconds(TimeoutSeconds),
                                DelayType.Realtime,
                                cancellationToken: token);
                        }
                        finally
                        {
                            childObservedCancellation = token.IsCancellationRequested;
                        }
                    });

                root.BeginStop();

                Assert.IsTrue(rootToken.IsCancellationRequested);
                Assert.IsTrue(childToken.IsCancellationRequested);
                Assert.AreNotEqual(LifecycleScopeState.Active, root.State);
                Assert.AreNotEqual(LifecycleScopeState.Active, child.State);

                await AwaitWithTimeout(root.StopAsync());

                Assert.IsTrue(childObservedCancellation);
                Assert.AreEqual(LifecycleScopeState.Stopped, child.State);
                Assert.AreEqual(LifecycleScopeState.Stopped, root.State);
                Assert.AreEqual(0, root.ChildCount);
            }
            finally
            {
                await AwaitWithTimeout(root.StopAsync());
            }
        }

        static async UniTask VerifyRepeatedStopAsync()
        {
            LifecycleScope scope = LifecycleScope.CreateRoot("Lifecycle-Test-RepeatedStop");
            int cleanupCount = 0;

            try
            {
                scope.Tasks.Run(
                    "wait-for-stop",
                    async token =>
                    {
                        try
                        {
                            await UniTask.Delay(
                                TimeSpan.FromSeconds(TimeoutSeconds),
                                DelayType.Realtime,
                                cancellationToken: token);
                        }
                        finally
                        {
                            cleanupCount++;
                        }
                    });

                UniTask firstStop = scope.StopAsync();
                UniTask secondStop = scope.StopAsync();
                await AwaitWithTimeout(UniTask.WhenAll(firstStop, secondStop));
                await AwaitWithTimeout(scope.StopAsync());

                Assert.AreEqual(1, cleanupCount);
                Assert.AreEqual(LifecycleScopeState.Stopped, scope.State);
            }
            finally
            {
                await AwaitWithTimeout(scope.StopAsync());
            }
        }

        static async UniTask VerifySelfStoppingTaskAsync()
        {
            LifecycleScope scope = LifecycleScope.CreateRoot("Lifecycle-Test-SelfStop");
            bool stopRequestedInsideTask = false;
            bool taskCompleted = false;

            try
            {
                LifecycleTaskHandle handle = scope.Tasks.Run(
                    "self-stop",
                    async token =>
                    {
                        await UniTask.Yield();
                        stopRequestedInsideTask = true;
                        scope.BeginStop();
                        await UniTask.Yield();
                        taskCompleted = true;
                    });

                await AwaitWithTimeout(handle.WaitAsync());
                await AwaitWithTimeout(scope.StopAsync());

                Assert.IsTrue(stopRequestedInsideTask);
                Assert.IsTrue(taskCompleted);
                Assert.AreEqual(LifecycleScopeState.Stopped, scope.State);
            }
            finally
            {
                await AwaitWithTimeout(scope.StopAsync());
            }
        }

        static async UniTask VerifyCancellationReentryAsync()
        {
            LifecycleScope scope = LifecycleScope.CreateRoot("Lifecycle-Test-CancelReentry");
            UniTask reentrantStop = UniTask.CompletedTask;
            CancellationTokenRegistration registration = scope.Token.Register(
                () => reentrantStop = scope.StopAsync());

            try
            {
                await AwaitWithTimeout(scope.StopAsync());
                await AwaitWithTimeout(reentrantStop);

                Assert.AreEqual(LifecycleScopeState.Stopped, scope.State);
            }
            finally
            {
                registration.Dispose();
                await AwaitWithTimeout(scope.StopAsync());
            }
        }

        static async UniTask VerifySynchronousSelfStopAsync()
        {
            LifecycleScope scope = LifecycleScope.CreateRoot("Lifecycle-Test-SynchronousSelfStop");
            bool taskCompleted = false;

            try
            {
                LifecycleTaskHandle handle = scope.Tasks.Run(
                    "synchronous-self-stop",
                    async token =>
                    {
                        scope.BeginStop();
                        await UniTask.Yield();
                        taskCompleted = true;
                    });

                await AwaitWithTimeout(scope.StopAsync());

                Assert.IsTrue(taskCompleted);
                Assert.AreEqual(LifecycleScopeState.Stopped, scope.State);
                await AwaitWithTimeout(handle.WaitAsync());
            }
            finally
            {
                await AwaitWithTimeout(scope.StopAsync());
            }
        }

        static async UniTask VerifyTaskFailureObservationAsync()
        {
            List<LifecycleTaskFailure> reportedFailures = new List<LifecycleTaskFailure>();
            LifecycleScope scope = LifecycleScope.CreateRoot(
                "Lifecycle-Test-Failure",
                reportedFailures.Add);
            InvalidOperationException expectedException = new InvalidOperationException(
                "expected lifecycle task failure");

            try
            {
                LifecycleTaskHandle handle = scope.Tasks.Run(
                    "failing-operation",
                    async token =>
                    {
                        await UniTask.Yield();
                        throw expectedException;
                    });

                await AwaitWithTimeout(handle.WaitAsync());

                Assert.AreEqual(LifecycleScopeState.Active, scope.State);
                Assert.AreEqual(1, scope.Tasks.Failures.Count);
                Assert.AreEqual(1, reportedFailures.Count);
                Assert.AreEqual("Lifecycle-Test-Failure", reportedFailures[0].ScopeName);
                Assert.AreEqual("failing-operation", reportedFailures[0].OperationName);
                Assert.AreSame(expectedException, reportedFailures[0].Exception);
                Assert.AreSame(expectedException, scope.Tasks.Failures[0].Exception);
            }
            finally
            {
                await AwaitWithTimeout(scope.StopAsync());
            }
        }

        static async UniTask VerifyActiveTaskTrackingAsync()
        {
            LifecycleScope scope = LifecycleScope.CreateRoot("Lifecycle-Test-ActiveTracking");
            UniTaskCompletionSource release = new UniTaskCompletionSource();

            try
            {
                LifecycleTaskHandle synchronousHandle = scope.Tasks.Run(
                    "synchronous-completion",
                    token => UniTask.CompletedTask);

                await AwaitWithTimeout(synchronousHandle.WaitAsync());
                Assert.AreEqual(0, scope.Tasks.TaskCount);

                LifecycleTaskHandle asynchronousHandle = scope.Tasks.Run(
                    "asynchronous-completion",
                    async token => await release.Task);

                Assert.AreEqual(1, scope.Tasks.TaskCount);
                release.TrySetResult();
                await AwaitWithTimeout(asynchronousHandle.WaitAsync());
                await UniTask.WaitUntil(() => scope.Tasks.TaskCount == 0)
                    .Timeout(TimeSpan.FromSeconds(TimeoutSeconds), DelayType.Realtime);
                Assert.AreEqual(0, scope.Tasks.TaskCount);
            }
            finally
            {
                release.TrySetResult();
                await AwaitWithTimeout(scope.StopAsync());
            }
        }

        static async UniTask VerifyArchitectureProviderAsync()
        {
            LifecycleScope firstOwner = null;
            LifecycleScope duplicateOwner = null;
            LifecycleScope recreatedOwner = null;
            ApplicationInputService inputService = new ApplicationInputService();

            try
            {
                Assert.IsFalse(GameArchitectureProvider.HasCurrent);
                Assert.Throws<InvalidOperationException>(
                    () => GameArchitectureProvider.RequireCurrent());

                int generationBeforeStart = GameArchitectureProvider.Generation;
                firstOwner = LifecycleScope.CreateRoot("Provider-Test-FirstOwner");
                IArchitecture firstArchitecture = GameArchitectureProvider.StartSession(
                    firstOwner,
                    inputService);

                Assert.IsTrue(GameArchitectureProvider.HasCurrent);
                Assert.AreSame(firstArchitecture, GameArchitectureProvider.RequireCurrent());
                Assert.AreEqual(generationBeforeStart + 1, GameArchitectureProvider.Generation);

                duplicateOwner = LifecycleScope.CreateRoot("Provider-Test-DuplicateOwner");
                Assert.Throws<InvalidOperationException>(
                    () => GameArchitectureProvider.StartSession(duplicateOwner, inputService));
                Assert.Throws<InvalidOperationException>(
                    () => GameArchitectureProvider.StopSession(duplicateOwner));

                await AwaitWithTimeout(firstOwner.StopAsync());
                GameArchitectureProvider.StopSession(firstOwner);

                Assert.IsFalse(GameArchitectureProvider.HasCurrent);
                Assert.Throws<InvalidOperationException>(
                    () => GameArchitectureProvider.RequireCurrent());

                recreatedOwner = LifecycleScope.CreateRoot("Provider-Test-RecreatedOwner");
                IArchitecture recreatedArchitecture =
                    GameArchitectureProvider.StartSession(recreatedOwner, inputService);

                Assert.AreNotSame(firstArchitecture, recreatedArchitecture);
                Assert.AreEqual(generationBeforeStart + 2, GameArchitectureProvider.Generation);
                Assert.AreSame(recreatedArchitecture, GameArchitectureProvider.RequireCurrent());
            }
            finally
            {
                if (GameArchitectureProvider.TryGetOwnerScope(out LifecycleScope currentOwner))
                {
                    await AwaitWithTimeout(currentOwner.StopAsync());
                    GameArchitectureProvider.StopSession(currentOwner);
                }

                await StopScopeIfNeeded(firstOwner);
                await StopScopeIfNeeded(duplicateOwner);
                await StopScopeIfNeeded(recreatedOwner);
                inputService.Dispose();
            }
        }

        static async UniTask VerifyStopTimeoutIsolationAsync()
        {
            List<LifecycleTaskFailure> reportedFailures = new List<LifecycleTaskFailure>();
            LifecycleScope scope = LifecycleScope.CreateRoot(
                "Lifecycle-Test-Timeout",
                reportedFailures.Add,
                taskStopTimeout: TimeSpan.FromMilliseconds(50d));
            UniTaskCompletionSource release = new UniTaskCompletionSource();
            InvalidOperationException lateException = new InvalidOperationException(
                "late lifecycle task failure");
            LifecycleTaskHandle handle = scope.Tasks.Run(
                "ignores-cancellation",
                async token =>
                {
                    await release.Task;
                    throw lateException;
                },
                failurePolicy: LifecycleTaskFailurePolicy.Propagate);

            await AwaitWithTimeout(scope.StopAsync());

            Assert.AreEqual(LifecycleScopeState.Abandoned, scope.State);
            Assert.IsTrue(scope.Tasks.StopTimedOut);
            Assert.AreEqual(1, scope.Tasks.Failures.Count);
            Assert.AreEqual("stop-timeout", scope.Tasks.Failures[0].OperationName);
            StringAssert.Contains(
                "ignores-cancellation",
                scope.Tasks.Failures[0].Exception.Message,
                "stop-timeout 诊断必须列出超时快照中的活动 operation");
            Assert.AreEqual(1, reportedFailures.Count);

            TimeoutException timeoutException = null;

            try
            {
                await AwaitWithTimeout(handle.WaitAsync());
            }
            catch (TimeoutException exception)
            {
                timeoutException = exception;
            }

            Assert.IsNotNull(timeoutException, "Propagate handle 必须以停止超时异常有界结算");
            StringAssert.Contains("ignores-cancellation", timeoutException.Message);

            release.TrySetResult();
            await UniTask.WaitUntil(() => scope.Tasks.TaskCount == 0)
                .Timeout(TimeSpan.FromSeconds(TimeoutSeconds), DelayType.Realtime);

            Assert.AreEqual(0, scope.Tasks.TaskCount);
            Assert.AreEqual(2, scope.Tasks.Failures.Count);
            Assert.AreSame(lateException, scope.Tasks.Failures[1].Exception);
            Assert.AreEqual(1, reportedFailures.Count, "迟到失败不得再次触发生命周期回调");
        }

        static async UniTask VerifyHangingRollbackIsolationAsync()
        {
            GameArchitectureProvider.ResetStaticState();
            LifecycleScope applicationScope = LifecycleScope.CreateRoot(
                "Lifecycle-Test-HangingRollback",
                taskStopTimeout: TimeSpan.FromMilliseconds(50d));
            LifecycleScope profileScope = applicationScope.CreateChild(
                "Lifecycle-Test-HangingRollback-Profile");
            LifecycleScope duplicateOwner = null;
            HangingRollbackInitializer initializer = new HangingRollbackInitializer();
            GameSessionHost session = null;
            ApplicationInputService inputService = new ApplicationInputService();

            try
            {
                session = new GameSessionHost(profileScope, 1, null, inputService);
                LifecycleResult initializationResult = await session.InitializeAsync(initializer);

                Assert.IsTrue(initializationResult.IsSuccess, initializationResult.Message);
                Assert.AreEqual(GameSessionState.Running, session.State);
                int generation = session.ArchitectureGeneration;
                applicationScope.BeginStop();
                LifecycleResult stopResult = await session.StopAsync()
                    .Timeout(
                        TimeSpan.FromSeconds(TimeoutSeconds),
                        DelayType.Realtime);

                Assert.IsFalse(stopResult.IsSuccess, "挂起回滚不得被误报为成功停止");
                Assert.AreEqual(GameSessionState.Abandoned, session.State);
                Assert.IsTrue(session.IsCurrentArchitectureLease);
                Assert.AreEqual(generation, GameArchitectureProvider.Generation);
                Assert.IsTrue(initializer.RollbackStarted);
                Assert.IsFalse(initializer.RollbackCompleted);

                LifecycleResult emergencyResult = session.EmergencyStop();
                Assert.IsFalse(emergencyResult.IsSuccess, "Abandoned 的应急停止不得返回成功");

                duplicateOwner = LifecycleScope.CreateRoot(
                    "Lifecycle-Test-HangingRollback-Duplicate");
                Assert.Throws<InvalidOperationException>(
                    () => GameArchitectureProvider.StartSession(duplicateOwner, inputService));

                initializer.Release();
                await UniTask.WaitUntil(() => session.SessionScope.Tasks.TaskCount == 0)
                    .Timeout(TimeSpan.FromSeconds(TimeoutSeconds), DelayType.Realtime);

                Assert.IsTrue(initializer.RollbackCompleted);
                Assert.AreEqual(GameSessionState.Abandoned, session.State);
                Assert.IsTrue(session.IsCurrentArchitectureLease);
                Assert.AreEqual(generation, GameArchitectureProvider.Generation);
            }
            finally
            {
                session?.EmergencyStop();
                applicationScope.BeginStop();
                initializer.Release();

                if (session != null)
                {
                    await UniTask.WaitUntil(() => session.SessionScope.Tasks.TaskCount == 0)
                        .Timeout(TimeSpan.FromSeconds(TimeoutSeconds), DelayType.Realtime);
                }

                GameArchitectureProvider.ResetStaticState();
                await StopScopeIfNeeded(duplicateOwner);
                await StopScopeIfNeeded(applicationScope);
                inputService.Dispose();
            }
        }

        static async UniTask VerifyAbandonedComponentTaintAsync()
        {
            GameArchitectureProvider.ResetStaticState();
            LifecycleScope applicationScope = LifecycleScope.CreateRoot(
                "Lifecycle-Test-ComponentTaint",
                taskStopTimeout: TimeSpan.FromMilliseconds(50d));
            LifecycleScope profileScope = applicationScope.CreateChild(
                "Lifecycle-Test-ComponentTaint-Profile");
            LifecycleScope componentScope = null;
            LifecycleScope duplicateOwner = null;
            GameSessionHost session = null;
            UniTaskCompletionSource release = new UniTaskCompletionSource();
            ApplicationInputService inputService = new ApplicationInputService();

            try
            {
                session = new GameSessionHost(profileScope, 1, null, inputService);
                LifecycleResult initializationResult = await session.InitializeAsync(
                    new NoOpSessionInitializer());

                Assert.IsTrue(initializationResult.IsSuccess, initializationResult.Message);
                Assert.AreEqual(GameSessionState.Running, session.State);
                int generation = session.ArchitectureGeneration;
                componentScope = session.SceneScope.CreateChild(
                    "Lifecycle-Test-ComponentTaint-Component");
                componentScope.Tasks.Run(
                    "ignores-component-cancellation",
                    async token => await release.Task);

                await AwaitWithTimeout(componentScope.StopAsync());

                Assert.AreEqual(LifecycleScopeState.Abandoned, componentScope.State);
                Assert.AreEqual(0, session.SceneScope.ChildCount);
                Assert.AreEqual(LifecycleScopeState.Active, session.SceneScope.State);
                Assert.AreEqual(LifecycleScopeState.Active, session.SessionScope.State);
                Assert.IsFalse(session.SceneScope.CanAcceptWork);
                Assert.IsFalse(session.SessionScope.CanAcceptWork);
                Assert.IsFalse(profileScope.CanAcceptWork);
                Assert.IsFalse(applicationScope.CanAcceptWork);
                InvalidOperationException createChildException = Assert.Throws<InvalidOperationException>(
                    () => session.SceneScope.CreateChild(
                        "Lifecycle-Test-ComponentTaint-RejectedChild"));
                StringAssert.Contains(
                    "Abandoned 后代污染",
                    createChildException.Message);
                Assert.Throws<InvalidOperationException>(
                    () => session.SceneScope.Tasks.Run(
                        "rejected-after-descendant-abandoned",
                        token => UniTask.CompletedTask));

                release.TrySetResult();
                await UniTask.WaitUntil(() => componentScope.Tasks.TaskCount == 0)
                    .Timeout(TimeSpan.FromSeconds(TimeoutSeconds), DelayType.Realtime);

                Assert.AreEqual(
                    LifecycleScopeState.Abandoned,
                    componentScope.State,
                    "迟到任务完成不得清除 Component 作用域的 Abandoned");

                LifecycleResult stopResult = await session.StopAsync()
                    .Timeout(
                        TimeSpan.FromSeconds(TimeoutSeconds),
                        DelayType.Realtime);

                Assert.IsFalse(stopResult.IsSuccess);
                Assert.AreEqual(LifecycleScopeState.Abandoned, session.SceneScope.State);
                Assert.AreEqual(LifecycleScopeState.Abandoned, session.SessionScope.State);
                Assert.AreEqual(GameSessionState.Abandoned, session.State);
                Assert.IsTrue(session.IsCurrentArchitectureLease);
                Assert.AreEqual(generation, GameArchitectureProvider.Generation);
                duplicateOwner = LifecycleScope.CreateRoot(
                    "Lifecycle-Test-ComponentTaint-DuplicateOwner");
                Assert.Throws<InvalidOperationException>(
                    () => GameArchitectureProvider.StartSession(duplicateOwner, inputService));
                Assert.Throws<InvalidOperationException>(
                    () => new GameSessionHost(profileScope, 2, null, inputService));
                Assert.AreEqual(generation, GameArchitectureProvider.Generation);

                await AwaitWithTimeout(profileScope.StopAsync());

                Assert.AreEqual(LifecycleScopeState.Abandoned, profileScope.State);
                Assert.AreEqual(0, applicationScope.ChildCount);
                Assert.AreEqual(LifecycleScopeState.Active, applicationScope.State);

                await AwaitWithTimeout(applicationScope.StopAsync());

                Assert.AreEqual(
                    LifecycleScopeState.Abandoned,
                    applicationScope.State,
                    "已从 children 摘除的 Abandoned 后代必须持续污染祖先");
            }
            finally
            {
                release.TrySetResult();
                session?.EmergencyStop();
                await StopScopeIfNeeded(applicationScope);
                GameArchitectureProvider.ResetStaticState();
                await StopScopeIfNeeded(duplicateOwner);
                inputService.Dispose();
            }
        }

        sealed class NoOpSessionInitializer : IGameSessionInitializer
        {
            public string Name => "no-op-session-test";

            public UniTask InitializeAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                return UniTask.CompletedTask;
            }

            public UniTask RollbackAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                return UniTask.CompletedTask;
            }
        }

        sealed class HangingRollbackInitializer : IGameSessionInitializer
        {
            readonly UniTaskCompletionSource _release = new UniTaskCompletionSource();

            public string Name => "hanging-rollback-test";

            public bool RollbackStarted { get; private set; }

            public bool RollbackCompleted { get; private set; }

            public UniTask InitializeAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                return UniTask.CompletedTask;
            }

            public async UniTask RollbackAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                RollbackStarted = true;
                await _release.Task;
                RollbackCompleted = true;
            }

            public void Release()
            {
                _release.TrySetResult();
            }
        }

        static async UniTask StopScopeIfNeeded(LifecycleScope scope)
        {
            if (scope != null)
            {
                await AwaitWithTimeout(scope.StopAsync());
            }
        }

        static UniTask AwaitWithTimeout(UniTask task)
        {
            return task.Timeout(
                TimeSpan.FromSeconds(TimeoutSeconds),
                DelayType.Realtime);
        }
    }
}
