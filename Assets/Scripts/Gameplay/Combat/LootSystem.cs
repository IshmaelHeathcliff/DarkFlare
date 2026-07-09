using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public class LootSystem : AbstractSystem
    {
        readonly System.Random _random = new System.Random();

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

            Debug.Log($"[LootSystem] {collector.ActorId} 拾取了 {DescribeItem(pickup.Item)}");
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

            // 怪物/区域等级体系还没做，先固定用 1 级掉落
            ItemInstance item = lootTable.GenerateLoot(_random, System.Guid.NewGuid().ToString("N"), 1);

            if (item == null)
            {
                return;
            }

            SpawnPickup(item, e.Actor.transform.position);
        }

        void SpawnPickup(ItemInstance item, Vector3 position)
        {
            GameObject prefab = this.GetUtility<PrefabAssetLoader>().GetPrefab(_pickupPrefabReference);

            if (prefab == null)
            {
                Debug.LogError("[LootSystem] 拾取物 Prefab 未加载");
                return;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            LootPickupController controller = instance.GetComponent<LootPickupController>();
            controller.Init(item);
        }

        static string DescribeItem(ItemInstance item)
        {
            int modifierCount = item.CollectModifiers().Count;
            return $"{item.BaseDefinition.DisplayName}[{item.Rarity}] 词条{modifierCount}条";
        }
    }
}
