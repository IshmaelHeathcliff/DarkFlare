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
        [LabelText("最大法力")]
        float _mana;

        [SerializeField]
        [MinValue(0)]
        [LabelText("生命恢复/秒")]
        float _healthRegeneration;

        [SerializeField]
        [MinValue(0)]
        [LabelText("额外法力恢复/秒")]
        float _manaRegeneration;

        [SerializeField]
        [MinValue(0)]
        [LabelText("移动速度")]
        float _moveSpeed = 5f;

        [SerializeField]
        [MinValue(0)]
        [LabelText("护甲")]
        float _armor;

        [SerializeField]
        [MinValue(0)]
        [LabelText("命中值")]
        float _accuracy = 100f;

        [SerializeField]
        [MinValue(0)]
        [LabelText("闪避值")]
        float _evasion = 20f;

        [SerializeField]
        [Range(0f, 100f)]
        [LabelText("暴击率")]
        float _criticalChance = 5f;

        [SerializeField]
        [MinValue(0)]
        [LabelText("暴击伤害")]
        float _criticalDamage = 50f;

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

        public float Mana => _mana;

        public float HealthRegeneration => _healthRegeneration;

        public float ManaRegeneration => _manaRegeneration;

        public float MoveSpeed => _moveSpeed;

        public float Armor => _armor;

        public float Accuracy => _accuracy;

        public float Evasion => _evasion;

        public float CriticalChance => _criticalChance;

        public float CriticalDamage => _criticalDamage;

        public float FireResistance => _fireResistance;

        public float ColdResistance => _coldResistance;

        public float LightningResistance => _lightningResistance;

        public float ChaosResistance => _chaosResistance;

        public StatBlock CreateStats()
        {
            StatBlock stats = new StatBlock();
            stats.SetValue(StatIds.MaxHealth, _maxHealth);
            stats.SetValue(StatIds.Mana, _mana);
            stats.SetValue(StatIds.HealthRegeneration, _healthRegeneration);
            stats.SetValue(StatIds.ManaRegeneration, _manaRegeneration);
            stats.SetValue(StatIds.MoveSpeed, _moveSpeed);
            stats.SetValue(StatIds.Armor, _armor);
            stats.SetValue(StatIds.Accuracy, _accuracy);
            stats.SetValue(StatIds.Evasion, _evasion);
            stats.SetValue(StatIds.CriticalChance, _criticalChance);
            stats.SetValue(StatIds.CriticalDamage, _criticalDamage);
            stats.SetValue(StatIds.FireResistance, _fireResistance);
            stats.SetValue(StatIds.ColdResistance, _coldResistance);
            stats.SetValue(StatIds.LightningResistance, _lightningResistance);
            stats.SetValue(StatIds.ChaosResistance, _chaosResistance);
            return stats;
        }
    }
}
