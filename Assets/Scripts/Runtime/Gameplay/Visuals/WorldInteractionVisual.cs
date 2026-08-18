using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldInteractionTarget))]
    public sealed class WorldInteractionVisual : MonoBehaviour, IController
    {
        [SerializeField]
        WorldInteractionTarget _target;

        [SerializeField]
        SpriteRenderer _mainRenderer;

        [SerializeField]
        SpriteRenderer _outlineRenderer;

        [SerializeField]
        TextMeshPro _nameplate;

        [SerializeField]
        TextMeshPro _focusPrompt;

        readonly List<IUnRegister> _registrations = new List<IUnRegister>();

        SceneSessionBinding _sessionBinding;
        bool _focused;
        bool _paused;

        public bool IsFocused => _focused && !_paused;

        public int SessionBindCount => _sessionBinding?.BindCount ?? 0;

        public IArchitecture GetArchitecture()
        {
            return _sessionBinding.RequireArchitecture();
        }

        void Awake()
        {
            EnsureComponents();
            ApplyState();
        }

        void OnEnable()
        {
            _sessionBinding ??= new SceneSessionBinding(
                this,
                BindSession,
                UnbindSession);
            _sessionBinding.Enable();
        }

        void OnDisable()
        {
            _sessionBinding?.Disable();
        }

        SceneSessionBindResult BindSession(IArchitecture architecture)
        {
            RegisterEvents();
            ApplyState();
            return SceneSessionBindResult.Success;
        }

        void UnbindSession()
        {
            UnregisterEvents();
            _focused = false;
            _paused = false;
            ApplyState();
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_target == null)
            {
                _target = GetComponent<WorldInteractionTarget>();
            }

            if (_mainRenderer == null)
            {
                _mainRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (_nameplate == null)
            {
                Transform label = transform.Find("Nameplate");
                _nameplate = label != null ? label.GetComponent<TextMeshPro>() : null;
            }

            if (_focusPrompt == null)
            {
                Transform prompt = transform.Find("FocusPrompt");
                _focusPrompt = prompt != null ? prompt.GetComponent<TextMeshPro>() : null;
            }
        }

        void RegisterEvents()
        {
            if (_registrations.Count > 0)
            {
                return;
            }

            _registrations.Add(this.RegisterEvent<InteractionFocusChangedEvent>(OnFocusChanged));
            _registrations.Add(this.RegisterEvent<GameplayPauseChangedEvent>(OnPauseChanged));
        }

        void UnregisterEvents()
        {
            for (int i = 0; i < _registrations.Count; i++)
            {
                _registrations[i].UnRegister();
            }

            _registrations.Clear();
        }

        void OnFocusChanged(InteractionFocusChangedEvent e)
        {
            _focused = e.Target == _target;
            ApplyState();
        }

        void OnPauseChanged(GameplayPauseChangedEvent e)
        {
            _paused = e.IsPaused;
            ApplyState();
        }

        void ApplyState()
        {
            bool highlighted = IsFocused && isActiveAndEnabled;

            if (_outlineRenderer != null)
            {
                _outlineRenderer.gameObject.SetActive(highlighted);
            }

            if (_focusPrompt != null)
            {
                _focusPrompt.gameObject.SetActive(highlighted);
                _focusPrompt.text = highlighted ? $"交互 · {_target.DisplayName}" : string.Empty;
            }

            if (_nameplate != null && _target != null)
            {
                _nameplate.text = _target.DisplayName;
            }
        }
    }
}
