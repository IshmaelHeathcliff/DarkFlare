using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/Data/Monsters/Monster Definition", fileName = "MonsterDefinition")]
    public class MonsterDefinition : ScriptableObject
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [LabelText("中文名")]
        string _displayName = string.Empty;

        [SerializeField]
        [LabelText("角色属性")]
        CharacterDefinition _character;

        [SerializeField]
        [LabelText("怪物预制体")]
        AssetReferenceGameObject _prefab;

        [SerializeField]
        [MinValue(1)]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("最大生命")]
        float _maxHealth = 24f;

        [SerializeField]
        [MinValue(0)]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("最大法力")]
        float _mana;

        [SerializeField]
        [MinValue(0)]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("生命恢复/秒")]
        float _healthRegeneration;

        [SerializeField]
        [MinValue(0)]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("额外法力恢复/秒")]
        float _manaRegeneration;

        [SerializeField]
        [MinValue(0)]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("移动速度")]
        float _moveSpeed = 2.6f;

        [SerializeField]
        [MinValue(0)]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("命中值")]
        float _accuracy = 90f;

        [SerializeField]
        [MinValue(0)]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("闪避值")]
        float _evasion = 15f;

        [SerializeField]
        [Range(0f, 100f)]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("暴击率")]
        float _criticalChance = 5f;

        [SerializeField]
        [MinValue(0)]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("暴击伤害")]
        float _criticalDamage = 50f;

        [SerializeField]
        [MinValue(0)]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("护甲")]
        float _armor;

        [SerializeField]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("火焰抗性")]
        float _fireResistance;

        [SerializeField]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("冰霜抗性")]
        float _coldResistance;

        [SerializeField]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("闪电抗性")]
        float _lightningResistance;

        [SerializeField]
        [HideIf(nameof(HasCharacterDefinition))]
        [LabelText("混沌抗性")]
        float _chaosResistance;

        [SerializeField]
        [LabelText("生命倍率范围")]
        Vector2 _healthMultiplierRange = Vector2.one;

        [SerializeField]
        [MinValue(0.05f)]
        [LabelText("碰撞伤害间隔")]
        float _contactDamageInterval = 0.75f;

        [SerializeField]
        [MinValue(0.05f)]
        [LabelText("碰撞伤害半径")]
        float _contactDamageRadius = 0.75f;

        [SerializeField]
        [MinValue(0f)]
        [LabelText("接近停止距离")]
        float _contactStopDistance = 0.6f;

        [SerializeField]
        [MinValue(0.01f)]
        [LabelText("软分离半径")]
        float _separationRadius = 0.8f;

        [SerializeField]
        [MinValue(0f)]
        [MaxValue(2f)]
        [LabelText("软分离权重")]
        float _separationWeight = 0.65f;

        [SerializeField]
        [LabelText("自定义角色标签")]
        List<TagDefinition> _tags = new List<TagDefinition>();

        [SerializeField]
        [LabelText("碰撞伤害")]
        List<DamageRollDefinition> _contactDamages = new List<DamageRollDefinition>();

        [SerializeField]
        [LabelText("掉落表")]
        LootTableDefinition _lootTable;

        [SerializeField]
        [LabelText("怪物词条池")]
        List<MonsterAffixDefinition> _affixPool = new List<MonsterAffixDefinition>();

        [SerializeField]
        [MinValue(0)]
        [LabelText("最少怪物词条")]
        int _minimumAffixCount;

        [SerializeField]
        [MinValue(0)]
        [LabelText("最多怪物词条")]
        int _maximumAffixCount;

        public string Id => _id;

        public string DisplayName => _displayName;

        public AssetReferenceGameObject Prefab => _prefab;

        public float MaxHealth => _character != null ? _character.MaxHealth : _maxHealth;

        public float Mana => _character != null ? _character.Mana : _mana;

        public float HealthRegeneration => _character != null
            ? _character.HealthRegeneration
            : _healthRegeneration;

        public float ManaRegeneration => _character != null
            ? _character.ManaRegeneration
            : _manaRegeneration;

        public float MoveSpeed => _character != null ? _character.MoveSpeed : _moveSpeed;

        public float Accuracy => _character != null ? _character.Accuracy : _accuracy;

        public float Evasion => _character != null ? _character.Evasion : _evasion;

        public float CriticalChance => _character != null ? _character.CriticalChance : _criticalChance;

        public float CriticalDamage => _character != null ? _character.CriticalDamage : _criticalDamage;

        public float Armor => _character != null ? _character.Armor : _armor;

        public float FireResistance => _character != null ? _character.FireResistance : _fireResistance;

        public float ColdResistance => _character != null ? _character.ColdResistance : _coldResistance;

        public float LightningResistance => _character != null ? _character.LightningResistance : _lightningResistance;

        public float ChaosResistance => _character != null ? _character.ChaosResistance : _chaosResistance;

        public Vector2 HealthMultiplierRange => _healthMultiplierRange;

        public float ContactDamageInterval => _contactDamageInterval;

        public float ContactDamageRadius => _contactDamageRadius;

        public float ContactStopDistance => _contactStopDistance;

        public float SeparationRadius => _separationRadius;

        public float SeparationWeight => _separationWeight;

        public IReadOnlyList<TagDefinition> Tags => _tags;

        public TagSet RuntimeTags => TagSet.FromDefinitions(_tags);

        public LootTableDefinition LootTable => _lootTable;

        public IReadOnlyList<DamageRollDefinition> ContactDamages => _contactDamages;

        public IReadOnlyList<MonsterAffixDefinition> AffixPool => _affixPool;

        public int MinimumAffixCount => _minimumAffixCount;

        public int MaximumAffixCount => _maximumAffixCount;

        bool HasCharacterDefinition => _character != null;

        public StatBlock CreateStats()
        {
            if (_character != null)
            {
                return _character.CreateStats();
            }

            StatBlock stats = new StatBlock();
            stats.SetValue(StatIds.MaxHealth, _maxHealth);
            stats.SetValue(StatIds.Mana, _mana);
            stats.SetValue(StatIds.HealthRegeneration, _healthRegeneration);
            stats.SetValue(StatIds.ManaRegeneration, _manaRegeneration);
            stats.SetValue(StatIds.MoveSpeed, _moveSpeed);
            stats.SetValue(StatIds.Accuracy, _accuracy);
            stats.SetValue(StatIds.Evasion, _evasion);
            stats.SetValue(StatIds.CriticalChance, _criticalChance);
            stats.SetValue(StatIds.CriticalDamage, _criticalDamage);
            stats.SetValue(StatIds.Armor, _armor);
            stats.SetValue(StatIds.FireResistance, _fireResistance);
            stats.SetValue(StatIds.ColdResistance, _coldResistance);
            stats.SetValue(StatIds.LightningResistance, _lightningResistance);
            stats.SetValue(StatIds.ChaosResistance, _chaosResistance);
            return stats;
        }

        public MonsterInstanceData CreateInstanceData(int seed)
        {
            MonsterInstanceRandomSeeds randomSeeds = new MonsterInstanceRandomSeeds(seed);
            System.Random random = new System.Random(randomSeeds.HealthSeed);
            float minimumMultiplier = Mathf.Max(0.01f, Mathf.Min(_healthMultiplierRange.x, _healthMultiplierRange.y));
            float maximumMultiplier = Mathf.Max(minimumMultiplier, Mathf.Max(_healthMultiplierRange.x, _healthMultiplierRange.y));
            float multiplier = Mathf.Lerp(minimumMultiplier, maximumMultiplier, (float)random.NextDouble());
            float baseMaxHealth = Mathf.Max(1f, MaxHealth * multiplier);
            StatBlock baseStats = CreateStats();
            baseStats.SetValue(StatIds.MaxHealth, baseMaxHealth);
            List<MonsterAffixInstance> affixes = MonsterAffixGenerator.Generate(
                _affixPool,
                _minimumAffixCount,
                _maximumAffixCount,
                randomSeeds);
            List<ModifierInstance> modifiers = new List<ModifierInstance>();

            for (int affixIndex = 0; affixIndex < affixes.Count; affixIndex++)
            {
                IReadOnlyList<ModifierInstance> affixModifiers = affixes[affixIndex].Modifiers;

                for (int modifierIndex = 0; modifierIndex < affixModifiers.Count; modifierIndex++)
                {
                    modifiers.Add(affixModifiers[modifierIndex]);
                }
            }

            StatBlock effectiveStats = CombatStatResolver.Build(baseStats, modifiers);
            return new MonsterInstanceData(
                randomSeeds,
                baseMaxHealth,
                baseStats,
                effectiveStats,
                affixes,
                modifiers);
        }

        public List<DamagePacket> CreateContactDamagePackets(int seed)
        {
            List<DamagePacket> packets = new List<DamagePacket>(_contactDamages.Count);
            System.Random random = new System.Random(seed);

            for (int i = 0; i < _contactDamages.Count; i++)
            {
                packets.Add(_contactDamages[i].CreatePacket(random));
            }

            return packets;
        }

        void OnValidate()
        {
            List<string> issues = RandomizationConfigurationValidator.Validate(this);

            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogWarning($"[MonsterDefinition] {name}: {issues[i]}", this);
            }
        }
    }
}
