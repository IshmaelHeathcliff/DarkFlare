using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    public sealed class BehaviorAnimatorBridge : MonoBehaviour, IController
    {
        [SerializeField] Animator _animator;
        [SerializeField] BehaviorAnimatorMap _mapping;
        IArchitecture _architecture;
        BehaviorHandle _handle;
        IUnRegister _subscription;
        BehaviorAnimatorPresenter _presenter;
        public int AppliedAttackCount => _presenter?.AppliedAttackCount ?? 0;
        public int ValidationIssueCount => _presenter?.ValidationIssueCount ?? 0;
        public IArchitecture GetArchitecture() { return _architecture; }
        void OnValidate() { if (_animator == null) { _animator = GetComponent<Animator>(); } }
        void OnEnable() { Subscribe(); }
        void OnDisable() { _subscription?.UnRegister(); _subscription = null; _presenter?.Clear(); }
        void OnDestroy() { Unbind(); }
        public void Bind(IArchitecture architecture, BehaviorHandle handle)
        {
            Unbind(); _architecture = architecture; _handle = handle;
            if (_animator == null) { _animator = GetComponent<Animator>(); }
            _presenter = new BehaviorAnimatorPresenter(_animator, _mapping);
            Subscribe();
        }
        void Subscribe()
        {
            if (!isActiveAndEnabled || _architecture == null || !_handle.IsValid || _subscription != null) { return; }
            ActorAnimatorController legacy = GetComponent<ActorAnimatorController>();
            if (legacy != null && !legacy.BehaviorDrivenAttack)
            {
                throw new System.InvalidOperationException("行为动画桥要求显式启用行为驱动攻击表现");
            }
            _presenter.Bind(_handle);
            _subscription = this.RegisterEvent<BehaviorPresentationEvent>(Apply);
            BehaviorDebugSnapshot snapshot = this.SendQuery(new GetBehaviorSnapshotQuery(_handle));
            if (snapshot != null) { Apply(snapshot.Presentation); }
        }
        public void Unbind()
        {
            _subscription?.UnRegister(); _subscription = null;
            _presenter?.Clear(); _architecture = null; _handle = default;
        }
        void Apply(BehaviorPresentationEvent signal) { _presenter?.Apply(signal); }
        // 回执必须携带捕获时的身份与动作序号，不能从当前 Animator 状态补造旧动作身份。
        public bool ReportMarker(BehaviorHandle handle, long actionInstanceId)
        {
            if (_architecture == null || !isActiveAndEnabled || !handle.Equals(_handle)) { return false; }
            return this.SendCommand(new ObserveBehaviorAnimationCommand(handle, actionInstanceId));
        }
    }
}
