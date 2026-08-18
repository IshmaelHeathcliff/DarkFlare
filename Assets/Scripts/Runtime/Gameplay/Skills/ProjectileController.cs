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

        public IArchitecture GetArchitecture()
        {
            return _architecture ?? GameArchitectureProvider.RequireCurrent();
        }

        public void Init(
            ProjectileSkillDefinition skill,
            Vector2 direction,
            AttackSnapshot attack)
        {
            EnsureComponents();
            _attack = attack;
            _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            _initialized = true;
            transform.right = _direction;
            _collider.radius = skill.ProjectileRadius;
            _rigidbody.linearVelocity = _direction * skill.ProjectileSpeed;
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

        public bool TryHit(CombatActor target, out DamageResult result)
        {
            result = null;

            if (!_initialized
                || target == null
                || _attack == null
                || target.Team == _attack.AttackerTeam
                || !target.IsAlive)
            {
                return false;
            }

            result = this.SendCommand(new ApplyDamageCommand(_attack, target));
            _initialized = false;

            if (CombatEffectPalette.ShouldPlayProjectileImpact(result))
            {
                this.GetUtility<VisualEffectPool>().TryPlay(
                    _impactEffectPrefab,
                    transform.position,
                    CombatEffectPalette.ArcaneProjectileTint);
            }

            Destroy(gameObject);
            return true;
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
                Destroy(gameObject);
            }
        }
    }
}
