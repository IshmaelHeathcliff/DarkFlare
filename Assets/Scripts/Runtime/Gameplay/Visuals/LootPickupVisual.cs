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
        TextMeshPro _label;

        [SerializeField]
        [Min(0f)]
        float _floatDistance = 0.1f;

        [SerializeField]
        [Min(0.01f)]
        float _floatDuration = 0.9f;

        [SerializeField]
        [Min(0f)]
        float _pulseAmount = 0.08f;

        ItemInstance _item;
        Vector3 _visualBasePosition;
        Vector3 _haloBaseScale;
        Tween _floatTween;
        Tween _pulseTween;
        bool _hasBaseState;

        public ItemInstance Item => _item;

        public string DisplayText => _label != null ? _label.text : string.Empty;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
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
                rarityColor.a = 0.55f;
                _haloRenderer.color = rarityColor;
            }

            if (_iconRenderer != null)
            {
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
                return new Color(1f, 0.88f, 0.25f, 1f);
            }

            if (rarity == ItemRarity.Rare)
            {
                return new Color(1f, 0.45f, 0.1f, 1f);
            }

            if (rarity == ItemRarity.Unique)
            {
                return new Color(0.85f, 0.35f, 1f, 1f);
            }

            return new Color(0.82f, 0.88f, 0.94f, 1f);
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
            _haloBaseScale = _haloRenderer.transform.localScale;
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
            Vector3 pulseOffset = Vector3.one * _pulseAmount;
            _pulseTween = Tween.Scale(
                _haloRenderer.transform,
                _haloBaseScale - pulseOffset,
                _haloBaseScale + pulseOffset,
                _floatDuration,
                Ease.InOutSine,
                -1,
                CycleMode.Yoyo);
        }

        void StopAnimations()
        {
            if (_floatTween.isAlive)
            {
                _floatTween.Stop();
            }

            if (_pulseTween.isAlive)
            {
                _pulseTween.Stop();
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
                _haloRenderer.transform.localScale = _haloBaseScale;
            }
        }
    }
}
