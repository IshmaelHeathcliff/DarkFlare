using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public enum ItemType
    {
        Weapon,
        Armor,
        Accessory,
        Material,
        Currency
    }

    public enum ItemRarity
    {
        Normal,
        Magic,
        Rare,
        Unique
    }

    [Serializable]
    public class DamageRollDefinition
    {
        [SerializeField]
        [LabelText("伤害类型")]
        DamageType _damageType = DamageType.Physical;

        [SerializeField]
        [LabelText("伤害范围")]
        Vector2 _amountRange;

        [SerializeField]
        [LabelText("自定义伤害标签")]
        List<TagDefinition> _tags = new List<TagDefinition>();

        public DamageType DamageType => _damageType;

        public Vector2 AmountRange => _amountRange;

        public IReadOnlyList<TagDefinition> Tags => _tags;

        public DamagePacket CreatePacket(System.Random random)
        {
            float amount = _amountRange.x;

            if (Math.Abs(_amountRange.x - _amountRange.y) > float.Epsilon)
            {
                amount = _amountRange.x + (float)random.NextDouble() * (_amountRange.y - _amountRange.x);
            }

            return new DamagePacket(
                _damageType,
                amount,
                DamagePacket.ToMask(_damageType),
                TagSet.FromDefinitions(_tags));
        }
    }

    [CreateAssetMenu(menuName = "DarkFlare/Data/Items/Item Base Definition", fileName = "ItemBaseDefinition")]
    public class ItemBaseDefinition : ScriptableObject
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [LabelText("中文名")]
        string _displayName = string.Empty;

        [SerializeField]
        [LabelText("物品类型")]
        ItemType _itemType;

        [SerializeField]
        [LabelText("图标")]
        AssetReferenceSprite _icon;

        [SerializeField]
        [EnumToggleButtons]
        [LabelText("允许装备槽")]
        EquipmentSlotMask _allowedEquipmentSlots;

        [SerializeField]
        [LabelText("默认稀有度")]
        ItemRarity _defaultRarity;

        [SerializeField]
        [MinValue(0)]
        [LabelText("基础价格")]
        int _baseValue;

        [SerializeField]
        [LabelText("格子尺寸")]
        Vector2Int _gridSize = Vector2Int.one;

        [SerializeField]
        [MinValue(0)]
        [LabelText("重量")]
        float _weight;

        [SerializeField]
        [LabelText("自定义物品生成标签")]
        List<TagDefinition> _tags = new List<TagDefinition>();

        [SerializeField]
        [LabelText("基础伤害")]
        List<DamageRollDefinition> _baseDamages = new List<DamageRollDefinition>();

        [SerializeField]
        [LabelText("隐式修改器")]
        List<StatModifierDefinition> _implicitModifiers = new List<StatModifierDefinition>();

        public string Id => _id;

        public string DisplayName => _displayName;

        public ItemType ItemType => _itemType;

        public AssetReferenceSprite Icon => _icon;

        public EquipmentSlotMask AllowedEquipmentSlots => _allowedEquipmentSlots;

        public ItemRarity DefaultRarity => _defaultRarity;

        public int BaseValue => _baseValue;

        public Vector2Int GridSize => _gridSize;

        public float Weight => _weight;

        public IReadOnlyList<TagDefinition> Tags => _tags;

        public IReadOnlyList<DamageRollDefinition> BaseDamages => _baseDamages;

        public IReadOnlyList<StatModifierDefinition> ImplicitModifiers => _implicitModifiers;

        public TagSet RuntimeTags => CombatTagResolver.ResolveItemSpawnTags(
            _itemType,
            _allowedEquipmentSlots,
            TagSet.FromDefinitions(_tags));

        public bool CanEquipTo(EquipmentSlot slot)
        {
            EquipmentSlotMask slotMask = EquipmentSlots.ToMask(slot);
            return slotMask != EquipmentSlotMask.None && (_allowedEquipmentSlots & slotMask) != 0;
        }

        public ItemInstance CreateInstance(string instanceId, int itemLevel, int seed)
        {
            return CreateInstance(instanceId, itemLevel, seed, _defaultRarity);
        }

        public ItemInstance CreateInstance(string instanceId, int itemLevel, int seed, ItemRarity rarity)
        {
            System.Random random = new System.Random(seed);
            List<ModifierInstance> implicitModifiers = new List<ModifierInstance>(_implicitModifiers.Count);

            for (int i = 0; i < _implicitModifiers.Count; i++)
            {
                implicitModifiers.Add(_implicitModifiers[i].CreateInstance(random));
            }

            return new ItemInstance(instanceId, this, rarity, itemLevel, seed, implicitModifiers);
        }

        void OnValidate()
        {
            List<string> issues = EquipmentConfigurationValidator.Validate(this);

            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogWarning($"[ItemBaseDefinition] {name}: {issues[i]}", this);
            }
        }
    }
}
