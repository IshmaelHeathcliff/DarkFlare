using PrimeTween;
using TMPro;
using UnityEngine;

namespace DarkFlare
{
    public enum CombatTextKind
    {
        Damage,
        Healing
    }

    [DisallowMultipleComponent]
    public sealed class DamageNumberVisual : MonoBehaviour
    {
        Tween _moveTween;
        Tween _fadeTween;
        TextMeshPro _label;

        public static DamageNumberVisual Spawn(
            Vector3 worldPosition,
            float amount,
            ActorTeam team,
            CombatTextKind kind)
        {
            GameObject instance = new GameObject(kind == CombatTextKind.Healing ? "HealingNumber" : "DamageNumber");
            instance.transform.position = worldPosition + new Vector3(0f, 0.5f, 0f);
            TextMeshPro label = instance.AddComponent<TextMeshPro>();
            label.text = FormatText(amount, kind);
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 3.2f;
            label.fontStyle = FontStyles.Bold;
            label.color = GetColor(team, kind);
            label.outlineColor = new Color(0.05f, 0.06f, 0.08f, 0.92f);
            label.outlineWidth = 0.18f;
            label.sortingOrder = 80;
            DamageNumberVisual visual = instance.AddComponent<DamageNumberVisual>();
            visual._label = label;
            visual.Play();
            return visual;
        }

        public static string FormatText(float amount, CombatTextKind kind)
        {
            string prefix = kind == CombatTextKind.Healing ? "+" : "-";
            return $"{prefix}{Mathf.Abs(amount):0.#}";
        }

        public static Color GetColor(ActorTeam team, CombatTextKind kind)
        {
            if (kind == CombatTextKind.Healing)
            {
                return team == ActorTeam.Player
                    ? new Color(0.39f, 0.94f, 0.51f, 1f)
                    : new Color(0.34f, 0.76f, 0.65f, 1f);
            }

            return team == ActorTeam.Player
                ? new Color(1f, 0.32f, 0.27f, 1f)
                : new Color(1f, 0.79f, 0.39f, 1f);
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
