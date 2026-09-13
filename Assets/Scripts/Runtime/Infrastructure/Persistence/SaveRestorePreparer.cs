using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkFlare
{
    public sealed class PreparedMonsterRestore
    {
        public MonsterDefinition Definition { get; }

        public MonsterInstanceData Instance { get; }

        public MonsterRunStateDto State { get; }

        internal PreparedMonsterRestore(
            MonsterDefinition definition,
            MonsterInstanceData instance,
            MonsterRunStateDto state)
        {
            Definition = definition;
            Instance = instance;
            State = state;
        }
    }

    public sealed class PreparedWorldDropRestore
    {
        public WorldDropId Id { get; }

        public ItemInstance Item { get; }

        public Vector3 Position { get; }

        internal PreparedWorldDropRestore(WorldDropId id, ItemInstance item, Vector3 position)
        {
            Id = id;
            Item = item;
            Position = position;
        }
    }

    public sealed class PreparedRestore
    {
        public SaveDocumentDto Document { get; }

        public CharacterDefinition PlayerDefinition { get; }

        public ProjectileSkillDefinition PlayerSkill { get; }

        public MonsterSpawnDefinition SpawnDefinition { get; }

        public DetachedRuntimeState RuntimeState { get; }

        public EquipmentLoadout PlayerEquipment { get; }

        public IReadOnlyList<PreparedMonsterRestore> Monsters { get; }

        public IReadOnlyList<PreparedWorldDropRestore> WorldDrops { get; }

        public GameplayRandomState RandomState { get; }

        public RunInstanceIdState InstanceIdState { get; }

        internal PreparedRestore(
            SaveDocumentDto document,
            CharacterDefinition playerDefinition,
            ProjectileSkillDefinition playerSkill,
            MonsterSpawnDefinition spawnDefinition,
            DetachedRuntimeState runtimeState,
            EquipmentLoadout playerEquipment,
            IReadOnlyList<PreparedMonsterRestore> monsters,
            IReadOnlyList<PreparedWorldDropRestore> worldDrops,
            GameplayRandomState randomState,
            RunInstanceIdState instanceIdState)
        {
            Document = document;
            PlayerDefinition = playerDefinition;
            PlayerSkill = playerSkill;
            SpawnDefinition = spawnDefinition;
            RuntimeState = runtimeState;
            PlayerEquipment = playerEquipment;
            Monsters = monsters;
            WorldDrops = worldDrops;
            RandomState = randomState;
            InstanceIdState = instanceIdState;
        }
    }

    public sealed class PreparedRestoreResult
    {
        public PreparedRestore Value { get; }

        public IReadOnlyList<DtoMapIssue> Issues { get; }

        public bool Succeeded => Value != null && Issues.Count == 0;

        internal PreparedRestoreResult(
            PreparedRestore value,
            IReadOnlyList<DtoMapIssue> issues)
        {
            Value = value;
            Issues = issues ?? Array.Empty<DtoMapIssue>();
        }
    }

    public static class SaveRestorePreparer
    {
        public static PreparedRestoreResult Prepare(
            SaveDocumentDto document,
            ContentCatalog catalog)
        {
            List<DtoMapIssue> issues = new List<DtoMapIssue>();

            if (document == null || catalog == null)
            {
                Add(issues, DtoMapIssueCode.NullInput, "$", "存档或内容目录为空");
                return Failure(issues);
            }

            SaveDataValidationResult validation = SaveDataValidator.ValidateDocument(document);

            for (int i = 0; i < validation.Issues.Count; i++)
            {
                SaveDataIssue issue = validation.Issues[i];
                Add(issues, DtoMapIssueCode.InvalidValue, issue.Path, issue.Message);
            }

            if (issues.Count > 0)
            {
                return Failure(issues);
            }

            if (document.Header.CatalogId == "core" && catalog.CatalogId == "core"
                && document.Header.ContentVersion == 1 && (catalog.ContentVersion == 2 || catalog.ContentVersion == 3))
            {
                // v1 stores four stable slots. New slots start empty; rolled values and stock stay intact.
                if (document.Payload.Profile.Equipment.Any(loadout =>
                    loadout.Entries.Any(entry => (int)entry.Slot > 3)))
                {
                    Add(issues, DtoMapIssueCode.InvalidValue, "payload.profile.equipment",
                        "旧内容版本不能包含新增装备槽");
                    return Failure(issues);
                }
                document = JObject.FromObject(document).ToObject<SaveDocumentDto>();
                document.Header.ContentVersion = 2;
            }

            if (document.Header.CatalogId == "core" && catalog.CatalogId == "core"
                && document.Header.ContentVersion == 2 && catalog.ContentVersion == 3)
            {
                document = JObject.FromObject(document).ToObject<SaveDocumentDto>();
                ItemBaseDefinition gold = catalog.GetAll<ItemBaseDefinition>().FirstOrDefault(item => item.ItemType == ItemType.Currency);
                if (gold == null)
                {
                    Add(issues, DtoMapIssueCode.MissingContent, "catalog", "迁移缺少金币配置");
                    return Failure(issues);
                }
                ProfileSaveData profile = document.Payload.Profile;
                if (profile.Gold > 0)
                {
                    long stacks = ((long)profile.Gold + gold.MaxStackSize - 1) / gold.MaxStackSize;
                    if (stacks + document.Payload.Items.Count > LocalSaveFormat.MaximumItems)
                    {
                        Add(issues, DtoMapIssueCode.InvalidValue, "payload.profile.gold", "金币迁移超出存档物品容量，请提高金币堆叠上限");
                        return Failure(issues);
                    }
                    InventoryLayoutDto inventory = profile.Inventory;
                    int x = 0;
                    int y = 0;
                    int remainingGold = profile.Gold;
                    while (remainingGold > 0)
                    {
                        while (y < inventory.Height && inventory.Placements.Any(placement =>
                            x >= placement.X && x < placement.X + placement.Width && y >= placement.Y && y < placement.Y + placement.Height))
                        {
                            x++;
                            if (x >= inventory.Width) { x = 0; y++; }
                        }
                        // 历史满背包增加一行，以保留所有旧物品的身份和原位置。
                        if (y == inventory.Height) { inventory.Height++; }
                        string id = new UuidItemInstanceIdGenerator().Next().Value;
                        catalog.TryGetContentId(gold, out ContentId contentId);
                        document.Payload.Items.Add(new ItemInstanceDto
                        {
                            InstanceId = id, BaseContentId = contentId.ToString(), Quantity = Math.Min(remainingGold, gold.MaxStackSize),
                            Rarity = ItemRarity.Normal, ItemLevel = 1, Durability = 1f
                        });
                        inventory.Placements.Add(new InventoryPlacementDto
                        {
                            ItemInstanceId = id, X = x, Y = y, Width = 1, Height = 1
                        });
                        remainingGold -= Math.Min(remainingGold, gold.MaxStackSize);
                    }
                    document.Header.Summary.ItemCount = document.Payload.Items.Count;
                }
                document.Header.ContentVersion = 3;
            }

            if (!string.Equals(document.Header.CatalogId, catalog.CatalogId, StringComparison.Ordinal)
                || document.Header.ContentVersion != catalog.ContentVersion)
            {
                Add(
                    issues,
                    DtoMapIssueCode.MissingContent,
                    "header.catalog",
                    $"存档内容目录 {document.Header.CatalogId}@{document.Header.ContentVersion} "
                    + $"与当前 {catalog.CatalogId}@{catalog.ContentVersion} 不一致");
            }

            if (issues.Count > 0)
            {
                return Failure(issues);
            }

            RuntimeStateDto runtimeDto = CreateRuntimeStateDto(document.Payload);
            DtoMapResult<DetachedRuntimeState> runtimeResult = RuntimeStateMapper.Restore(
                runtimeDto,
                catalog);

            if (!runtimeResult.Succeeded)
            {
                issues.AddRange(runtimeResult.Issues);
                return Failure(issues);
            }

            PlayerRunStateDto playerState = document.Payload.Run.Player;
            if (document.Header.CatalogId == "core" && document.Header.ContentVersion >= 3)
            {
                long gold = runtimeResult.Value.Inventory.Placements.Keys
                    .Where(item => item.BaseDefinition.ItemType == ItemType.Currency).Sum(item => (long)item.Quantity);
                if (gold != document.Payload.Profile.Gold)
                {
                    Add(issues, DtoMapIssueCode.InvalidValue, "payload.profile.gold", "背包金币与摘要不一致");
                    return Failure(issues);
                }
            }
            CharacterDefinition playerDefinition = Resolve<CharacterDefinition>(
                playerState.DefinitionContentId,
                catalog,
                "payload.run.player.definitionContentId",
                issues);
            ProjectileSkillDefinition playerSkill = Resolve<ProjectileSkillDefinition>(
                playerState.SkillContentId,
                catalog,
                "payload.run.player.skillContentId",
                issues);
            MonsterSpawnDefinition spawnDefinition = Resolve<MonsterSpawnDefinition>(
                document.Payload.Run.Spawner.SpawnContentId,
                catalog,
                "payload.run.spawner.spawnContentId",
                issues);
            EquipmentLoadout playerEquipment = ResolvePlayerEquipment(
                runtimeResult.Value.Equipment,
                issues);
            List<PreparedMonsterRestore> monsters = RestoreMonsters(
                document.Payload.Run.Monsters,
                catalog,
                issues);
            List<PreparedWorldDropRestore> worldDrops = RestoreWorldDrops(
                document.Payload.Run.WorldDrops,
                runtimeResult.Value.Items,
                issues);
            GameplayRandomState randomState = RestoreRandomState(
                document.Payload.Run.Random,
                issues);
            RunInstanceIdState instanceIdState = RestoreInstanceIdState(
                document.Payload.Run.InstanceIds,
                issues);

            if (issues.Count > 0)
            {
                return Failure(issues);
            }

            PreparedRestore prepared = new PreparedRestore(
                document,
                playerDefinition,
                playerSkill,
                spawnDefinition,
                runtimeResult.Value,
                playerEquipment,
                monsters.AsReadOnly(),
                worldDrops.AsReadOnly(),
                randomState,
                instanceIdState);
            return new PreparedRestoreResult(prepared, Array.Empty<DtoMapIssue>());
        }

        static RuntimeStateDto CreateRuntimeStateDto(SavePayloadDto payload)
        {
            RuntimeStateDto result = new RuntimeStateDto
            {
                Items = payload.Items,
                Inventory = payload.Profile.Inventory,
                Equipment = payload.Profile.Equipment,
                Merchant = payload.Run.Merchant,
            };
            result.Actors.Add(new ActorIdentityDto
            {
                Kind = ActorIdentityKind.Player,
                InstanceId = payload.Run.Player.PlayerId,
                DefinitionContentId = payload.Run.Player.DefinitionContentId,
            });

            for (int i = 0; i < payload.Run.Monsters.Count; i++)
            {
                MonsterRunStateDto monster = payload.Run.Monsters[i];
                result.Actors.Add(new ActorIdentityDto
                {
                    Kind = ActorIdentityKind.Monster,
                    InstanceId = monster.InstanceId,
                    DefinitionContentId = monster.DefinitionContentId,
                });
            }

            return result;
        }

        static EquipmentLoadout ResolvePlayerEquipment(
            IReadOnlyList<DetachedEquipmentLoadout> loadouts,
            List<DtoMapIssue> issues)
        {
            EquipmentLoadout result = null;

            for (int i = 0; i < loadouts.Count; i++)
            {
                if (!string.Equals(
                    loadouts[i].ActorInstanceId,
                    PlayerId.LocalPlayer.Value,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                if (result != null)
                {
                    Add(
                        issues,
                        DtoMapIssueCode.OwnershipConflict,
                        "payload.profile.equipment",
                        "玩家装备栏重复");
                    return null;
                }

                result = loadouts[i].Loadout;
            }

            if (result == null)
            {
                Add(
                    issues,
                    DtoMapIssueCode.DanglingReference,
                    "payload.profile.equipment",
                    "缺少玩家装备栏");
            }

            return result;
        }

        static List<PreparedMonsterRestore> RestoreMonsters(
            IReadOnlyList<MonsterRunStateDto> states,
            ContentCatalog catalog,
            List<DtoMapIssue> issues)
        {
            List<PreparedMonsterRestore> result = new List<PreparedMonsterRestore>();

            for (int i = 0; i < states.Count; i++)
            {
                MonsterRunStateDto state = states[i];
                string path = $"payload.run.monsters[{i}]";
                MonsterDefinition definition = Resolve<MonsterDefinition>(
                    state.DefinitionContentId,
                    catalog,
                    $"{path}.definitionContentId",
                    issues);
                MonsterInstanceData instance = RestoreMonsterInstance(
                    state,
                    catalog,
                    path,
                    issues);

                if (definition != null && instance != null)
                {
                    result.Add(new PreparedMonsterRestore(definition, instance, state));
                }
            }

            return result;
        }

        static MonsterInstanceData RestoreMonsterInstance(
            MonsterRunStateDto state,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            if (!MonsterInstanceId.TryParse(state.InstanceId, out MonsterInstanceId id))
            {
                Add(issues, DtoMapIssueCode.InvalidInstanceId, $"{path}.instanceId", "怪物实例 ID 非法");
                return null;
            }

            MonsterInstanceRandomSeeds seeds = new MonsterInstanceRandomSeeds(
                state.RandomSeeds.RootSeed);
            StatBlock baseStats = RestoreStats(state.BaseStats, catalog, $"{path}.baseStats", issues);
            StatBlock effectiveStats = RestoreStats(
                state.EffectiveStats,
                catalog,
                $"{path}.effectiveStats",
                issues);
            List<ModifierInstance> modifiers = RuntimeStateMapper.RestoreModifiers(
                state.Modifiers,
                catalog,
                $"{path}.modifiers",
                issues);
            List<MonsterAffixInstance> affixes = new List<MonsterAffixInstance>();

            for (int i = 0; i < state.Affixes.Count; i++)
            {
                MonsterAffixInstanceDto affixDto = state.Affixes[i];
                string affixPath = $"{path}.affixes[{i}]";
                MonsterAffixDefinition definition = Resolve<MonsterAffixDefinition>(
                    affixDto.DefinitionContentId,
                    catalog,
                    $"{affixPath}.definitionContentId",
                    issues);
                List<ModifierInstance> affixModifiers = RuntimeStateMapper.RestoreModifiers(
                    affixDto.Modifiers,
                    catalog,
                    $"{affixPath}.modifiers",
                    issues);

                if (definition != null)
                {
                    affixes.Add(new MonsterAffixInstance(
                        definition,
                        seeds.GetAffixValueSeed(definition.Id),
                        affixModifiers));
                }
            }

            StatBlock calculated = CombatStatResolver.Build(baseStats, modifiers);

            if (!StatsEqual(calculated, effectiveStats))
            {
                Add(
                    issues,
                    DtoMapIssueCode.InvalidValue,
                    $"{path}.effectiveStats",
                    "保存的怪物最终属性与基础属性、修改器计算结果不一致");
            }

            return new MonsterInstanceData(
                id,
                seeds,
                state.BaseMaxHealth,
                baseStats,
                effectiveStats,
                affixes,
                modifiers);
        }

        static StatBlock RestoreStats(
            IReadOnlyList<StatValueDto> values,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            StatBlock result = new StatBlock();

            for (int i = 0; i < values.Count; i++)
            {
                StatValueDto value = values[i];
                string localId = RuntimeStateMapper.ResolveOptionalLocalId<StatDefinition>(
                    value.StatContentId,
                    catalog,
                    $"{path}[{i}].statContentId",
                    issues);

                if (!string.IsNullOrEmpty(localId))
                {
                    result.SetValue(localId, value.Value);
                }
            }

            return result;
        }

        static List<PreparedWorldDropRestore> RestoreWorldDrops(
            IReadOnlyList<WorldDropStateDto> states,
            IReadOnlyDictionary<ItemInstanceId, ItemInstance> items,
            List<DtoMapIssue> issues)
        {
            List<PreparedWorldDropRestore> result = new List<PreparedWorldDropRestore>();

            for (int i = 0; i < states.Count; i++)
            {
                WorldDropStateDto state = states[i];
                string path = $"payload.run.worldDrops[{i}]";

                if (!WorldDropId.TryParse(state.InstanceId, out WorldDropId id)
                    || !ItemInstanceId.TryParse(state.ItemInstanceId, out ItemInstanceId itemId)
                    || !items.TryGetValue(itemId, out ItemInstance item))
                {
                    Add(issues, DtoMapIssueCode.DanglingReference, path, "世界掉落引用非法");
                    continue;
                }

                result.Add(new PreparedWorldDropRestore(
                    id,
                    item,
                    new Vector3(state.Position.X, state.Position.Y, state.Position.Z)));
            }

            return result;
        }

        static GameplayRandomState RestoreRandomState(
            GameplayRandomStateDto dto,
            List<DtoMapIssue> issues)
        {
            try
            {
                Dictionary<GameplayRandomChannel, int> sequences = dto.Channels.ToDictionary(
                    channel => channel.Channel,
                    channel => channel.NextSequence);
                return new GameplayRandomState(dto.RootSeed, sequences);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException)
            {
                Add(issues, DtoMapIssueCode.InvalidValue, "payload.run.random", exception.Message);
                return null;
            }
        }

        static RunInstanceIdState RestoreInstanceIdState(
            RunInstanceIdStateDto dto,
            List<DtoMapIssue> issues)
        {
            try
            {
                return new RunInstanceIdState(
                    RunId.Parse(dto.RunId),
                    dto.NextMonsterSequence,
                    dto.NextWorldDropSequence);
            }
            catch (ArgumentException exception)
            {
                Add(issues, DtoMapIssueCode.InvalidValue, "payload.run.instanceIds", exception.Message);
                return default;
            }
        }

        static T Resolve<T>(
            string raw,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
            where T : ScriptableObject, IContentDefinition
        {
            return RuntimeStateMapper.Resolve<T>(raw, catalog, path, issues);
        }

        static bool StatsEqual(StatBlock left, StatBlock right)
        {
            HashSet<string> ids = new HashSet<string>(left.Values.Keys, StringComparer.Ordinal);
            ids.UnionWith(right.Values.Keys);

            foreach (string id in ids)
            {
                if (Mathf.Abs(left.GetValue(id) - right.GetValue(id)) > 0.001f)
                {
                    return false;
                }
            }

            return true;
        }

        static void Add(
            List<DtoMapIssue> issues,
            DtoMapIssueCode code,
            string path,
            string message)
        {
            issues.Add(new DtoMapIssue(code, path, message));
        }

        static PreparedRestoreResult Failure(List<DtoMapIssue> issues)
        {
            return new PreparedRestoreResult(null, issues.AsReadOnly());
        }
    }
}
