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

        [SerializeField, MinValue(0), LabelText("随机抽取权重")]
        int _weight = 100;

        public int Weight => _weight;

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
        [LabelText("本地化名称")]
        LocalizedContentReference _localizedName = new LocalizedContentReference("monsters", string.Empty);

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

        [SerializeField, LabelText("随机库存")]
        bool _randomStock;

        [SerializeField, Range(1, 256), LabelText("每批商品件数")]
        int _stockSize = 16;

        [SerializeField, LabelText("品质权重：普通 / 魔法 / 稀有 / 传奇")]
        Vector4 _rarityWeights = new Vector4(40, 40, 18, 2);

        [SerializeField, LabelText("装备词条池")]
        List<AffixDefinition> _affixPool = new List<AffixDefinition>();

        public bool RandomStock => _randomStock;
        public int StockSize => _stockSize;
        public Vector4 RarityWeights => _rarityWeights;
        public IReadOnlyList<AffixDefinition> AffixPool => _affixPool;

        public string Id => _id;

        public string DisplayName => _displayName;

        public LocalizedContentReference LocalizedName => _localizedName;

        public float BuyMultiplier => _buyMultiplier;

        public float SellMultiplier => _sellMultiplier;

        public IReadOnlyList<TraderStockEntry> Stock => _stock;

        public IEnumerable<ItemInstance> CreateStock(
            System.Random random,
            IItemInstanceIdGenerator instanceIds)
        {
            if (random == null) { throw new ArgumentNullException(nameof(random)); }
            if (instanceIds == null)
            {
                throw new System.ArgumentNullException(nameof(instanceIds));
            }

            List<string> issues = ValidateConfiguration();
            if (issues.Count > 0) { throw new InvalidOperationException(string.Join("；", issues)); }

            if (_randomStock)
            {
                long total = 0;
                foreach (TraderStockEntry entry in _stock) { total += entry.Weight; }
                for (int i = 0; i < _stockSize; i++)
                {
                    double roll = random.NextDouble() * total;
                    TraderStockEntry selected = null;
                    foreach (TraderStockEntry entry in _stock)
                    {
                        if (entry.Weight <= 0) { continue; }
                        selected = entry;
                        roll -= entry.Weight;
                        if (roll < 0) { break; }
                    }
                    ItemRarity rarity = selected.Item.IsEquipment ? PickRarity(random) : ItemRarity.Normal;
                    ItemInstance item = selected.Item.CreateInstance(instanceIds.Next(), selected.ItemLevel,
                        random.Next(int.MinValue, int.MaxValue), rarity);
                    if (selected.Item.IsEquipment && rarity != ItemRarity.Normal)
                    {
                        CraftingResult result = CraftingOperations.TryCraft(CraftOperation.RerollAffixes,
                            CraftingAffixScope.Any, item, _affixPool, random.Next(int.MinValue, int.MaxValue));
                        if (!result.Succeeded) { throw new InvalidOperationException($"{selected.Item.Id} 无法生成 {rarity} 词条"); }
                    }
                    yield return item;
                }
                yield break;
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

        ItemRarity PickRarity(System.Random random)
        {
            double total = (double)_rarityWeights.x + _rarityWeights.y + _rarityWeights.z + _rarityWeights.w;
            double roll = random.NextDouble() * total;
            for (int i = 0; i < 4; i++)
            {
                roll -= _rarityWeights[i];
                if (roll < 0) { return (ItemRarity)i; }
            }
            return ItemRarity.Unique;
        }

        public List<string> ValidateConfiguration()
        {
            var issues = new List<string>();
            if (!float.IsFinite(_buyMultiplier) || !float.IsFinite(_sellMultiplier) || _buyMultiplier <= 0 || _sellMultiplier <= 0)
            { issues.Add("交易倍率必须为有限正数"); }
            if (_stock == null || _stock.Count == 0) { issues.Add("商人候选池为空"); return issues; }
            long total = 0;
            foreach (TraderStockEntry entry in _stock)
            {
                if (entry == null || entry.Item == null) { issues.Add("商人候选不能为空"); continue; }
                if (entry.Item.ItemType == ItemType.Currency || entry.ItemLevel < 1 || entry.Count < 1 || entry.Weight < 0)
                { issues.Add($"{entry.Item.Id} 的库存配置非法"); }
                total += entry.Weight;
            }
            if (!_randomStock) { return issues; }
            if (_stockSize < 1 || _stockSize > 256 || total <= 0) { issues.Add("随机库存件数或候选权重非法"); }
            double rarityTotal = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!float.IsFinite(_rarityWeights[i]) || _rarityWeights[i] < 0) { issues.Add("品质权重必须为有限非负数"); }
                rarityTotal += _rarityWeights[i];
            }
            if (rarityTotal <= 0) { issues.Add("至少启用一种装备品质"); }
            if (_affixPool == null || _affixPool.Exists(affix => affix == null)) { issues.Add("词条池包含空引用"); }
            return issues;
        }
    }
}
