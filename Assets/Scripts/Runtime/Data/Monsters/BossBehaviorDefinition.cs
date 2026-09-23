using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/行为/守卫 Boss 配置", fileName = "BossBehavior")]
    public sealed class BossBehaviorDefinition : ScriptableObject
    {
        [SerializeField, BoxGroup("强化阶段"), LabelText("生命阈值比例"), Range(.01f, .99f)] float _phaseThreshold = .5f;
        [SerializeField, BoxGroup("强化阶段"), LabelText("转换时长（秒）"), MinValue(.05f)] float _phaseSeconds = .8f;
        [SerializeField, BoxGroup("强化阶段"), LabelText("技能冷却倍率"), Range(.1f, 1f)] float _empoweredCooldownScale = .75f;
        [SerializeField, BoxGroup("挥击"), LabelText("半径（米）"), MinValue(.1f)] float _swipeRadius = 1.5f;
        [SerializeField, BoxGroup("挥击"), LabelText("扇形角度（度）"), Range(1f, 360f)] float _swipeAngle = 120f;
        [SerializeField, BoxGroup("挥击"), LabelText("预警（秒）"), MinValue(.05f)] float _swipeWarningSeconds = .4f;
        [SerializeField, BoxGroup("挥击"), LabelText("后摇（秒）"), MinValue(.05f)] float _swipeRecoverySeconds = .5f;
        [SerializeField, BoxGroup("挥击"), LabelText("冷却（秒）"), MinValue(.1f)] float _swipeCooldownSeconds = 1.6f;
        [SerializeField, BoxGroup("冲锋"), LabelText("最短启动距离（米）"), MinValue(.1f)] float _chargeMinimumDistance = 2f;
        [SerializeField, BoxGroup("冲锋"), LabelText("最大距离（米）"), MinValue(.1f)] float _chargeDistance = 5f;
        [SerializeField, BoxGroup("冲锋"), LabelText("速度（米/秒）"), MinValue(.1f)] float _chargeSpeed = 7f;
        [SerializeField, BoxGroup("冲锋"), LabelText("碰撞半径（米）"), MinValue(.1f)] float _chargeHitRadius = .9f;
        [SerializeField, BoxGroup("冲锋"), LabelText("预警（秒）"), MinValue(.05f)] float _chargeWarningSeconds = .8f;
        [SerializeField, BoxGroup("冲锋"), LabelText("后摇（秒）"), MinValue(.05f)] float _chargeRecoverySeconds = .8f;
        [SerializeField, BoxGroup("冲锋"), LabelText("冷却（秒）"), MinValue(.1f)] float _chargeCooldownSeconds = 5f;

        public float PhaseThreshold => _phaseThreshold;
        public float PhaseSeconds => _phaseSeconds;
        public float EmpoweredCooldownScale => _empoweredCooldownScale;
        public float SwipeRadius => _swipeRadius;
        public float SwipeCosine => Mathf.Cos(_swipeAngle * .5f * Mathf.Deg2Rad);
        public float SwipeAngle => _swipeAngle;
        public float SwipeWarningSeconds => _swipeWarningSeconds;
        public float SwipeRecoverySeconds => _swipeRecoverySeconds;
        public float SwipeCooldownSeconds => _swipeCooldownSeconds;
        public float ChargeMinimumDistance => _chargeMinimumDistance;
        public float ChargeDistance => _chargeDistance;
        public float ChargeSpeed => _chargeSpeed;
        public float ChargeHitRadius => _chargeHitRadius;
        public float ChargeWarningSeconds => _chargeWarningSeconds;
        public float ChargeRecoverySeconds => _chargeRecoverySeconds;
        public float ChargeCooldownSeconds => _chargeCooldownSeconds;

        public void Validate()
        {
            float[] values = { _phaseThreshold, _phaseSeconds, _empoweredCooldownScale, _swipeRadius, _swipeAngle,
                _swipeWarningSeconds, _swipeRecoverySeconds, _swipeCooldownSeconds, _chargeMinimumDistance,
                _chargeDistance, _chargeSpeed, _chargeHitRadius, _chargeWarningSeconds, _chargeRecoverySeconds,
                _chargeCooldownSeconds };
            foreach (float value in values)
            {
                if (!float.IsFinite(value) || value <= 0) { throw new ArgumentException("Boss 行为数值必须有限且大于零"); }
            }
            if (_phaseThreshold >= 1 || _empoweredCooldownScale > 1 || _swipeAngle > 360
                || _chargeMinimumDistance >= _chargeDistance)
            { throw new ArgumentException("Boss 行为阈值、角度或冲锋距离无效"); }
        }
    }
}
