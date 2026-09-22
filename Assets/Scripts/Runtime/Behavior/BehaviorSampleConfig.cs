using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/行为/诊断样例配置")]
    public sealed class BehaviorSampleConfig : ScriptableObject
    {
        [SerializeField, LabelText("巡逻速度"), Min(.1f)] float _speed = 1.5f;
        [SerializeField, LabelText("巡逻距离"), Min(.1f)] float _distance = 3;
        [SerializeField, LabelText("等待秒数"), Min(.01f)] float _wait = .7f;
        [SerializeField, LabelText("预备秒数"), Min(.01f)] float _windup = .5f;
        [SerializeField, LabelText("攻击秒数"), Min(.01f)] float _attack = .25f;
        [SerializeField, LabelText("恢复秒数"), Min(.01f)] float _recovery = .5f;
        public float Speed => _speed;
        public float Distance => _distance;
        public float WaitSeconds => _wait;
        public float WindupSeconds => _windup;
        public float AttackSeconds => _attack;
        public float RecoverySeconds => _recovery;
        public void Validate()
        {
            foreach (float value in new[] { _speed, _distance, _wait, _windup, _attack, _recovery })
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) { throw new System.InvalidOperationException("行为样例参数必须为有限正数"); }
            }
        }
    }
}
