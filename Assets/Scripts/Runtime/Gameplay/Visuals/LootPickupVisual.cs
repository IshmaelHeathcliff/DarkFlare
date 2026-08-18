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
        bool _hasBaseState;

        public ItemInstance Item => _item;

        public string DisplayText => _label != null ? _label.text : string.Empty;

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.RequireCurrent();
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

            if (_label != null)
            {
                string displayName = item?.BaseDefinition != null
                    && !string.IsNullOrWhiteSpace(item.BaseDefinition.DisplayName)
                        ? item.BaseDefinition.DisplayName
                        : "未知物品";
                _label.text = $"{displayName} · {GetRarityText(rarity)}";
                _label.color = Color.Lerp(Color.white, GetRarityColor(rarity), 0.35f);
            }

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

        public static string GetRarityText(ItemRarity rarity)
        {
            if (rarity == ItemRarity.Magic)
            {
                return "魔法";
            }

            if (rarity == ItemRarity.Rare)
            {
                return "稀有";
            }

            if (rarity == ItemRarity.Unique)
            {
                return "独特";
            }

            return "普通";
        }

        void Awake()
        {
            EnsureComponents();
            CacheBaseState();
        }

        void OnEnable()
        {
            if (_item != null)
            {
                StartAnimations();
            }
        }

        void OnDisable()
        {
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
    }
}
