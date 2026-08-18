using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public sealed class GameplaySceneConfiguration
    {
        public GameplaySceneConfiguration(
            CharacterDefinition playerCharacter,
            ProjectileSkillDefinition playerSkill,
            ItemBaseDefinition startingWeapon,
            MonsterSpawnDefinition monsterSpawnDefinition,
            AssetReferenceGameObject lootPickupPrefab,
            TraderDefinition trader,
            CraftingDefinition craftingDefinition,
            int startingGold,
            Vector3 playerSpawnPosition,
            MonsterSpawner monsterSpawner,
            CameraFollowTarget cameraFollowTarget,
            bool useFixedRandomSeed,
            int fixedRandomSeed)
        {
            PlayerCharacter = playerCharacter;
            PlayerSkill = playerSkill;
            StartingWeapon = startingWeapon;
            MonsterSpawnDefinition = monsterSpawnDefinition;
            LootPickupPrefab = lootPickupPrefab;
            Trader = trader;
            CraftingDefinition = craftingDefinition;
            StartingGold = startingGold;
            PlayerSpawnPosition = playerSpawnPosition;
            MonsterSpawner = monsterSpawner;
            CameraFollowTarget = cameraFollowTarget;
            UseFixedRandomSeed = useFixedRandomSeed;
            FixedRandomSeed = fixedRandomSeed;
        }

        public CharacterDefinition PlayerCharacter { get; }

        public ProjectileSkillDefinition PlayerSkill { get; }

        public ItemBaseDefinition StartingWeapon { get; }

        public MonsterSpawnDefinition MonsterSpawnDefinition { get; }

        public AssetReferenceGameObject LootPickupPrefab { get; }

        public TraderDefinition Trader { get; }

        public CraftingDefinition CraftingDefinition { get; }

        public int StartingGold { get; }

        public Vector3 PlayerSpawnPosition { get; }

        public MonsterSpawner MonsterSpawner { get; }

        public CameraFollowTarget CameraFollowTarget { get; }

        public bool UseFixedRandomSeed { get; }

        public int FixedRandomSeed { get; }

        public IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            RequireAsset(PlayerCharacter, "玩家定义", errors);
            RequireAddressable(PlayerCharacter != null ? PlayerCharacter.Prefab : null, "玩家 Prefab", errors);
            RequireAsset(PlayerSkill, "玩家默认技能", errors);
            RequireAddressable(PlayerSkill != null ? PlayerSkill.Prefab : null, "投射物 Prefab", errors);
            RequireAsset(StartingWeapon, "初始武器", errors);
            RequireAsset(MonsterSpawnDefinition, "怪物生成定义", errors);
            RequireAddressable(LootPickupPrefab, "掉落物 Prefab", errors);
            RequireAsset(Trader, "商人定义", errors);
            RequireAsset(CraftingDefinition, "打造定义", errors);

            if (StartingGold < 0)
            {
                errors.Add("初始金币不能为负数");
            }

            if (MonsterSpawner == null)
            {
                errors.Add("场景缺少 MonsterSpawner");
            }

            if (CameraFollowTarget == null)
            {
                errors.Add("场景缺少 CameraFollowTarget");
            }

            if (MonsterSpawnDefinition != null)
            {
                if (MonsterSpawnDefinition.SpawnInterval <= 0f)
                {
                    errors.Add("怪物生成间隔必须大于 0");
                }

                if (MonsterSpawnDefinition.MaxAliveCount <= 0)
                {
                    errors.Add("怪物最大存活数量必须大于 0");
                }

                if (MonsterSpawnDefinition.SpawnRadius <= 0f)
                {
                    errors.Add("怪物生成半径必须大于 0");
                }

                bool hasPositiveRule = false;

                for (int i = 0; i < MonsterSpawnDefinition.Rules.Count; i++)
                {
                    MonsterSpawnRule rule = MonsterSpawnDefinition.Rules[i];

                    if (rule == null)
                    {
                        errors.Add("怪物生成定义包含空规则");
                        continue;
                    }

                    if (rule.Monster == null)
                    {
                        errors.Add("怪物生成规则包含空怪物引用");
                        continue;
                    }

                    if (rule.Weight > 0)
                    {
                        hasPositiveRule = true;
                    }

                    RequireAddressable(
                        rule.Monster.Prefab,
                        $"怪物 Prefab: {rule.Monster.Id}",
                        errors);
                }

                if (!hasPositiveRule)
                {
                    errors.Add("怪物生成定义至少需要一条正权重规则");
                }
            }

            return errors;
        }

        public List<AssetReferenceSprite> CollectItemIcons()
        {
            List<AssetReferenceSprite> icons = new List<AssetReferenceSprite>();
            HashSet<string> iconGuids = new HashSet<string>();
            AddItemIcon(StartingWeapon, icons, iconGuids);

            if (Trader != null)
            {
                for (int i = 0; i < Trader.Stock.Count; i++)
                {
                    AddItemIcon(Trader.Stock[i].Item, icons, iconGuids);
                }
            }

            if (MonsterSpawnDefinition != null)
            {
                foreach (MonsterDefinition monster in MonsterSpawnDefinition.AllMonsters)
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

        static void RequireAsset(Object asset, string displayName, List<string> errors)
        {
            if (asset == null)
            {
                errors.Add($"{displayName} 未配置");
            }
        }

        static void RequireAddressable(AssetReference reference, string displayName, List<string> errors)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.AssetGUID))
            {
                errors.Add($"{displayName} 未配置有效 Addressable 引用");
            }
        }
    }
}
