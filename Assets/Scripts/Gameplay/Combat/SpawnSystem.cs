using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public class SpawnSystem : AbstractSystem
    {
        protected override void OnInit()
        {
        }

        public async UniTask PreloadAsync(
            CharacterDefinition player,
            ProjectileSkillDefinition playerSkill,
            MonsterSpawnDefinition spawnDefinition,
            CancellationToken token)
        {
            List<AssetReferenceGameObject> references = new List<AssetReferenceGameObject>();

            if (player != null)
            {
                references.Add(player.Prefab);
            }

            if (playerSkill != null)
            {
                references.Add(playerSkill.Prefab);
            }

            if (spawnDefinition != null)
            {
                foreach (MonsterDefinition monster in spawnDefinition.AllMonsters)
                {
                    references.Add(monster.Prefab);
                }
            }

            await this.GetUtility<CombatAssetLoader>().PreloadAsync(references, token);
        }

        public CombatActor SpawnPlayer(CharacterDefinition definition, ProjectileSkillDefinition skill, Vector3 position)
        {
            GameObject prefab = this.GetUtility<CombatAssetLoader>().GetPrefab(definition.Prefab);

            if (prefab == null)
            {
                Debug.LogError("[SpawnSystem] 玩家 Prefab 未加载");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            PlayerController controller = instance.GetComponent<PlayerController>();
            controller.Configure(definition, skill);
            return controller.Actor;
        }

        public MonsterController SpawnMonster(MonsterSpawnDefinition spawnDefinition, System.Random random, Vector3 position)
        {
            MonsterDefinition definition = spawnDefinition != null ? spawnDefinition.PickMonster(random) : null;

            if (definition == null)
            {
                return null;
            }

            GameObject prefab = this.GetUtility<CombatAssetLoader>().GetPrefab(definition.Prefab);

            if (prefab == null)
            {
                Debug.LogError($"[SpawnSystem] 怪物 Prefab 未加载: {definition.Id}");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            MonsterController controller = instance.GetComponent<MonsterController>();
            controller.SetDefinition(definition);
            return controller;
        }

        public ProjectileController SpawnProjectile(ProjectileSkillDefinition skill, CombatActor owner, Vector3 position, Vector2 direction)
        {
            GameObject prefab = this.GetUtility<CombatAssetLoader>().GetPrefab(skill.Prefab);

            if (prefab == null)
            {
                Debug.LogError($"[SpawnSystem] 投射物 Prefab 未加载: {skill.Id}");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            ProjectileController controller = instance.GetComponent<ProjectileController>();
            controller.Init(owner, skill, direction);
            return controller;
        }
    }
}
