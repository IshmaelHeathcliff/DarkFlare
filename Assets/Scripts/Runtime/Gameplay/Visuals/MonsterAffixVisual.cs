using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatActor))]
    public sealed class MonsterAffixVisual : MonoBehaviour, IController
    {
        const int MaximumVisibleAffixes = 2;

        [SerializeField]
        CombatActor _actor;

        [SerializeField]
        TextMeshPro _label;

        [SerializeField]
        Vector3 _labelLocalPosition = new Vector3(0f, 1.1f, 0f);

        [SerializeField]
        [Min(0.1f)]
        float _fontSize = 1.65f;

        readonly List<IUnRegister> _registrations = new List<IUnRegister>();

        MonsterInstanceData _instance;
        LocalizationService _localizationService;

        public TextMeshPro Label => _label;

        public bool IsVisible => _label != null && _label.gameObject.activeSelf;

        public string DisplayText => _label != null ? _label.text : string.Empty;

        public Vector3 LabelWorldScale => _label != null ? _label.transform.lossyScale : Vector3.zero;

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.RequireCurrent();
        }

        public void Configure(MonsterInstanceData instance)
        {
            _instance = instance;
            Refresh();
        }

        public void Hide()
        {
            EnsureComponents();

            if (_label != null)
            {
                _label.gameObject.SetActive(false);
            }
        }

        void Awake()
        {
            ResolveComponents();
            EnsureLabel();
            ApplyLabelStyle();
            Hide();
        }

        void OnEnable()
        {
            BindLocalization();
            RegisterEvents();
            Refresh();
        }

        void OnDisable()
        {
            if (_localizationService != null)
            {
                _localizationService.LocaleChanged -= OnLocaleChanged;
                _localizationService = null;
            }

            UnregisterEvents();
            Hide();
        }

        void OnValidate()
        {
            _fontSize = Mathf.Max(0.1f, _fontSize);
            ResolveComponents();
        }

        void EnsureComponents()
        {
            ResolveComponents();
            EnsureLabel();
            ApplyLabelStyle();
        }

        void ResolveComponents()
        {
            if (_actor == null)
            {
                _actor = GetComponent<CombatActor>();
            }

            if (_label == null)
            {
                Transform existing = transform.Find("AffixLabel");
                _label = existing != null ? existing.GetComponent<TextMeshPro>() : null;
            }

            if (_label != null)
            {
                _label.gameObject.layer = gameObject.layer;
            }
        }

        void EnsureLabel()
        {
            if (_label == null)
            {
                GameObject labelObject = new GameObject("AffixLabel");
                labelObject.layer = gameObject.layer;
                labelObject.transform.SetParent(transform, false);
                _label = labelObject.AddComponent<TextMeshPro>();
            }
        }

        void ApplyLabelStyle()
        {
            if (_label == null)
            {
                return;
            }

            _label.transform.localPosition = _labelLocalPosition;
            _label.transform.localRotation = Quaternion.identity;
            Vector3 parentScale = transform.lossyScale;
            _label.transform.localScale = new Vector3(
                InverseScale(parentScale.x),
                InverseScale(parentScale.y),
                InverseScale(parentScale.z));
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = _fontSize;
            _label.fontStyle = FontStyles.Bold;
            _label.color = Color.white;
            _label.outlineColor = new Color(0.02f, 0.025f, 0.035f, 0.95f);
            _label.outlineWidth = 0.2f;
            _label.renderer.sortingLayerName = "WorldObject";
            _label.sortingOrder = 30;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.richText = true;
        }

        static float InverseScale(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
        }

        void RegisterEvents()
        {
            if (_registrations.Count > 0)
            {
                return;
            }

            _registrations.Add(this.RegisterEvent<ActorDiedEvent>(OnActorDied));
            _registrations.Add(this.RegisterEvent<ActorRevivedEvent>(OnActorRevived));
        }

        void UnregisterEvents()
        {
            for (int i = 0; i < _registrations.Count; i++)
            {
                _registrations[i].UnRegister();
            }

            _registrations.Clear();
        }

        void Refresh()
        {
            EnsureComponents();

            if (_label == null
                || _actor == null
                || !_actor.IsAlive
                || _instance == null
                || _instance.Affixes.Count == 0)
            {
                Hide();
                return;
            }

            List<string> lines = new List<string>(MaximumVisibleAffixes);
            int count = Mathf.Min(MaximumVisibleAffixes, _instance.Affixes.Count);

            for (int i = 0; i < count; i++)
            {
                MonsterAffixDefinition definition = _instance.Affixes[i].Definition;

                if (definition == null)
                {
                    continue;
                }

                string displayName = Resolve(definition.LocalizedName?.Message ?? default);
                string color = ColorUtility.ToHtmlStringRGBA(definition.DisplayColor);
                lines.Add($"<color=#{color}>{displayName}</color>");
            }

            if (lines.Count == 0)
            {
                Hide();
                return;
            }

            _label.text = string.Join("\n", lines);
            _label.gameObject.SetActive(true);
        }

        void OnActorDied(ActorDiedEvent e)
        {
            if (e.Actor == _actor)
            {
                Hide();
            }
        }

        void OnActorRevived(ActorRevivedEvent e)
        {
            if (e.Actor == _actor)
            {
                Refresh();
            }
        }

        void BindLocalization()
        {
            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host)
                || ReferenceEquals(_localizationService, host.Localization))
            {
                return;
            }

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged -= OnLocaleChanged;
            }

            _localizationService = host.Localization;

            if (_localizationService != null)
            {
                _localizationService.LocaleChanged += OnLocaleChanged;
            }
        }

        void OnLocaleChanged(string localeCode)
        {
            Refresh();
        }

        string Resolve(LocalizedMessage message)
        {
            return _localizationService?.GetString(message)
                ?? $"[{message.TableName}.{message.EntryKey}]";
        }
    }
}
