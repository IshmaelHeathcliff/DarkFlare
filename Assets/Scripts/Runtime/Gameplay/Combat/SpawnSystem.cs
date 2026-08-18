using System.Collections.Generic;
using System.Globalization;
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
            this.GetUtility<SessionObjectRegistry>().Register(instance);
            PlayerController controller = instance.GetComponent<PlayerController>();

            if (controller == null)
            {
                Debug.LogError("[SpawnSystem] 玩家 Prefab 缺少 PlayerController");
                this.GetUtility<SessionObjectRegistry>().Release(instance);
                return null;
            }

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
            this.GetUtility<SessionObjectRegistry>().Register(instance);
            MonsterController controller = instance.GetComponent<MonsterController>();

            if (controller == null)
            {
                Debug.LogError($"[SpawnSystem] 怪物 Prefab 缺少 MonsterController: {definition.Id}");
                this.GetUtility<SessionObjectRegistry>().Release(instance);
                return null;
            }

            int instanceSeed = random.Next(int.MinValue, int.MaxValue);
            MonsterInstanceData instanceData = definition.CreateInstanceData(
                this.GetUtility<IRunInstanceIdGenerator>().NextMonsterId(),
                instanceSeed);
            CombatResourceSnapshot previousResources = controller.Actor.Resources;
            controller.Configure(definition, instanceData);
            this.GetSystem<CombatSystem>().PublishResourceChanges(
                controller.Actor,
                previousResources,
                ActorResourceChangeReason.Configure);
            Debug.Log(
                $"[SpawnSystem] 生成怪物 {definition.Id}，生成种子 {seed}，实例根种子 {instanceSeed}，"
                + $"词条 {DescribeAffixes(instanceData)}，最终属性 {DescribeMonsterStats(instanceData.EffectiveStats)}");
            return controller;
        }

        static string DescribeAffixes(MonsterInstanceData instance)
        {
            if (instance == null || instance.Affixes.Count == 0)
            {
                return "无";
            }

            List<string> affixes = new List<string>(instance.Affixes.Count);

            for (int affixIndex = 0; affixIndex < instance.Affixes.Count; affixIndex++)
            {
                MonsterAffixInstance affix = instance.Affixes[affixIndex];
                List<string> values = new List<string>(affix.Modifiers.Count);

                for (int modifierIndex = 0; modifierIndex < affix.Modifiers.Count; modifierIndex++)
                {
                    ModifierInstance modifier = affix.Modifiers[modifierIndex];
                    string target = modifier.Operation == ModifierOperation.GainAsExtra
                        ? $"{modifier.FromDamageType}->{modifier.ToDamageType}"
                        : modifier.StatId;
                    values.Add(
                        $"{target}:{modifier.Operation}={modifier.Value.ToString("0.##", CultureInfo.InvariantCulture)}");
                }

                affixes.Add($"{affix.Definition.Id}[{string.Join(",", values)}]");
            }

            return string.Join(";", affixes);
        }

        static string DescribeMonsterStats(StatBlock stats)
        {
            return $"生命={FormatStat(stats, StatIds.MaxHealth)},"
                + $"护甲={FormatStat(stats, StatIds.Armor)},"
                + $"闪避={FormatStat(stats, StatIds.Evasion)},"
                + $"移速={FormatStat(stats, StatIds.MoveSpeed)},"
                + $"火抗={FormatStat(stats, StatIds.FireResistance)},"
                + $"冰抗={FormatStat(stats, StatIds.ColdResistance)},"
                + $"电抗={FormatStat(stats, StatIds.LightningResistance)}";
        }

        static string FormatStat(StatBlock stats, string statId)
        {
            float value = stats != null ? stats.GetValue(statId) : 0f;
            return value.ToString("0.##", CultureInfo.InvariantCulture);
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
            this.GetUtility<SessionObjectRegistry>().Register(instance);
            ProjectileController controller = instance.GetComponent<ProjectileController>();

            if (controller == null)
            {
                Debug.LogError($"[SpawnSystem] 投射物 Prefab 缺少 ProjectileController: {skill.Id}");
                this.GetUtility<SessionObjectRegistry>().Release(instance);
                return null;
            }

            controller.Init(skill, direction, attack);
            return controller;
        }
    }
}
