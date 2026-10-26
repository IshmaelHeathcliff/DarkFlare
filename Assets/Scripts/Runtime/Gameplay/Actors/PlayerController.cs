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
        readonly SkillIntent[] _skillIntents = new SkillIntent[7];
        readonly GetPlayerSkillQuery[] _skillQueries = new GetPlayerSkillQuery[7];
        GetActorActionsQuery _actionsQuery;
        bool _castingHeldSkills;

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
            for (int i = 0; i < _skillQueries.Length; i++) { _skillQueries[i] = new GetPlayerSkillQuery(_actor, i - 1); }
            _actionsQuery = new GetActorActionsQuery(_actor);
        }

        void OnEnable()
        {
            _enableVersion++;
            _componentScope = ComponentLifecycle.CreateScope(
                this,
                "enabled",
                this.GetCancellationTokenOnDestroy());
            this.RegisterEvent<ActorDiedEvent>(OnActorDied).UnRegisterWhenDisabled(this);
            this.RegisterEvent<EquipmentChangedEvent>(e => { if (e.Actor == _actor) { InvalidateChangedSkillSources(); } }).UnRegisterWhenDisabled(this);
            this.RegisterEvent<PlayerSkillsChangedEvent>(_ => InvalidateChangedSkillSources()).UnRegisterWhenDisabled(this);
            this.RegisterEvent<SkillBoardChangedEvent>(_ => InvalidateChangedSkillSources()).UnRegisterWhenDisabled(this);
            this.RegisterEvent<GameplayPauseChangedEvent>(e => { if (e.IsPaused) { CancelHeldSkills(); } }).UnRegisterWhenDisabled(this);
            if (_gameInput != null) { _gameInput.SkillRequested += OnSkillRequested; }
        }

        void OnDisable()
        {
            if (_gameInput != null) { _gameInput.SkillRequested -= OnSkillRequested; }
            _enableVersion++;
            System.Array.Clear(_skillIntents, 0, _skillIntents.Length);
            _castingHeldSkills = false;
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
            if (_componentScope == null || !_componentScope.CanAcceptWork || _actor == null || !_actor.IsAlive
                || request.Slot < -1 || request.Slot > 5) { return; }
            PlayerSkillState source = this.SendQuery(_skillQueries[request.Slot + 1]);
            if (source.Skill == null) { return; }
            _skillIntents[request.Slot + 1] = new SkillIntent(request, source.SourceId, _sessionObjects.MapOwner);
            if (_castingHeldSkills) { return; }
            _castingHeldSkills = true;
            uint version = _enableVersion;
            _componentScope.Tasks.Run("held-skills", token => CastWhileHeldAsync(version, token),
                failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }

        async UniTask CastWhileHeldAsync(uint version, CancellationToken token)
        {
            try
            {
                do
                {
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
                    if (version != _enableVersion || !isActiveAndEnabled || _actor == null || !_actor.IsAlive) { return; }
                    for (int i = 0; i < _skillIntents.Length; i++)
                    {
                        SkillIntent intent = _skillIntents[i];
                        if (intent == null) { continue; }
                        bool first = intent.FirstPending;
                        intent.FirstPending = false;
                        PlayerSkillState source = this.SendQuery(_skillQueries[i]);
                        if (!_gameInput.TryReadSkillInput(intent.Input, !first, out PlayerSkillInput current)
                            || !ReferenceEquals(intent.MapOwner, _sessionObjects.MapOwner)
                            || source.Skill == null || source.SourceId != intent.SourceId)
                        {
                            _skillIntents[i] = null;
                            continue;
                        }
                        if (source.RemainingSeconds > 0 || !_actor.CanSpendMana(source.Skill.ManaCost))
                        {
                            continue;
                        }
                        ActorActionPermission permission = this.SendQuery(_actionsQuery);
                        if (!permission.CanCast || source.Skill.DamageSource == ProjectileDamageSource.EquippedWeapon && !permission.CanAttack)
                        {
                            continue;
                        }
                        Vector2 direction = ReadAim(current);
                        Vector3 origin = _firePoint != null ? _firePoint.position : transform.position + (Vector3)direction * .55f;
                        SkillCastResult result = this.SendCommand(new CastPlayerSkillCommand(_actor, current.Slot, intent.SourceId, origin, direction));
                        if (result.Status == SkillCastStatus.InvalidSource || result.Status == SkillCastStatus.InvalidOwner
                            || result.Status == SkillCastStatus.InvalidDamageSource || result.Status == SkillCastStatus.SpawnFailed)
                        {
                            _skillIntents[i] = null;
                        }
                    }
                }
                while (HasSkillIntents());
            }
            finally
            {
                if (version == _enableVersion)
                {
                    _castingHeldSkills = false;
                    System.Array.Clear(_skillIntents, 0, _skillIntents.Length);
                }
            }
        }

        bool HasSkillIntents()
        {
            foreach (SkillIntent intent in _skillIntents) { if (intent != null) { return true; } }
            return false;
        }

        void InvalidateChangedSkillSources()
        {
            for (int i = 0; i < _skillIntents.Length; i++)
            {
                if (_skillIntents[i] != null && this.SendQuery(_skillQueries[i]).SourceId != _skillIntents[i].SourceId)
                {
                    _skillIntents[i] = null;
                }
            }
        }

        void CancelHeldSkills()
        {
            System.Array.Clear(_skillIntents, 0, _skillIntents.Length);
            _gameInput?.InvalidateSkills();
        }

        Vector2 ReadAim(PlayerSkillInput request)
        {
            Vector2 direction = request.Look;
            Camera camera = Camera.main;
            if (request.Family == InputDeviceFamily.KeyboardMouse && camera != null)
            {
                Vector3 point = camera.ScreenToWorldPoint(new Vector3(request.Pointer.x, request.Pointer.y,
                    transform.position.z - camera.transform.position.z));
                direction = point - transform.position;
            }
            if (direction.sqrMagnitude > 0.01f) { _lastAimDirection = direction.normalized; }
            return _lastAimDirection;
        }

        sealed class SkillIntent
        {
            public PlayerSkillInput Input { get; }
            public string SourceId { get; }
            public object MapOwner { get; }
            public bool FirstPending { get; set; } = true;
            public SkillIntent(PlayerSkillInput input, string sourceId, object mapOwner)
            {
                Input = input;
                SourceId = sourceId;
                MapOwner = mapOwner;
            }
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
            System.Array.Clear(_skillIntents, 0, _skillIntents.Length);
            _castingHeldSkills = false;
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
