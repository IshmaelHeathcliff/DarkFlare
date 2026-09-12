using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    /// <summary>统一接收原生面板重载，先释放全部旧绑定，再发布新视觉树。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    [DefaultExecutionOrder(-200)]
    public sealed class RuntimePanelView : MonoBehaviour
    {
        [SerializeField] PanelRenderer _renderer;
        int _version = -1;
        bool _detached;

        public PanelRenderer Renderer => _renderer;
        public VisualElement Root { get; private set; }
        public event Action Reloading;
        public event Action Reloaded;

        void OnEnable()
        {
            EnsureRenderer();
            _renderer.RegisterUIReloadCallback(OnUIReloaded);
        }

        void OnDisable()
        {
            _renderer.UnregisterUIReloadCallback(OnUIReloaded);
        }

        void OnDestroy()
        {
            Root?.UnregisterCallback<DetachFromPanelEvent>(OnDetached);
            Root = null;
            Reloading = null;
            Reloaded = null;
        }

        void OnValidate()
        {
            EnsureRenderer();
        }

        void EnsureRenderer()
        {
            if (_renderer == null) { _renderer = GetComponent<PanelRenderer>(); }
            if (_renderer == null && gameObject.scene.IsValid()) { _renderer = gameObject.AddComponent<PanelRenderer>(); }
        }

        void OnUIReloaded(PanelRenderer renderer, VisualElement root, int version)
        {
            if (!_detached && ReferenceEquals(Root, root) && _version == version) { return; }
            if (!_detached) { Reloading?.Invoke(); }
            Root = root;
            _version = version;
            _detached = false;
            root.RegisterCallback<DetachFromPanelEvent>(OnDetached);
            Reloaded?.Invoke();
        }

        void OnDetached(DetachFromPanelEvent evt)
        {
            if (!ReferenceEquals(evt.target, Root) || _detached) { return; }
            _detached = true;
            Reloading?.Invoke();
        }
    }
}
