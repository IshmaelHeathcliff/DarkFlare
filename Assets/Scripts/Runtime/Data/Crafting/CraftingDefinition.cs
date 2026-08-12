using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/Data/Crafting/Crafting Definition", fileName = "CraftingDefinition")]
    public class CraftingDefinition : ScriptableObject
    {
        [SerializeField]
        [LabelText("词条池")]
        List<AffixDefinition> _affixPool = new List<AffixDefinition>();

        [SerializeField]
        [MinValue(0)]
        [LabelText("普通升魔法成本")]
        int _normalToMagicCost = 15;

        [SerializeField]
        [MinValue(0)]
        [LabelText("魔法升稀有成本")]
        int _magicToRareCost = 60;

        [SerializeField]
        [MinValue(0)]
        [LabelText("稀有升传奇成本")]
        int _rareToUniqueCost = 160;

        [SerializeField]
        [MinValue(0)]
        [LabelText("还原普通成本")]
        int _resetToNormalCost = 20;

        [SerializeField]
        [MinValue(0)]
        [LabelText("重随全部成本")]
        int _rerollAffixesCost = 40;

        [SerializeField]
        [MinValue(0)]
        [LabelText("增加词缀成本")]
        int _addAffixCost = 60;

        [SerializeField]
        [MinValue(0)]
        [LabelText("移除词缀成本")]
        int _removeAffixCost = 60;

        [SerializeField]
        [MinValue(0)]
        [LabelText("重随词缀数值成本")]
        int _rerollAffixValuesCost = 80;

        [SerializeField]
        [MinValue(1f)]
        [LabelText("精准范围倍率")]
        float _precisionMultiplier = 3f;

        public IReadOnlyList<AffixDefinition> AffixPool => _affixPool;

        public int NormalToMagicCost => _normalToMagicCost;

        public int MagicToRareCost => _magicToRareCost;

        public int RareToUniqueCost => _rareToUniqueCost;

        public int ResetToNormalCost => _resetToNormalCost;

        public int RerollAffixesCost => _rerollAffixesCost;

        public int AddAffixCost => _addAffixCost;

        public int RemoveAffixCost => _removeAffixCost;

        public int RerollAffixValuesCost => _rerollAffixValuesCost;

        public float PrecisionMultiplier => _precisionMultiplier;

        public int GetCost(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemRarity currentRarity)
        {
            int baseCost = GetBaseCost(operation, currentRarity);

            if (CraftingOperationRules.UsesAffixScope(operation) && scope != CraftingAffixScope.Any)
            {
                return Mathf.CeilToInt(baseCost * _precisionMultiplier);
            }

            return baseCost;
        }

        int GetBaseCost(CraftOperation operation, ItemRarity currentRarity)
        {
            switch (operation)
            {
                case CraftOperation.UpgradeRarity:
                    if (currentRarity == ItemRarity.Normal)
                    {
                        return _normalToMagicCost;
                    }

                    if (currentRarity == ItemRarity.Magic)
                    {
                        return _magicToRareCost;
                    }

                    return currentRarity == ItemRarity.Rare ? _rareToUniqueCost : 0;
                case CraftOperation.ResetToNormal:
                    return _resetToNormalCost;
                case CraftOperation.RerollAffixes:
                    return _rerollAffixesCost;
                case CraftOperation.AddAffix:
                    return _addAffixCost;
                case CraftOperation.RemoveAffix:
                    return _removeAffixCost;
                case CraftOperation.RerollAffixValues:
                    return _rerollAffixValuesCost;
                default:
                    return 0;
            }
        }
    }
}
