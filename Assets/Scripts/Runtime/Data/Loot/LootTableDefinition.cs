using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [Serializable]
    public class LootTableEntry
    {
        [SerializeField]
        [LabelText("物品")]
        ItemBaseDefinition _item;

        [SerializeField]
        [MinValue(0)]
        [LabelText("权重")]
        int _weight = 100;

        [SerializeField]
        [LabelText("稀有度")]
        ItemRarity _rarity;

        [SerializeField]
        [MinValue(0)]
        [LabelText("前缀数量")]
        int _prefixCount;

        [SerializeField]
        [MinValue(0)]
        [LabelText("后缀数量")]
        int _suffixCount;

        [SerializeField, MinValue(1), LabelText("掉落数量范围")]
        Vector2Int _quantityRange = Vector2Int.one;

        public Vector2Int QuantityRange => _quantityRange;

        public ItemBaseDefinition Item => _item;

        public int Weight => _weight;

        public ItemRarity Rarity => _rarity;

        public int PrefixCount => _prefixCount;

        public int SuffixCount => _suffixCount;
    }

    [ContentDefinition(ContentNamespaces.Loot)]
    [CreateAssetMenu(menuName = "DarkFlare/Data/Loot/Loot Table Definition", fileName = "LootTableDefinition")]
    public class LootTableDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [Range(0f, 1f)]
        [LabelText("掉落概率")]
        float _dropChance = 1f;

        [SerializeField]
        [LabelText("掉落池")]
        List<LootTableEntry> _entries = new List<LootTableEntry>();

        [SerializeField]
        [LabelText("词条池")]
        List<AffixDefinition> _affixPool = new List<AffixDefinition>();

        public string Id => _id;

        public float DropChance => _dropChance;

        public IReadOnlyList<LootTableEntry> Entries => _entries;

        public IReadOnlyList<AffixDefinition> AffixPool => _affixPool;

        public ItemBaseDefinition PickItem(System.Random random)
        {
            LootTableEntry entry = PickEntry(random);
            return entry != null ? entry.Item : null;
        }

        public ItemInstance GenerateLoot(System.Random random, string instanceId, int itemLevel)
        {
            return GenerateLoot(random, ItemInstanceId.FromLegacy(instanceId), itemLevel);
        }

        public ItemInstance GenerateLoot(System.Random random, ItemInstanceId instanceId, int itemLevel)
        {
            if (!ShouldDrop(random))
            {
                return null;
            }

            LootTableEntry entry = PickEntry(random);

            if (entry == null || entry.Item == null)
            {
                return null;
            }

            if (!ItemRarityRules.IsNormalGenerationValid(
                    entry.Rarity,
                    entry.PrefixCount,
                    entry.SuffixCount))
            {
                ApplicationLog.Error(LogEventIds.DataValidation,
                    $"[LootTableDefinition] {name} 的掉落条目配置非法："
                    + $"{entry.Item.Id} / {entry.Rarity} / 前缀 {entry.PrefixCount} / 后缀 {entry.SuffixCount}",
                    this);
                return null;
            }

            int seed = random.Next(int.MinValue, int.MaxValue);
            ItemGenerationOptions options = new ItemGenerationOptions(instanceId, itemLevel, seed, entry.Rarity, entry.PrefixCount, entry.SuffixCount);

            try
            {
                ItemInstance item = ItemGenerator.Generate(entry.Item, _affixPool, options);
                if (entry.QuantityRange.x < 1 || entry.QuantityRange.y < entry.QuantityRange.x
                    || entry.QuantityRange.y > entry.Item.MaxStackSize)
                {
                    throw new InvalidOperationException("掉落数量超出堆叠合同");
                }
                int quantity = entry.QuantityRange.x == entry.QuantityRange.y ? entry.QuantityRange.x
                    : (int)(entry.QuantityRange.x + random.NextDouble() * ((long)entry.QuantityRange.y - entry.QuantityRange.x + 1));
                item.TrySetQuantity(quantity);
                return item;
            }
            catch (InvalidOperationException exception)
            {
                ApplicationLog.Error(LogEventIds.DataValidation,
                    $"[LootTableDefinition] {name} 无法生成完整掉落：{exception.Message}",
                    this);
                return null;
            }
        }

        bool ShouldDrop(System.Random random)
        {
            if (_dropChance <= 0f)
            {
                return false;
            }

            return _dropChance >= 1f || random.NextDouble() < _dropChance;
        }

        LootTableEntry PickEntry(System.Random random)
        {
            int totalWeight = 0;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Item != null && _entries[i].Weight > 0)
                {
                    totalWeight += _entries[i].Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = random.Next(0, totalWeight);

            for (int i = 0; i < _entries.Count; i++)
            {
                LootTableEntry entry = _entries[i];

                if (entry.Item == null || entry.Weight <= 0)
                {
                    continue;
                }

                if (roll < entry.Weight)
                {
                    return entry;
                }

                roll -= entry.Weight;
            }

            return null;
        }

        void OnValidate()
        {
            List<string> issues = RandomizationConfigurationValidator.Validate(this);

            for (int i = 0; i < issues.Count; i++)
            {
                ApplicationLog.Warning(LogEventIds.DataValidation, $"[LootTableDefinition] {name}: {issues[i]}", this);
            }
        }
    }
}
