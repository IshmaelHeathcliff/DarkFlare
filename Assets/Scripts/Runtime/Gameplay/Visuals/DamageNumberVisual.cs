using PrimeTween;
using TMPro;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    public sealed class DamageNumberVisual : MonoBehaviour
    {
        Tween _moveTween;
        Tween _fadeTween;
        TextMeshPro _label;

        public static DamageNumberVisual Spawn(Vector3 worldPosition, float damage)
        {
            GameObject instance = new GameObject("DamageNumber");
            instance.transform.position = worldPosition + new Vector3(0f, 0.5f, 0f);
            TextMeshPro label = instance.AddComponent<TextMeshPro>();
            label.text = damage.ToString("0.#");
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 3.2f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(1f, 0.83f, 0.58f, 1f);
            label.sortingOrder = 80;
            DamageNumberVisual visual = instance.AddComponent<DamageNumberVisual>();
            visual._label = label;
            visual.Play();
            return visual;
        }

        void OnDisable()
        {
            StopTweens();
        }

        void Play()
        {
            Vector3 start = transform.position;
            _moveTween = Tween.PositionY(transform, start.y, start.y + 0.65f, 0.65f, Ease.OutCubic);
            Color startColor = _label.color;
            Color endColor = startColor;
            endColor.a = 0f;
            _fadeTween = Tween.Custom(
                    _label,
                    startColor,
                    endColor,
                    0.65f,
                    (label, color) => label.color = color,
                    Ease.InQuad)
                .OnComplete(this, visual =>
                {
                    if (visual != null)
                    {
                        Destroy(visual.gameObject);
                    }
                });
        }

        void StopTweens()
        {
            if (_moveTween.isAlive)
            {
                _moveTween.Stop();
            }

            if (_fadeTween.isAlive)
            {
                _fadeTween.Stop();
            }
        }
    }
}
