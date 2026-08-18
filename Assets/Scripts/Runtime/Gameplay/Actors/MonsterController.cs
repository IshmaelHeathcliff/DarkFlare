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
        MonsterAffixVisual _affixVisual;
        WorldSortParticipant _sortParticipant;
        MonsterDefinition _definition;
        MonsterInstanceData _instance;
        IArchitecture _architecture;
        SessionObjectRegistry _sessionObjects;
        LifecycleScope _componentScope;
        bool _despawnScheduled;
        float _lastContactDamageTime = -999f;

        [SerializeField]
        [Min(0f)]
        float _deathDespawnDelay = 0.65f;

        public CombatActor Actor => _actor;

        public MonsterDefinition Definition => _definition;

        public MonsterInstanceData Instance => _instance;

        public IArchitecture GetArchitecture()
        {
            return _architecture ?? GameArchitectureProvider.RequireCurrent();
        }

        public void Configure(MonsterDefinition definition, MonsterInstanceData instance)
        {
            _definition = definition;
            _instance = instance;
            ApplyDefinition();
            _sortParticipant?.ConfigureIdentity(
                WorldSortCategory.Monster,
                _instance.Id.Value);
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            if (_architecture == null)
            {
                _architecture = GameArchitectureProvider.RequireCurrent();
                _sessionObjects = this.GetUtility<SessionObjectRegistry>();
            }

            _componentScope = ComponentLifecycle.CreateScope(
                this,
                "enabled",
                this.GetCancellationTokenOnDestroy());
            _despawnScheduled = false;
            this.RegisterEvent<ActorDiedEvent>(OnActorDied).UnRegisterWhenDisabled(this);
        }

        void OnDisable()
        {
            _componentScope?.BeginStop();
            _componentScope = null;
            _despawnScheduled = false;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }
        }

        void OnDestroy()
        {
            _sessionObjects?.Unregister(gameObject);
        }

        void FixedUpdate()
        {
            if (_actor == null || !_actor.IsAlive || _definition == null || _instance == null)
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
            Vector2 direction = GetMoveDirection(target, offset);
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
            _affixVisual = GetComponent<MonsterAffixVisual>();
            _sortParticipant = GetComponent<WorldSortParticipant>();

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
            _actor.Configure(
                _definition.Id,
                ActorTeam.Monster,
                _instance.BaseMaxHealth,
                _instance.BaseStats,
                _definition.RuntimeTags);
            _actor.SetModifiers(_instance.Modifiers);

            if (_affixVisual != null)
            {
                _affixVisual.Configure(_instance);
            }
        }

        float GetMoveSpeed()
        {
            return _actor.Stats.GetValue(StatIds.MoveSpeed);
        }

        Vector2 GetMoveDirection(CombatActor target, Vector2 targetOffset)
        {
            Vector2 pursuit = MonsterSteeringCalculator.GetPursuitDirection(
                targetOffset,
                _definition.ContactStopDistance);
            Vector2 separation = Vector2.zero;
            IReadOnlyList<CombatActor> monsters = this.SendQuery(new GetActorsByTeamQuery(ActorTeam.Monster));
            Vector2 selfPosition = transform.position;

            for (int i = 0; i < monsters.Count; i++)
            {
                CombatActor neighbor = monsters[i];

                if (neighbor == null || neighbor == _actor || !neighbor.IsAlive || neighbor == target)
                {
                    continue;
                }

                separation += MonsterSteeringCalculator.GetSeparationContribution(
                    selfPosition,
                    neighbor.transform.position,
                    _definition.SeparationRadius,
                    _instance.Seed);
            }

            return MonsterSteeringCalculator.Combine(
                pursuit,
                separation,
                _definition.SeparationWeight);
        }

        void TryDealContactDamage(CombatActor target, float distance)
        {
            if (distance > _definition.ContactDamageRadius || Time.time < _lastContactDamageTime + _definition.ContactDamageInterval)
            {
                return;
            }

            if (_definition.ContactDamages.Count == 0)
            {
                Debug.LogError($"[MonsterController] {_definition.Id} 没有可用的碰撞伤害配置", _definition);
                return;
            }

            _lastContactDamageTime = Time.time;
            int seed = this.GetSystem<GameplayRandomSystem>().NextSeed(GameplayRandomChannel.MonsterAttack);
            AttackRandomRolls rolls = AttackRandomRolls.FromRootSeed(seed);
            List<DamagePacket> packets = _definition.CreateContactDamagePackets(rolls.BaseDamageSeed);
            this.SendCommand(new NotifyActorAttackCommand(_actor));
            this.SendCommand(new ApplyDamageCommand(_actor, target, "monster_contact", packets, seed));
        }

        void OnActorDied(ActorDiedEvent e)
        {
            if (e.Actor != _actor)
            {
                return;
            }

            if (_despawnScheduled || _componentScope == null)
            {
                return;
            }

            _despawnScheduled = true;
            _componentScope.Tasks.Run(
                "death-despawn",
                DespawnAfterDeathAnimationAsync,
                failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }

        async UniTask DespawnAfterDeathAnimationAsync(CancellationToken token)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(_deathDespawnDelay),
                cancellationToken: token);
            token.ThrowIfCancellationRequested();
            Destroy(gameObject);
        }
    }
}
