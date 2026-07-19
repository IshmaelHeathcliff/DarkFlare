using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatActor))]
    public class PlayerInteractionController : MonoBehaviour, IController
    {
        readonly List<WorldInteractionTarget> _targets = new List<WorldInteractionTarget>();

        CombatActor _actor;
        GameInput _gameInput;
        IUnRegister _actorDiedRegistration;
        IUnRegister _actorRevivedRegistration;
        WorldInteractionTarget _focusedTarget;

        public WorldInteractionTarget FocusedTarget => _focusedTarget;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        void Awake()
        {
            _actor = GetComponent<CombatActor>();
        }

        void OnEnable()
        {
            _actor = GetComponent<CombatActor>();
            _gameInput = this.GetUtility<GameInput>();

            if (_gameInput == null)
            {
                Debug.LogError("[PlayerInteractionController] 缺少 GameInput，无法处理交互", this);
                return;
            }

            _gameInput.InteractPerformed += OnInteractPerformed;
            _actorDiedRegistration = this.RegisterEvent<ActorDiedEvent>(OnActorDied);
            _actorRevivedRegistration = this.RegisterEvent<ActorRevivedEvent>(OnActorRevived);
        }

        void OnDisable()
        {
            if (_gameInput != null)
            {
                _gameInput.InteractPerformed -= OnInteractPerformed;
            }

            _actorDiedRegistration?.UnRegister();
            _actorRevivedRegistration?.UnRegister();
            _actorDiedRegistration = null;
            _actorRevivedRegistration = null;
            _gameInput = null;
            _targets.Clear();
            SetFocusedTarget(null);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            TryAddTarget(other);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            TryAddTarget(other);
            RefreshFocus();
        }

        void OnTriggerExit2D(Collider2D other)
        {
            WorldInteractionTarget target = FindTarget(other);

            if (target == null)
            {
                return;
            }

            _targets.Remove(target);
            RefreshFocus();
        }

        void TryAddTarget(Collider2D other)
        {
            WorldInteractionTarget target = FindTarget(other);

            if (target == null || !target.CanInteract || _targets.Contains(target))
            {
                return;
            }

            _targets.Add(target);
            RefreshFocus();
        }

        void RefreshFocus()
        {
            WorldInteractionTarget closest = null;
            float closestDistance = float.MaxValue;

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                WorldInteractionTarget target = _targets[i];

                if (target == null || !target.CanInteract)
                {
                    _targets.RemoveAt(i);
                    continue;
                }

                float distance = (target.transform.position - transform.position).sqrMagnitude;

                if (distance < closestDistance)
                {
                    closest = target;
                    closestDistance = distance;
                }
            }

            SetFocusedTarget(closest);
        }

        void SetFocusedTarget(WorldInteractionTarget target)
        {
            if (_focusedTarget == target)
            {
                return;
            }

            _focusedTarget = target;
            this.SendCommand(new SetInteractionFocusCommand(target));
        }

        void OnInteractPerformed()
        {
            if (_actor == null || !_actor.IsAlive)
            {
                return;
            }

            RefreshFocus();

            if (_focusedTarget == null)
            {
                return;
            }

            bool opened = this.SendCommand(new OpenGameMenuCommand(_focusedTarget));

            if (opened)
            {
                Debug.Log($"[PlayerInteractionController] 与 {_focusedTarget.DisplayName} 交互", this);
            }
        }

        void OnActorDied(ActorDiedEvent e)
        {
            if (e.Actor != _actor)
            {
                return;
            }

            _targets.Clear();
            SetFocusedTarget(null);
        }

        void OnActorRevived(ActorRevivedEvent e)
        {
            if (e.Actor == _actor)
            {
                RefreshFocus();
            }
        }

        static WorldInteractionTarget FindTarget(Collider2D other)
        {
            if (other == null)
            {
                return null;
            }

            WorldInteractionTarget target = other.GetComponent<WorldInteractionTarget>();
            return target != null ? target : other.GetComponentInParent<WorldInteractionTarget>();
        }
    }
}
