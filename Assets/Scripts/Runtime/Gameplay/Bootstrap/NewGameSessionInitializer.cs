using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public sealed class GameplaySceneConfigurationException : Exception
    {
        public GameplaySceneConfigurationException(IReadOnlyList<string> errors)
            : base($"场景配置无效: {string.Join("；", errors)}")
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }

    public sealed class NewGameSessionInitializer : IGameSessionInitializer, IRequiresContentCatalog
    {
        readonly GameplaySceneConfiguration _configuration;

        Transform _previousCameraTarget;
        bool _cameraBound;

        public NewGameSessionInitializer(GameplaySceneConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public string Name => "new-game";

        public async UniTask InitializeAsync(
            SessionInitializationContext context,
            CancellationToken token)
        {
            _configuration.MonsterSpawner?.PrepareForInitialization(
                _configuration.MonsterSpawnDefinition);
            IReadOnlyList<string> validationErrors = _configuration.Validate(context.ContentCatalog);

            if (validationErrors.Count > 0)
            {
                throw new GameplaySceneConfigurationException(validationErrors);
            }

            IArchitecture architecture = context.Architecture;
            architecture.GetSystem<GameplayRandomSystem>().Configure(
                _configuration.UseFixedRandomSeed,
                _configuration.FixedRandomSeed);
            Debug.Log("[NewGameSessionInitializer] 开始预热资源");
            List<AssetReferenceSprite> itemIcons = _configuration.CollectItemIcons();
            await UniTask.WhenAll(
                architecture.GetSystem<SpawnSystem>().PreloadAsync(
                    _configuration.PlayerCharacter,
                    _configuration.PlayerSkill,
                    _configuration.MonsterSpawnDefinition,
                    token),
                architecture.GetSystem<LootSystem>().PreloadAsync(
                    _configuration.LootPickupPrefab,
                    token),
                architecture.GetUtility<SpriteAssetLoader>().PreloadAsync(itemIcons, token));
            token.ThrowIfCancellationRequested();
            ValidatePreloadedAssets(architecture, itemIcons);

            CombatActor player = architecture.SendCommand(new SpawnPlayerCommand(
                _configuration.PlayerCharacter,
                _configuration.PlayerSkill,
                _configuration.PlayerSpawnPosition));

            if (player == null)
            {
                throw new InvalidOperationException("玩家生成失败");
            }

            bool weaponReady = architecture.SendCommand(
                new GrantStartingWeaponCommand(player, _configuration.StartingWeapon));

            if (!weaponReady)
            {
                throw new InvalidOperationException(
                    $"初始武器发放或装备失败: {_configuration.StartingWeapon.Id}");
            }

            TradingSystem tradingSystem = architecture.GetSystem<TradingSystem>();
            tradingSystem.SetupMerchant(_configuration.Trader);
            tradingSystem.GrantGold(_configuration.StartingGold);
            architecture.GetSystem<CraftingSystem>().Setup(_configuration.CraftingDefinition);
            _previousCameraTarget = _configuration.CameraFollowTarget.Target;
            _configuration.CameraFollowTarget.SetTarget(player.transform);
            _cameraBound = true;
            _configuration.MonsterSpawner.gameObject.SetActive(true);
            _configuration.MonsterSpawner.BeginSpawning();
            Debug.Log("[NewGameSessionInitializer] 新游戏 Session 初始化完成");
        }

        public async UniTask RollbackAsync(
            SessionInitializationContext context,
            CancellationToken token)
        {
            if (_configuration.MonsterSpawner != null)
            {
                _configuration.MonsterSpawner.PrepareForInitialization(
                    _configuration.MonsterSpawnDefinition);
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

        void ValidatePreloadedAssets(
            IArchitecture architecture,
            IReadOnlyList<AssetReferenceSprite> itemIcons)
        {
            PrefabAssetLoader prefabLoader = architecture.GetUtility<PrefabAssetLoader>();
            RequirePrefabComponent<PlayerController>(
                prefabLoader,
                _configuration.PlayerCharacter.Prefab,
                "玩家 Prefab");
            RequirePrefabComponent<ProjectileController>(
                prefabLoader,
                _configuration.PlayerSkill.Prefab,
                "投射物 Prefab");
            RequirePrefabComponent<LootPickupController>(
                prefabLoader,
                _configuration.LootPickupPrefab,
                "掉落物 Prefab");

            foreach (MonsterDefinition monster in _configuration.MonsterSpawnDefinition.AllMonsters)
            {
                RequirePrefabComponent<MonsterController>(
                    prefabLoader,
                    monster.Prefab,
                    $"怪物 Prefab: {monster.Id}");
            }

            SpriteAssetLoader spriteLoader = architecture.GetUtility<SpriteAssetLoader>();

            for (int i = 0; i < itemIcons.Count; i++)
            {
                AssetReferenceSprite icon = itemIcons[i];

                if (spriteLoader.GetSprite(icon.AssetGUID) == null)
                {
                    throw new InvalidOperationException($"物品图标预热失败: {icon.AssetGUID}");
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
    }
}
