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

        [SerializeField]
        Transform _hitEffectAnchor;

        [SerializeField]
        GameObject _defaultHitEffectPrefab;

        [SerializeField]
        GameObject _criticalHitEffectPrefab;

        readonly List<IUnRegister> _registrations = new List<IUnRegister>();

        Tween _deathTween;
        Color _baseColor = Color.white;

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.RequireCurrent();
        }

        void Awake()
        {
            EnsureComponents();

            if (_renderer != null)
            {
                _baseColor = _renderer.color;
            }

            VisualEffectPool effectPool = this.GetUtility<VisualEffectPool>();
            effectPool.Prewarm(_defaultHitEffectPrefab);
            effectPool.Prewarm(_criticalHitEffectPrefab);
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

            if (_hitEffectAnchor == null)
            {
                _hitEffectAnchor = transform.Find("HitEffectAnchor");
            }
        }

        void RegisterEvents()
        {
            if (_registrations.Count > 0)
            {
                return;
            }

            _registrations.Add(this.RegisterEvent<ActorDamagedEvent>(OnActorDamaged));
            _registrations.Add(this.RegisterEvent<DamageResolvedEvent>(OnDamageResolved));
            _registrations.Add(this.RegisterEvent<ActorHealedEvent>(OnActorHealed));
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
            if (e.Actor != _actor || e.Result == null || !e.Result.DidDealDamage)
            {
                return;
            }

            PlayHitEffect(e.Result);
            DamageNumberVisual.Spawn(
                transform.position,
                e.Result.TotalDamage,
                _actor.Team,
                e.Result.IsCritical ? CombatTextKind.CriticalDamage : CombatTextKind.Damage);

            if (_monsterHealthBar != null)
            {
                _monsterHealthBar.Refresh(_actor);
            }
        }

        void OnDamageResolved(DamageResolvedEvent e)
        {
            if (e.Actor != _actor || e.Result == null || e.Result.Form != DamageForm.Hit || e.Result.DidDealDamage)
            {
                return;
            }

            CombatTextKind kind;

            switch (e.Result.Outcome)
            {
                case HitOutcome.Missed:
                    kind = CombatTextKind.Missed;
                    break;
                case HitOutcome.Evaded:
                    kind = CombatTextKind.Evaded;
                    break;
                case HitOutcome.NoDamage:
                    kind = CombatTextKind.NoDamage;
                    break;
                default:
                    return;
            }

            DamageNumberVisual.Spawn(transform.position, 0f, _actor.Team, kind);
        }

        void OnActorHealed(ActorHealedEvent e)
        {
            if (e.Actor != _actor || e.Amount <= 0f)
            {
                return;
            }

            DamageNumberVisual.Spawn(
                transform.position,
                e.Amount,
                _actor.Team,
                CombatTextKind.Healing);

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

        void PlayHitEffect(DamageResult result)
        {
            if (!CombatEffectPalette.ShouldPlayActorHit(result))
            {
                return;
            }

            GameObject prefab = result.IsCritical ? _criticalHitEffectPrefab : _defaultHitEffectPrefab;
            Vector3 position = _hitEffectAnchor != null ? _hitEffectAnchor.position : transform.position;
            this.GetUtility<VisualEffectPool>().TryPlay(
                prefab,
                position,
                CombatEffectPalette.GetHitTint(result));
        }

        void StopTweens()
        {
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
