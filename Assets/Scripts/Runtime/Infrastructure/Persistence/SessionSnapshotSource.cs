using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    public interface ISessionSnapshotSource
    {
        int ArchitectureGeneration { get; }

        bool IsAvailable { get; }

        SessionSnapshotResult Capture();

        void Invalidate();
    }

    public sealed class SessionSnapshotResult
    {
        public SavePayloadDto Payload { get; }

        public IReadOnlyList<DtoMapIssue> Issues { get; }

        public bool Succeeded => Payload != null && Issues.Count == 0;

        public SessionSnapshotResult(
            SavePayloadDto payload,
            IReadOnlyList<DtoMapIssue> issues)
        {
            Payload = payload;
            Issues = issues ?? Array.Empty<DtoMapIssue>();
        }
    }

    public interface IPreparedSessionSnapshotSource
    {
        UniTask<SessionSnapshotResult> CaptureAsync(CancellationToken token);
    }

    public sealed class SessionSnapshotSource : ISessionSnapshotSource, IPreparedSessionSnapshotSource
    {
        readonly GameSessionHost _session;
        readonly ContentCatalog _catalog;
        readonly MonsterSpawner _spawner;
        bool _invalidated;

        public SessionSnapshotSource(
            GameSessionHost session,
            ContentCatalog catalog,
            MonsterSpawner spawner)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        }

        public int ArchitectureGeneration => _session.ArchitectureGeneration;

        public bool IsAvailable => !_invalidated
            && _session.State == GameSessionState.Running
            && _session.IsCurrentArchitectureLease
            && _session.SessionScope.CanAcceptWork
            && _session.SceneScope.CanAcceptWork;

        public void Invalidate()
        {
            _invalidated = true;
        }

        public async UniTask<SessionSnapshotResult> CaptureAsync(CancellationToken token)
        {
            LifecycleScope scope = _session.SessionScope.CreateChild("status-snapshot", token);
            try
            {
                using (await _session.Architecture.GetSystem<StatusSystem>().HoldClockAsync(scope.Token))
                {
                    scope.Token.ThrowIfCancellationRequested();
                    return Capture();
                }
            }
            finally { await scope.StopAsync(); }
        }

        public SessionSnapshotResult Capture()
        {
            List<DtoMapIssue> issues = new List<DtoMapIssue>();

            if (!IsAvailable)
            {
                Add(issues, DtoMapIssueCode.InvalidValue, "$", "Session 当前不可捕获");
                return Failure(issues);
            }

            IArchitecture architecture = _session.Architecture;
            if (!architecture.GetSystem<StatusSystem>().Store.IsQuiescent)
            {
                Add(issues, DtoMapIssueCode.InvalidValue, "payload.run.statuses", "状态尚未到达保存边界");
                return Failure(issues);
            }
            IReadOnlyList<GameObject> objects = architecture
                .GetUtility<SessionObjectRegistry>()
                .CaptureObjects();
            List<PlayerController> players = CaptureComponents<PlayerController>(objects);
            List<MonsterController> monsters = CaptureComponents<MonsterController>(objects)
                .Where(monster => monster.Actor != null && monster.Actor.IsAlive)
                .OrderBy(monster => monster.Instance?.Id.Value, StringComparer.Ordinal)
                .ToList();
            List<LootPickupController> worldDrops = CaptureComponents<LootPickupController>(objects)
                .Where(drop => drop.Item != null)
                .OrderBy(drop => drop.Id.Value, StringComparer.Ordinal)
                .ToList();

            if (players.Count != 1)
            {
                Add(issues, DtoMapIssueCode.InvalidValue, "payload.run.player", "Session 必须恰好有一个玩家");
                return Failure(issues);
            }

            PlayerController player = players[0];
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            EquipmentModel equipment = architecture.GetModel<EquipmentModel>();
            EconomyModel economy = architecture.GetModel<EconomyModel>();
            EquipmentLoadout playerLoadout = equipment.GetLoadout(player.Actor)
                ?? new EquipmentLoadout();
            List<ItemInstance> items = CollectItems(
                inventory,
                playerLoadout,
                economy,
                worldDrops,
                issues);
            List<ItemInstanceDto> itemDtos = MapItems(items, issues);
            MerchantInventoryDto merchant = MapMerchant(economy, issues);
            SavePayloadDto payload = new SavePayloadDto
            {
                Items = itemDtos,
                Profile = new ProfileSaveData
                {
                    ProfileId = "local_default",
                    PlayerId = player.Id.Value,
                    Gold = inventory.Gold,
                    Inventory = RuntimeStateMapper.ToDto(inventory.Grid),
                    Equipment = new List<EquipmentLoadoutDto>
                    {
                        RuntimeStateMapper.ToDto(player.Id.Value, playerLoadout),
                    },
                },
                Run = new RunSaveData
                {
                    Statuses = architecture.GetSystem<StatusSystem>().Store.CaptureSave(_catalog, issues),
                    InstanceIds = MapInstanceIds(
                        architecture.GetUtility<IRunInstanceIdGenerator>().CaptureState()),
                    Random = MapRandom(
                        architecture.GetSystem<GameplayRandomSystem>().CaptureState()),
                    Player = MapPlayer(player, issues),
                    Monsters = MapMonsters(monsters, issues),
                    WorldDrops = MapWorldDrops(worldDrops),
                    Merchant = merchant,
                    Spawner = MapSpawner(issues),
                },
            };

            StatusSaveValidation.Validate(payload.Run.Statuses, payload, _catalog, issues);
            SaveDataValidationResult validation = SaveDataValidator.ValidatePayload(payload);

            for (int i = 0; i < validation.Issues.Count; i++)
            {
                SaveDataIssue issue = validation.Issues[i];
                Add(issues, DtoMapIssueCode.InvalidValue, issue.Path, issue.Message);
            }

            return issues.Count == 0
                ? new SessionSnapshotResult(payload, Array.Empty<DtoMapIssue>())
                : Failure(issues);
        }

        static List<T> CaptureComponents<T>(IReadOnlyList<GameObject> objects)
            where T : Component
        {
            List<T> result = new List<T>();

            for (int i = 0; i < objects.Count; i++)
            {
                T component = objects[i] != null ? objects[i].GetComponent<T>() : null;

                if (component != null)
                {
                    result.Add(component);
                }
            }

            return result;
        }

        static List<ItemInstance> CollectItems(
            InventoryModel inventory,
            EquipmentLoadout equipment,
            EconomyModel economy,
            IReadOnlyList<LootPickupController> worldDrops,
            List<DtoMapIssue> issues)
        {
            Dictionary<ItemInstanceId, ItemInstance> byId = new Dictionary<ItemInstanceId, ItemInstance>();

            foreach (ItemInstance item in inventory.Grid.Placements.Keys)
            {
                AddItem(item, "payload.profile.inventory", byId, issues);
            }

            foreach (KeyValuePair<EquipmentSlot, ItemInstance> pair in equipment.Slots)
            {
                AddItem(pair.Value, $"payload.profile.equipment.{pair.Key}", byId, issues);
            }

            for (int i = 0; i < economy.MerchantStock.Count; i++)
            {
                AddItem(economy.MerchantStock[i], $"payload.run.merchant[{i}]", byId, issues);
            }

            for (int i = 0; i < worldDrops.Count; i++)
            {
                AddItem(worldDrops[i].Item, $"payload.run.worldDrops[{i}]", byId, issues);
            }

            return byId.Values.OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToList();
        }

        static void AddItem(
            ItemInstance item,
            string path,
            IDictionary<ItemInstanceId, ItemInstance> byId,
            List<DtoMapIssue> issues)
        {
            if (item == null)
            {
                Add(issues, DtoMapIssueCode.NullInput, path, "物品实例为空");
                return;
            }

            if (byId.TryGetValue(item.Id, out ItemInstance existing) && existing != item)
            {
                Add(issues, DtoMapIssueCode.DuplicateInstanceId, path, $"物品 ID 重复：{item.Id}");
                return;
            }

            byId[item.Id] = item;
        }

        List<ItemInstanceDto> MapItems(
            IReadOnlyList<ItemInstance> items,
            List<DtoMapIssue> issues)
        {
            List<ItemInstanceDto> result = new List<ItemInstanceDto>(items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                DtoMapResult<ItemInstanceDto> mapped = RuntimeStateMapper.ToDto(items[i], _catalog);

                if (mapped.Succeeded)
                {
                    result.Add(mapped.Value);
                }
                else
                {
                    issues.AddRange(mapped.Issues);
                }
            }

            return result;
        }

        MerchantInventoryDto MapMerchant(EconomyModel economy, List<DtoMapIssue> issues)
        {
            if (economy.Trader == null)
            {
                Add(issues, DtoMapIssueCode.NullInput, "payload.run.merchant", "商人尚未配置");
                return new MerchantInventoryDto();
            }

            try
            {
                MerchantInventoryDto result = RuntimeStateMapper.ToDto(
                    economy.Trader,
                    economy.MerchantStock,
                    _catalog);
                result.BuyMultiplier = economy.BuyMultiplier;
                result.SellMultiplier = economy.SellMultiplier;
                return result;
            }
            catch (InvalidOperationException exception)
            {
                Add(issues, DtoMapIssueCode.MissingContent, "payload.run.merchant", exception.Message);
                return new MerchantInventoryDto();
            }
        }

        PlayerRunStateDto MapPlayer(PlayerController player, List<DtoMapIssue> issues)
        {
            CombatResourceSnapshot resources = player.Actor.Resources;
            return new PlayerRunStateDto
            {
                PlayerId = player.Id.Value,
                DefinitionContentId = RuntimeStateMapper.GetContentId(
                    player.Definition,
                    _catalog,
                    "payload.run.player.definitionContentId",
                    issues),
                SkillContentId = RuntimeStateMapper.GetContentId(
                    player.DefaultSkill,
                    _catalog,
                    "payload.run.player.skillContentId",
                    issues),
                Position = MapVector(player.transform.position),
                Resources = MapResources(resources, player.Actor.IsAlive),
                RespawnRemainingSeconds = player.RespawnRemainingSeconds,
                AutoCastCooldownRemainingSeconds = player.AutoCastCooldownRemainingSeconds,
            };
        }

        List<MonsterRunStateDto> MapMonsters(
            IReadOnlyList<MonsterController> monsters,
            List<DtoMapIssue> issues)
        {
            List<MonsterRunStateDto> result = new List<MonsterRunStateDto>(monsters.Count);

            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterController monster = monsters[i];
                MonsterInstanceData instance = monster.Instance;
                string path = $"payload.run.monsters[{i}]";

                if (monster.Definition == null || instance == null)
                {
                    Add(issues, DtoMapIssueCode.NullInput, path, "怪物定义或实例为空");
                    continue;
                }

                MonsterInstanceRandomSeeds seeds = instance.RandomSeeds;
                result.Add(new MonsterRunStateDto
                {
                    InstanceId = instance.Id.Value,
                    DefinitionContentId = RuntimeStateMapper.GetContentId(
                        monster.Definition,
                        _catalog,
                        $"{path}.definitionContentId",
                        issues),
                    RandomSeeds = new MonsterRandomSeedsDto
                    {
                        RootSeed = seeds.RootSeed,
                        HealthSeed = seeds.HealthSeed,
                        AffixCountSeed = seeds.AffixCountSeed,
                        AffixSelectionSeed = seeds.AffixSelectionSeed,
                    },
                    BaseMaxHealth = instance.BaseMaxHealth,
                    BaseStats = MapStats(instance.BaseStats, $"{path}.baseStats", issues),
                    EffectiveStats = MapStats(instance.EffectiveStats, $"{path}.effectiveStats", issues),
                    Affixes = MapMonsterAffixes(instance.Affixes, $"{path}.affixes", issues),
                    Modifiers = RuntimeStateMapper.MapModifiersToDto(
                        instance.Modifiers,
                        _catalog,
                        $"{path}.modifiers",
                        issues),
                    Position = MapVector(monster.transform.position),
                    Resources = MapResources(monster.Actor.Resources, true),
                    ContactDamageCooldownRemainingSeconds =
                        monster.ContactDamageCooldownRemainingSeconds,
                });
            }

            return result;
        }

        List<StatValueDto> MapStats(
            StatBlock stats,
            string path,
            List<DtoMapIssue> issues)
        {
            List<StatValueDto> result = new List<StatValueDto>();

            foreach (KeyValuePair<string, float> pair in stats.Values.OrderBy(
                pair => pair.Key,
                StringComparer.Ordinal))
            {
                result.Add(new StatValueDto
                {
                    StatContentId = RuntimeStateMapper.MapOptionalLocalContentId(
                        ContentNamespaces.Stat,
                        pair.Key,
                        _catalog,
                        path,
                        issues),
                    Value = pair.Value,
                });
            }

            return result;
        }

        List<MonsterAffixInstanceDto> MapMonsterAffixes(
            IReadOnlyList<MonsterAffixInstance> affixes,
            string path,
            List<DtoMapIssue> issues)
        {
            List<MonsterAffixInstanceDto> result = new List<MonsterAffixInstanceDto>();

            for (int i = 0; i < affixes.Count; i++)
            {
                MonsterAffixInstance affix = affixes[i];

                if (affix?.Definition == null)
                {
                    Add(issues, DtoMapIssueCode.NullInput, $"{path}[{i}]", "怪物词条为空");
                    continue;
                }

                result.Add(new MonsterAffixInstanceDto
                {
                    DefinitionContentId = RuntimeStateMapper.GetContentId(
                        affix.Definition,
                        _catalog,
                        $"{path}[{i}].definitionContentId",
                        issues),
                    Modifiers = RuntimeStateMapper.MapModifiersToDto(
                        affix.Modifiers,
                        _catalog,
                        $"{path}[{i}].modifiers",
                        issues),
                });
            }

            return result;
        }

        static List<WorldDropStateDto> MapWorldDrops(
            IReadOnlyList<LootPickupController> drops)
        {
            List<WorldDropStateDto> result = new List<WorldDropStateDto>(drops.Count);

            for (int i = 0; i < drops.Count; i++)
            {
                result.Add(new WorldDropStateDto
                {
                    InstanceId = drops[i].Id.Value,
                    ItemInstanceId = drops[i].Item.Id.Value,
                    Position = MapVector(drops[i].transform.position),
                });
            }

            return result;
        }

        MonsterSpawnerStateDto MapSpawner(List<DtoMapIssue> issues)
        {
            string contentId = RuntimeStateMapper.GetContentId(
                _spawner.SpawnDefinition,
                _catalog,
                "payload.run.spawner.spawnContentId",
                issues);
            return new MonsterSpawnerStateDto
            {
                SpawnContentId = contentId,
                IsRunning = _spawner.IsSpawning,
                NextSpawnRemainingSeconds = _spawner.NextSpawnRemainingSeconds,
            };
        }

        static RunInstanceIdStateDto MapInstanceIds(RunInstanceIdState state)
        {
            return new RunInstanceIdStateDto
            {
                RunId = state.RunId.Value,
                NextMonsterSequence = state.NextMonsterSequence,
                NextWorldDropSequence = state.NextWorldDropSequence,
            };
        }

        static GameplayRandomStateDto MapRandom(GameplayRandomState state)
        {
            GameplayRandomStateDto result = new GameplayRandomStateDto
            {
                RootSeed = state.RootSeed,
            };

            foreach (KeyValuePair<GameplayRandomChannel, int> pair in state.NextSequences.OrderBy(
                pair => pair.Key))
            {
                result.Channels.Add(new GameplayRandomChannelStateDto
                {
                    Channel = pair.Key,
                    NextSequence = pair.Value,
                });
            }

            return result;
        }

        static Vector3Dto MapVector(Vector3 value)
        {
            return new Vector3Dto { X = value.x, Y = value.y, Z = value.z };
        }

        static CombatResourceStateDto MapResources(
            CombatResourceSnapshot value,
            bool isAlive)
        {
            return new CombatResourceStateDto
            {
                CurrentHealth = value.CurrentHealth,
                MaxHealth = value.MaxHealth,
                CurrentMana = value.CurrentMana,
                MaxMana = value.MaxMana,
                IsAlive = isAlive,
            };
        }

        static void Add(
            List<DtoMapIssue> issues,
            DtoMapIssueCode code,
            string path,
            string message)
        {
            issues.Add(new DtoMapIssue(code, path, message));
        }

        static SessionSnapshotResult Failure(List<DtoMapIssue> issues)
        {
            return new SessionSnapshotResult(null, issues.AsReadOnly());
        }
    }
}
