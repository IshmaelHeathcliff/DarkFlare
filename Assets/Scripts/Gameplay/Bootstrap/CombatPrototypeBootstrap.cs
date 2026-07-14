using System.Threading;
using Cysharp.Threading.Tasks;
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

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        async UniTaskVoid Start()
        {
            Debug.Log("[CombatPrototypeBootstrap] 开始预热资源");
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            await UniTask.WhenAll(
                this.GetSystem<SpawnSystem>().PreloadAsync(_playerCharacter, _playerSkill, _monsterSpawnDefinition, token),
                this.GetSystem<LootSystem>().PreloadAsync(_lootPickupPrefab, token));
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
    }
}
