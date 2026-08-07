using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public class CombatPrototypeBootstrap : MonoBehaviour, IController
    {
        [SerializeField]
        CharacterDefinition _playerCharacter;

        [SerializeField]
        ProjectileSkillDefinition _playerSkill;

        [SerializeField]
        MonsterSpawnDefinition _monsterSpawnDefinition;

        [SerializeField]
        AssetReferenceGameObject _lootPickupPrefab;

        [SerializeField]
        TraderDefinition _trader;

        [SerializeField]
        CraftingDefinition _craftingDefinition;

        [SerializeField]
        [Min(0)]
        int _startingGold = 100;

        [SerializeField]
        Transform _playerSpawnPoint;

        [SerializeField]
        MonsterSpawner _monsterSpawner;

        [SerializeField]
        [LabelText("使用固定随机种子")]
        bool _useFixedRandomSeed;

        [SerializeField]
        [ShowIf(nameof(_useFixedRandomSeed))]
        [LabelText("固定随机种子")]
        int _fixedRandomSeed = 12345;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        async UniTaskVoid Start()
        {
            this.GetSystem<GameplayRandomSystem>().Configure(_useFixedRandomSeed, _fixedRandomSeed);
            Debug.Log("[CombatPrototypeBootstrap] 开始预热资源");
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            List<AssetReferenceSprite> itemIcons = CollectItemIcons();
            await UniTask.WhenAll(
                this.GetSystem<SpawnSystem>().PreloadAsync(_playerCharacter, _playerSkill, _monsterSpawnDefinition, token),
                this.GetSystem<LootSystem>().PreloadAsync(_lootPickupPrefab, token),
                this.GetUtility<SpriteAssetLoader>().PreloadAsync(itemIcons, token));
            Debug.Log("[CombatPrototypeBootstrap] 资源预热完成，开始生成玩家");

            Vector3 spawnPosition = _playerSpawnPoint != null ? _playerSpawnPoint.position : transform.position;
            CombatActor player = this.SendCommand(new SpawnPlayerCommand(_playerCharacter, _playerSkill, spawnPosition));
            Debug.Log($"[CombatPrototypeBootstrap] 玩家生成结果: {(player != null ? player.ActorId : "null")}");

            if (_monsterSpawner != null)
            {
                _monsterSpawner.gameObject.SetActive(true);
                Debug.Log("[CombatPrototypeBootstrap] 已启用刷怪器");
            }

            this.GetSystem<TradingSystem>().SetupMerchant(_trader);
            this.GetSystem<TradingSystem>().GrantGold(_startingGold);
            this.GetSystem<CraftingSystem>().Setup(_craftingDefinition);

            if (player != null)
            {
                SetupCamera(player.transform);
            }
        }

        void SetupCamera(Transform target)
        {
            Camera camera = Camera.main;

            if (camera == null)
            {
                Debug.LogWarning("[CombatPrototypeBootstrap] 场景中缺少 Main Camera");
                return;
            }

            CameraFollowTarget follow = camera.GetComponent<CameraFollowTarget>();

            if (follow == null)
            {
                Debug.LogWarning("[CombatPrototypeBootstrap] Main Camera 缺少 CameraFollowTarget 组件");
                return;
            }

            follow.SetTarget(target);
        }

        List<AssetReferenceSprite> CollectItemIcons()
        {
            List<AssetReferenceSprite> icons = new List<AssetReferenceSprite>();
            HashSet<string> iconGuids = new HashSet<string>();

            if (_trader != null)
            {
                for (int i = 0; i < _trader.Stock.Count; i++)
                {
                    AddItemIcon(_trader.Stock[i].Item, icons, iconGuids);
                }
            }

            if (_monsterSpawnDefinition != null)
            {
                foreach (MonsterDefinition monster in _monsterSpawnDefinition.AllMonsters)
                {
                    LootTableDefinition lootTable = monster != null ? monster.LootTable : null;

                    if (lootTable == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < lootTable.Entries.Count; i++)
                    {
                        AddItemIcon(lootTable.Entries[i].Item, icons, iconGuids);
                    }
                }
            }

            return icons;
        }

        static void AddItemIcon(
            ItemBaseDefinition item,
            List<AssetReferenceSprite> icons,
            HashSet<string> iconGuids)
        {
            AssetReferenceSprite icon = item != null ? item.Icon : null;
            string guid = icon != null ? icon.AssetGUID : string.Empty;

            if (!string.IsNullOrWhiteSpace(guid) && iconGuids.Add(guid))
            {
                icons.Add(icon);
            }
        }
    }
}
