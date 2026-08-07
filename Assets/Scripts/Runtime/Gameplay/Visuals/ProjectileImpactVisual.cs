using PrimeTween;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    public sealed class ProjectileImpactVisual : MonoBehaviour
    {
        Tween _scaleTween;
        Tween _fadeTween;
        SpriteRenderer _renderer;

        public static void Spawn(Vector3 position, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            GameObject instance = new GameObject("ProjectileImpact");
            instance.transform.position = position;
            instance.transform.localScale = Vector3.one * 0.35f;
            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.35f, 0.95f, 1f, 0.9f);
            renderer.sortingOrder = 40;
            ProjectileImpactVisual visual = instance.AddComponent<ProjectileImpactVisual>();
            visual._renderer = renderer;
            visual.Play();
        }

        void OnDisable()
        {
            StopTweens();
        }

        void Play()
        {
            _scaleTween = Tween.Scale(transform, Vector3.one * 0.35f, Vector3.one * 1.1f, 0.25f, Ease.OutCubic);
            _fadeTween = Tween.Alpha(_renderer, 0.9f, 0f, 0.25f, Ease.InQuad)
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
            if (_scaleTween.isAlive)
            {
                _scaleTween.Stop();
            }

            if (_fadeTween.isAlive)
            {
                _fadeTween.Stop();
            }
        }
    }
}
