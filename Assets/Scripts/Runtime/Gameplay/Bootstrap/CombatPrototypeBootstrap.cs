using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public class CombatPrototypeBootstrap : MonoBehaviour, IController
    {
        [SerializeField]
        ContentCatalogDefinition _contentCatalog;

        [SerializeField]
        CharacterDefinition _playerCharacter;

        [SerializeField]
        ProjectileSkillDefinition _playerSkill;

        [SerializeField]
        ItemBaseDefinition _startingWeapon;

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
        CameraFollowTarget _cameraFollowTarget;

        [SerializeField]
        [LabelText("使用固定随机种子")]
        bool _useFixedRandomSeed;

        [SerializeField]
        [ShowIf(nameof(_useFixedRandomSeed))]
        [LabelText("固定随机种子")]
        int _fixedRandomSeed = 12345;

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.RequireCurrent();
        }

        void Start()
        {
            ApplicationHost host = ApplicationHost.Current;
            LifecycleResult catalogResult = host.InstallContentCatalog(_contentCatalog);

            if (!catalogResult.IsSuccess)
            {
                Debug.LogError(
                    $"[CombatPrototypeBootstrap] 无法安装内容目录: {catalogResult.Message}",
                    this);
                return;
            }

            GameplaySceneConfiguration configuration = CreateSceneConfiguration();
            LifecycleResult request = host.BeginSceneSessionInitialization(
                gameObject.scene,
                new NewGameSessionInitializer(configuration),
                this.GetCancellationTokenOnDestroy(),
                OnInitializationCompleted);

            if (!request.IsSuccess)
            {
                Debug.LogError($"[CombatPrototypeBootstrap] 无法提交初始化请求: {request.Message}", this);
            }
        }

        public GameplaySceneConfiguration CreateSceneConfiguration()
        {
            Vector3 spawnPosition = _playerSpawnPoint != null
                ? _playerSpawnPoint.position
                : transform.position;
            return new GameplaySceneConfiguration(
                _contentCatalog,
                _playerCharacter,
                _playerSkill,
                _startingWeapon,
                _monsterSpawnDefinition,
                _lootPickupPrefab,
                _trader,
                _craftingDefinition,
                _startingGold,
                spawnPosition,
                _monsterSpawner,
                ResolveCameraFollowTarget(),
                _useFixedRandomSeed,
                _fixedRandomSeed);
        }

        CameraFollowTarget ResolveCameraFollowTarget()
        {
            if (_cameraFollowTarget != null)
            {
                return _cameraFollowTarget;
            }

            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.GetComponent<CameraFollowTarget>() : null;
        }

        void OnInitializationCompleted(LifecycleResult result)
        {
            if (result.Code == LifecycleResultCode.Cancelled)
            {
                return;
            }

            if (result.IsSuccess)
            {
                Debug.Log($"[CombatPrototypeBootstrap] {result.Message}", this);
                return;
            }

            Debug.LogError(
                $"[CombatPrototypeBootstrap] Session 初始化未完成: {result.Code} - {result.Message}\n"
                + result.Exception,
                this);
        }
    }
}
