using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    public enum SceneSessionBindResult
    {
        Success,
        Retry,
        Failed
    }

    /// <summary>
    /// 将场景预置组件延迟绑定到属于自身场景且已经 Running 的 Session。
    /// </summary>
    public sealed class SceneSessionBinding : IDisposable
    {
        readonly MonoBehaviour _owner;
        readonly Func<IArchitecture, SceneSessionBindResult> _bind;
        readonly Action _unbind;

        ApplicationHost _host;
        GameSessionHost _session;
        LifecycleScope _bindingScope;
        IArchitecture _architecture;
        CancellationTokenRegistration _scopeCancellationRegistration;
        bool _enabled;
        bool _binding;
        bool _bound;
        bool _retryScheduled;
        int _retryVersion;

        public SceneSessionBinding(
            MonoBehaviour owner,
            Func<IArchitecture, SceneSessionBindResult> bind,
            Action unbind)
        {
            _owner = owner != null
                ? owner
                : throw new ArgumentNullException(nameof(owner));
            _bind = bind ?? throw new ArgumentNullException(nameof(bind));
            _unbind = unbind ?? throw new ArgumentNullException(nameof(unbind));
        }

        public bool IsBound => _bound;

        public int BindCount { get; private set; }

        public IArchitecture RequireArchitecture()
        {
            if ((!_binding && !_bound) || _architecture == null)
            {
                throw new InvalidOperationException(
                    $"{_owner.GetType().Name} 尚未绑定到所属场景的 Running Session");
            }

            return _architecture;
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _enabled = true;
            _retryVersion++;

            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                _host = host;
                _host.SessionRunning += OnSessionRunning;
                TryBind(host.CurrentSession);
                return;
            }

            TryBindStandaloneSession();
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _enabled = false;
            InvalidateRetry();

            if (_host != null)
            {
                _host.SessionRunning -= OnSessionRunning;
            }

            _host = null;
            UnbindCurrent(true);
        }

        public void Dispose()
        {
            Disable();
        }

        void OnSessionRunning(GameSessionHost session)
        {
            TryBind(session);
        }

        void TryBind(GameSessionHost session)
        {
            if (!_enabled
                || _owner == null
                || !_owner.isActiveAndEnabled
                || session == null
                || session.State != GameSessionState.Running
                || !session.SceneScope.CanAcceptWork
                || !session.IsBoundToScene(_owner.gameObject.scene))
            {
                return;
            }

            if (_bound && ReferenceEquals(_session, session))
            {
                return;
            }

            if (!ReferenceEquals(_session, session)
                || !ReferenceEquals(_bindingScope, session.SceneScope))
            {
                UnbindCurrent(true);
                _session = session;
                _bindingScope = session.SceneScope;
                _architecture = session.Architecture;
                CancellationTokenRegistration registration = session.SceneScope.Token.Register(
                    () => OnSessionStopping(session));
                _scopeCancellationRegistration = registration;

                if (!ReferenceEquals(_session, session)
                    || session.SceneScope.Token.IsCancellationRequested)
                {
                    registration.Dispose();
                    return;
                }
            }

            CompleteAttempt(session, _bindingScope, TryBindCurrent());
        }

        void TryBindStandaloneSession()
        {
            if (!GameArchitectureProvider.TryGetCurrent(out IArchitecture architecture)
                || !GameArchitectureProvider.TryGetOwnerScope(out LifecycleScope ownerScope)
                || !ownerScope.CanAcceptWork)
            {
                return;
            }

            _bindingScope = ownerScope;
            _architecture = architecture;
            CancellationTokenRegistration registration = ownerScope.Token.Register(
                OnStandaloneSessionStopping);
            _scopeCancellationRegistration = registration;

            if (ownerScope.Token.IsCancellationRequested)
            {
                registration.Dispose();
                _bindingScope = null;
                _architecture = null;
                return;
            }

            CompleteAttempt(null, ownerScope, TryBindCurrent());
        }

        SceneSessionBindResult TryBindCurrent()
        {
            if (_binding || _bound || _architecture == null)
            {
                return _bound
                    ? SceneSessionBindResult.Success
                    : SceneSessionBindResult.Failed;
            }

            _binding = true;
            SceneSessionBindResult result;

            try
            {
                result = _bind(_architecture);

                if (result != SceneSessionBindResult.Success)
                {
                    RollbackBindingAttempt();
                    return result;
                }

                if (!_enabled
                    || _bindingScope == null
                    || !_bindingScope.CanAcceptWork)
                {
                    RollbackBindingAttempt();
                    return SceneSessionBindResult.Failed;
                }

                _binding = false;
                _bound = true;
                BindCount++;
                return SceneSessionBindResult.Success;
            }
            catch (Exception exception)
            {
                RollbackBindingAttempt();
                ApplicationLog.Exception(LogEventIds.InfrastructureLifecycle, exception, _owner);
                return SceneSessionBindResult.Failed;
            }
        }

        void CompleteAttempt(
            GameSessionHost session,
            LifecycleScope bindingScope,
            SceneSessionBindResult result)
        {
            if (result == SceneSessionBindResult.Retry)
            {
                ScheduleRetry(session, bindingScope);
                return;
            }

            InvalidateRetry();
        }

        void ScheduleRetry(GameSessionHost session, LifecycleScope bindingScope)
        {
            if (_retryScheduled
                || !_enabled
                || bindingScope == null
                || !bindingScope.CanAcceptWork)
            {
                return;
            }

            _retryScheduled = true;
            int retryVersion = _retryVersion;

            try
            {
                bindingScope.Tasks.Run(
                    $"scene-component-bind:{_owner.GetType().Name}:{_owner.GetInstanceID()}",
                    token => RetryBindingAsync(
                        session,
                        bindingScope,
                        retryVersion,
                        token),
                    _owner.GetCancellationTokenOnDestroy(),
                    LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                if (retryVersion == _retryVersion)
                {
                    _retryScheduled = false;
                }

                ApplicationLog.Exception(LogEventIds.InfrastructureLifecycle, exception, _owner);
            }
        }

        async UniTask RetryBindingAsync(
            GameSessionHost session,
            LifecycleScope bindingScope,
            int retryVersion,
            CancellationToken token)
        {
            try
            {
                while (CanRetry(session, bindingScope, retryVersion))
                {
                    await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, token);

                    if (!CanRetry(session, bindingScope, retryVersion))
                    {
                        return;
                    }

                    SceneSessionBindResult result = TryBindCurrent();

                    if (result == SceneSessionBindResult.Retry)
                    {
                        continue;
                    }

                    InvalidateRetry();
                    return;
                }
            }
            finally
            {
                if (retryVersion == _retryVersion)
                {
                    _retryScheduled = false;
                }
            }
        }

        bool CanRetry(
            GameSessionHost session,
            LifecycleScope bindingScope,
            int retryVersion)
        {
            if (!_enabled
                || _bound
                || retryVersion != _retryVersion
                || !ReferenceEquals(_bindingScope, bindingScope)
                || !bindingScope.CanAcceptWork)
            {
                return false;
            }

            if (session == null)
            {
                return _session == null;
            }

            return ReferenceEquals(_session, session)
                && session.State == GameSessionState.Running
                && session.IsBoundToScene(_owner.gameObject.scene);
        }

        void OnSessionStopping(GameSessionHost session)
        {
            if (!ReferenceEquals(_session, session))
            {
                return;
            }

            UnbindCurrent(false);
        }

        void OnStandaloneSessionStopping()
        {
            if (_session == null)
            {
                UnbindCurrent(false);
            }
        }

        void UnbindCurrent(bool disposeCancellationRegistration)
        {
            InvalidateRetry();

            if (disposeCancellationRegistration)
            {
                _scopeCancellationRegistration.Dispose();
            }

            _scopeCancellationRegistration = default;
            bool needsUnbind = _binding || _bound;
            _session = null;
            _bindingScope = null;
            _architecture = null;

            if (!needsUnbind)
            {
                return;
            }

            _binding = false;
            _bound = false;

            try
            {
                _unbind();
            }
            catch (Exception exception)
            {
                ApplicationLog.Exception(LogEventIds.InfrastructureLifecycle, exception, _owner);
            }
        }

        void RollbackBindingAttempt()
        {
            try
            {
                _unbind();
            }
            catch (Exception exception)
            {
                ApplicationLog.Exception(LogEventIds.InfrastructureLifecycle, exception, _owner);
            }
            finally
            {
                _binding = false;
                _bound = false;
            }
        }

        void InvalidateRetry()
        {
            _retryVersion++;
            _retryScheduled = false;
        }
    }
}
