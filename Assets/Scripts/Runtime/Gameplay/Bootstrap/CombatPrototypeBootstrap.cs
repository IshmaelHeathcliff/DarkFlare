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

    }
}
