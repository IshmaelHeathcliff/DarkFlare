using System;
using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class BehaviorModel : AbstractModel
    {
        internal sealed class Entry
        {
            internal BehaviorHandle Handle { get; }
            internal IBehaviorRuntime Runtime { get; }
            internal bool Revoked { get; set; }
            internal Entry(BehaviorHandle handle, IBehaviorRuntime runtime) { Handle = handle; Runtime = runtime; }
        }
        internal List<Entry> Entries { get; } = new List<Entry>();
        public int Count => Entries.Count;
        protected override void OnInit() { }
    }

    public sealed class BehaviorSystem : AbstractSystem
    {
        BehaviorModel _model;
        long _nextRegistration;
        bool _ticking;
        bool _flushing;
        protected override void OnInit() { _model = this.GetModel<BehaviorModel>(); }
        protected override void OnDeinit() { StopScope(Guid.Empty); }

        public BehaviorHandle Register(Guid scope, IBehaviorRuntime runtime)
        {
            if (scope == Guid.Empty) { throw new ArgumentException("需要行为作用域", nameof(scope)); }
            if (runtime == null) { throw new ArgumentNullException(nameof(runtime)); }
            foreach (BehaviorModel.Entry entry in _model.Entries)
            {
                if (ReferenceEquals(entry.Runtime, runtime)) { throw new InvalidOperationException("行为运行实例只能注册一次"); }
            }
            var handle = new BehaviorHandle(GameArchitectureProvider.Generation, scope, checked(++_nextRegistration));
            var registration = new BehaviorModel.Entry(handle, runtime);
            _model.Entries.Add(registration);
            try { runtime.Start(handle, Publish); }
            catch { registration.Revoked = true; runtime.Revoke(); if (!_ticking) { Flush(); } throw; }
            return handle;
        }

        public void Tick(Guid scope, float deltaTime)
        {
            if (_ticking) { throw new InvalidOperationException("行为调度不能重入"); }
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            if (deltaTime == 0 || GameTimeService.Shared.IsPaused) { return; }
            _ticking = true;
            try
            {
                int count = _model.Entries.Count;
                for (int i = 0; i < count; i++)
                {
                    BehaviorModel.Entry entry = _model.Entries[i];
                    if (!entry.Revoked && entry.Handle.Scope == scope)
                    {
                        try { entry.Runtime.Tick(deltaTime); }
                        catch (Exception error)
                        {
                            entry.Revoked = true; entry.Runtime.Revoke();
                            ApplicationLog.Exception(LogEventIds.InfrastructureLifecycle, error);
                        }
                    }
                }
            }
            finally { _ticking = false; Flush(); }
        }

        public void Unregister(BehaviorHandle handle)
        {
            BehaviorModel.Entry entry = Find(handle);
            if (entry == null) { return; }
            entry.Revoked = true;
            entry.Runtime.Revoke();
            if (!_ticking) { Flush(); }
        }

        public void StopScope(Guid scope)
        {
            foreach (BehaviorModel.Entry entry in _model.Entries)
            {
                if (scope == Guid.Empty || entry.Handle.Scope == scope) { entry.Revoked = true; entry.Runtime.Revoke(); }
            }
            if (!_ticking) { Flush(); }
        }

        void Flush()
        {
            if (_flushing) { return; }
            _flushing = true;
            try
            {
                while (true)
                {
                    int index = _model.Entries.Count - 1;
                    while (index >= 0 && !_model.Entries[index].Revoked) { index--; }
                    if (index < 0) { break; }
                    BehaviorModel.Entry entry = _model.Entries[index];
                    _model.Entries.RemoveAt(index);
                    try
                    {
                        entry.Runtime.Stop();
                        BehaviorPresentationEvent last = entry.Runtime.Presentation;
                        this.SendEvent(new BehaviorPresentationEvent(entry.Handle, last.ActionInstanceId, BehaviorSemantic.Stopped, last.Position, false));
                    }
                    catch (Exception error) { ApplicationLog.Exception(LogEventIds.InfrastructureLifecycle, error); }
                }
            }
            finally { _flushing = false; }
        }

        void Publish(BehaviorPresentationEvent signal)
        {
            if (Find(signal.Handle) != null) { this.SendEvent(signal); }
        }

        BehaviorModel.Entry Find(BehaviorHandle handle)
        {
            foreach (BehaviorModel.Entry entry in _model.Entries)
            {
                if (!entry.Revoked && entry.Handle.Equals(handle)) { return entry; }
            }
            return null;
        }

        public void Control(BehaviorHandle handle, BehaviorControl control) { Find(handle)?.Runtime.Control(control); }
        public bool ObserveAnimation(BehaviorHandle handle, long action) { return Find(handle)?.Runtime.ObserveAnimation(action) ?? false; }
        public BehaviorDebugSnapshot Capture(BehaviorHandle handle) { return _ticking ? null : Find(handle)?.Runtime.Capture(); }
        public IDisposable AcquireDiagnostics(BehaviorHandle handle) { return Find(handle)?.Runtime.AcquireDiagnostics(); }
        public BehaviorHandle[] ListHandles()
        {
            var handles = new List<BehaviorHandle>();
            foreach (BehaviorModel.Entry entry in _model.Entries) { if (!entry.Revoked) { handles.Add(entry.Handle); } }
            return handles.ToArray();
        }
    }

    public sealed class RegisterBehaviorCommand : AbstractCommand<BehaviorHandle>
    {
        readonly Guid _scope; readonly IBehaviorRuntime _runtime;
        public RegisterBehaviorCommand(Guid scope, IBehaviorRuntime runtime) { _scope = scope; _runtime = runtime; }
        protected override BehaviorHandle OnExecute() { return this.GetSystem<BehaviorSystem>().Register(_scope, _runtime); }
    }
    public sealed class UnregisterBehaviorCommand : AbstractCommand
    {
        readonly BehaviorHandle _handle;
        public UnregisterBehaviorCommand(BehaviorHandle handle) { _handle = handle; }
        protected override void OnExecute() { this.GetSystem<BehaviorSystem>().Unregister(_handle); }
    }
    public sealed class ControlBehaviorCommand : AbstractCommand
    {
        readonly BehaviorHandle _handle; readonly BehaviorControl _control;
        public ControlBehaviorCommand(BehaviorHandle handle, BehaviorControl control) { _handle = handle; _control = control; }
        protected override void OnExecute() { this.GetSystem<BehaviorSystem>().Control(_handle, _control); }
    }
    public sealed class ObserveBehaviorAnimationCommand : AbstractCommand<bool>
    {
        readonly BehaviorHandle _handle; readonly long _action;
        public ObserveBehaviorAnimationCommand(BehaviorHandle handle, long action) { _handle = handle; _action = action; }
        protected override bool OnExecute() { return this.GetSystem<BehaviorSystem>().ObserveAnimation(_handle, _action); }
    }
    public sealed class GetBehaviorSnapshotQuery : AbstractQuery<BehaviorDebugSnapshot>
    {
        readonly BehaviorHandle _handle;
        public GetBehaviorSnapshotQuery(BehaviorHandle handle) { _handle = handle; }
        protected override BehaviorDebugSnapshot OnDo() { return this.GetSystem<BehaviorSystem>().Capture(_handle); }
    }
    public sealed class GetBehaviorHandlesQuery : AbstractQuery<BehaviorHandle[]>
    {
        protected override BehaviorHandle[] OnDo() { return this.GetSystem<BehaviorSystem>().ListHandles(); }
    }
    public sealed class AcquireBehaviorDiagnosticsCommand : AbstractCommand<IDisposable>
    {
        readonly BehaviorHandle _handle;
        public AcquireBehaviorDiagnosticsCommand(BehaviorHandle handle) { _handle = handle; }
        protected override IDisposable OnExecute() { return this.GetSystem<BehaviorSystem>().AcquireDiagnostics(_handle); }
    }
    public sealed class TickBehaviorScopeCommand : AbstractCommand
    {
        readonly Guid _scope;
        public float DeltaTime { get; set; }
        public TickBehaviorScopeCommand(Guid scope) { _scope = scope; }
        protected override void OnExecute() { this.GetSystem<BehaviorSystem>().Tick(_scope, DeltaTime); }
    }
}
