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
        [LabelText("移动速度")]
        float _moveSpeed = 2.6f;

        [SerializeField]
        [MinValue(0.05f)]
        [LabelText("碰撞伤害间隔")]
        float _contactDamageInterval = 0.75f;

        [SerializeField]
        [MinValue(0.05f)]
        [LabelText("碰撞伤害半径")]
        float _contactDamageRadius = 0.75f;

        [SerializeField]
        [LabelText("怪物标签")]
        List<TagDefinition> _tags = new List<TagDefinition>();

        [SerializeField]
        [LabelText("碰撞伤害")]
        List<DamageRollDefinition> _contactDamages = new List<DamageRollDefinition>();

        [SerializeField]
        [LabelText("掉落表")]
        LootTableDefinition _lootTable;

        public string Id => _id;

        public string DisplayName => _displayName;

        public AssetReferenceGameObject Prefab => _prefab;

        public float MaxHealth => _character != null ? _character.MaxHealth : _maxHealth;

        public float MoveSpeed => _character != null ? _character.MoveSpeed : _moveSpeed;

        public float ContactDamageInterval => _contactDamageInterval;

        public float ContactDamageRadius => _contactDamageRadius;

        public TagSet RuntimeTags => TagSet.FromDefinitions(_tags);

        public LootTableDefinition LootTable => _lootTable;

        bool HasCharacterDefinition => _character != null;

        public StatBlock CreateStats()
        {
            if (_character != null)
            {
                return _character.CreateStats();
            }

            StatBlock stats = new StatBlock();
            stats.SetValue(StatIds.MaxHealth, _maxHealth);
            stats.SetValue(StatIds.MoveSpeed, _moveSpeed);
            return stats;
        }

        public List<DamagePacket> CreateContactDamagePackets(int seed)
        {
            List<DamagePacket> packets = new List<DamagePacket>(_contactDamages.Count);
            System.Random random = new System.Random(seed);

            for (int i = 0; i < _contactDamages.Count; i++)
            {
                packets.Add(_contactDamages[i].CreatePacket(random));
            }

            if (packets.Count == 0)
            {
                packets.Add(new DamagePacket(DamageType.Physical, 8f, TagSet.Empty));
            }

            return packets;
        }
    }
}
