using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(CombatActor))]
    public class MonsterController : MonoBehaviour, IController
    {
        Rigidbody2D _rigidbody;
        CircleCollider2D _collider;
        CombatActor _actor;
        MonsterDefinition _definition;
        MonsterInstanceData _instance;
        float _lastContactDamageTime = -999f;

        [SerializeField]
        [Min(0f)]
        float _deathDespawnDelay = 0.65f;

        public CombatActor Actor => _actor;

        public MonsterDefinition Definition => _definition;

        public MonsterInstanceData Instance => _instance;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void Configure(MonsterDefinition definition, MonsterInstanceData instance)
        {
            _definition = definition;
            _instance = instance;
            ApplyDefinition();
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            this.RegisterEvent<ActorDiedEvent>(OnActorDied).UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        void FixedUpdate()
        {
            if (_actor == null || !_actor.IsAlive)
            {
                return;
            }

            CombatActor target = this.SendQuery(new GetClosestActorQuery(ActorTeam.Player, transform.position, 1000f));

            if (target == null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 offset = target.transform.position - transform.position;
            Vector2 direction = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector2.zero;
            _rigidbody.linearVelocity = direction * GetMoveSpeed();
            TryDealContactDamage(target, offset.magnitude);
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
            _actor = GetComponent<CombatActor>();

            if (_rigidbody != null)
            {
                _rigidbody.gravityScale = 0f;
                _rigidbody.freezeRotation = true;
            }

            if (_collider != null)
            {
                _collider.radius = 0.4f;
            }
        }

        void ApplyDefinition()
        {
            _actor.Configure(_definition.Id, ActorTeam.Monster, _instance.MaxHealth, _instance.Stats, _definition.RuntimeTags);
        }

        float GetMoveSpeed()
        {
            return _actor.Stats.GetValue(StatIds.MoveSpeed);
        }

        void TryDealContactDamage(CombatActor target, float distance)
        {
            if (distance > _definition.ContactDamageRadius || Time.time < _lastContactDamageTime + _definition.ContactDamageInterval)
            {
                return;
            }

            _lastContactDamageTime = Time.time;
            int seed = this.GetSystem<GameplayRandomSystem>().NextSeed(GameplayRandomChannel.MonsterAttack);
            List<DamagePacket> packets = _definition.CreateContactDamagePackets(seed);
            this.SendCommand(new NotifyActorAttackCommand(_actor));
            this.SendCommand(new ApplyDamageCommand(_actor, target, "monster_contact", packets, seed));
        }

        void OnActorDied(ActorDiedEvent e)
        {
            if (e.Actor != _actor)
            {
                return;
            }

            DespawnAfterDeathAnimation(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid DespawnAfterDeathAnimation(CancellationToken token)
        {
            bool cancelled = await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_deathDespawnDelay),
                    cancellationToken: token)
                .SuppressCancellationThrow();

            if (!cancelled)
            {
                Destroy(gameObject);
            }
        }
    }
}
