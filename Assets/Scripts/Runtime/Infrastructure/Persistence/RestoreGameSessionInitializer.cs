using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public readonly struct SaveStateRestoredEvent
    {
        public string RunId { get; }

        public SaveStateRestoredEvent(string runId)
        {
            RunId = runId ?? string.Empty;
        }
    }

    public sealed class RestoreGameSessionInitializer : IGameSessionInitializer, IRequiresContentCatalog
    {
        readonly GameplaySceneConfiguration _configuration;
        readonly PreparedRestore _prepared;

        Transform _previousCameraTarget;
        bool _cameraBound;

        public RestoreGameSessionInitializer(
            GameplaySceneConfiguration configuration,
            PreparedRestore prepared)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _prepared = prepared ?? throw new ArgumentNullException(nameof(prepared));
        }

        public string Name => "restore-game";

        public async UniTask InitializeAsync(
            SessionInitializationContext context,
            CancellationToken token)
        {
            _configuration.MonsterSpawner?.PrepareForInitialization(_prepared.SpawnDefinition);
            IReadOnlyList<string> validationErrors = _configuration.Validate(context.ContentCatalog);

            if (validationErrors.Count > 0)
            {
                throw new GameplaySceneConfigurationException(validationErrors);
            }

            ValidateCatalogCompatibility(context.ContentCatalog);
            IArchitecture architecture = context.Architecture;
            architecture.GetModel<InventoryModel>().ConfigureCurrency(context.ContentCatalog);
            IReadOnlyList<MonsterDefinition> monsterDefinitions = _prepared.SpawnDefinition
                .AllMonsters
                .Concat(_prepared.Monsters.Select(monster => monster.Definition))
                .Where(monster => monster != null)
                .Distinct()
                .ToArray();
            List<AssetReferenceSprite> itemIcons = CollectItemIcons();
            await UniTask.WhenAll(
                architecture.GetSystem<SpawnSystem>().PreloadAsync(
                    _prepared.PlayerDefinition,
                    _prepared.PlayerSkill,
                    monsterDefinitions,
                    token),
                architecture.GetSystem<LootSystem>().PreloadAsync(
                    _configuration.LootPickupPrefab,
                    token),
                architecture.GetUtility<SpriteAssetLoader>().PreloadAsync(itemIcons, token));
            token.ThrowIfCancellationRequested();
            ValidatePreloadedAssets(architecture, monsterDefinitions, itemIcons);
            CommitPreparedState(architecture, token);
        }

        public async UniTask RollbackAsync(
            SessionInitializationContext context,
            CancellationToken token)
        {
            if (_configuration.MonsterSpawner != null)
            {
                _configuration.MonsterSpawner.PrepareForInitialization(_prepared.SpawnDefinition);
            }

            if (_cameraBound && _configuration.CameraFollowTarget != null)
            {
                _configuration.CameraFollowTarget.SetTarget(_previousCameraTarget);
                _cameraBound = false;
            }

            await context.Architecture
                .GetUtility<SessionObjectRegistry>()
                .ReleaseAllAsync();
        }

        void CommitPreparedState(IArchitecture architecture, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            architecture.GetSystem<GameplayRandomSystem>().RestoreState(_prepared.RandomState);
            architecture.RegisterUtility<IRunInstanceIdGenerator>(
                new RunInstanceIdGenerator(_prepared.InstanceIdState));
            PlayerRunStateDto playerState = _prepared.Document.Payload.Run.Player;
            PlayerController player = architecture.GetSystem<SpawnSystem>().SpawnRestoredPlayer(
                _prepared.PlayerDefinition,
                _prepared.PlayerSkill,
                ToVector(playerState.Position));

            if (player == null)
            {
                throw new InvalidOperationException("恢复玩家失败");
            }

            token.ThrowIfCancellationRequested();
            ProfileSaveData profile = _prepared.Document.Payload.Profile;
            architecture.GetModel<InventoryModel>().RestoreState(
                _prepared.RuntimeState.Inventory,
                profile.Gold);
            architecture.GetSystem<EquipmentSystem>().RestoreLoadout(
                player.Actor,
                _prepared.PlayerEquipment);
            CombatResourceSnapshot previousPlayerResources = player.Actor.Resources;
            player.RestoreRuntime(
                ToResources(playerState.Resources),
                playerState.Resources.IsAlive,
                playerState.RespawnRemainingSeconds,
                playerState.AutoCastCooldownRemainingSeconds);
            architecture.GetSystem<CombatSystem>().PublishResourceChanges(
                player.Actor,
                previousPlayerResources,
                ActorResourceChangeReason.Configure);
            MerchantInventoryDto merchant = _prepared.Document.Payload.Run.Merchant;
            architecture.GetModel<EconomyModel>().RestoreState(
                _prepared.RuntimeState.Trader,
                _prepared.RuntimeState.MerchantStock,
                merchant.BuyMultiplier,
                merchant.SellMultiplier);
            architecture.GetSystem<CraftingSystem>().Setup(_configuration.CraftingDefinition);
            List<MonsterController> restoredMonsters = new List<MonsterController>();

            for (int i = 0; i < _prepared.Monsters.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                PreparedMonsterRestore monster = _prepared.Monsters[i];
                MonsterController controller = architecture.GetSystem<SpawnSystem>()
                    .SpawnRestoredMonster(
                        monster.Definition,
                        monster.Instance,
                        ToVector(monster.State.Position),
                        ToResources(monster.State.Resources),
                        monster.State.ContactDamageCooldownRemainingSeconds);

                if (controller == null)
                {
                    throw new InvalidOperationException(
                        $"恢复怪物失败：{monster.State.InstanceId}");
                }

                restoredMonsters.Add(controller);
            }

            for (int i = 0; i < _prepared.WorldDrops.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                PreparedWorldDropRestore drop = _prepared.WorldDrops[i];
                LootPickupController controller = architecture.GetSystem<LootSystem>()
                    .SpawnRestoredPickup(drop.Id, drop.Item, drop.Position);

                if (controller == null)
                {
                    throw new InvalidOperationException($"恢复世界掉落失败：{drop.Id}");
                }
            }

            token.ThrowIfCancellationRequested();
            MonsterSpawnerStateDto spawnerState = _prepared.Document.Payload.Run.Spawner;
            _configuration.MonsterSpawner.PrepareForInitialization(_prepared.SpawnDefinition);

            for (int i = 0; i < restoredMonsters.Count; i++)
            {
                _configuration.MonsterSpawner.RegisterRestoredMonster(restoredMonsters[i]);
            }

            _previousCameraTarget = _configuration.CameraFollowTarget.Target;
            _configuration.CameraFollowTarget.SetTarget(player.transform);
            _cameraBound = true;

            if (spawnerState.IsRunning)
            {
                _configuration.MonsterSpawner.gameObject.SetActive(true);
                _configuration.MonsterSpawner.BeginSpawning(
                    spawnerState.NextSpawnRemainingSeconds);
            }

            architecture.SendEvent(new SaveStateRestoredEvent(
                _prepared.InstanceIdState.RunId.Value));
        }

        void ValidateCatalogCompatibility(ContentCatalog catalog)
        {
            SaveHeaderDto header = _prepared.Document.Header;

            if (catalog == null
                || !string.Equals(header.CatalogId, catalog.CatalogId, StringComparison.Ordinal)
                || header.ContentVersion != catalog.ContentVersion)
            {
                throw new InvalidOperationException("准备恢复的内容目录版本已经失效");
            }
        }

        List<AssetReferenceSprite> CollectItemIcons()
        {
            List<AssetReferenceSprite> result = new List<AssetReferenceSprite>();
            HashSet<string> guids = new HashSet<string>(StringComparer.Ordinal);

            foreach (ItemInstance item in _prepared.RuntimeState.Items.Values)
            {
                AssetReferenceSprite icon = item.BaseDefinition != null
                    ? item.BaseDefinition.Icon
                    : null;
                string guid = icon?.AssetGUID;

                if (!string.IsNullOrWhiteSpace(guid) && guids.Add(guid))
                {
                    result.Add(icon);
                }
            }

            return result;
        }

        void ValidatePreloadedAssets(
            IArchitecture architecture,
            IReadOnlyList<MonsterDefinition> monsterDefinitions,
            IReadOnlyList<AssetReferenceSprite> itemIcons)
        {
            PrefabAssetLoader prefabLoader = architecture.GetUtility<PrefabAssetLoader>();
            RequirePrefabComponent<PlayerController>(
                prefabLoader,
                _prepared.PlayerDefinition.Prefab,
                "玩家 Prefab");
            RequirePrefabComponent<ProjectileController>(
                prefabLoader,
                _prepared.PlayerSkill.Prefab,
                "投射物 Prefab");
            RequirePrefabComponent<LootPickupController>(
                prefabLoader,
                _configuration.LootPickupPrefab,
                "掉落物 Prefab");

            for (int i = 0; i < monsterDefinitions.Count; i++)
            {
                RequirePrefabComponent<MonsterController>(
                    prefabLoader,
                    monsterDefinitions[i].Prefab,
                    $"怪物 Prefab: {monsterDefinitions[i].Id}");
            }

            SpriteAssetLoader spriteLoader = architecture.GetUtility<SpriteAssetLoader>();

            for (int i = 0; i < itemIcons.Count; i++)
            {
                if (spriteLoader.GetSprite(itemIcons[i].AssetGUID) == null)
                {
                    throw new InvalidOperationException(
                        $"物品图标预热失败: {itemIcons[i].AssetGUID}");
                }
            }
        }

        static void RequirePrefabComponent<T>(
            PrefabAssetLoader loader,
            AssetReferenceGameObject reference,
            string displayName)
            where T : Component
        {
            GameObject prefab = loader.GetPrefab(reference);

            if (prefab == null)
            {
                throw new InvalidOperationException($"{displayName} 预热失败");
            }

            if (prefab.GetComponent<T>() == null)
            {
                throw new InvalidOperationException(
                    $"{displayName} 缺少必需组件 {typeof(T).Name}");
            }
        }

        static Vector3 ToVector(Vector3Dto value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        static CombatResourceSnapshot ToResources(CombatResourceStateDto value)
        {
            return new CombatResourceSnapshot(
                value.CurrentHealth,
                value.MaxHealth,
                value.CurrentMana,
                value.MaxMana);
        }
    }
}
