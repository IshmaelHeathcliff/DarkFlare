using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DarkFlare.Editor
{
    public enum ContentValidationSeverity
    {
        Error,
        Warning
    }

    [Serializable]
    public sealed class ContentValidationIssue
    {
        public ContentValidationSeverity Severity { get; }

        public UnityEngine.Object Asset { get; }

        public string AssetPath { get; }

        public string Message { get; }

        public ContentValidationIssue(
            ContentValidationSeverity severity,
            UnityEngine.Object asset,
            string message)
        {
            Severity = severity;
            Asset = asset;
            AssetPath = asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty;
            Message = message;
        }
    }

    public static class ContentConfigurationValidator
    {
        const string PresetRoot = "Assets/Data/Preset";

        static readonly Regex StableIdPattern = new Regex(
            "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly string[] ExpectedTagIds =
        {
            "damage",
            "weapon",
            "armor",
            "ring",
            "sword",
            "axe",
            "physical",
            "fire",
            "cold",
            "lightning",
            "chaos",
            "projectile",
            "melee",
            "monster",
        };

        static readonly Dictionary<string, ExpectedTagMetadata> ExpectedTags =
            new Dictionary<string, ExpectedTagMetadata>(StringComparer.Ordinal)
            {
                { "damage", TagMetadata(CombatTagDomain.Damage, CombatTagUsage.Reserved) },
                { "weapon", TagMetadata(CombatTagDomain.ItemSpawn, CombatTagUsage.Active) },
                { "armor", TagMetadata(CombatTagDomain.ItemSpawn, CombatTagUsage.Active) },
                { "ring", TagMetadata(CombatTagDomain.ItemSpawn, CombatTagUsage.Active) },
                { "sword", TagMetadata(CombatTagDomain.ItemSpawn, CombatTagUsage.Reserved) },
                { "axe", TagMetadata(CombatTagDomain.ItemSpawn, CombatTagUsage.Reserved) },
                { "physical", TagMetadata(CombatTagDomain.Damage, CombatTagUsage.Active) },
                { "fire", TagMetadata(CombatTagDomain.Damage, CombatTagUsage.Reserved) },
                { "cold", TagMetadata(CombatTagDomain.Damage, CombatTagUsage.Reserved) },
                { "lightning", TagMetadata(CombatTagDomain.Damage, CombatTagUsage.Reserved) },
                { "chaos", TagMetadata(CombatTagDomain.Damage, CombatTagUsage.Reserved) },
                { "projectile", TagMetadata(CombatTagDomain.Skill, CombatTagUsage.Reserved) },
                { "melee", TagMetadata(CombatTagDomain.Skill, CombatTagUsage.Reserved) },
                { "monster", TagMetadata(CombatTagDomain.Actor, CombatTagUsage.Reserved) },
            };

        static readonly HashSet<string> ForbiddenItemCustomTagIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "weapon",
            "armor",
            "ring",
            "sword",
            "axe",
        };

        static readonly HashSet<string> DerivedDamageTagIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "damage",
            "physical",
            "fire",
            "cold",
            "lightning",
            "chaos",
        };

        static readonly HashSet<string> DerivedSkillTagIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "projectile",
        };

        static readonly HashSet<string> DerivedActorTagIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "monster",
        };

        static readonly string[] ExpectedAffixIds =
        {
            "sharp",
            "tempered",
            "flame_touched",
            "frost_touched",
            "healthy",
            "reinforced",
            "of_power",
            "of_endurance",
            "of_fire_guard",
            "of_cold_guard",
            "of_lightning_guard",
            "of_chaos_guard",
        };

        static readonly string[] ExpectedItemIds =
        {
            "great_sword",
            "war_axe",
            "leather_armor",
            "plate_armor",
            "iron_ring",
            "jade_ring",
            "obsidian_ring",
        };

        static readonly string[] ExpectedMonsterIds =
        {
            "wasteland_wraith",
            "razor_hound",
            "iron_husk",
        };

        public static List<ContentValidationIssue> Scan()
        {
            List<ContentValidationIssue> issues = new List<ContentValidationIssue>();
            List<TagDefinition> tags = LoadAssets<TagDefinition>($"{PresetRoot}/Tags");
            List<AffixDefinition> affixes = LoadAssets<AffixDefinition>($"{PresetRoot}/Affixes");
            List<ItemBaseDefinition> items = LoadAssets<ItemBaseDefinition>($"{PresetRoot}/Items");
            List<CharacterDefinition> characters = LoadAssets<CharacterDefinition>($"{PresetRoot}/Actors");
            List<ProjectileSkillDefinition> skills = LoadAssets<ProjectileSkillDefinition>($"{PresetRoot}/Skills");
            List<MonsterDefinition> monsters = LoadAssets<MonsterDefinition>($"{PresetRoot}/Monsters");
            List<MonsterSpawnDefinition> spawnDefinitions = LoadAssets<MonsterSpawnDefinition>($"{PresetRoot}/Monsters");
            List<LootTableDefinition> lootTables = LoadAssets<LootTableDefinition>($"{PresetRoot}/Loot");
            List<TraderDefinition> traders = LoadAssets<TraderDefinition>($"{PresetRoot}/Traders");
            List<CraftingDefinition> craftingDefinitions = LoadAssets<CraftingDefinition>($"{PresetRoot}/Crafting");

            ValidateExpectedIds(tags, ExpectedTagIds, tag => tag.Id, "标签", false, issues);
            ValidateExpectedIds(affixes, ExpectedAffixIds, affix => affix.Id, "词条", true, issues);
            ValidateExpectedIds(items, ExpectedItemIds, item => item.Id, "装备", true, issues);
            ValidateExpectedIds(monsters, ExpectedMonsterIds, monster => monster.Id, "怪物", true, issues);
            ValidateTags(tags, issues);
            ValidateAffixes(affixes, items, issues);
            ValidateItems(items, affixes, issues);
            ValidateCharacters(characters, issues);
            ValidateSkills(skills, issues);
            ValidateMonsters(monsters, issues);
            ValidateSpawnDefinitions(spawnDefinitions, monsters, issues);
            ValidateLootTables(lootTables, items, affixes, issues);
            ValidateTraders(traders, items, issues);
            ValidateCrafting(craftingDefinitions, affixes, issues);

            List<string> physicsIssues = GameplayPhysicsConfigurationValidator.Validate();

            for (int i = 0; i < physicsIssues.Count; i++)
            {
                AddError(issues, null, physicsIssues[i]);
            }

            return issues
                .OrderBy(issue => issue.Severity)
                .ThenBy(issue => issue.AssetPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToList();
        }

        static List<T> LoadAssets<T>(string root) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root });
            List<T> assets = new List<T>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));

                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        static void ValidateExpectedIds<T>(
            IReadOnlyList<T> assets,
            IReadOnlyList<string> expectedIds,
            Func<T, string> getId,
            string label,
            bool requireExactCount,
            List<ContentValidationIssue> issues) where T : UnityEngine.Object
        {
            Dictionary<string, T> byId = new Dictionary<string, T>(StringComparer.Ordinal);

            for (int i = 0; i < assets.Count; i++)
            {
                T asset = assets[i];
                string id = getId(asset);

                if (string.IsNullOrWhiteSpace(id))
                {
                    AddError(issues, asset, $"{label}稳定 ID 不能为空");
                    continue;
                }

                if (!StableIdPattern.IsMatch(id))
                {
                    AddError(issues, asset, $"{label}稳定 ID 必须使用小写 snake_case：{id}");
                }

                if (byId.TryGetValue(id, out T duplicate))
                {
                    AddError(issues, asset, $"{label}稳定 ID 重复：{id}，首次出现于 {AssetDatabase.GetAssetPath(duplicate)}");
                }
                else
                {
                    byId.Add(id, asset);
                }
            }

            for (int i = 0; i < expectedIds.Count; i++)
            {
                if (!byId.ContainsKey(expectedIds[i]))
                {
                    AddError(issues, null, $"缺少正式{label}：{expectedIds[i]}");
                }
            }

            if (requireExactCount && assets.Count != expectedIds.Count)
            {
                AddError(issues, null, $"正式{label}数量应为 {expectedIds.Count}，当前为 {assets.Count}");
            }
        }

        static void ValidateTags(IReadOnlyList<TagDefinition> tags, List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                TagDefinition tag = tags[i];

                if (string.IsNullOrWhiteSpace(tag.DisplayName))
                {
                    AddError(issues, tag, "标签中文名不能为空");
                }

                if (string.IsNullOrWhiteSpace(tag.Description))
                {
                    AddError(issues, tag, "标签说明不能为空");
                }

                if (!ExpectedTags.TryGetValue(tag.Id, out ExpectedTagMetadata expected))
                {
                    continue;
                }

                if (tag.Domain != expected.Domain)
                {
                    AddError(issues, tag, $"标签 Domain 应为 {expected.Domain}，当前为 {tag.Domain}");
                }

                if (tag.Usage != expected.Usage)
                {
                    AddError(issues, tag, $"标签使用状态应为 {expected.Usage}，当前为 {tag.Usage}");
                }
            }
        }

        static void ValidateAffixes(
            IReadOnlyList<AffixDefinition> affixes,
            IReadOnlyList<ItemBaseDefinition> items,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < affixes.Count; i++)
            {
                AffixDefinition affix = affixes[i];

                if (string.IsNullOrWhiteSpace(affix.DisplayName))
                {
                    AddError(issues, affix, "词条中文名不能为空");
                }

                if (string.IsNullOrWhiteSpace(affix.GroupId))
                {
                    AddError(issues, affix, "词条组不能为空");
                }

                if (!StableIdPattern.IsMatch(affix.GroupId))
                {
                    AddError(issues, affix, $"词条组必须使用小写 snake_case：{affix.GroupId}");
                }

                if (affix.Weight <= 0)
                {
                    AddError(issues, affix, "词条权重必须大于 0");
                }

                if (affix.SpawnQuery == null || !affix.SpawnQuery.HasConditions)
                {
                    AddError(issues, affix, "词条必须使用 SpawnQuery 声明物品兼容条件");
                }
                else
                {
                    ValidateQuery(
                        affix,
                        affix.SpawnQuery,
                        "SpawnQuery",
                        CombatTagScope.SourceItem,
                        issues);
                }

                if (affix.SpawnQuery == null || affix.SpawnQuery.RequiredAny.Count == 0)
                {
                    AddError(issues, affix, "SpawnQuery 至少需要一个 RequiredAny 物品标签");
                }

                ValidateTagReferences(
                    affix,
                    affix.ModifierTags,
                    CombatTagDomain.Modifier,
                    "ModifierTags",
                    null,
                    issues);
                ValidateLegacyAffixFields(affix, issues);

                if (affix.Modifiers.Count == 0)
                {
                    AddError(issues, affix, "词条修改器不能为空");
                }

                List<string> baseIssues = EquipmentConfigurationValidator.Validate(affix);

                for (int issueIndex = 0; issueIndex < baseIssues.Count; issueIndex++)
                {
                    AddError(issues, affix, baseIssues[issueIndex]);
                }

                for (int modifierIndex = 0; modifierIndex < affix.Modifiers.Count; modifierIndex++)
                {
                    ValidateModifier(affix, affix.Modifiers[modifierIndex], modifierIndex, issues);
                }

                bool hasCompatibleItem = false;

                for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    if (affix.CanApplyTo(items[itemIndex].RuntimeTags, 1))
                    {
                        hasCompatibleItem = true;
                        break;
                    }
                }

                if (!hasCompatibleItem)
                {
                    AddError(issues, affix, "词条没有任何兼容装备");
                }
            }
        }

        static void ValidateModifier(
            AffixDefinition affix,
            StatModifierDefinition modifier,
            int modifierIndex,
            List<ContentValidationIssue> issues)
        {
            if (modifier == null)
            {
                AddError(issues, affix, $"修改器 {modifierIndex} 为空");
                return;
            }

            if (!IsSupportedOperation(modifier.Operation))
            {
                AddError(issues, affix, $"修改器 {modifierIndex} 使用未生效 Operation：{modifier.Operation}");
            }

            if (!IsSupportedScope(modifier.Scope))
            {
                AddError(issues, affix, $"修改器 {modifierIndex} 使用未生效 Scope：{modifier.Scope}");
            }

            if (modifier.ValueRange.x < 0f || modifier.ValueRange.y < 0f)
            {
                AddError(issues, affix, $"修改器 {modifierIndex} 数值不能为负数");
            }

            if (modifier.ValueRange.x > modifier.ValueRange.y)
            {
                AddError(issues, affix, $"修改器 {modifierIndex} 数值范围上下限倒置");
            }

            if ((modifier.Operation == ModifierOperation.Conversion
                 || modifier.Operation == ModifierOperation.GainAsExtra)
                && modifier.FromDamageType == modifier.ToDamageType)
            {
                AddError(issues, affix, $"修改器 {modifierIndex} 的来源与目标伤害类型不能相同");
            }

            if (modifier.Condition != null && modifier.Condition.HasConditions)
            {
                ValidateQuery(
                    affix,
                    modifier.Condition,
                    $"修改器 {modifierIndex} Condition",
                    null,
                    issues);
            }
        }

        static void ValidateItems(
            IReadOnlyList<ItemBaseDefinition> items,
            IReadOnlyList<AffixDefinition> affixes,
            List<ContentValidationIssue> issues)
        {
            HashSet<string> iconGuids = new HashSet<string>(StringComparer.Ordinal);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            for (int i = 0; i < items.Count; i++)
            {
                ItemBaseDefinition item = items[i];

                if (string.IsNullOrWhiteSpace(item.DisplayName))
                {
                    AddError(issues, item, "装备中文名不能为空");
                }

                ValidateTagReferences(
                    item,
                    item.Tags,
                    CombatTagDomain.ItemSpawn,
                    "自定义物品生成标签",
                    ForbiddenItemCustomTagIds,
                    issues);
                ValidateDamageRollTags(item, item.BaseDamages, "基础伤害", issues);
                ValidateItemIcon(item, iconGuids, settings, issues);

                List<string> baseIssues = EquipmentConfigurationValidator.Validate(item);

                for (int issueIndex = 0; issueIndex < baseIssues.Count; issueIndex++)
                {
                    AddError(issues, item, baseIssues[issueIndex]);
                }

                int candidateCount = 0;

                for (int affixIndex = 0; affixIndex < affixes.Count; affixIndex++)
                {
                    if (affixes[affixIndex].CanApplyTo(item.RuntimeTags, 1))
                    {
                        candidateCount++;
                    }
                }

                if (candidateCount < 4)
                {
                    AddError(issues, item, $"兼容词条不足 4 个，当前为 {candidateCount}");
                }
            }
        }

        static void ValidateItemIcon(
            ItemBaseDefinition item,
            HashSet<string> iconGuids,
            AddressableAssetSettings settings,
            List<ContentValidationIssue> issues)
        {
            string guid = item.Icon != null ? item.Icon.AssetGUID : string.Empty;

            if (string.IsNullOrWhiteSpace(guid))
            {
                AddError(issues, item, "正式装备图标不能为空");
                return;
            }

            if (!iconGuids.Add(guid))
            {
                AddError(issues, item, $"正式装备图标必须唯一：{guid}");
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                AddError(issues, item, $"装备图标不是 Sprite 或无法加载：{guid}");
                return;
            }

            if (settings == null || settings.FindAssetEntry(guid) == null)
            {
                AddError(issues, item, "装备图标未加入 Addressables");
            }

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                AddError(issues, item, "装备图标缺少 TextureImporter");
                return;
            }

            if (importer.textureType != TextureImporterType.Sprite
                || importer.filterMode != FilterMode.Point
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || Math.Abs(importer.spritePixelsPerUnit - 64f) > 0.01f)
            {
                AddError(issues, item, "装备图标导入规格必须为 Sprite、64 PPU、Point、无压缩");
            }
        }

        static void ValidateSkills(
            IReadOnlyList<ProjectileSkillDefinition> skills,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                ProjectileSkillDefinition skill = skills[i];
                ValidateTagReferences(
                    skill,
                    skill.Tags,
                    CombatTagDomain.Skill,
                    "自定义技能标签",
                    DerivedSkillTagIds,
                    issues);
                ValidateDamageRollTags(skill, skill.BaseDamages, "基础伤害", issues);

                List<string> baseIssues = RandomizationConfigurationValidator.Validate(skill);

                for (int issueIndex = 0; issueIndex < baseIssues.Count; issueIndex++)
                {
                    AddError(issues, skill, baseIssues[issueIndex]);
                }
            }
        }

        static void ValidateCharacters(
            IReadOnlyList<CharacterDefinition> characters,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                CharacterDefinition character = characters[i];
                List<string> baseIssues = RandomizationConfigurationValidator.Validate(character);

                for (int issueIndex = 0; issueIndex < baseIssues.Count; issueIndex++)
                {
                    AddError(issues, character, baseIssues[issueIndex]);
                }
            }
        }

        static void ValidateMonsters(
            IReadOnlyList<MonsterDefinition> monsters,
            List<ContentValidationIssue> issues)
        {
            HashSet<string> prefabGuids = new HashSet<string>(StringComparer.Ordinal);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterDefinition monster = monsters[i];

                if (string.IsNullOrWhiteSpace(monster.DisplayName))
                {
                    AddError(issues, monster, "怪物中文名不能为空");
                }

                ValidateTagReferences(
                    monster,
                    monster.Tags,
                    CombatTagDomain.Actor,
                    "自定义角色标签",
                    DerivedActorTagIds,
                    issues);
                ValidateDamageRollTags(monster, monster.ContactDamages, "碰撞伤害", issues);

                if (monster.LootTable == null)
                {
                    AddError(issues, monster, "怪物掉落表不能为空");
                }

                List<string> baseIssues = RandomizationConfigurationValidator.Validate(monster);

                for (int issueIndex = 0; issueIndex < baseIssues.Count; issueIndex++)
                {
                    AddError(issues, monster, baseIssues[issueIndex]);
                }

                string guid = monster.Prefab != null ? monster.Prefab.AssetGUID : string.Empty;

                if (string.IsNullOrWhiteSpace(guid))
                {
                    AddError(issues, monster, "怪物 Prefab 引用不能为空");
                    continue;
                }

                if (!prefabGuids.Add(guid))
                {
                    AddError(issues, monster, $"怪物 Prefab GUID 未独立：{guid}");
                }

                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    AddError(issues, monster, $"无法加载怪物 Prefab：{guid}");
                    continue;
                }

                if (prefab.GetComponent<MonsterController>() == null
                    || prefab.GetComponent<CombatActor>() == null
                    || prefab.GetComponent<Animator>() == null
                    || prefab.GetComponent<Rigidbody2D>() == null
                    || prefab.GetComponent<Collider2D>() == null)
                {
                    AddError(issues, prefab, "怪物 Prefab 缺少运行组件或 Animator");
                }

                if (settings == null || settings.FindAssetEntry(guid) == null)
                {
                    AddError(issues, prefab, "怪物 Prefab 未加入 Addressables");
                }
            }
        }

        static void ValidateSpawnDefinitions(
            IReadOnlyList<MonsterSpawnDefinition> spawnDefinitions,
            IReadOnlyList<MonsterDefinition> monsters,
            List<ContentValidationIssue> issues)
        {
            if (spawnDefinitions.Count != 1)
            {
                AddError(issues, null, $"正式刷怪表应为 1 张，当前为 {spawnDefinitions.Count}");
            }

            for (int i = 0; i < spawnDefinitions.Count; i++)
            {
                MonsterSpawnDefinition definition = spawnDefinitions[i];

                if (definition.Rules.Count == 0)
                {
                    AddError(issues, definition, "怪物池不能为空");
                    continue;
                }

                HashSet<MonsterDefinition> covered = new HashSet<MonsterDefinition>();

                for (int ruleIndex = 0; ruleIndex < definition.Rules.Count; ruleIndex++)
                {
                    MonsterSpawnRule rule = definition.Rules[ruleIndex];

                    if (rule == null || rule.Monster == null || rule.Weight <= 0)
                    {
                        AddError(issues, definition, $"刷怪规则 {ruleIndex} 缺少怪物或正权重");
                        continue;
                    }

                    if (!covered.Add(rule.Monster))
                    {
                        AddError(issues, definition, $"刷怪规则重复引用：{rule.Monster.Id}");
                    }
                }

                ValidateCoverage(definition, covered, monsters, "刷怪池缺少怪物", issues);
            }
        }

        static void ValidateLootTables(
            IReadOnlyList<LootTableDefinition> lootTables,
            IReadOnlyList<ItemBaseDefinition> items,
            IReadOnlyList<AffixDefinition> affixes,
            List<ContentValidationIssue> issues)
        {
            if (lootTables.Count != 3)
            {
                AddError(issues, null, $"正式掉落表应为 3 张，当前为 {lootTables.Count}");
            }

            for (int i = 0; i < lootTables.Count; i++)
            {
                LootTableDefinition table = lootTables[i];
                List<string> baseIssues = RandomizationConfigurationValidator.Validate(table);

                for (int issueIndex = 0; issueIndex < baseIssues.Count; issueIndex++)
                {
                    AddError(issues, table, baseIssues[issueIndex]);
                }

                HashSet<ItemBaseDefinition> coveredItems = new HashSet<ItemBaseDefinition>();

                for (int entryIndex = 0; entryIndex < table.Entries.Count; entryIndex++)
                {
                    LootTableEntry entry = table.Entries[entryIndex];

                    if (entry != null && entry.Item != null && entry.Weight > 0)
                    {
                        coveredItems.Add(entry.Item);
                    }
                }

                ValidateCoverage(table, coveredItems, items, "掉落池缺少装备", issues);
                ValidateCoverage(
                    table,
                    new HashSet<AffixDefinition>(table.AffixPool),
                    affixes,
                    "掉落表词条池缺少词条",
                    issues);
            }
        }

        static void ValidateTraders(
            IReadOnlyList<TraderDefinition> traders,
            IReadOnlyList<ItemBaseDefinition> items,
            List<ContentValidationIssue> issues)
        {
            if (traders.Count != 1)
            {
                AddError(issues, null, $"正式商人配置应为 1 个，当前为 {traders.Count}");
            }

            for (int i = 0; i < traders.Count; i++)
            {
                TraderDefinition trader = traders[i];
                HashSet<ItemBaseDefinition> covered = new HashSet<ItemBaseDefinition>();

                for (int stockIndex = 0; stockIndex < trader.Stock.Count; stockIndex++)
                {
                    TraderStockEntry entry = trader.Stock[stockIndex];

                    if (entry == null || entry.Item == null)
                    {
                        AddError(issues, trader, $"商人库存 {stockIndex} 为空");
                        continue;
                    }

                    if (entry.Rarity != ItemRarity.Normal || entry.ItemLevel != 1 || entry.Count != 1)
                    {
                        AddError(issues, trader, $"商人库存 {stockIndex} 必须为普通、1 级、1 件");
                    }

                    covered.Add(entry.Item);
                }

                ValidateCoverage(trader, covered, items, "商人库存缺少装备", issues);
            }
        }

        static void ValidateCrafting(
            IReadOnlyList<CraftingDefinition> craftingDefinitions,
            IReadOnlyList<AffixDefinition> affixes,
            List<ContentValidationIssue> issues)
        {
            if (craftingDefinitions.Count != 1)
            {
                AddError(issues, null, $"正式打造配置应为 1 个，当前为 {craftingDefinitions.Count}");
            }

            for (int i = 0; i < craftingDefinitions.Count; i++)
            {
                CraftingDefinition definition = craftingDefinitions[i];
                ValidateCoverage(
                    definition,
                    new HashSet<AffixDefinition>(definition.AffixPool),
                    affixes,
                    "打造池缺少词条",
                    issues);
            }
        }

        static void ValidateCoverage<T>(
            UnityEngine.Object owner,
            HashSet<T> covered,
            IReadOnlyList<T> expected,
            string message,
            List<ContentValidationIssue> issues) where T : UnityEngine.Object
        {
            for (int i = 0; i < expected.Count; i++)
            {
                if (!covered.Contains(expected[i]))
                {
                    AddError(issues, owner, $"{message}：{expected[i].name}");
                }
            }
        }

        static ExpectedTagMetadata TagMetadata(CombatTagDomain domain, CombatTagUsage usage)
        {
            return new ExpectedTagMetadata(domain, usage);
        }

        static void ValidateTagReferences(
            UnityEngine.Object owner,
            IReadOnlyList<TagDefinition> tags,
            CombatTagDomain expectedDomain,
            string label,
            ISet<string> forbiddenIds,
            List<ContentValidationIssue> issues)
        {
            if (tags == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < tags.Count; i++)
            {
                TagDefinition tag = tags[i];

                if (tag == null)
                {
                    AddError(issues, owner, $"{label} {i} 为空引用");
                    continue;
                }

                if (!seen.Add(tag.Id))
                {
                    AddError(issues, owner, $"{label} 重复引用标签：{tag.Id}");
                }

                if (tag.Domain != expectedDomain)
                {
                    AddError(
                        issues,
                        owner,
                        $"{label} 的 {tag.Id} 属于 {tag.Domain}，此处只允许 {expectedDomain}");
                }

                if (tag.Usage == CombatTagUsage.Reserved)
                {
                    AddError(issues, owner, $"{label} 不得消费预留标签：{tag.Id}");
                }

                if (forbiddenIds != null && forbiddenIds.Contains(tag.Id))
                {
                    AddError(issues, owner, $"{label} 不得手填可派生或当前预留的标签：{tag.Id}");
                }
            }
        }

        static void ValidateDamageRollTags(
            UnityEngine.Object owner,
            IReadOnlyList<DamageRollDefinition> damages,
            string label,
            List<ContentValidationIssue> issues)
        {
            if (damages == null)
            {
                return;
            }

            for (int i = 0; i < damages.Count; i++)
            {
                DamageRollDefinition damage = damages[i];

                if (damage == null)
                {
                    AddError(issues, owner, $"{label} {i} 为空");
                    continue;
                }

                ValidateTagReferences(
                    owner,
                    damage.Tags,
                    CombatTagDomain.Damage,
                    $"{label} {i} 自定义伤害标签",
                    DerivedDamageTagIds,
                    issues);
            }
        }

        static void ValidateQuery(
            UnityEngine.Object owner,
            TagQueryDefinition query,
            string label,
            CombatTagScope? requiredScope,
            List<ContentValidationIssue> issues)
        {
            if (query == null)
            {
                AddError(issues, owner, $"{label} 不能为空");
                return;
            }

            if (query.HasConditions && query.ScopeMask == CombatTagScope.None)
            {
                AddError(issues, owner, $"{label} 有条件但 Scope 为空");
            }

            if (requiredScope.HasValue && query.ScopeMask != requiredScope.Value)
            {
                AddError(issues, owner, $"{label} Scope 必须为 {requiredScope.Value}，当前为 {query.ScopeMask}");
            }

            Dictionary<string, string> required = new Dictionary<string, string>(StringComparer.Ordinal);
            ValidateQueryList(owner, query, query.RequiredAll, $"{label}.RequiredAll", required, issues);
            ValidateQueryList(owner, query, query.RequiredAny, $"{label}.RequiredAny", required, issues);

            HashSet<string> blocked = new HashSet<string>(StringComparer.Ordinal);
            ValidateQueryList(owner, query, query.BlockedAny, $"{label}.BlockedAny", null, issues, blocked);

            foreach (KeyValuePair<string, string> pair in required)
            {
                if (blocked.Contains(pair.Key))
                {
                    AddError(issues, owner, $"{label} 的 {pair.Key} 同时是必要条件和阻止条件");
                }
            }
        }

        static void ValidateQueryList(
            UnityEngine.Object owner,
            TagQueryDefinition query,
            IReadOnlyList<TagDefinition> tags,
            string label,
            Dictionary<string, string> requiredLocations,
            List<ContentValidationIssue> issues,
            HashSet<string> destination = null)
        {
            if (tags == null)
            {
                return;
            }

            HashSet<string> local = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < tags.Count; i++)
            {
                TagDefinition tag = tags[i];

                if (tag == null)
                {
                    AddError(issues, owner, $"{label} {i} 为空引用");
                    continue;
                }

                if (!local.Add(tag.Id))
                {
                    AddError(issues, owner, $"{label} 重复引用标签：{tag.Id}");
                }

                if (requiredLocations != null
                    && requiredLocations.TryGetValue(tag.Id, out string previousLocation))
                {
                    AddError(issues, owner, $"{tag.Id} 同时出现在 {previousLocation} 与 {label}");
                }
                else
                {
                    requiredLocations?.Add(tag.Id, label);
                }

                destination?.Add(tag.Id);

                if (!CombatTagScopeRules.SupportsDomain(query.ScopeMask, tag.Domain))
                {
                    AddError(
                        issues,
                        owner,
                        $"{label} 的 {tag.Id} 属于 {tag.Domain}，与 Scope {query.ScopeMask} 不相容");
                }

                if (tag.Usage == CombatTagUsage.Reserved)
                {
                    AddError(issues, owner, $"{label} 不得消费预留标签：{tag.Id}");
                }
            }
        }

        static void ValidateLegacyAffixFields(
            AffixDefinition affix,
            List<ContentValidationIssue> issues)
        {
            SerializedObject serialized = new SerializedObject(affix);
            ValidateLegacyList(serialized.FindProperty("_allowedItemTags"), affix, "旧 AllowedItemTags", issues);
            ValidateLegacyList(serialized.FindProperty("_blockedItemTags"), affix, "旧 BlockedItemTags", issues);

            SerializedProperty modifiers = serialized.FindProperty("_modifiers");

            if (modifiers == null || !modifiers.isArray)
            {
                return;
            }

            for (int i = 0; i < modifiers.arraySize; i++)
            {
                SerializedProperty modifier = modifiers.GetArrayElementAtIndex(i);
                ValidateLegacyList(
                    modifier.FindPropertyRelative("_requiredTags"),
                    affix,
                    $"修改器 {i} 旧 RequiredTags",
                    issues);
                ValidateLegacyList(
                    modifier.FindPropertyRelative("_blockedTags"),
                    affix,
                    $"修改器 {i} 旧 BlockedTags",
                    issues);
            }
        }

        static void ValidateLegacyList(
            SerializedProperty property,
            UnityEngine.Object owner,
            string label,
            List<ContentValidationIssue> issues)
        {
            if (property != null && property.isArray && property.arraySize > 0)
            {
                AddError(issues, owner, $"{label} 必须在结构化查询迁移后清空");
            }
        }

        static bool IsSupportedOperation(ModifierOperation operation)
        {
            return operation == ModifierOperation.Flat
                || operation == ModifierOperation.Increase
                || operation == ModifierOperation.More
                || operation == ModifierOperation.Override
                || operation == ModifierOperation.Conversion
                || operation == ModifierOperation.GainAsExtra;
        }

        static bool IsSupportedScope(ModifierScope scope)
        {
            return scope == ModifierScope.LocalItem
                || scope == ModifierScope.GlobalActor
                || scope == ModifierScope.Skill
                || scope == ModifierScope.TargetTaken;
        }

        static void AddError(
            List<ContentValidationIssue> issues,
            UnityEngine.Object asset,
            string message)
        {
            issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, asset, message));
        }

        readonly struct ExpectedTagMetadata
        {
            public CombatTagDomain Domain { get; }

            public CombatTagUsage Usage { get; }

            public ExpectedTagMetadata(CombatTagDomain domain, CombatTagUsage usage)
            {
                Domain = domain;
                Usage = usage;
            }
        }
    }
}
