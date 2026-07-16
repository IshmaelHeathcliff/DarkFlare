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
        CharacterDefinition _definition;
        ProjectileSkillDefinition _defaultSkill;
        GameInput _gameInput;
        CancellationTokenSource _skillLoopCancellation;
        Vector2 _lastAimDirection = Vector2.right;
        Vector3 _spawnPosition;

        public CombatActor Actor => _actor;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void Configure(CharacterDefinition definition, ProjectileSkillDefinition skill)
        {
            _definition = definition;
            _defaultSkill = skill;
            _spawnPosition = transform.position;
            _actor.ConfigureFromCharacter(_definition, ActorTeam.Player);
            AutoCastLoop(_skillLoopCancellation.Token).Forget();
        }

        void Awake()
        {
            EnsureComponents();
            _gameInput = this.GetUtility<GameInput>();
        }

        void OnEnable()
        {
            this.RegisterEvent<ActorDiedEvent>(OnActorDied).UnRegisterWhenGameObjectDestroyed(gameObject);
            _skillLoopCancellation = new CancellationTokenSource();
        }

        void OnDisable()
        {
            _skillLoopCancellation?.Cancel();
            _skillLoopCancellation?.Dispose();
            _skillLoopCancellation = null;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }
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

        async UniTaskVoid AutoCastLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(_defaultSkill.Cooldown), cancellationToken: token);
                TryFireProjectile();
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

            RespawnAfterDelay(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid RespawnAfterDelay(CancellationToken token)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(_respawnDelay), cancellationToken: token);
            this.SendCommand(new ReviveActorCommand(_actor, _spawnPosition));
        }
    }
}
