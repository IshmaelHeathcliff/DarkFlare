using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatActor))]
    public sealed class ActorVisualFeedbackController : MonoBehaviour, IController
    {
        [SerializeField]
        CombatActor _actor;

        [SerializeField]
        SpriteRenderer _renderer;

        [SerializeField]
        MonsterHealthBarVisual _monsterHealthBar;

        readonly List<IUnRegister> _registrations = new List<IUnRegister>();

        Tween _flashTween;
        Tween _deathTween;
        Color _baseColor = Color.white;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        void Awake()
        {
            EnsureComponents();

            if (_renderer != null)
            {
                _baseColor = _renderer.color;
            }
        }

        void OnEnable()
        {
            RegisterEvents();
            RestoreVisuals();
        }

        void OnDisable()
        {
            UnregisterEvents();
            StopTweens();
            RestoreVisuals();
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_actor == null)
            {
                _actor = GetComponent<CombatActor>();
            }

            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (_monsterHealthBar == null)
            {
                _monsterHealthBar = GetComponent<MonsterHealthBarVisual>();
            }
        }

        void RegisterEvents()
        {
            if (_registrations.Count > 0)
            {
                return;
            }

            _registrations.Add(this.RegisterEvent<ActorDamagedEvent>(OnActorDamaged));
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

        void OnActorDamaged(ActorDamagedEvent e)
        {
            if (e.Actor != _actor || e.Result == null || !e.Result.IsHit)
            {
                return;
            }

            Flash();
            DamageNumberVisual.Spawn(transform.position, e.Result.TotalDamage);

            if (_monsterHealthBar != null)
            {
                _monsterHealthBar.Refresh(_actor);
            }
        }

        void OnActorDied(ActorDiedEvent e)
        {
            if (e.Actor != _actor)
            {
                return;
            }

            if (_monsterHealthBar != null)
            {
                _monsterHealthBar.Hide();
            }

            if (_renderer != null)
            {
                StopTween(ref _deathTween);
                _deathTween = Tween.Alpha(_renderer, _renderer.color.a, 0f, 0.25f, Ease.OutQuad, startDelay: 0.38f);
            }
        }

        void OnActorRevived(ActorRevivedEvent e)
        {
            if (e.Actor != _actor)
            {
                return;
            }

            StopTweens();
            RestoreVisuals();
        }

        void Flash()
        {
            if (_renderer == null)
            {
                return;
            }

            StopTween(ref _flashTween);
            Color current = _renderer.color;
            Color flash = Color.white;
            flash.a = current.a;
            _flashTween = Tween.Color(_renderer, current, flash, 0.04f, Ease.OutQuad)
                .OnComplete(this, controller =>
                {
                    if (controller == null || controller._renderer == null)
                    {
                        return;
                    }

                    Color target = controller._baseColor;
                    target.a = controller._renderer.color.a;
                    controller._flashTween = Tween.Color(
                        controller._renderer,
                        controller._renderer.color,
                        target,
                        0.04f,
                        Ease.InQuad);
                });
        }

        void StopTweens()
        {
            StopTween(ref _flashTween);
            StopTween(ref _deathTween);
        }

        static void StopTween(ref Tween tween)
        {
            if (tween.isAlive)
            {
                tween.Stop();
            }
        }

        void RestoreVisuals()
        {
            if (_renderer != null)
            {
                _renderer.color = _baseColor;
            }

            if (_monsterHealthBar != null)
            {
                _monsterHealthBar.Hide();
            }
        }
    }
}
