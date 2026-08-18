using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DarkFlare
{
    public sealed class LifecycleTaskHandle
    {
        readonly UniTask _completion;

        internal LifecycleTaskHandle(UniTask completion)
        {
            _completion = completion;
        }

        public UniTask WaitAsync()
        {
            return _completion;
        }
    }

    public sealed class LifecycleTaskGroup
    {
        sealed class ActiveTask
        {
            public ActiveTask(
                string operationName,
                LifecycleTaskFailurePolicy failurePolicy)
            {
                OperationName = operationName;
                FailurePolicy = failurePolicy;
                CompletionSource = new UniTaskCompletionSource();
                ObservationSource = new UniTaskCompletionSource();
            }

            public string OperationName { get; }

            public LifecycleTaskFailurePolicy FailurePolicy { get; }

            public UniTaskCompletionSource CompletionSource { get; }

            public UniTaskCompletionSource ObservationSource { get; }

            public UniTask Runner { get; set; }

            public void CompleteFromTimeout(TimeoutException exception)
            {
                if (FailurePolicy == LifecycleTaskFailurePolicy.Propagate)
                {
                    CompletionSource.TrySetException(exception);
                    return;
                }

                CompletionSource.TrySetResult();
            }
        }

        readonly string _scopeName;
        readonly CancellationToken _scopeToken;
        readonly Action<LifecycleTaskFailure> _failureHandler;
        readonly Action _requestScopeStop;
        readonly TimeSpan _stopTimeout;
        readonly object _gate = new object();
        readonly List<ActiveTask> _activeTasks = new List<ActiveTask>();
        readonly List<LifecycleTaskFailure> _failures = new List<LifecycleTaskFailure>();

        bool _acceptsTasks = true;
        bool _stopStarted;
        volatile bool _stopTimedOut;
        UniTaskCompletionSource _stopCompletion;
        UniTask _stopRunner;

        internal LifecycleTaskGroup(
            string scopeName,
            CancellationToken scopeToken,
            Action<LifecycleTaskFailure> failureHandler,
            Action requestScopeStop,
            TimeSpan stopTimeout)
        {
            _scopeName = scopeName;
            _scopeToken = scopeToken;
            _failureHandler = failureHandler;
            _requestScopeStop = requestScopeStop;
            _stopTimeout = stopTimeout;
        }

        public IReadOnlyList<LifecycleTaskFailure> Failures
        {
            get
            {
                lock (_gate)
                {
                    return _failures.ToArray();
                }
            }
        }

        public int TaskCount
        {
            get
            {
                lock (_gate)
                {
                    return _activeTasks.Count;
                }
            }
        }

        public bool StopTimedOut => _stopTimedOut;

        public LifecycleTaskHandle Run(
            string operationName,
            Func<CancellationToken, UniTask> operation,
            CancellationToken externalToken = default,
            LifecycleTaskFailurePolicy failurePolicy = LifecycleTaskFailurePolicy.Report)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("任务名称不能为空", nameof(operationName));
            }

            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            ActiveTask activeTask = new ActiveTask(operationName, failurePolicy);

            lock (_gate)
            {
                if (!_acceptsTasks)
                {
                    throw new InvalidOperationException($"生命周期作用域 {_scopeName} 已停止接收任务");
                }

                _activeTasks.Add(activeTask);
            }

            UniTask runner = ExecuteAsync(
                operationName,
                operation,
                externalToken,
                failurePolicy,
                activeTask);
            activeTask.Runner = runner;
            return new LifecycleTaskHandle(activeTask.CompletionSource.Task);
        }

        internal void StopAccepting()
        {
            lock (_gate)
            {
                _acceptsTasks = false;
            }
        }

        internal UniTask StopAsync()
        {
            UniTaskCompletionSource stopCompletion;
            bool shouldStart = false;

            lock (_gate)
            {
                _acceptsTasks = false;

                if (!_stopStarted)
                {
                    _stopStarted = true;
                    _stopCompletion = new UniTaskCompletionSource();
                    shouldStart = true;
                }

                stopCompletion = _stopCompletion;
            }

            if (shouldStart)
            {
                _stopRunner = CompleteStopAsync().Preserve();
            }

            return stopCompletion.Task;
        }

        async UniTask ExecuteAsync(
            string operationName,
            Func<CancellationToken, UniTask> operation,
            CancellationToken externalToken,
            LifecycleTaskFailurePolicy failurePolicy,
            ActiveTask activeTask)
        {
            CancellationTokenSource linkedCancellation = null;
            CancellationToken token = _scopeToken;
            Exception propagatedException = null;

            try
            {
                if (externalToken.CanBeCanceled)
                {
                    linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        _scopeToken,
                        externalToken);
                    token = linkedCancellation.Token;
                }

                await operation(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                LifecycleTaskFailure failure = new LifecycleTaskFailure(
                    _scopeName,
                    operationName,
                    exception);
                AddFailure(failure);

                if (!StopTimedOut)
                {
                    try
                    {
                        _failureHandler?.Invoke(failure);
                    }
                    catch
                    {
                    }

                    if (failurePolicy == LifecycleTaskFailurePolicy.ReportAndStopScope)
                    {
                        try
                        {
                            _requestScopeStop?.Invoke();
                        }
                        catch
                        {
                        }
                    }
                }

                if (failurePolicy == LifecycleTaskFailurePolicy.Propagate)
                {
                    propagatedException = exception;
                }
            }
            finally
            {
                linkedCancellation?.Dispose();
            }

            RemoveActiveTask(activeTask);

            if (propagatedException != null)
            {
                activeTask.CompletionSource.TrySetException(propagatedException);
            }
            else
            {
                activeTask.CompletionSource.TrySetResult();
            }

            // Observation is completed last so StopAsync cannot resume before the
            // public handle has settled and the task has left the active set.
            activeTask.ObservationSource.TrySetResult();
        }

        async UniTask StopCoreAsync()
        {
            ActiveTask[] activeTasks;

            lock (_gate)
            {
                activeTasks = _activeTasks.ToArray();
            }

            if (activeTasks.Length == 0)
            {
                return;
            }

            UniTask[] observations = new UniTask[activeTasks.Length];

            for (int i = 0; i < activeTasks.Length; i++)
            {
                observations[i] = activeTasks[i].ObservationSource.Task;
            }

            bool timedOut = await UniTask.WhenAll(observations)
                .TimeoutWithoutException(_stopTimeout, DelayType.Realtime);

            if (!timedOut)
            {
                return;
            }

            _stopTimedOut = true;

            ActiveTask[] timedOutTasks;

            lock (_gate)
            {
                timedOutTasks = _activeTasks.ToArray();
            }

            string[] timedOutOperationNames = new string[timedOutTasks.Length];

            for (int i = 0; i < timedOutTasks.Length; i++)
            {
                timedOutOperationNames[i] = timedOutTasks[i].OperationName;
            }

            string timedOutOperations = timedOutOperationNames.Length > 0
                ? string.Join(", ", timedOutOperationNames)
                : "<none>";

            LifecycleTaskFailure failure = new LifecycleTaskFailure(
                _scopeName,
                "stop-timeout",
                new TimeoutException(
                    $"生命周期作用域 {_scopeName} 在 {_stopTimeout.TotalSeconds:0.##} 秒内未能停止全部任务；"
                    + $"超时时仍活动: {timedOutOperations}"));
            AddFailure(failure);

            TimeoutException timeoutException = (TimeoutException)failure.Exception;

            for (int i = 0; i < activeTasks.Length; i++)
            {
                activeTasks[i].CompleteFromTimeout(timeoutException);
            }

            try
            {
                _failureHandler?.Invoke(failure);
            }
            catch
            {
            }
        }

        async UniTask CompleteStopAsync()
        {
            try
            {
                await StopCoreAsync();
            }
            catch
            {
            }

            _stopCompletion.TrySetResult();
        }

        void AddFailure(LifecycleTaskFailure failure)
        {
            lock (_gate)
            {
                _failures.Add(failure);
            }
        }

        void RemoveActiveTask(ActiveTask activeTask)
        {
            lock (_gate)
            {
                _activeTasks.Remove(activeTask);
            }
        }
    }
}
