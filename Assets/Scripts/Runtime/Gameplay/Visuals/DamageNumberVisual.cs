using PrimeTween;
using TMPro;
using UnityEngine;

namespace DarkFlare
{
    public enum CombatTextKind
    {
        Damage,
        CriticalDamage,
        Healing,
        Missed,
        Evaded,
        NoDamage
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
            GameObject instance = new GameObject(GetObjectName(kind));
            instance.transform.position = worldPosition + new Vector3(0f, 0.5f, 0f);
            TextMeshPro label = instance.AddComponent<TextMeshPro>();
            label.text = FormatText(amount, kind);
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = GetFontSize(kind);
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
            if (kind == CombatTextKind.Missed)
            {
                return "未命中";
            }

            if (kind == CombatTextKind.Evaded)
            {
                return "闪避";
            }

            if (kind == CombatTextKind.NoDamage)
            {
                return "无伤害";
            }

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

            if (kind == CombatTextKind.Missed || kind == CombatTextKind.Evaded || kind == CombatTextKind.NoDamage)
            {
                return team == ActorTeam.Player
                    ? new Color(0.72f, 0.82f, 0.94f, 1f)
                    : new Color(0.84f, 0.8f, 0.69f, 1f);
            }

            if (kind == CombatTextKind.CriticalDamage)
            {
                return team == ActorTeam.Player
                    ? new Color(1f, 0.5f, 0.32f, 1f)
                    : new Color(1f, 0.9f, 0.36f, 1f);
            }

            return team == ActorTeam.Player
                ? new Color(1f, 0.32f, 0.27f, 1f)
                : new Color(1f, 0.79f, 0.39f, 1f);
        }

        static string GetObjectName(CombatTextKind kind)
        {
            return kind switch
            {
                CombatTextKind.Healing => "HealingNumber",
                CombatTextKind.CriticalDamage => "CriticalDamageNumber",
                CombatTextKind.Missed => "MissedText",
                CombatTextKind.Evaded => "EvadedText",
                CombatTextKind.NoDamage => "NoDamageText",
                _ => "DamageNumber",
            };
        }

        static float GetFontSize(CombatTextKind kind)
        {
            if (kind == CombatTextKind.CriticalDamage)
            {
                return 3.8f;
            }

            return kind == CombatTextKind.Damage || kind == CombatTextKind.Healing ? 3.2f : 2.6f;
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
