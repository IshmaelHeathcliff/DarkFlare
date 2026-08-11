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

            await this.GetUtility<PrefabAssetLoader>().PreloadAsync(references, token);
        }

        public CombatActor SpawnPlayer(CharacterDefinition definition, ProjectileSkillDefinition skill, Vector3 position)
        {
            GameObject prefab = this.GetUtility<PrefabAssetLoader>().GetPrefab(definition.Prefab);

            if (prefab == null)
            {
                Debug.LogError("[SpawnSystem] 玩家 Prefab 未加载");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            PlayerController controller = instance.GetComponent<PlayerController>();
            CombatResourceSnapshot previousResources = controller.Actor.Resources;
            controller.Configure(definition, skill);
            this.GetSystem<CombatSystem>().PublishResourceChanges(
                controller.Actor,
                previousResources,
                ActorResourceChangeReason.Configure);
            return controller.Actor;
        }

        public MonsterController SpawnMonster(MonsterSpawnDefinition spawnDefinition, int seed, Vector3 position)
        {
            System.Random random = new System.Random(seed);
            MonsterDefinition definition = spawnDefinition != null ? spawnDefinition.PickMonster(random) : null;

            if (definition == null)
            {
                return null;
            }

            GameObject prefab = this.GetUtility<PrefabAssetLoader>().GetPrefab(definition.Prefab);

            if (prefab == null)
            {
                Debug.LogError($"[SpawnSystem] 怪物 Prefab 未加载: {definition.Id}");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            MonsterController controller = instance.GetComponent<MonsterController>();
            int instanceSeed = random.Next(int.MinValue, int.MaxValue);
            MonsterInstanceData instanceData = definition.CreateInstanceData(instanceSeed);
            CombatResourceSnapshot previousResources = controller.Actor.Resources;
            controller.Configure(definition, instanceData);
            this.GetSystem<CombatSystem>().PublishResourceChanges(
                controller.Actor,
                previousResources,
                ActorResourceChangeReason.Configure);
            Debug.Log(
                $"[SpawnSystem] 生成怪物 {definition.Id}，生成种子 {seed}，实例种子 {instanceSeed}，最大生命 {instanceData.MaxHealth:0.##}");
            return controller;
        }

        public ProjectileController SpawnProjectile(
            ProjectileSkillDefinition skill,
            Vector3 position,
            Vector2 direction,
            AttackSnapshot attack)
        {
            GameObject prefab = this.GetUtility<PrefabAssetLoader>().GetPrefab(skill.Prefab);

            if (prefab == null)
            {
                Debug.LogError($"[SpawnSystem] 投射物 Prefab 未加载: {skill.Id}");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            ProjectileController controller = instance.GetComponent<ProjectileController>();
            controller.Init(skill, direction, attack);
            return controller;
        }
    }
}
