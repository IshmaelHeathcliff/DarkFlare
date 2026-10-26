using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class ProjectileController : MonoBehaviour, IController
    {
        [SerializeField]
        GameObject _impactEffectPrefab;

        Rigidbody2D _rigidbody;
        CircleCollider2D _collider;
        AttackSnapshot _attack;
        IArchitecture _architecture;
        SessionObjectRegistry _sessionObjects;
        Vector2 _direction;
        LifecycleScope _lifetimeScope;
        bool _initialized;
        uint _lifetimeVersion;
        WorldSortParticipant _sortParticipant;
        string _visualIdentity;
        readonly HashSet<CombatActor> _hitTargets = new HashSet<CombatActor>();
        int _remainingTargets;
        float _speed;

        public AttackSnapshot Attack => _attack;

        public IArchitecture GetArchitecture()
        {
            return _architecture ?? GameArchitectureProvider.RequireCurrent();
        }

        public void Init(
            ProjectileSkillDefinition skill,
            Vector2 direction,
            AttackSnapshot attack)
        {
            Prepare(skill, direction, attack);
            Launch();
        }

        internal void Prepare(ProjectileSkillDefinition skill, Vector2 direction, AttackSnapshot attack)
        {
            if (skill == null || attack == null || !skill.HasValidProjectileSettings || !isActiveAndEnabled)
            {
                throw new ArgumentException("投射物技能或攻击快照无效");
            }
            EnsureComponents();
            _attack = attack;
            _initialized = false;
            _collider.enabled = false;
            _rigidbody.linearVelocity = Vector2.zero;
            _hitTargets.Clear();
            _remainingTargets = 1 + skill.PierceCount;
            _speed = skill.ProjectileSpeed;
            _sortParticipant = GetComponent<WorldSortParticipant>();
            if (_sortParticipant == null) { _sortParticipant = gameObject.AddComponent<WorldSortParticipant>(); }
            _visualIdentity = this.GetUtility<VisualEffectPool>().NextIdentity(attack.AttackerId);
            _sortParticipant.ConfigureExplicit(WorldSortCategory.Effect, _visualIdentity, transform);
            this.GetUtility<WorldSortingSystem>().Register(_sortParticipant);
            _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            transform.right = _direction;
            _collider.radius = skill.ProjectileRadius;
            _lifetimeScope?.BeginStop();
            _lifetimeScope = ComponentLifecycle.CreateScope(
                this,
                "projectile-lifetime",
                this.GetCancellationTokenOnDestroy());
            uint version = ++_lifetimeVersion;
            _lifetimeScope.Tasks.Run(
                "destroy-after-lifetime",
                token => DestroyAfterDelayAsync(skill.ProjectileLifetime, version, token),
                failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }

        internal void Launch()
        {
            _initialized = true;
            _collider.enabled = true;
            _rigidbody.linearVelocity = _direction * _speed;
        }

        public bool TryHit(CombatActor target, out DamageResult result)
        {
            result = null;

            if (!_initialized
                || target == null
                || _attack == null
                || target.Team == _attack.AttackerTeam
                || !target.IsAlive
                || !target.isActiveAndEnabled
                || !IsRegistered(target)
                || !_hitTargets.Add(target))
            {
                return false;
            }

            bool exhausted = --_remainingTargets == 0;
            if (exhausted)
            {
                _initialized = false;
                _collider.enabled = false;
            }

            try
            {
                result = this.SendCommand(new ApplyDamageCommand(_attack, target));

                if (CombatEffectPalette.ShouldPlayProjectileImpact(result))
                {
                    this.GetUtility<VisualEffectPool>().TryPlay(
                        _impactEffectPrefab,
                        transform.position,
                        CombatEffectPalette.ArcaneProjectileTint,
                        new WorldEffectContext(this.GetUtility<WorldSortingSystem>(), _visualIdentity + ":impact", transform.position));
                }

                return true;
            }
            finally
            {
                if (exhausted) { _sessionObjects.Release(gameObject); }
            }
        }

        bool IsRegistered(CombatActor target)
        {
            foreach (CombatActor actor in this.GetModel<CombatModel>().GetActorsByTeam(target.Team))
            {
                if (actor == target) { return true; }
            }
            return false;
        }

        void Awake()
        {
            EnsureComponents();
            _architecture = GameArchitectureProvider.RequireCurrent();
            _sessionObjects = this.GetUtility<SessionObjectRegistry>();
            this.GetUtility<VisualEffectPool>().Prewarm(_impactEffectPrefab);
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void OnDisable()
        {
            _sortParticipant?.ReleaseRegistration();
            _initialized = false;
            _lifetimeVersion++;
            _lifetimeScope?.BeginStop();
            _lifetimeScope = null;
        }

        void OnDestroy()
        {
            _sessionObjects?.Unregister(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!_initialized)
            {
                return;
            }

            CombatActor target = other.GetComponentInParent<CombatActor>();
            TryHit(target, out _);
        }

        void EnsureComponents()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
            if (_rigidbody != null)
            {
                _rigidbody.gravityScale = 0f;
                _rigidbody.bodyType = RigidbodyType2D.Dynamic;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (_collider != null)
            {
                _collider.isTrigger = true;
                _collider.radius = 0.16f;
            }
        }

        async UniTask DestroyAfterDelayAsync(
            float lifetime,
            uint version,
            CancellationToken token)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(lifetime), cancellationToken: token);
            token.ThrowIfCancellationRequested();

            if (this != null && version == _lifetimeVersion)
            {
                _sessionObjects.Release(gameObject);
            }
        }
    }
}
