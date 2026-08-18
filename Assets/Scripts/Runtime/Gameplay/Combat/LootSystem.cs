using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public class LootSystem : AbstractSystem
    {
        AssetReferenceGameObject _pickupPrefabReference;

        protected override void OnInit()
        {
            this.RegisterEvent<ActorDiedEvent>(OnActorDied);
        }

        public async UniTask PreloadAsync(AssetReferenceGameObject pickupPrefab, CancellationToken token)
        {
            _pickupPrefabReference = pickupPrefab;

            if (pickupPrefab == null)
            {
                return;
            }

            await this.GetUtility<PrefabAssetLoader>().PreloadAsync(new List<AssetReferenceGameObject> { pickupPrefab }, token);
        }

        public bool CollectLoot(LootPickupController pickup, CombatActor collector)
        {
            if (pickup == null || pickup.Item == null || collector == null)
            {
                return false;
            }

            if (!this.GetModel<InventoryModel>().TryAddItem(pickup.Item))
            {
                Debug.Log($"[LootSystem] 背包已满，无法拾取 {DescribeItem(pickup.Item)}");
                return false;
            }

            Debug.Log($"[LootSystem] {collector.ActorId} 拾取了 {DescribeItem(pickup.Item)}，放入背包");
            return true;
        }

        void OnActorDied(ActorDiedEvent e)
        {
            if (e.Actor == null || e.Actor.Team != ActorTeam.Monster)
            {
                return;
            }

            MonsterController monster = e.Actor.GetComponent<MonsterController>();
            LootTableDefinition lootTable = monster != null && monster.Definition != null ? monster.Definition.LootTable : null;

            if (lootTable == null)
            {
                return;
            }

            int lootSeed = this.GetSystem<GameplayRandomSystem>().NextSeed(GameplayRandomChannel.Loot);
            System.Random random = new System.Random(lootSeed);
            ItemInstanceId instanceId = this.GetUtility<IItemInstanceIdGenerator>().Next();

            // 怪物/区域等级体系还没做，先固定用 1 级掉落
            ItemInstance item = lootTable.GenerateLoot(random, instanceId, 1);

            if (item == null)
            {
                Debug.Log($"[LootSystem] {e.Actor.ActorId} 未掉落物品，掉落种子 {lootSeed}");
                return;
            }

            Debug.Log($"[LootSystem] {e.Actor.ActorId} 生成 {DescribeItem(item)}，掉落种子 {lootSeed}，物品种子 {item.Seed}");
            SpawnPickup(
                this.GetUtility<IRunInstanceIdGenerator>().NextWorldDropId(),
                item,
                e.Actor.transform.position);
        }

        void SpawnPickup(WorldDropId worldDropId, ItemInstance item, Vector3 position)
        {
            GameObject prefab = this.GetUtility<PrefabAssetLoader>().GetPrefab(_pickupPrefabReference);

            if (prefab == null)
            {
                Debug.LogError("[LootSystem] 拾取物 Prefab 未加载");
                return;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            this.GetUtility<SessionObjectRegistry>().Register(instance);
            LootPickupController controller = instance.GetComponent<LootPickupController>();

            if (controller == null)
            {
                Debug.LogError("[LootSystem] 拾取物 Prefab 缺少 LootPickupController");
                this.GetUtility<SessionObjectRegistry>().Release(instance);
                return;
            }

            controller.Init(worldDropId, item);
        }

        static string DescribeItem(ItemInstance item)
        {
            int modifierCount = item.CollectModifiers().Count;
            return $"{item.BaseDefinition.DisplayName}[{item.Rarity}] 词条{modifierCount}条";
        }
    }
}
