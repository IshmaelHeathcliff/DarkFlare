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

        public ItemBaseDefinition Item => _item;

        public int Weight => _weight;

        public ItemRarity Rarity => _rarity;

        public int PrefixCount => _prefixCount;

        public int SuffixCount => _suffixCount;
    }

    [CreateAssetMenu(menuName = "DarkFlare/Data/Loot/Loot Table Definition", fileName = "LootTableDefinition")]
    public class LootTableDefinition : ScriptableObject
    {
        [SerializeField]
        [LabelText("掉落池")]
        List<LootTableEntry> _entries = new List<LootTableEntry>();

        [SerializeField]
        [LabelText("词条池")]
        List<AffixDefinition> _affixPool = new List<AffixDefinition>();

        public ItemBaseDefinition PickItem(System.Random random)
        {
            LootTableEntry entry = PickEntry(random);
            return entry != null ? entry.Item : null;
        }

        public ItemInstance GenerateLoot(System.Random random, string instanceId, int itemLevel)
        {
            LootTableEntry entry = PickEntry(random);

            if (entry == null || entry.Item == null)
            {
                return null;
            }

            int seed = random.Next(int.MinValue, int.MaxValue);
            ItemGenerationOptions options = new ItemGenerationOptions(instanceId, itemLevel, seed, entry.Rarity, entry.PrefixCount, entry.SuffixCount);
            return ItemGenerator.Generate(entry.Item, _affixPool, options);
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
    }
}
