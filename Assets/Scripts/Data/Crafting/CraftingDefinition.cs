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
        [LabelText("添加词条成本")]
        int _addAffixCost = 20;

        [SerializeField]
        [MinValue(0)]
        [LabelText("重随全部成本")]
        int _rerollAllCost = 40;

        [SerializeField]
        [MinValue(0)]
        [LabelText("移除重随成本")]
        int _removeRerollCost = 30;

        [SerializeField]
        [MinValue(0)]
        [LabelText("提升数值成本")]
        int _upgradeCost = 25;

        public IReadOnlyList<AffixDefinition> AffixPool => _affixPool;

        public int AddAffixCost => _addAffixCost;

        public int RerollAllCost => _rerollAllCost;

        public int RemoveRerollCost => _removeRerollCost;

        public int UpgradeCost => _upgradeCost;
    }
}
