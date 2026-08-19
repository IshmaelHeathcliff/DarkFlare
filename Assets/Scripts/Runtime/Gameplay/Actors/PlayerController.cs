using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(CombatActor))]
    public class PlayerController : MonoBehaviour, IController
    {
        [SerializeField]
        Transform _firePoint;

        [SerializeField]
        [Min(0f)]
        float _respawnDelay = 2f;

        Rigidbody2D _rigidbody;
        CircleCollider2D _collider;
        CombatActor _actor;
        WorldSortParticipant _sortParticipant;
        CharacterDefinition _definition;
        ProjectileSkillDefinition _defaultSkill;
        GameInput _gameInput;
        IArchitecture _architecture;
        SessionObjectRegistry _sessionObjects;
        LifecycleScope _componentScope;
        bool _autoCastRunning;
        bool _respawnScheduled;
        uint _enableVersion;
        Vector2 _lastAimDirection = Vector2.right;
        Vector3 _spawnPosition;
        float _nextAutoCastTime;
        float _respawnTime;

        public CombatActor Actor => _actor;

        public PlayerId Id => PlayerId.LocalPlayer;

        public CharacterDefinition Definition => _definition;

        public ProjectileSkillDefinition DefaultSkill => _defaultSkill;

        public float AutoCastCooldownRemainingSeconds => _autoCastRunning
            ? Mathf.Max(0f, _nextAutoCastTime - Time.time)
            : 0f;

        public float RespawnRemainingSeconds => _respawnScheduled
            ? Mathf.Max(0f, _respawnTime - Time.time)
            : 0f;

        public IArchitecture GetArchitecture()
        {
            return _architecture ?? GameArchitectureProvider.RequireCurrent();
        }

        public void Configure(CharacterDefinition definition, ProjectileSkillDefinition skill)
        {
            ConfigureCore(definition, skill, true);
        }

        public void ConfigureForRestore(
            CharacterDefinition definition,
            ProjectileSkillDefinition skill)
        {
            ConfigureCore(definition, skill, false);
        }

        public void RestoreRuntime(
            CombatResourceSnapshot resources,
            bool isAlive,
            float respawnRemainingSeconds,
            float autoCastCooldownRemainingSeconds)
        {
            if (respawnRemainingSeconds < 0f || autoCastCooldownRemainingSeconds < 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(respawnRemainingSeconds));
            }

            if (isAlive && respawnRemainingSeconds > 0f)
            {
                throw new System.ArgumentException("存活玩家不能带有复活倒计时", nameof(respawnRemainingSeconds));
            }

            _actor.RestoreResources(resources, isAlive);
            TryStartAutoCast(autoCastCooldownRemainingSeconds);

            if (!isAlive)
            {
                StartRespawn(respawnRemainingSeconds);
            }
        }

        void ConfigureCore(
            CharacterDefinition definition,
            ProjectileSkillDefinition skill,
            bool startRuntime)
        {
            _definition = definition;
            _defaultSkill = skill;
            _spawnPosition = transform.position;
            _actor.ConfigureFromCharacter(_definition, ActorTeam.Player);
            _sortParticipant?.ConfigureIdentity(WorldSortCategory.Player, _actor.ActorId);

            if (startRuntime)
            {
                TryStartAutoCast();
            }
        }

        void Awake()
        {
            EnsureComponents();
            _architecture = GameArchitectureProvider.RequireCurrent();
            _gameInput = this.GetUtility<GameInput>();
            _sessionObjects = this.GetUtility<SessionObjectRegistry>();
        }

        void OnEnable()
        {
            _enableVersion++;
            _componentScope = ComponentLifecycle.CreateScope(
                this,
                "enabled",
                this.GetCancellationTokenOnDestroy());
            this.RegisterEvent<ActorDiedEvent>(OnActorDied).UnRegisterWhenDisabled(this);
            TryStartAutoCast();
        }

        void OnDisable()
        {
            _enableVersion++;
            _componentScope?.BeginStop();
            _componentScope = null;
            _autoCastRunning = false;
            _respawnScheduled = false;
            _nextAutoCastTime = 0f;
            _respawnTime = 0f;

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
            if (_actor == null || !_actor.IsAlive)
            {
                if (_rigidbody != null)
                {
                    _rigidbody.linearVelocity = Vector2.zero;
                }

                return;
            }

            Vector2 movement = ReadMovementInput();

            if (movement.sqrMagnitude > 1f)
            {
                movement.Normalize();
            }

            if (movement.sqrMagnitude > 0.001f)
            {
                _lastAimDirection = movement.normalized;
            }

            _rigidbody.linearVelocity = movement * GetMoveSpeed();
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

        Vector2 ReadMovementInput()
        {
            return _gameInput != null ? _gameInput.Move : Vector2.zero;
        }

        float GetMoveSpeed()
        {
            return _actor.Stats.GetValue(StatIds.MoveSpeed);
        }

        void TryStartAutoCast(float initialDelaySeconds = -1f)
        {
            if (_autoCastRunning
                || _defaultSkill == null
                || !isActiveAndEnabled
                || _componentScope == null
                || _componentScope.State != LifecycleScopeState.Active)
            {
                return;
            }

            _autoCastRunning = true;
            uint version = _enableVersion;
            _componentScope.Tasks.Run(
                "auto-cast",
                async token =>
                {
                    try
                    {
                        await AutoCastLoopAsync(initialDelaySeconds, token);
                    }
                    finally
                    {
                        if (version == _enableVersion)
                        {
                            _autoCastRunning = false;
                            _nextAutoCastTime = 0f;
                        }
                    }
                },
                failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }

        async UniTask AutoCastLoopAsync(float initialDelaySeconds, CancellationToken token)
        {
            float delaySeconds = initialDelaySeconds >= 0f
                ? initialDelaySeconds
                : _defaultSkill.Cooldown;

            while (!token.IsCancellationRequested)
            {
                _nextAutoCastTime = Time.time + delaySeconds;
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(delaySeconds),
                    cancellationToken: token);
                token.ThrowIfCancellationRequested();
                TryFireProjectile();
                delaySeconds = _defaultSkill.Cooldown;
            }
        }

        void TryFireProjectile()
        {
            if (_actor == null || !_actor.IsAlive)
            {
                return;
            }

            CombatActor target = this.SendQuery(new GetClosestActorQuery(ActorTeam.Monster, transform.position, _defaultSkill.TargetRange));
            Vector2 direction = _lastAimDirection;

            if (target != null)
            {
                direction = (target.transform.position - transform.position).normalized;
            }

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            Vector3 origin = _firePoint != null ? _firePoint.position : transform.position + (Vector3)direction * 0.55f;
            this.SendCommand(new FireProjectileCommand(_actor, _defaultSkill, origin, direction));
        }

        void OnActorDied(ActorDiedEvent e)
        {
            if (e.Actor != _actor)
            {
                return;
            }

            if (_respawnScheduled || _componentScope == null)
            {
                return;
            }

            StartRespawn(_respawnDelay);
        }

        void StartRespawn(float delaySeconds)
        {
            if (_respawnScheduled || _componentScope == null)
            {
                return;
            }

            _respawnScheduled = true;
            _respawnTime = Time.time + delaySeconds;
            uint version = _enableVersion;
            _componentScope.Tasks.Run(
                "respawn-delay",
                async token =>
                {
                    try
                    {
                        await RespawnAfterDelayAsync(delaySeconds, token);
                    }
                    finally
                    {
                        if (version == _enableVersion)
                        {
                            _respawnScheduled = false;
                            _respawnTime = 0f;
                        }
                    }
                },
                failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }

        async UniTask RespawnAfterDelayAsync(float delaySeconds, CancellationToken token)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delaySeconds),
                cancellationToken: token);
            token.ThrowIfCancellationRequested();
            this.SendCommand(new ReviveActorCommand(_actor, _spawnPosition));
        }
    }
}
