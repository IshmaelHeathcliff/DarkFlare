using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/行为/普通怪物配置", fileName = "MonsterBehavior")]
    public sealed class MonsterBehaviorDefinition : ScriptableObject
    {
        [SerializeField, BoxGroup("感知"), LabelText("激活距离（米）"), MinValue(.1f)] float _activationDistance = 14;
        [SerializeField, BoxGroup("感知"), LabelText("退出距离（米）"), MinValue(.1f)] float _deactivationDistance = 16;
        [SerializeField, BoxGroup("感知"), LabelText("视距（米）"), MinValue(.1f)] float _sightDistance = 9;
        [SerializeField, BoxGroup("感知"), LabelText("视角（度）"), Range(1, 360)] float _sightAngle = 180;
        [SerializeField, BoxGroup("感知"), LabelText("刷新间隔（秒）"), Range(.02f, .25f)] float _perceptionInterval = .1f;
        [SerializeField, BoxGroup("感知"), LabelText("目标记忆（秒）"), MinValue(0)] float _memorySeconds = 2;
        [SerializeField, BoxGroup("闲逛"), LabelText("锚点半径（米）"), MinValue(.1f)] float _wanderRadius = 2;
        [SerializeField, BoxGroup("闲逛"), LabelText("等待下限（秒）"), MinValue(.1f)] float _waitMinimum = .7f;
        [SerializeField, BoxGroup("闲逛"), LabelText("等待上限（秒）"), MinValue(.1f)] float _waitMaximum = 1.8f;
        [SerializeField, BoxGroup("攻击"), LabelText("前摇（秒）"), MinValue(.02f)] float _windupSeconds = .3f;
        [SerializeField, BoxGroup("攻击"), LabelText("后摇（秒）"), MinValue(.02f)] float _recoverySeconds = .3f;

        public float ActivationDistance => _activationDistance;
        public float DeactivationDistance => _deactivationDistance;
        public float SightDistance => _sightDistance;
        public float SightCosine => Mathf.Cos(_sightAngle * .5f * Mathf.Deg2Rad);
        public float SightAngle => _sightAngle;
        public float PerceptionInterval => _perceptionInterval;
        public float MemorySeconds => _memorySeconds;
        public float WanderRadius => _wanderRadius;
        public float WaitMinimum => _waitMinimum;
        public float WaitMaximum => _waitMaximum;
        public float WindupSeconds => _windupSeconds;
        public float RecoverySeconds => _recoverySeconds;

        public void Validate()
        {
            Positive(_activationDistance); Positive(_deactivationDistance); Positive(_sightDistance);
            Positive(_sightAngle); Positive(_perceptionInterval); Positive(_wanderRadius);
            Positive(_waitMinimum); Positive(_waitMaximum); Positive(_windupSeconds); Positive(_recoverySeconds);
            if (_deactivationDistance <= _activationDistance || _sightDistance > _activationDistance
                || _sightAngle > 360 || _perceptionInterval < .02f || _perceptionInterval > .25f
                || !float.IsFinite(_memorySeconds) || _memorySeconds < 0 || _waitMinimum > _waitMaximum)
            {
                throw new ArgumentException("怪物行为配置无效：检查激活迟滞、视距、视角、感知间隔及等待范围");
            }
        }

        static void Positive(float value)
        {
            if (!float.IsFinite(value) || value <= 0) { throw new ArgumentException("行为配置数值必须有限且大于零"); }
        }
    }
}
