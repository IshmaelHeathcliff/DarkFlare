using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CombatActor))]
    public sealed class ActorAnimatorController : MonoBehaviour, IController
    {
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int DeathHash = Animator.StringToHash("Death");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int MovingHash = Animator.StringToHash("Moving");

        [SerializeField]
        Animator _animator;

        [SerializeField]
        SpriteRenderer _renderer;

        [SerializeField]
        [Min(0f)]
        float _movementThreshold = 0.05f;

        Rigidbody2D _rigidbody;
        CombatActor _actor;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            this.RegisterEvent<ActorAttackedEvent>(OnActorAttacked).UnRegisterWhenGameObjectDestroyed(gameObject);
            this.RegisterEvent<ActorDamagedEvent>(OnActorDamaged).UnRegisterWhenGameObjectDestroyed(gameObject);
            this.RegisterEvent<ActorDiedEvent>(OnActorDied).UnRegisterWhenGameObjectDestroyed(gameObject);
            this.RegisterEvent<ActorRevivedEvent>(OnActorRevived).UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        void FixedUpdate()
        {
            if (_animator == null || _rigidbody == null)
            {
                return;
            }

            Vector2 velocity = _actor != null && _actor.IsAlive ? _rigidbody.linearVelocity : Vector2.zero;
            bool isMoving = velocity.sqrMagnitude > _movementThreshold * _movementThreshold;
            _animator.SetBool(MovingHash, isMoving);

            if (_renderer != null && Mathf.Abs(velocity.x) > _movementThreshold)
            {
                _renderer.flipX = velocity.x < 0f;
            }
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<SpriteRenderer>();
            }

            _rigidbody = GetComponent<Rigidbody2D>();
            _actor = GetComponent<CombatActor>();
        }

        void OnActorAttacked(ActorAttackedEvent e)
        {
            if (e.Actor != _actor || _actor == null || !_actor.IsAlive)
            {
                return;
            }

            _animator.ResetTrigger(HitHash);
            _animator.SetTrigger(AttackHash);
        }

        void OnActorDamaged(ActorDamagedEvent e)
        {
            if (e.Actor != _actor || _actor == null || !_actor.IsAlive || !e.Result.IsHit)
            {
                return;
            }

            _animator.ResetTrigger(AttackHash);
            _animator.SetTrigger(HitHash);
        }

        void OnActorDied(ActorDiedEvent e)
        {
            if (e.Actor != _actor)
            {
                return;
            }

            _animator.ResetTrigger(AttackHash);
            _animator.ResetTrigger(HitHash);
            _animator.SetBool(MovingHash, false);
            _animator.SetTrigger(DeathHash);
        }

        void OnActorRevived(ActorRevivedEvent e)
        {
            if (e.Actor != _actor)
            {
                return;
            }

            _animator.ResetTrigger(AttackHash);
            _animator.ResetTrigger(HitHash);
            _animator.ResetTrigger(DeathHash);
            _animator.Rebind();
            _animator.Update(0f);
        }
    }
}
