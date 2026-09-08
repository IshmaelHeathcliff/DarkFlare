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
            if (pickup == null || pickup.Item == null || collector == null || !collector.IsAlive
                || collector.Team != ActorTeam.Player || !pickup.isActiveAndEnabled)
            {
                return false;
            }

            InventoryModel inventory = this.GetModel<InventoryModel>();
            ItemInstance item = pickup.Item;
            if (!inventory.TryAddItemWithoutEvents(item))
            {
                ApplicationLog.Info(LogEventIds.GameplayCombat, $"[LootSystem] 背包已满，无法拾取 {DescribeItem(pickup.Item)}");
                return false;
            }

            pickup.ClearItem();
            this.GetUtility<SessionObjectRegistry>().Release(pickup.gameObject);
            inventory.NotifyItemChanged(item, InventoryChangeType.Added);
            ApplicationLog.Info(LogEventIds.GameplayCombat, $"[LootSystem] {collector.ActorId} 拾取了 {DescribeItem(item)}，放入背包");
            return true;
        }

        public bool CanDiscardItem(ItemInstance item, CombatActor player)
        {
            return TryGetDiscardPosition(item, player, out _);
        }

        public bool DiscardItem(ItemInstance item, CombatActor player)
        {
            if (!TryGetDiscardPosition(item, player, out Vector3 position))
            {
                return false;
            }

            InventoryModel inventory = this.GetModel<InventoryModel>();
            RectInt placement = inventory.Grid.Placements[item];
            LootPickupController pickup = null;
            bool removed = false;
            try
            {
                pickup = PreparePickup(position);
                if (pickup == null)
                {
                    return false;
                }

                removed = inventory.RemoveItemWithoutEvents(item);
                if (!removed)
                {
                    this.GetUtility<SessionObjectRegistry>().Release(pickup.gameObject);
                    return false;
                }

                pickup.Init(this.GetUtility<IRunInstanceIdGenerator>().NextWorldDropId(), item);
            }
            catch (System.Exception exception)
            {
                if (pickup != null) { this.GetUtility<SessionObjectRegistry>().Release(pickup.gameObject); }
                if (removed) { inventory.TryAddItemAtWithoutEvents(item, placement.position); }
                ApplicationLog.Error(LogEventIds.GameplayCombat, $"[LootSystem] 丢弃准备失败，已保留原物品：{exception}");
                return false;
            }

            inventory.NotifyItemChanged(item, InventoryChangeType.Removed);
            ApplicationLog.Info(LogEventIds.GameplayCombat, $"[LootSystem] 丢弃 {DescribeItem(item)}，世界身份 {pickup.Id.Value}");
            return true;
        }

        bool TryGetDiscardPosition(ItemInstance item, CombatActor player, out Vector3 position)
        {
            position = default;
            if (item == null || player == null || !player.IsAlive || !player.isActiveAndEnabled
                || player.Team != ActorTeam.Player
                || !GameArchitectureProvider.TryGetCurrent(out IArchitecture current) || current.GetSystem<LootSystem>() != this
                || !GameArchitectureProvider.TryGetOwnerScope(out LifecycleScope scope) || !scope.CanAcceptWork
                || !this.GetModel<InventoryModel>().Grid.Placements.ContainsKey(item))
            {
                return false;
            }

            GameObject prefab = this.GetUtility<PrefabAssetLoader>().GetPrefab(_pickupPrefabReference);
            CircleCollider2D pickupCollider = prefab != null ? prefab.GetComponent<CircleCollider2D>() : null;
            CameraFollowTarget camera = Object.FindAnyObjectByType<CameraFollowTarget>();
            Collider2D world = camera != null ? camera.WorldBounds : null;
            if (pickupCollider == null || prefab.GetComponent<LootPickupController>() == null || world == null)
            {
                return false;
            }

            float pickupRadius = pickupCollider.radius * Mathf.Max(Mathf.Abs(prefab.transform.lossyScale.x), Mathf.Abs(prefab.transform.lossyScale.y));
            float playerRadius = 0.5f;
            foreach (Collider2D collider in player.GetComponentsInChildren<Collider2D>())
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy) { continue; }
                playerRadius = Mathf.Max(playerRadius, Vector2.Distance(player.transform.position, collider.bounds.center)
                    + ((Vector2)collider.bounds.extents).magnitude);
            }

            Vector2 center = player.transform.position;
            float distance = playerRadius + pickupRadius + 0.5f;
            for (int i = 0; i < 16; i++)
            {
                float angle = i % 8 * Mathf.PI / 4f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 candidate = center + direction * (distance + i / 8);
                Bounds bounds = world.bounds;
                if (candidate.x - pickupRadius < bounds.min.x || candidate.x + pickupRadius > bounds.max.x
                    || candidate.y - pickupRadius < bounds.min.y || candidate.y + pickupRadius > bounds.max.y) { continue; }
                bool blocked = false;
                foreach (RaycastHit2D hit in Physics2D.CircleCastAll(center, pickupRadius, direction, Vector2.Distance(center, candidate)))
                {
                    if (hit.collider != null && !hit.collider.isTrigger && !hit.collider.transform.IsChildOf(player.transform))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked) { continue; }
                position = new Vector3(candidate.x, candidate.y, player.transform.position.z);
                return true;
            }
            return false;
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
                ApplicationLog.Info(LogEventIds.GameplayCombat, $"[LootSystem] {e.Actor.ActorId} 未掉落物品，掉落种子 {lootSeed}");
                return;
            }

            ApplicationLog.Info(LogEventIds.GameplayCombat, $"[LootSystem] {e.Actor.ActorId} 生成 {DescribeItem(item)}，掉落种子 {lootSeed}，物品种子 {item.Seed}");
            SpawnPickup(
                this.GetUtility<IRunInstanceIdGenerator>().NextWorldDropId(),
                item,
                e.Actor.transform.position);
        }

        public LootPickupController SpawnRestoredPickup(
            WorldDropId worldDropId,
            ItemInstance item,
            Vector3 position)
        {
            LootPickupController controller = PreparePickup(position);
            if (controller != null) { controller.Init(worldDropId, item); }
            return controller;
        }

        LootPickupController PreparePickup(Vector3 position)
        {
            GameObject prefab = this.GetUtility<PrefabAssetLoader>().GetPrefab(_pickupPrefabReference);

            if (prefab == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayCombat, "[LootSystem] 拾取物 Prefab 未加载");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            this.GetUtility<SessionObjectRegistry>().Register(instance);
            LootPickupController controller = instance.GetComponent<LootPickupController>();

            if (controller == null)
            {
                ApplicationLog.Error(LogEventIds.GameplayCombat, "[LootSystem] 拾取物 Prefab 缺少 LootPickupController");
                this.GetUtility<SessionObjectRegistry>().Release(instance);
                return null;
            }

            return controller;
        }

        void SpawnPickup(WorldDropId worldDropId, ItemInstance item, Vector3 position)
        {
            SpawnRestoredPickup(worldDropId, item, position);
        }

        static string DescribeItem(ItemInstance item)
        {
            int modifierCount = item.CollectModifiers().Count;
            return $"{item.BaseDefinition.DisplayName}[{item.Rarity}] 词条{modifierCount}条";
        }
    }
}
