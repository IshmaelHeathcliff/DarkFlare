using UnityEngine;

namespace DarkFlare
{
    public enum CraftOperation
    {
        AddAffix,
        RerollAll,
        RemoveReroll,
        UpgradeAffix
    }

    public class CraftingSystem : AbstractSystem
    {
        readonly System.Random _random = new System.Random();

        CraftingDefinition _definition;

        public bool IsConfigured => _definition != null;

        protected override void OnInit()
        {
        }

        public void Setup(CraftingDefinition definition)
        {
            _definition = definition;
        }

        public int GetCost(CraftOperation operation)
        {
            if (_definition == null)
            {
                return 0;
            }

            if (operation == CraftOperation.AddAffix)
            {
                return _definition.AddAffixCost;
            }

            if (operation == CraftOperation.RerollAll)
            {
                return _definition.RerollAllCost;
            }

            if (operation == CraftOperation.RemoveReroll)
            {
                return _definition.RemoveRerollCost;
            }

            return _definition.UpgradeCost;
        }

        public bool Craft(CraftOperation operation, ItemInstance item, AffixInstance targetAffix)
        {
            if (_definition == null || item == null)
            {
                return false;
            }

            InventoryModel inventory = this.GetModel<InventoryModel>();

            if (!inventory.Grid.Placements.ContainsKey(item))
            {
                Debug.Log($"[CraftingSystem] 打造失败：物品不在玩家背包中（{DescribeItem(item)}）");
                return false;
            }

            int cost = GetCost(operation);

            if (inventory.Gold < cost)
            {
                Debug.Log($"[CraftingSystem] 打造失败：金币不足（需要 {cost}，持有 {inventory.Gold}）");
                return false;
            }

            bool applied = Apply(operation, item, targetAffix);

            if (!applied)
            {
                Debug.Log($"[CraftingSystem] 打造无效：{operation} 未能作用于 {DescribeItem(item)}");
                return false;
            }

            inventory.TrySpendGold(cost);
            this.SendEvent(new ItemCraftedEvent(operation, item));
            Debug.Log($"[CraftingSystem] {operation} 成功，花费 {cost} 金币，{DescribeItem(item)} 当前词条 {item.Prefixes.Count + item.Suffixes.Count} 条，剩余金币 {inventory.Gold}");
            return true;
        }

        bool Apply(CraftOperation operation, ItemInstance item, AffixInstance targetAffix)
        {
            if (operation == CraftOperation.AddAffix)
            {
                return CraftingOperations.AddRandomAffix(item, _definition.AffixPool, _random);
            }

            if (operation == CraftOperation.RerollAll)
            {
                return CraftingOperations.RerollAllAffixes(item, _definition.AffixPool, _random);
            }

            if (operation == CraftOperation.RemoveReroll)
            {
                return CraftingOperations.RemoveAndRerollAffix(item, targetAffix, _definition.AffixPool, _random);
            }

            return CraftingOperations.UpgradeAffix(item, targetAffix, _random);
        }

        static string DescribeItem(ItemInstance item)
        {
            return item.BaseDefinition != null ? $"{item.BaseDefinition.DisplayName}[{item.Rarity}]" : item.InstanceId;
        }
    }
}
