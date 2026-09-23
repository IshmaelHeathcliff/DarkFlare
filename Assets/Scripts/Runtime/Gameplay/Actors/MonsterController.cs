using System.Linq;
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
        MonsterBehaviorStateDto _behaviorState;
        BossBehaviorStateDto _bossState;
        float _frozenCooldown;
        internal bool NavigationOverride { get; set; }
        public MonsterBehaviorRuntime BehaviorRuntime { get; internal set; }
        public BossBehaviorRuntime BossRuntime { get; internal set; }
        public BehaviorHandle BehaviorHandle => BossRuntime?.Handle ?? BehaviorRuntime?.Handle ?? default;

        public BossBehaviorStateDto CaptureBoss()
        {
            if (_definition?.BossBehavior == null) { return null; }
            return BossRuntime?.CaptureState() ?? (_bossState ??= new BossBehaviorStateDto()).Copy();
        }

        internal void RestoreBoss(BossBehaviorStateDto state)
        {
            if (_definition?.BossBehavior == null || state == null || !state.IsValid) { throw new System.ArgumentException("Boss 行为状态非法"); }
            _bossState = state.Copy(); BossRuntime?.Restore(state);
        }

        public MonsterBehaviorStateDto CaptureBehavior()
        {
            if (BehaviorRuntime != null) { return BehaviorRuntime.CaptureState(); }
            return (_behaviorState ??= MonsterBehaviorStateDto.Create(new Vector3Dto
                { X = transform.position.x, Y = transform.position.y, Z = transform.position.z }, _instance.RandomSeeds.RootSeed)).Copy();
        }
        internal void RestoreBehavior(MonsterBehaviorStateDto state)
        {
            if (state == null || !state.IsValid) { throw new System.ArgumentException("怪物行为状态非法"); }
            _behaviorState = state.Copy();
            BehaviorRuntime?.Restore(state, ContactDamageCooldownRemainingSeconds);
        }
        internal void BindBehavior(MonsterBehaviorRuntime runtime)
        {
            BehaviorRuntime = runtime;
            GetComponent<ActorAnimatorController>()?.SetBehaviorDriven(true);
            GetComponent<BehaviorAnimatorBridge>()?.Bind(GetArchitecture(), runtime.Handle);
        }
        internal void BindBoss(BossBehaviorRuntime runtime)
        {
            BossRuntime = runtime;
            GetComponent<ActorAnimatorController>()?.SetBehaviorDriven(true);
            GetComponent<BehaviorAnimatorBridge>()?.Bind(GetArchitecture(), runtime.Handle);
        }
        internal void UnbindBehavior()
        {
            if (BehaviorRuntime != null)
            {
                _behaviorState = BehaviorRuntime.CaptureState(); _frozenCooldown = BehaviorRuntime.CooldownRemaining;
                GetComponent<BehaviorAnimatorBridge>()?.Unbind(); BehaviorRuntime = null;
            }
            if (BossRuntime != null)
            {
                _bossState = BossRuntime.CaptureState();
                GetComponent<BehaviorAnimatorBridge>()?.Unbind(); BossRuntime = null;
            }
        }

        [SerializeField]
        [Min(0f)]
        float _deathDespawnDelay = 0.65f;

        NavigationIntentDto _navigationIntent = new();
        Transform _navigationTransform;
        internal Rigidbody2D Body => _rigidbody;
        internal NavigationIntentDto NavigationIntent => _navigationIntent;
        internal int NavigationRevision { get; private set; }
        public float NavigationRadius
        {
            get
            {
                _navigationTransform ??= transform;
                Vector3 scale = _navigationTransform.lossyScale;
                return _collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }
        }
        public NavigationIntentDto CaptureNavigation() { return _navigationIntent.Copy(); }
        internal void RestoreNavigation(NavigationIntentDto intent)
        {
            if (intent == null || !intent.IsValid) { throw new System.ArgumentException("导航意图无效"); }
            _navigationIntent = intent.Copy(); NavigationRevision++;
        }
        public CombatActor Actor => _actor;

        public MonsterDefinition Definition => _definition;

        public MonsterInstanceData Instance => _instance;

        public float ContactDamageCooldownRemainingSeconds => BehaviorRuntime != null ? BehaviorRuntime.CooldownRemaining :
            _definition != null && _definition.Behavior != null ? _frozenCooldown : _definition != null
            ? Mathf.Max(
                0f,
                _lastContactDamageTime + _definition.ContactDamageInterval - Time.time)
            : 0f;

        public IArchitecture GetArchitecture()
        {
            return _architecture ?? GameArchitectureProvider.RequireCurrent();
        }

        public void Configure(MonsterDefinition definition, MonsterInstanceData instance)
        {
            if (_instance != null) { this.SendCommand(new RegisterNavigationAgentCommand(this, false)); }
            _definition = definition;
            _instance = instance;
            _behaviorState = null; _bossState = null; _frozenCooldown = 0; NavigationOverride = false;
            ApplyDefinition();
            this.SendCommand(new BindActorStatusesCommand(_actor));
            _sortParticipant?.ConfigureIdentity(
                WorldSortCategory.Monster,
                _instance.Id.Value);
            this.SendCommand(new RegisterNavigationAgentCommand(this, true));
        }

        public void RestoreRuntime(
            CombatResourceSnapshot resources,
            float contactDamageCooldownRemainingSeconds)
        {
            if (contactDamageCooldownRemainingSeconds < 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(contactDamageCooldownRemainingSeconds));
            }

            _actor.RestoreResources(resources, true);
            _frozenCooldown = contactDamageCooldownRemainingSeconds;
            BehaviorRuntime?.RestoreCooldown(contactDamageCooldownRemainingSeconds);
            _lastContactDamageTime = Time.time
                + contactDamageCooldownRemainingSeconds
                - _definition.ContactDamageInterval;
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
            if (_instance != null) { this.SendCommand(new RegisterNavigationAgentCommand(this, true)); }
            this.RegisterEvent<ActorDiedEvent>(OnActorDied).UnRegisterWhenDisabled(this);
        }

        void OnDisable()
        {
            if (_architecture != null) { this.SendCommand(new RegisterNavigationAgentCommand(this, false)); }
            _componentScope?.BeginStop();
            _componentScope = null;
            _despawnScheduled = false;
            NavigationOverride = false;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }
        }

        void OnDestroy()
        {
            _sessionObjects?.Unregister(gameObject);
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
            _actor.SetModifierSource("monster", _instance.Modifiers.Select(modifier => modifier.WithOrigin(new ModifierOrigin(ModifierOriginKind.Monster))));

            if (_affixVisual != null)
            {
                _affixVisual.Configure(_instance);
            }
        }

        internal void MarkContactAttack() { _lastContactDamageTime = Time.time; }

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

            this.SendCommand(new RegisterNavigationAgentCommand(this, false));
            _rigidbody.linearVelocity = Vector2.zero;
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
