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
        GameInput _gameInput;
        IArchitecture _architecture;
        SessionObjectRegistry _sessionObjects;
        LifecycleScope _componentScope;
        bool _respawnScheduled;
        uint _enableVersion;
        Vector2 _lastAimDirection = Vector2.right;
        Vector3 _spawnPosition;
        float _respawnTime;

        public CombatActor Actor => _actor;

        public PlayerId Id => PlayerId.LocalPlayer;

        public CharacterDefinition Definition => _definition;

        public ProjectileSkillDefinition DefaultSkill => this.SendQuery(new GetPlayerNormalSkillQuery(_actor)).Skill;

        public float NormalSkillCooldownRemainingSeconds => this.SendQuery(new GetPlayerNormalSkillQuery(_actor)).RemainingSeconds;

        public float RespawnRemainingSeconds => _respawnScheduled
            ? Mathf.Max(0f, _respawnTime - Time.time)
            : 0f;

        public IArchitecture GetArchitecture()
        {
            return _architecture ?? GameArchitectureProvider.RequireCurrent();
        }

        public void Configure(CharacterDefinition definition, ProjectileSkillDefinition skill)
        {
            ConfigureCore(definition);
        }

        public void ConfigureForRestore(
            CharacterDefinition definition,
            ProjectileSkillDefinition skill)
        {
            ConfigureCore(definition);
        }

        public void RestoreRuntime(
            CombatResourceSnapshot resources,
            bool isAlive,
            float respawnRemainingSeconds,
            float normalCooldownRemainingSeconds)
        {
            if (!PlayerSkillStateRegistry.FiniteNonnegative(respawnRemainingSeconds)
                || !PlayerSkillStateRegistry.FiniteNonnegative(normalCooldownRemainingSeconds))
            {
                throw new System.ArgumentOutOfRangeException(nameof(respawnRemainingSeconds));
            }

            if (isAlive && respawnRemainingSeconds > 0f)
            {
                throw new System.ArgumentException("存活玩家不能带有复活倒计时", nameof(respawnRemainingSeconds));
            }

            _actor.RestoreResources(resources, isAlive);
            this.SendCommand(new RestoreNormalSkillCooldownCommand(normalCooldownRemainingSeconds));

            if (!isAlive)
            {
                StartRespawn(respawnRemainingSeconds);
            }
        }

        void ConfigureCore(CharacterDefinition definition)
        {
            _definition = definition;
            _spawnPosition = transform.position;
            _actor.ConfigureFromCharacter(_definition, ActorTeam.Player);
            this.SendCommand(new ConfigurePlayerGrowthCommand(_actor, _definition.Growth));
            this.SendCommand(new BindActorStatusesCommand(_actor));
            _sortParticipant?.ConfigureIdentity(WorldSortCategory.Player, _actor.ActorId);
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
            if (_gameInput != null) { _gameInput.SkillRequested += OnSkillRequested; }
        }

        void OnDisable()
        {
            if (_gameInput != null) { _gameInput.SkillRequested -= OnSkillRequested; }
            _enableVersion++;
            _componentScope?.BeginStop();
            _componentScope = null;
            _respawnScheduled = false;
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
            if (!this.SendQuery(new GetActorActionsQuery(_actor)).CanMove)
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

        void OnSkillRequested(PlayerSkillInput request)
        {
            if (_componentScope == null || _actor == null || !_actor.IsAlive) { return; }
            PlayerSkillState source = request.Slot < 0 ? this.SendQuery(new GetPlayerNormalSkillQuery(_actor))
                : this.SendQuery(new GetPlayerSkillsQuery())[request.Slot];
            if (source.Skill == null) { return; }
            Vector2 direction = request.Look;
            if (request.Family == InputDeviceFamily.KeyboardMouse && Camera.main != null)
            {
                Camera camera = Camera.main;
                Vector3 point = camera.ScreenToWorldPoint(new Vector3(request.Pointer.x, request.Pointer.y,
                    transform.position.z - camera.transform.position.z));
                direction = point - transform.position;
            }
            if (direction.sqrMagnitude > 0.01f) { _lastAimDirection = direction.normalized; }
            direction = _lastAimDirection;
            Vector3 origin = _firePoint != null ? _firePoint.position : transform.position + (Vector3)direction * 0.55f;
            uint version = _enableVersion;
            _componentScope.Tasks.Run("manual-cast", async token =>
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
                if (version != _enableVersion || !isActiveAndEnabled || !_gameInput.IsCurrent(request)) { return; }
                this.SendCommand(new CastPlayerSkillCommand(_actor, request.Slot, source.SourceId, origin, direction));
            }, failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
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

            _enableVersion++;
            _gameInput?.InvalidateSkills();
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
            WorldMapRuntime maps = this.GetUtility<WorldMapRuntime>();
            this.SendCommand(new ReviveActorCommand(_actor, maps.IsConfigured ? maps.RespawnPosition : _spawnPosition));
        }
    }
}
