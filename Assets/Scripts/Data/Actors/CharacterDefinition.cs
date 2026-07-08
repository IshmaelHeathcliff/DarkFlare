using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/Data/Actors/Character Definition", fileName = "CharacterDefinition")]
    public class CharacterDefinition : ScriptableObject
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [LabelText("中文名")]
        string _displayName = string.Empty;

        [SerializeField]
        [LabelText("角色 Prefab")]
        AssetReferenceGameObject _prefab;

        [SerializeField]
        [MinValue(1)]
        [LabelText("最大生命")]
        float _maxHealth = 100f;

        [SerializeField]
        [MinValue(0)]
        [LabelText("移动速度")]
        float _moveSpeed = 5f;

        [SerializeField]
        [MinValue(0)]
        [LabelText("护甲")]
        float _armor;

        [SerializeField]
        [LabelText("火焰抗性")]
        float _fireResistance;

        [SerializeField]
        [LabelText("冰霜抗性")]
        float _coldResistance;

        [SerializeField]
        [LabelText("闪电抗性")]
        float _lightningResistance;

        [SerializeField]
        [LabelText("混沌抗性")]
        float _chaosResistance;

        public string Id => _id;

        public string DisplayName => _displayName;

        public AssetReferenceGameObject Prefab => _prefab;

        public float MaxHealth => _maxHealth;

        public float MoveSpeed => _moveSpeed;

        public StatBlock CreateStats()
        {
            StatBlock stats = new StatBlock();
            stats.SetValue(StatIds.MaxHealth, _maxHealth);
            stats.SetValue(StatIds.MoveSpeed, _moveSpeed);
            stats.SetValue(StatIds.Armor, _armor);
            stats.SetValue(StatIds.FireResistance, _fireResistance);
            stats.SetValue(StatIds.ColdResistance, _coldResistance);
            stats.SetValue(StatIds.LightningResistance, _lightningResistance);
            stats.SetValue(StatIds.ChaosResistance, _chaosResistance);
            return stats;
        }
    }
}
