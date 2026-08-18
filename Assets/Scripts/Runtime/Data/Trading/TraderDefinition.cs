using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [Serializable]
    public class TraderStockEntry
    {
        [SerializeField]
        [LabelText("物品")]
        ItemBaseDefinition _item;

        [SerializeField]
        [LabelText("稀有度")]
        ItemRarity _rarity;

        [SerializeField]
        [MinValue(1)]
        [LabelText("物品等级")]
        int _itemLevel = 1;

        [SerializeField]
        [MinValue(1)]
        [LabelText("数量")]
        int _count = 1;

        public ItemBaseDefinition Item => _item;

        public ItemRarity Rarity => _rarity;

        public int ItemLevel => _itemLevel;

        public int Count => _count;
    }

    [ContentDefinition(ContentNamespaces.Trader)]
    [CreateAssetMenu(menuName = "DarkFlare/Data/Trading/Trader Definition", fileName = "TraderDefinition")]
    public class TraderDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [LabelText("中文名")]
        string _displayName = string.Empty;

        [SerializeField]
        [MinValue(0)]
        [LabelText("买入倍率")]
        float _buyMultiplier = 1.5f;

        [SerializeField]
        [MinValue(0)]
        [LabelText("卖出倍率")]
        float _sellMultiplier = 0.4f;

        [SerializeField]
        [LabelText("库存")]
        List<TraderStockEntry> _stock = new List<TraderStockEntry>();

        public string Id => _id;

        public string DisplayName => _displayName;

        public float BuyMultiplier => _buyMultiplier;

        public float SellMultiplier => _sellMultiplier;

        public IReadOnlyList<TraderStockEntry> Stock => _stock;

        public IEnumerable<ItemInstance> CreateStock(
            System.Random random,
            IItemInstanceIdGenerator instanceIds)
        {
            if (instanceIds == null)
            {
                throw new System.ArgumentNullException(nameof(instanceIds));
            }

            for (int i = 0; i < _stock.Count; i++)
            {
                TraderStockEntry entry = _stock[i];

                if (entry.Item == null)
                {
                    continue;
                }

                for (int c = 0; c < entry.Count; c++)
                {
                    int seed = random.Next(int.MinValue, int.MaxValue);
                    ItemInstanceId instanceId = instanceIds.Next();
                    yield return entry.Item.CreateInstance(instanceId, entry.ItemLevel, seed, entry.Rarity);
                }
            }
        }
    }
}
