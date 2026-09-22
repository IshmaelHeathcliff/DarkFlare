using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/行为/动画语义映射")]
    public sealed class BehaviorAnimatorMap : ScriptableObject
    {
        [SerializeField, LabelText("移动 Bool 参数")] string _moving = "Moving";
        [SerializeField, LabelText("攻击 Trigger 参数")] string _attack = "Attack";
        public string Moving => _moving;
        public string Attack => _attack;
    }

    // 仅负责表现，既不访问执行器，也不决定动作完成或伤害。
    public sealed class BehaviorAnimatorPresenter
    {
        readonly Animator _animator;
        readonly string _moving;
        readonly string _attack;
        RuntimeAnimatorController _controller;
        int _movingHash;
        int _attackHash;
        bool _hasMoving;
        bool _hasAttack;
        bool _validated;
        long _triggeredAction;
        long _latestAction;
        BehaviorHandle _handle;
        BehaviorHandle _lastIdentity;
        public int AppliedAttackCount { get; private set; }
        public int ValidationIssueCount { get; private set; }
        public bool IsAvailable => _animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null;

        public BehaviorAnimatorPresenter(Animator animator, BehaviorAnimatorMap map)
        {
            _animator = animator;
            _moving = map != null ? map.Moving : "Moving";
            _attack = map != null ? map.Attack : "Attack";
        }
        public void Bind(BehaviorHandle handle)
        {
            Clear(); _handle = handle;
            if (!_lastIdentity.Equals(handle)) { _triggeredAction = 0; _latestAction = 0; _lastIdentity = handle; }
        }
        public void Apply(BehaviorPresentationEvent signal)
        {
            if (!signal.Handle.Equals(_handle) || signal.ActionInstanceId < _latestAction || !IsAvailable) { return; }
            _latestAction = signal.ActionInstanceId;
            Validate();
            _animator.speed = signal.Paused ? 0 : 1;
            if (_hasMoving) { _animator.SetBool(_movingHash, signal.Semantic == BehaviorSemantic.Move); }
            if (signal.Semantic != BehaviorSemantic.Attack)
            {
                if (_hasAttack) { _animator.ResetTrigger(_attackHash); }
                return;
            }
            if (!signal.Paused && signal.ActionInstanceId > _triggeredAction && _hasAttack)
            {
                _triggeredAction = signal.ActionInstanceId;
                _animator.SetTrigger(_attackHash);
                AppliedAttackCount++;
            }
        }
        void Validate()
        {
            if (_validated && _controller == _animator.runtimeAnimatorController) { return; }
            _validated = true; _controller = _animator.runtimeAnimatorController;
            _movingHash = Animator.StringToHash(_moving ?? string.Empty);
            _attackHash = Animator.StringToHash(_attack ?? string.Empty);
            _hasMoving = false; _hasAttack = false;
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.name == _moving && parameter.type == AnimatorControllerParameterType.Bool) { _hasMoving = true; }
                if (parameter.name == _attack && parameter.type == AnimatorControllerParameterType.Trigger) { _hasAttack = true; }
            }
            ValidationIssueCount = (_hasMoving ? 0 : 1) + (_hasAttack ? 0 : 1);
        }
        public void Clear()
        {
            if (IsAvailable)
            {
                Validate();
                if (_hasMoving) { _animator.SetBool(_movingHash, false); }
                if (_hasAttack) { _animator.ResetTrigger(_attackHash); }
                _animator.speed = 1;
            }
            _handle = default;
        }
    }
}
