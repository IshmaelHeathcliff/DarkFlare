using UnityEngine;

namespace DarkFlare
{
    public class CraftingSystem : AbstractSystem
    {
        CraftingDefinition _definition;

        public bool IsConfigured => _definition != null;

        protected override void OnInit()
        {
        }

        public void Setup(CraftingDefinition definition)
        {
            _definition = definition;
        }

        public int GetCost(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemRarity currentRarity)
        {
            return _definition != null ? _definition.GetCost(operation, scope, currentRarity) : 0;
        }

        public CraftingEvaluation Evaluate(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemInstance item)
        {
            int cost = GetCost(operation, scope, item != null ? item.Rarity : ItemRarity.Normal);

            if (_definition == null || (_definition.Material != null
                && (_definition.Material.ItemType != ItemType.Material || !_definition.Material.IsConsumable)))
            {
                return new CraftingEvaluation(CraftingFailureReason.NotConfigured, cost);
            }

            if (item == null)
            {
                return new CraftingEvaluation(CraftingFailureReason.ItemMissing, cost);
            }

            InventoryModel inventory = this.GetModel<InventoryModel>();

            if (item.BaseDefinition == null || !item.BaseDefinition.IsEquipment)
            {
                return new CraftingEvaluation(CraftingFailureReason.NotEquipment, cost);
            }

            if (!inventory.Grid.Placements.ContainsKey(item))
            {
                return new CraftingEvaluation(CraftingFailureReason.ItemNotInInventory, cost);
            }

            CraftingFailureReason domainFailure = CraftingOperations.Evaluate(
                operation,
                scope,
                item,
                _definition.AffixPool);

            if (domainFailure != CraftingFailureReason.None)
            {
                return new CraftingEvaluation(domainFailure, cost);
            }

            int materialCost = _definition.GetMaterialCost(scope);
            int materialOwned = inventory.CountItems(_definition.Material);
            CraftingFailureReason failure = inventory.Gold < cost ? CraftingFailureReason.InsufficientGold
                : materialOwned < materialCost ? CraftingFailureReason.InsufficientMaterial : CraftingFailureReason.None;
            return new CraftingEvaluation(failure, cost, _definition.Material, materialCost, materialOwned);
        }

        public CraftingResult Craft(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemInstance item)
        {
            InventoryModel inventory = this.GetModel<InventoryModel>();
            CraftingEvaluation evaluation = Evaluate(operation, scope, item);

            if (!evaluation.IsAvailable)
            {
                ApplicationLog.Info(LogEventIds.GameplayCrafting,
                    $"[CraftingSystem] 打造不可用：{evaluation.FailureReason}，{DescribeItem(item)}");
                return CraftingResult.Failure(
                    operation,
                    scope,
                    item,
                    evaluation.FailureReason,
                    evaluation.Cost,
                    inventory.Gold);
            }

            int rootSeed = this.GetSystem<GameplayRandomSystem>().NextSeed(GameplayRandomChannel.Crafting);
            CraftingResult result = CraftingOperations.TryCraft(
                operation,
                scope,
                item,
                _definition.AffixPool,
                rootSeed);

            if (!result.Succeeded)
            {
                ApplicationLog.Info(LogEventIds.GameplayCrafting,
                    $"[CraftingSystem] 打造失败：{result.FailureReason}，根种子 {rootSeed}，{DescribeItem(item)}");
                return result.WithEconomy(evaluation.Cost, inventory.Gold);
            }

            int previousGold = inventory.Gold;
            if (!inventory.TryChangeGoldWithoutEvents(-evaluation.Cost))
            {
                item.TryApplyAffixState(
                    item.Revision,
                    result.PreviousRarity,
                    result.PreviousPrefixes,
                    result.PreviousSuffixes);
                return CraftingResult.Failure(
                    operation,
                    scope,
                    item,
                    CraftingFailureReason.CommitFailed,
                    evaluation.Cost,
                    inventory.Gold,
                    rootSeed);
            }

            if (evaluation.MaterialCost > 0)
            {
                inventory.TryConsumeWithoutEvents(evaluation.Material, evaluation.MaterialCost);
            }
            inventory.NotifyItemChanged(item, InventoryChangeType.Added);
            inventory.NotifyGoldChanged(previousGold);

            CraftingResult committed = result.WithEconomy(evaluation.Cost, inventory.Gold);
            this.SendEvent(new ItemCraftedEvent(committed));
            ApplicationLog.Info(LogEventIds.GameplayCrafting,
                $"[CraftingSystem] {operation}/{scope} 成功，根种子 {rootSeed}，花费 {evaluation.Cost}，"
                + $"{DescribeItem(item)} 当前词条 {item.Prefixes.Count + item.Suffixes.Count} 条，剩余金币 {inventory.Gold}");
            return committed;
        }

        static string DescribeItem(ItemInstance item)
        {
            if (item == null)
            {
                return "<null>";
            }

            return item.BaseDefinition != null
                ? $"{item.BaseDefinition.DisplayName}[{item.Rarity}]"
                : item.InstanceId;
        }
    }
}
