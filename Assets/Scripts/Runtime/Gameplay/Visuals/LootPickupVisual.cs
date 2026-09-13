using PrimeTween;
using TMPro;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    public class LootPickupVisual : MonoBehaviour, IController
    {
        [SerializeField]
        Transform _visualRoot;

        [SerializeField]
        SpriteRenderer _shadowRenderer;

        [SerializeField]
        SpriteRenderer _haloRenderer;

        [SerializeField]
        SpriteRenderer _iconRenderer;

        [SerializeField]
        Sprite _missingIcon;

        [SerializeField]
        TextMeshPro _label;

        [SerializeField]
        [Min(0f)]
        float _floatDistance = 0.1f;

        [SerializeField]
        [Min(0.01f)]
        float _floatDuration = 0.9f;

        [SerializeField]
        [Min(1f)]
        float _rotationDegreesPerSecond = 55f;

        ItemInstance _item;
        Vector3 _visualBasePosition;
        Quaternion _haloBaseRotation;
        Tween _floatTween;
        Tween _rotationTween;
        LocalizationService _localizationService;
        AccessibilityService _accessibilityService;
        bool _hasBaseState;

        public ItemInstance Item => _item;

        public string DisplayText => _label != null ? _label.text : string.Empty;

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.RequireCurrent();
        }

        internal static bool ShouldRunContinuousMotion(MotionProfile profile)
        {
            return profile.AllowContinuousMotion;
        }

        public void Bind(ItemInstance item)
        {
            EnsureComponents();
            CacheBaseState();
            _item = item;
            ItemRarity rarity = item != null ? item.Rarity : ItemRarity.Normal;
            Color rarityColor = GetRarityColor(rarity);

            if (_haloRenderer != null)
            {
                rarityColor.a = 0.82f;
                _haloRenderer.color = rarityColor;
            }

            if (_iconRenderer != null)
            {
                string iconGuid = item?.BaseDefinition?.Icon != null
                    ? item.BaseDefinition.Icon.AssetGUID
                    : string.Empty;
                Sprite icon = ItemVisualPresenter.GetSprite(iconGuid);
                _iconRenderer.sprite = icon != null ? icon : _missingIcon;
                _iconRenderer.color = Color.white;
            }

            BindLocalization();
            RefreshLabel();

            if (isActiveAndEnabled)
            {
                StartAnimations();
            }
        }

        public static Color GetRarityColor(ItemRarity rarity)
        {
            if (rarity == ItemRarity.Magic)
            {
                return new Color32(92, 138, 220, 255);
            }

            if (rarity == ItemRarity.Rare)
            {
                return new Color32(220, 176, 63, 255);
            }

            if (rarity == ItemRarity.Unique)
            {
                return new Color32(211, 105, 48, 255);
            }

            return new Color32(153, 158, 168, 255);
        }

        static string GetRarityKey(ItemRarity rarity)
        {
            if (rarity == ItemRarity.Magic)
            {
                return "item.rarity.magic";
            }

            if (rarity == ItemRarity.Rare)
            {
                return "item.rarity.rare";
            }

            if (rarity == ItemRarity.Unique)
            {
                return "item.rarity.unique";
            }

            return "item.rarity.normal";
        }

        void Awake()
        {
            EnsureComponents();
            CacheBaseState();
        }

        void OnEnable()
        {
            BindLocalization();
            BindAccessibility();
            RefreshLabel();

            if (_item != null)
            {
                StartAnimations();
            }
        }

        void OnDisable()
        {
            if (_localizationService != null)
            {
                _localizationService.LocaleChanged -= OnLocaleChanged;
                _localizationService = null;
            }

            if (_accessibilityService != null)
            {
                _accessibilityService.ProfileChanged -= OnMotionProfileChanged;
                _accessibilityService = null;
            }

            StopAnimations();
            ResetTransforms();
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_visualRoot == null)
            {
                Transform visual = transform.Find("Visual");
                _visualRoot = visual;
            }

            if (_shadowRenderer == null)
            {
                Transform shadow = transform.Find("Shadow");
                _shadowRenderer = shadow != null ? shadow.GetComponent<SpriteRenderer>() : null;
            }

            if (_haloRenderer == null && _visualRoot != null)
            {
                Transform halo = _visualRoot.Find("Halo");
                _haloRenderer = halo != null ? halo.GetComponent<SpriteRenderer>() : null;
            }

            if (_iconRenderer == null && _visualRoot != null)
            {
                Transform icon = _visualRoot.Find("Icon");
                _iconRenderer = icon != null ? icon.GetComponent<SpriteRenderer>() : null;
            }

            if (_label == null && _visualRoot != null)
            {
                _label = _visualRoot.GetComponentInChildren<TextMeshPro>(true);
            }
        }

        void CacheBaseState()
        {
            if (_hasBaseState || _visualRoot == null || _haloRenderer == null)
            {
                return;
            }

            _visualBasePosition = _visualRoot.localPosition;
            _haloBaseRotation = _haloRenderer.transform.localRotation;
            _hasBaseState = true;
        }

        void StartAnimations()
        {
            if (!_hasBaseState || _visualRoot == null || _haloRenderer == null)
            {
                return;
            }

            StopAnimations();
            ResetTransforms();

            MotionProfile profile = _accessibilityService?.Profile ?? MotionProfile.Default;

            if (!ShouldRunContinuousMotion(profile))
            {
                return;
            }

            _floatTween = Tween.LocalPositionY(
                _visualRoot,
                _visualBasePosition.y,
                _visualBasePosition.y + _floatDistance,
                _floatDuration,
                Ease.InOutSine,
                -1,
                CycleMode.Yoyo);
            _rotationTween = Tween.Custom(
                0f,
                -360f,
                360f / _rotationDegreesPerSecond,
                ApplyHaloRotation,
                Ease.Linear,
                -1,
                CycleMode.Restart);
        }

        void StopAnimations()
        {
            if (_floatTween.isAlive)
            {
                _floatTween.Stop();
            }

            if (_rotationTween.isAlive)
            {
                _rotationTween.Stop();
            }
        }

        void ResetTransforms()
        {
            if (!_hasBaseState)
            {
                return;
            }

            if (_visualRoot != null)
            {
                _visualRoot.localPosition = _visualBasePosition;
            }

            if (_haloRenderer != null)
            {
                _haloRenderer.transform.localRotation = _haloBaseRotation;
            }
        }

        void ApplyHaloRotation(float angle)
        {
            if (_haloRenderer == null)
            {
                return;
            }

            _haloRenderer.transform.localRotation = _haloBaseRotation * Quaternion.Euler(0f, 0f, angle);
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

        void BindAccessibility()
        {
            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host)
                || ReferenceEquals(_accessibilityService, host.Accessibility))
            {
                return;
            }

            if (_accessibilityService != null)
            {
                _accessibilityService.ProfileChanged -= OnMotionProfileChanged;
            }

            _accessibilityService = host.Accessibility;

            if (_accessibilityService != null)
            {
                _accessibilityService.ProfileChanged += OnMotionProfileChanged;
            }
        }

        void OnMotionProfileChanged(MotionProfile profile)
        {
            if (!isActiveAndEnabled || _item == null)
            {
                return;
            }

            StartAnimations();
        }

        void RefreshLabel()
        {
            if (_label == null || _item == null)
            {
                return;
            }

            LocalizedMessage name = _item.BaseDefinition?.LocalizedName?.Message ?? default;
            string displayName = Resolve(name);
            ItemRarity rarity = _item.Rarity;
            _label.text = Localize("loot.label", displayName, Localize(GetRarityKey(rarity)));
            if (_item.BaseDefinition.IsStackable) { _label.text += $" ×{_item.Quantity}"; }
            _label.color = Color.Lerp(Color.white, GetRarityColor(rarity), 0.35f);
        }

        void OnLocaleChanged(string localeCode)
        {
            RefreshLabel();
        }

        string Localize(string entryKey, params object[] arguments)
        {
            LocalizedMessage message = LocalizedMessage.Ui(entryKey, arguments);
            return Resolve(message);
        }

        string Resolve(LocalizedMessage message)
        {
            return _localizationService?.GetString(message)
                ?? $"[{message.TableName}.{message.EntryKey}]";
        }
    }
}
