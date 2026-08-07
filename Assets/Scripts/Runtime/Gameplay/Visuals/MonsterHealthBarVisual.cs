using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    public sealed class MonsterHealthBarVisual : MonoBehaviour
    {
        [SerializeField]
        Transform _barRoot;

        [SerializeField]
        SpriteRenderer _fillRenderer;

        Vector3 _baseFillScale;
        Vector3 _baseFillPosition;
        bool _initialized;

        public void Refresh(CombatActor actor)
        {
            EnsureComponents();

            if (_barRoot == null || _fillRenderer == null || actor == null)
            {
                return;
            }

            float normalized = actor.MaxHealth > 0f
                ? Mathf.Clamp01(actor.CurrentHealth / actor.MaxHealth)
                : 0f;
            bool visible = actor.IsAlive && normalized > 0f && normalized < 0.999f;
            _barRoot.gameObject.SetActive(visible);

            if (!visible)
            {
                return;
            }

            Vector3 scale = _baseFillScale;
            scale.x *= normalized;
            _fillRenderer.transform.localScale = scale;
            Vector3 position = _baseFillPosition;
            position.x -= _baseFillScale.x * (1f - normalized) * 0.48f;
            _fillRenderer.transform.localPosition = position;
        }

        public void Hide()
        {
            EnsureComponents();

            if (_barRoot != null)
            {
                _barRoot.gameObject.SetActive(false);
            }
        }

        void Awake()
        {
            EnsureComponents();
            Hide();
        }

        void OnDisable()
        {
            Hide();
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_barRoot == null)
            {
                Transform root = transform.Find("HealthBar");
                _barRoot = root;
            }

            if (_fillRenderer == null && _barRoot != null)
            {
                Transform fill = _barRoot.Find("Fill");
                _fillRenderer = fill != null ? fill.GetComponent<SpriteRenderer>() : null;
            }

            if (!_initialized && _fillRenderer != null)
            {
                _baseFillScale = _fillRenderer.transform.localScale;
                _baseFillPosition = _fillRenderer.transform.localPosition;
                _initialized = true;
            }
        }
    }
}
