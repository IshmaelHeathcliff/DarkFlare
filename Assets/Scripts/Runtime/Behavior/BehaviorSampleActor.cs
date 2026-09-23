using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    public sealed class BehaviorSampleActor : MonoBehaviour, IController
    {
        [SerializeField] BehaviorSampleHost _host;
        [SerializeField] BehaviorAnimatorBridge _bridge;
        [SerializeField] SpriteRenderer _renderer;
        SceneSessionBinding _binding;
        IArchitecture _owner;
        IUnRegister _subscription;
        Vector3 _origin;
        bool _originCaptured;
        public BehaviorHandle Handle { get; private set; }
        public IArchitecture GetArchitecture() { return _binding.RequireArchitecture(); }
        void Awake() { CaptureOrigin(); EnsureComponents(); }
        void CaptureOrigin() { if (!_originCaptured) { _origin = transform.position; _originCaptured = true; } }
        void OnValidate() { EnsureComponents(); }
        void EnsureComponents()
        {
            if (_host == null) { _host = GetComponentInParent<BehaviorSampleHost>(); }
            if (_bridge == null) { _bridge = GetComponent<BehaviorAnimatorBridge>(); }
            if (_renderer == null) { _renderer = GetComponent<SpriteRenderer>(); }
        }
        void OnEnable() { Rebind(); }
        public void Rebind()
        {
            if (!isActiveAndEnabled) { return; }
            CaptureOrigin(); EnsureComponents();
            _binding ??= new SceneSessionBinding(this, Bind, Unbind);
            _binding.Disable();
            _binding.Enable();
        }
        SceneSessionBindResult Bind(IArchitecture owner)
        {
            if (_host == null || !_host.IsReady) { return SceneSessionBindResult.Retry; }
            _owner = owner;
            PatrolBehaviorRuntime runtime = _host.Template.Create(name, _origin);
            Handle = this.SendCommand(new RegisterBehaviorCommand(_host.ScopeId, runtime));
            _subscription = this.GetSystem<BehaviorSystem>().RegisterPresentation(Handle, OnPresentation);
            _bridge?.Bind(owner, Handle);
            return SceneSessionBindResult.Success;
        }
        void Unbind()
        {
            _bridge?.Unbind(); _subscription?.UnRegister(); _subscription = null;
            if (_owner != null && Handle.IsValid) { _owner.SendCommand(new UnregisterBehaviorCommand(Handle)); }
            _owner = null; Handle = default;
        }
        void OnPresentation(BehaviorPresentationEvent signal)
        {
            if (!signal.Handle.Equals(Handle)) { return; }
            float dx = signal.Position.x - transform.position.x;
            transform.position = new Vector3(signal.Position.x, signal.Position.y, _origin.z);
            if (_renderer != null && Mathf.Abs(dx) > .0001f) { _renderer.flipX = dx < 0; }
        }
        void OnDisable() { _binding?.Disable(); }
        void OnDestroy() { _binding?.Dispose(); }
    }
}
