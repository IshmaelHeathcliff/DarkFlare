using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DarkFlare
{
    public sealed class LifecycleScope
    {
        readonly LifecycleScope _parent;
        readonly CancellationTokenSource _cancellation;
        readonly CancellationToken _token;
        readonly Action<LifecycleTaskFailure> _failureHandler;
        readonly TimeSpan _taskStopTimeout;
        readonly List<LifecycleScope> _children = new List<LifecycleScope>();

        UniTaskCompletionSource _stopCompletion;
        UniTask _stopRunner;
        bool _descendantAbandoned;

        LifecycleScope(
            string name,
            LifecycleScope parent,
            CancellationTokenSource cancellation,
            Action<LifecycleTaskFailure> failureHandler,
            TimeSpan taskStopTimeout)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("作用域名称不能为空", nameof(name));
            }

            Name = name;
            _parent = parent;
            _cancellation = cancellation;
            _token = cancellation.Token;
            _failureHandler = failureHandler;
            _taskStopTimeout = taskStopTimeout;
            Tasks = new LifecycleTaskGroup(
                name,
                _token,
                failureHandler,
                BeginStop,
                taskStopTimeout);
            State = LifecycleScopeState.Active;
        }

        public string Name { get; }

        public LifecycleScopeState State { get; private set; }

        public CancellationToken Token => _token;

        public LifecycleTaskGroup Tasks { get; }

        public int ChildCount => _children.Count;

        public bool IsAbandoned => State == LifecycleScopeState.Abandoned;

        public bool CanAcceptWork =>
            State == LifecycleScopeState.Active && !_descendantAbandoned;

        public static LifecycleScope CreateRoot(
            string name,
            Action<LifecycleTaskFailure> failureHandler = null,
            CancellationToken externalToken = default,
            TimeSpan? taskStopTimeout = null)
        {
            TimeSpan resolvedTimeout = taskStopTimeout ?? TimeSpan.FromSeconds(10d);

            if (resolvedTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(taskStopTimeout),
                    "任务停止时限必须大于零");
            }

            CancellationTokenSource cancellation = externalToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(externalToken)
                : new CancellationTokenSource();
            return new LifecycleScope(
                name,
                null,
                cancellation,
                failureHandler,
                resolvedTimeout);
        }

        public LifecycleScope CreateChild(
            string name,
            CancellationToken externalToken = default)
        {
            if (!CanAcceptWork)
            {
                if (_descendantAbandoned)
                {
                    throw new InvalidOperationException(
                        $"生命周期作用域 {Name} 已被 Abandoned 后代污染，不能创建子作用域");
                }

                throw new InvalidOperationException($"生命周期作用域 {Name} 已开始停止，不能创建子作用域");
            }

            CancellationTokenSource cancellation = externalToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(Token, externalToken)
                : CancellationTokenSource.CreateLinkedTokenSource(Token);
            LifecycleScope child = new LifecycleScope(
                name,
                this,
                cancellation,
                _failureHandler,
                _taskStopTimeout);
            _children.Add(child);
            return child;
        }

        public void BeginStop()
        {
            if (State != LifecycleScopeState.Active)
            {
                return;
            }

            _stopCompletion = new UniTaskCompletionSource();
            State = LifecycleScopeState.Stopping;
            Tasks.StopAccepting();
            LifecycleScope[] children = _children.ToArray();

            for (int i = children.Length - 1; i >= 0; i--)
            {
                children[i].BeginStop();
            }

            try
            {
                _cancellation.Cancel();
            }
            catch (Exception exception)
            {
                ReportStopFailure("cancel", exception);
            }

            _stopRunner = CompleteStopAsync().Preserve();
        }

        public UniTask StopAsync()
        {
            BeginStop();
            return _stopCompletion.Task;
        }

        async UniTask StopCoreAsync()
        {
            bool abandoned = _descendantAbandoned;

            try
            {
                LifecycleScope[] children = _children.ToArray();

                for (int i = children.Length - 1; i >= 0; i--)
                {
                    await children[i].StopAsync();
                    abandoned |= children[i].IsAbandoned;
                }

                await Tasks.StopAsync();
                abandoned |= Tasks.StopTimedOut;
            }
            finally
            {
                _cancellation.Dispose();
                State = abandoned || _descendantAbandoned
                    ? LifecycleScopeState.Abandoned
                    : LifecycleScopeState.Stopped;
                _parent?.RemoveChild(this);
            }
        }

        async UniTask CompleteStopAsync()
        {
            try
            {
                await StopCoreAsync();
            }
            catch (Exception exception)
            {
                ReportStopFailure("stop", exception);
            }

            _stopCompletion.TrySetResult();
        }

        void ReportStopFailure(string operationName, Exception exception)
        {
            try
            {
                _failureHandler?.Invoke(new LifecycleTaskFailure(
                    Name,
                    operationName,
                    exception));
            }
            catch
            {
            }
        }

        void RemoveChild(LifecycleScope child)
        {
            if (child.IsAbandoned)
            {
                MarkDescendantAbandoned();
            }

            _children.Remove(child);
        }

        void MarkDescendantAbandoned()
        {
            if (_descendantAbandoned)
            {
                return;
            }

            _descendantAbandoned = true;
            Tasks.StopAccepting();
            _parent?.MarkDescendantAbandoned();
        }

    }
}
