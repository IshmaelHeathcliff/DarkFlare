using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DarkFlare
{
    public enum SaveDataIssueCode
    {
        MissingValue,
        InvalidValue,
        LimitExceeded,
        DuplicateInstanceId,
        DanglingReference,
        DuplicateOwnership,
        InvalidOwnership,
        DuplicateRandomChannel,
        MissingRandomChannel,
        InvalidSequence,
        NonFiniteNumber,
        InconsistentState,
    }

    public sealed class SaveDataIssue
    {
        public SaveDataIssueCode Code { get; }

        public string Path { get; }

        public string Message { get; }

        public SaveDataIssue(SaveDataIssueCode code, string path, string message)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public sealed class SaveDataValidationResult
    {
        public IReadOnlyList<SaveDataIssue> Issues { get; }

        public bool Succeeded => Issues.Count == 0;

        internal SaveDataValidationResult(IReadOnlyList<SaveDataIssue> issues)
        {
            Issues = issues;
        }
    }

    public static class SaveDataValidator
    {
        public static SaveDataValidationResult ValidateDocument(SaveDocumentDto document)
        {
            List<SaveDataIssue> issues = new List<SaveDataIssue>();

            if (document == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, "$", "存档文档为空");
                return Result(issues);
            }

            ValidateHeader(document.Header, "header", issues);
            int headerIssues = issues.Count;
            ValidateMapStates(document.Payload, issues);
            if (issues.Count != headerIssues) { return Result(issues); }
            ValidatePayloadCore(MapStateData.Flatten(document.Payload), "payload", issues);

            if (document.Header != null && document.Payload != null)
            {
                ValidateSummary(document.Header.Summary, document.Payload, "header.summary", issues);
            }

            return Result(issues);
        }

        public static SaveDataValidationResult ValidatePayload(SavePayloadDto payload)
        {
            List<SaveDataIssue> issues = new List<SaveDataIssue>();
            ValidateMapStates(payload, issues);
            if (issues.Count != 0) { return Result(issues); }
            ValidatePayloadCore(MapStateData.Flatten(payload), "payload", issues);
            return Result(issues);
        }

        static void ValidateMapStates(SavePayloadDto payload, List<SaveDataIssue> issues)
        {
            RunSaveData run = payload?.Run;
            if (run == null) { return; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (!ContentId.TryParse(run.ActiveMapId, out ContentId active) || active.Namespace != ContentNamespaces.Map
                || run.MapRevision < 0 || run.FrozenMaps == null || run.FrozenMaps.Count > 64)
            { Add(issues, SaveDataIssueCode.InvalidValue, "payload.run.maps", "地图身份、修订或容量非法"); return; }
            ids.Add(run.ActiveMapId);
            var actorMaps = new Dictionary<string, string>(StringComparer.Ordinal);
            IndexMapActors(run.Monsters, run.ActiveMapId, actorMaps);
            ValidateBoss(run.Boss, run.Monsters, "payload.run.boss", issues);
            ValidateSpawner(run.Spawner, "payload.run.spawner", issues, run.Monsters);
            foreach (MapStateDto map in run.FrozenMaps)
            { if (map != null) { IndexMapActors(map.Monsters, map.MapId, actorMaps); } }
            ValidateNavigation(run.Monsters, run.ActiveMapId, run.Player?.PlayerId, actorMaps, issues);
            if (run.MapRandom != null || run.MapRevision != 0)
            { ValidateRandom(run.MapRandom, "payload.run.mapRandom", issues); }
            if (run.Monsters?.Count > LocalSaveFormat.MaximumMapMonsters)
            { Add(issues, SaveDataIssueCode.LimitExceeded, "payload.run.monsters", "当前地图怪物超限"); }
            int layers = run.Statuses?.Actors?.Sum(actor => actor?.Layers?.Count ?? 0) ?? 0;
            foreach (MapStateDto map in run.FrozenMaps)
            {
                if (map == null || !ContentId.TryParse(map.MapId, out ContentId id) || id.Namespace != ContentNamespaces.Map
                    || !ids.Add(map.MapId) || map.Revision < 0 || map.Monsters == null || map.WorldDrops == null
                    || map.Monsters.Count > LocalSaveFormat.MaximumMapMonsters || map.Statuses?.Actors == null)
                { Add(issues, SaveDataIssueCode.InvalidValue, "payload.run.maps", "冻结地图数据非法或重复"); continue; }
                ValidateNavigation(map.Monsters, map.MapId, run.Player?.PlayerId, actorMaps, issues);
                ValidateBoss(map.Boss, map.Monsters, "payload.run.maps.boss", issues);
                ValidateRandom(map.Random, "payload.run.maps.random", issues);
                ValidateSpawner(map.Spawner, "payload.run.maps.spawner", issues, map.Monsters);
                if (map.Statuses.Time > (run.Statuses?.Time ?? 0)
                    || map.Statuses.Actors.Any(actor => actor?.ActorKey?.StartsWith("player:", StringComparison.Ordinal) == true))
                { Add(issues, SaveDataIssueCode.InvalidValue, "payload.run.maps.statuses", "冻结地图不能包含玩家状态或未来时钟"); }
                var local = new SavePayloadDto { Profile = payload.Profile, Items = payload.Items,
                    Run = new RunSaveData { Player = run.Player, Monsters = map.Monsters } };
                var statusIssues = new List<DtoMapIssue>();
                StatusSaveValidation.Validate(map.Statuses, local, null, statusIssues);
                foreach (DtoMapIssue issue in statusIssues) { Add(issues, SaveDataIssueCode.InvalidValue, "payload.run.maps.statuses", issue.Message); }
                layers += map.Statuses.Actors.Sum(actor => actor?.Layers?.Count ?? 0);
            }
            if (layers > StatusSaveValidation.MaximumLayers)
            { Add(issues, SaveDataIssueCode.LimitExceeded, "payload.run.maps.statuses", "全图状态层超限"); }
            int reserved = ReservationCount(run.Spawner, run.Monsters?.Count ?? 0, run.Boss);
            foreach (MapStateDto map in run.FrozenMaps)
            {
                if (map != null) { reserved += ReservationCount(map.Spawner, map.Monsters?.Count ?? 0, map.Boss); }
            }
            if (reserved > LocalSaveFormat.MaximumMonsters)
            { Add(issues, SaveDataIssueCode.LimitExceeded, "payload.run.spawner", "全档怪物名额超过容量"); }
        }

        static int ReservationCount(MonsterSpawnerStateDto spawner, int monsters, BossEncounterStateDto boss)
        {
            if (spawner?.Mode != FiniteSpawnMode.Finite) { return monsters; }
            return (spawner.Slots?.Count ?? 0) + (boss?.Status == BossEncounterStatus.None ? 0 : 1);
        }

        static void ValidateHeader(
            SaveHeaderDto header,
            string path,
            List<SaveDataIssue> issues)
        {
            if (header == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "存档 Header 为空");
                return;
            }

            if (!string.Equals(header.FormatId, LocalSaveFormat.FormatId, StringComparison.Ordinal))
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.formatId", "存档格式标识不受支持");
            }

            if (header.FormatVersion != LocalSaveFormat.FormatVersion)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.formatVersion", "存档封装版本不受支持");
            }

            if (header.SaveSchemaVersion < 0)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.saveSchemaVersion", "Schema 版本不能小于 0");
            }

            if (string.IsNullOrWhiteSpace(header.GameVersion))
            {
                Add(issues, SaveDataIssueCode.MissingValue, $"{path}.gameVersion", "游戏版本为空");
            }

            if (!ContentId.IsValidSegment(header.CatalogId))
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.catalogId", "内容目录 ID 非法");
            }

            if (header.ContentVersion <= 0)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.contentVersion", "内容版本必须大于 0");
            }

            try
            {
                _ = new SaveSlotId(header.SlotId);
            }
            catch (ArgumentException)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.slotId", "存档槽位 ID 非法");
            }

            if (header.CommitSequence <= 0)
            {
                Add(issues, SaveDataIssueCode.InvalidSequence, $"{path}.commitSequence", "提交代际必须大于 0");
            }

            bool createdValid = TryParseUtc(header.CreatedUtc, out DateTimeOffset created);
            bool updatedValid = TryParseUtc(header.UpdatedUtc, out DateTimeOffset updated);

            if (!createdValid)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.createdUtc", "创建时间不是规范 UTC 时间");
            }

            if (!updatedValid)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.updatedUtc", "更新时间不是规范 UTC 时间");
            }

            if (createdValid && updatedValid && updated < created)
            {
                Add(issues, SaveDataIssueCode.InconsistentState, $"{path}.updatedUtc", "更新时间早于创建时间");
            }

            if (!IsLowerHex(header.PayloadSha256, 64))
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.payloadSha256", "Payload SHA-256 非法");
            }

            if (header.Summary == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, $"{path}.summary", "存档摘要为空");
            }
        }

        static void ValidatePayloadCore(
            SavePayloadDto payload,
            string path,
            List<SaveDataIssue> issues)
        {
            if (payload == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "存档 Payload 为空");
                return;
            }

            Dictionary<ItemInstanceId, ItemInstanceDto> items = ValidateItems(
                payload.Items,
                $"{path}.items",
                issues);
            Dictionary<ItemInstanceId, string> ownership = new Dictionary<ItemInstanceId, string>();
            ValidateProfile(payload.Profile, items, ownership, $"{path}.profile", issues);
            ValidateRun(payload.Run, payload.Profile, items, ownership, $"{path}.run", issues);
            if (issues.Count == 0)
            {
                var statusIssues = new List<DtoMapIssue>();
                StatusSaveValidation.Validate(payload.Run.Statuses, payload, null, statusIssues);
                foreach (DtoMapIssue issue in statusIssues)
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, issue.Path, issue.Message);
                }
            }

            foreach (KeyValuePair<ItemInstanceId, ItemInstanceDto> pair in items)
            {
                if (!ownership.ContainsKey(pair.Key))
                {
                    Add(
                        issues,
                        SaveDataIssueCode.InvalidOwnership,
                        $"{path}.items[{pair.Key.Value}]",
                        "物品实例没有唯一持有者");
                }
            }
        }

        static Dictionary<ItemInstanceId, ItemInstanceDto> ValidateItems(
            IReadOnlyList<ItemInstanceDto> itemDtos,
            string path,
            List<SaveDataIssue> issues)
        {
            Dictionary<ItemInstanceId, ItemInstanceDto> items = new Dictionary<ItemInstanceId, ItemInstanceDto>();

            if (itemDtos == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "物品表为空");
                return items;
            }

            if (itemDtos.Count > LocalSaveFormat.MaximumItems)
            {
                Add(issues, SaveDataIssueCode.LimitExceeded, path, "物品数量超过 V1 上限");
            }

            for (int i = 0; i < itemDtos.Count; i++)
            {
                ItemInstanceDto item = itemDtos[i];
                string itemPath = $"{path}[{i}]";

                if (item == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, itemPath, "物品为空");
                    continue;
                }

                if (!ItemInstanceId.TryParse(item.InstanceId, out ItemInstanceId id))
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, $"{itemPath}.instanceId", "物品实例 ID 非法");
                }
                else if (!items.TryAdd(id, item))
                {
                    Add(issues, SaveDataIssueCode.DuplicateInstanceId, $"{itemPath}.instanceId", "物品实例 ID 重复");
                }

                ValidateContentId(item.BaseContentId, ContentNamespaces.Item, $"{itemPath}.baseContentId", issues);

                if (!Enum.IsDefined(typeof(ItemRarity), item.Rarity))
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, $"{itemPath}.rarity", "物品稀有度非法");
                }

                if (item.ItemLevel <= 0)
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, $"{itemPath}.itemLevel", "物品等级必须大于 0");
                }
                if (item.Quantity <= 0)
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, path, "物品数量必须大于 0");
                }

                ValidateFinite(item.Durability, $"{itemPath}.durability", issues);
                ValidateModifiers(item.ImplicitModifiers, $"{itemPath}.implicitModifiers", issues);
                ValidateAffixes(item.Prefixes, ContentNamespaces.Affix, $"{itemPath}.prefixes", issues);
                ValidateAffixes(item.Suffixes, ContentNamespaces.Affix, $"{itemPath}.suffixes", issues);
            }

            return items;
        }

        static void ValidateProfile(
            ProfileSaveData profile,
            IReadOnlyDictionary<ItemInstanceId, ItemInstanceDto> items,
            IDictionary<ItemInstanceId, string> ownership,
            string path,
            List<SaveDataIssue> issues)
        {
            if (profile == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "Profile 数据为空");
                return;
            }

            if (!ContentId.IsValidSegment(profile.ProfileId))
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.profileId", "Profile ID 非法");
            }

            try
            {
                _ = new PlayerId(profile.PlayerId);
            }
            catch (ArgumentException)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.playerId", "Player ID 非法");
            }

            if (profile.Gold < 0)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.gold", "金币不能小于 0");
            }

            ValidateInventory(profile.Inventory, items, ownership, $"{path}.inventory", issues);
            ValidateEquipment(profile.Equipment, profile.PlayerId, items, ownership, $"{path}.equipment", issues);
        }

        static void ValidateInventory(
            InventoryLayoutDto inventory,
            IReadOnlyDictionary<ItemInstanceId, ItemInstanceDto> items,
            IDictionary<ItemInstanceId, string> ownership,
            string path,
            List<SaveDataIssue> issues)
        {
            if (inventory == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "背包数据为空");
                return;
            }

            if (inventory.Width <= 0 || inventory.Height <= 0)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, path, "背包尺寸必须大于 0");
            }

            if (inventory.Placements == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, $"{path}.placements", "背包位置集合为空");
                return;
            }

            for (int i = 0; i < inventory.Placements.Count; i++)
            {
                InventoryPlacementDto placement = inventory.Placements[i];
                string placementPath = $"{path}.placements[{i}]";

                if (placement == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, placementPath, "背包位置为空");
                    continue;
                }

                ValidateItemReference(placement.ItemInstanceId, items, ownership, placementPath, issues);

                if (placement.X < 0
                    || placement.Y < 0
                    || placement.Width <= 0
                    || placement.Height <= 0
                    || placement.X + placement.Width > inventory.Width
                    || placement.Y + placement.Height > inventory.Height)
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, placementPath, "背包位置超出边界");
                }
            }
        }

        static void ValidateEquipment(
            IReadOnlyList<EquipmentLoadoutDto> equipment,
            string playerId,
            IReadOnlyDictionary<ItemInstanceId, ItemInstanceDto> items,
            IDictionary<ItemInstanceId, string> ownership,
            string path,
            List<SaveDataIssue> issues)
        {
            if (equipment == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "装备集合为空");
                return;
            }

            HashSet<string> actorIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < equipment.Count; i++)
            {
                EquipmentLoadoutDto loadout = equipment[i];
                string loadoutPath = $"{path}[{i}]";

                if (loadout == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, loadoutPath, "装备数据为空");
                    continue;
                }

                if (!string.Equals(loadout.ActorInstanceId, playerId, StringComparison.Ordinal))
                {
                    Add(issues, SaveDataIssueCode.InvalidOwnership, $"{loadoutPath}.actorInstanceId", "V1 只允许玩家装备所有者");
                }

                if (!actorIds.Add(loadout.ActorInstanceId))
                {
                    Add(issues, SaveDataIssueCode.DuplicateInstanceId, $"{loadoutPath}.actorInstanceId", "装备所有者重复");
                }

                if (loadout.Entries == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, $"{loadoutPath}.entries", "装备槽集合为空");
                    continue;
                }

                HashSet<EquipmentSlot> slots = new HashSet<EquipmentSlot>();

                for (int entryIndex = 0; entryIndex < loadout.Entries.Count; entryIndex++)
                {
                    EquipmentEntryDto entry = loadout.Entries[entryIndex];
                    string entryPath = $"{loadoutPath}.entries[{entryIndex}]";

                    if (entry == null)
                    {
                        Add(issues, SaveDataIssueCode.MissingValue, entryPath, "装备槽数据为空");
                        continue;
                    }

                    if (!Enum.IsDefined(typeof(EquipmentSlot), entry.Slot) || !slots.Add(entry.Slot))
                    {
                        Add(issues, SaveDataIssueCode.InvalidValue, $"{entryPath}.slot", "装备槽非法或重复");
                    }

                    ValidateItemReference(entry.ItemInstanceId, items, ownership, entryPath, issues);
                }
            }
        }

        static void ValidateRun(
            RunSaveData run,
            ProfileSaveData profile,
            IReadOnlyDictionary<ItemInstanceId, ItemInstanceDto> items,
            IDictionary<ItemInstanceId, string> ownership,
            string path,
            List<SaveDataIssue> issues)
        {
            if (run == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "Run 数据为空");
                return;
            }

            RunId runId = ValidateRunInstanceState(run.InstanceIds, $"{path}.instanceIds", issues);
            ValidateRandom(run.Random, $"{path}.random", issues);
            ValidatePlayer(run.Player, profile, $"{path}.player", issues);
            long maximumMonsterSequence = ValidateMonsters(run.Monsters, runId, $"{path}.monsters", issues);
            long maximumDropSequence = ValidateWorldDrops(
                run.WorldDrops,
                runId,
                items,
                ownership,
                $"{path}.worldDrops",
                issues);
            ValidateMerchant(run.Merchant, items, ownership, $"{path}.merchant", issues);

            if (run.InstanceIds != null)
            {
                if (run.InstanceIds.NextMonsterSequence <= maximumMonsterSequence)
                {
                    Add(issues, SaveDataIssueCode.InvalidSequence, $"{path}.instanceIds.nextMonsterSequence", "下一怪物序号与现有实例冲突");
                }

                if (run.InstanceIds.NextWorldDropSequence <= maximumDropSequence)
                {
                    Add(issues, SaveDataIssueCode.InvalidSequence, $"{path}.instanceIds.nextWorldDropSequence", "下一掉落序号与现有实例冲突");
                }
            }
        }

        static RunId ValidateRunInstanceState(
            RunInstanceIdStateDto state,
            string path,
            List<SaveDataIssue> issues)
        {
            if (state == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "Run 实例 ID 状态为空");
                return default;
            }

            if (!RunId.TryParse(state.RunId, out RunId runId))
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.runId", "Run ID 非法");
            }

            if (state.NextMonsterSequence <= 0)
            {
                Add(issues, SaveDataIssueCode.InvalidSequence, $"{path}.nextMonsterSequence", "下一怪物序号必须大于 0");
            }

            if (state.NextWorldDropSequence <= 0)
            {
                Add(issues, SaveDataIssueCode.InvalidSequence, $"{path}.nextWorldDropSequence", "下一掉落序号必须大于 0");
            }

            return runId;
        }

        static void ValidateRandom(
            GameplayRandomStateDto random,
            string path,
            List<SaveDataIssue> issues)
        {
            if (random == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "随机状态为空");
                return;
            }

            if (random.Channels == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, $"{path}.channels", "随机通道集合为空");
                return;
            }

            HashSet<GameplayRandomChannel> channels = new HashSet<GameplayRandomChannel>();

            for (int i = 0; i < random.Channels.Count; i++)
            {
                GameplayRandomChannelStateDto channelState = random.Channels[i];
                string channelPath = $"{path}.channels[{i}]";

                if (channelState == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, channelPath, "随机通道状态为空");
                    continue;
                }

                if (!Enum.IsDefined(typeof(GameplayRandomChannel), channelState.Channel))
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, $"{channelPath}.channel", "随机通道非法");
                }
                else if (!channels.Add(channelState.Channel))
                {
                    Add(issues, SaveDataIssueCode.DuplicateRandomChannel, $"{channelPath}.channel", "随机通道重复");
                }

                if (channelState.NextSequence < 0)
                {
                    Add(issues, SaveDataIssueCode.InvalidSequence, $"{channelPath}.nextSequence", "随机序号不能小于 0");
                }
            }

            Array values = Enum.GetValues(typeof(GameplayRandomChannel));

            for (int i = 0; i < values.Length; i++)
            {
                GameplayRandomChannel channel = (GameplayRandomChannel)values.GetValue(i);

                if (!channels.Contains(channel))
                {
                    Add(issues, SaveDataIssueCode.MissingRandomChannel, $"{path}.channels", $"缺少随机通道 {channel}");
                }
            }
        }

        static void ValidatePlayer(
            PlayerRunStateDto player,
            ProfileSaveData profile,
            string path,
            List<SaveDataIssue> issues)
        {
            if (player == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "玩家 Run 状态为空");
                return;
            }

            if (profile != null && !string.Equals(player.PlayerId, profile.PlayerId, StringComparison.Ordinal))
            {
                Add(issues, SaveDataIssueCode.InconsistentState, $"{path}.playerId", "Profile 与 Run 的 Player ID 不一致");
            }

            try
            {
                _ = new PlayerId(player.PlayerId);
            }
            catch (ArgumentException)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, $"{path}.playerId", "Player ID 非法");
            }

            ValidateContentId(player.DefinitionContentId, ContentNamespaces.Actor, $"{path}.definitionContentId", issues);
            ValidateContentId(player.SkillContentId, ContentNamespaces.Skill, $"{path}.skillContentId", issues);
            ValidateVector(player.Position, $"{path}.position", issues);
            ValidateResources(player.Resources, $"{path}.resources", issues);
            ValidateNonNegativeFinite(player.RespawnRemainingSeconds, $"{path}.respawnRemainingSeconds", issues);
            ValidateNonNegativeFinite(player.AutoCastCooldownRemainingSeconds, $"{path}.autoCastCooldownRemainingSeconds", issues);

            if (player.Resources != null)
            {
                if (player.Resources.IsAlive && player.RespawnRemainingSeconds != 0f)
                {
                    Add(issues, SaveDataIssueCode.InconsistentState, $"{path}.respawnRemainingSeconds", "存活玩家不能有复活倒计时");
                }

                if (!player.Resources.IsAlive && player.RespawnRemainingSeconds <= 0f)
                {
                    Add(issues, SaveDataIssueCode.InconsistentState, $"{path}.respawnRemainingSeconds", "死亡玩家必须有复活倒计时");
                }
            }
        }

        static void IndexMapActors(IReadOnlyList<MonsterRunStateDto> monsters, string mapId, Dictionary<string, string> actorMaps)
        {
            if (monsters == null) { return; }
            foreach (MonsterRunStateDto monster in monsters)
            { if (!string.IsNullOrEmpty(monster?.InstanceId)) { actorMaps.TryAdd(monster.InstanceId, mapId); } }
        }

        static void ValidateBoss(BossEncounterStateDto boss, IReadOnlyList<MonsterRunStateDto> monsters,
            string path, List<SaveDataIssue> issues)
        {
            if (boss == null || !boss.IsValid)
            { Add(issues, SaveDataIssueCode.InvalidValue, path, "Boss 名额状态非法"); return; }
            int count = monsters?.Count(monster => monster?.Boss != null) ?? 0;
            if (boss.Status == BossEncounterStatus.Alive)
            {
                if (count != 1 || !monsters.Any(monster => monster.Boss != null && monster.InstanceId == boss.InstanceId
                    && monster.DefinitionContentId == boss.MonsterDefinitionId))
                { Add(issues, SaveDataIssueCode.InconsistentState, path, "活跃 Boss 名额与怪物实例不一致"); }
            }
            else if (count != 0)
            { Add(issues, SaveDataIssueCode.InconsistentState, path, "非活跃 Boss 名额包含 Boss 怪物"); }
        }

        static void ValidateNavigation(IReadOnlyList<MonsterRunStateDto> monsters, string mapId, string playerId,
            IReadOnlyDictionary<string, string> actorMaps, List<SaveDataIssue> issues)
        {
            if (monsters == null) { return; }
            foreach (MonsterRunStateDto monster in monsters)
            {
                if (monster == null) { continue; }
                NavigationIntentDto intent = monster.Navigation;
                if (monster.Behavior != null && !string.IsNullOrEmpty(monster.Behavior.TargetId) && monster.Behavior.TargetId != "player:" + playerId)
                { Add(issues, SaveDataIssueCode.InvalidValue, "payload.run.monsters.behavior.targetId", "行为目标必须是本次 Run 的玩家"); }
                if (monster.Boss != null && !string.IsNullOrEmpty(monster.Boss.TargetId) && monster.Boss.TargetId != "player:" + playerId)
                { Add(issues, SaveDataIssueCode.InvalidValue, "payload.run.monsters.boss.targetId", "Boss 行为目标必须是本次 Run 的玩家"); }
                if (intent == null || !intent.IsValid
                    || (intent.Target == NavigationTargetKind.Actor || intent.Target == NavigationTargetKind.Point) && intent.MapId != mapId
                    || intent.Target == NavigationTargetKind.Actor && intent.ActorId != "player:" + playerId
                        && (!MonsterInstanceId.TryParse(intent.ActorId, out MonsterInstanceId target)
                            || !MonsterInstanceId.TryParse(monster.InstanceId, out MonsterInstanceId owner) || target.RunId != owner.RunId
                            || actorMaps.TryGetValue(intent.ActorId, out string targetMap) && targetMap != mapId))
                { Add(issues, SaveDataIssueCode.InvalidValue, "payload.run.monsters.navigation", "导航模式或同图目标身份非法"); }
            }
        }

        static long ValidateMonsters(
            IReadOnlyList<MonsterRunStateDto> monsters,
            RunId expectedRunId,
            string path,
            List<SaveDataIssue> issues)
        {
            if (monsters == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "怪物集合为空");
                return 0;
            }

            if (monsters.Count > LocalSaveFormat.MaximumMonsters)
            {
                Add(issues, SaveDataIssueCode.LimitExceeded, path, "怪物数量超过 V1 上限");
            }

            HashSet<MonsterInstanceId> ids = new HashSet<MonsterInstanceId>();
            long maximumSequence = 0;

            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterRunStateDto monster = monsters[i];
                string monsterPath = $"{path}[{i}]";

                if (monster == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, monsterPath, "怪物状态为空");
                    continue;
                }

                if (!MonsterInstanceId.TryParse(monster.InstanceId, out MonsterInstanceId id))
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, $"{monsterPath}.instanceId", "怪物实例 ID 非法");
                }
                else
                {
                    if (!ids.Add(id))
                    {
                        Add(issues, SaveDataIssueCode.DuplicateInstanceId, $"{monsterPath}.instanceId", "怪物实例 ID 重复");
                    }

                    if (expectedRunId.Value != null && id.RunId != expectedRunId)
                    {
                        Add(issues, SaveDataIssueCode.InconsistentState, $"{monsterPath}.instanceId", "怪物实例不属于当前 Run");
                    }

                    maximumSequence = Math.Max(maximumSequence, id.Sequence);
                }

                ValidateContentId(monster.DefinitionContentId, ContentNamespaces.Monster, $"{monsterPath}.definitionContentId", issues);
                ValidateMonsterSeeds(monster.RandomSeeds, $"{monsterPath}.randomSeeds", issues);
                ValidateNonNegativeFinite(monster.BaseMaxHealth, $"{monsterPath}.baseMaxHealth", issues);
                ValidateStats(monster.BaseStats, $"{monsterPath}.baseStats", issues);
                ValidateStats(monster.EffectiveStats, $"{monsterPath}.effectiveStats", issues);
                ValidateMonsterAffixes(monster.Affixes, $"{monsterPath}.affixes", issues);
                ValidateModifiers(monster.Modifiers, $"{monsterPath}.modifiers", issues);
                ValidateVector(monster.Position, $"{monsterPath}.position", issues);
                ValidateResources(monster.Resources, $"{monsterPath}.resources", issues);
                if (monster.Behavior == null || !monster.Behavior.IsValid || monster.Behavior.Mode == MonsterBehaviorMode.Dead)
                { Add(issues, SaveDataIssueCode.InvalidValue, $"{monsterPath}.behavior", "行为版本、状态或动作阶段非法"); }
                if (monster.Boss != null && !monster.Boss.IsValid)
                { Add(issues, SaveDataIssueCode.InvalidValue, $"{monsterPath}.boss", "Boss 动作状态非法"); }
                ValidateNonNegativeFinite(
                    monster.ContactDamageCooldownRemainingSeconds,
                    $"{monsterPath}.contactDamageCooldownRemainingSeconds",
                    issues);

                if (monster.Resources != null && !monster.Resources.IsAlive)
                {
                    Add(issues, SaveDataIssueCode.InconsistentState, $"{monsterPath}.resources.isAlive", "V1 不保存死亡怪物");
                }
            }

            return maximumSequence;
        }

        static long ValidateWorldDrops(
            IReadOnlyList<WorldDropStateDto> drops,
            RunId expectedRunId,
            IReadOnlyDictionary<ItemInstanceId, ItemInstanceDto> items,
            IDictionary<ItemInstanceId, string> ownership,
            string path,
            List<SaveDataIssue> issues)
        {
            if (drops == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "世界掉落集合为空");
                return 0;
            }

            if (drops.Count > LocalSaveFormat.MaximumWorldDrops)
            {
                Add(issues, SaveDataIssueCode.LimitExceeded, path, "世界掉落数量超过 V1 上限");
            }

            HashSet<WorldDropId> ids = new HashSet<WorldDropId>();
            long maximumSequence = 0;

            for (int i = 0; i < drops.Count; i++)
            {
                WorldDropStateDto drop = drops[i];
                string dropPath = $"{path}[{i}]";

                if (drop == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, dropPath, "世界掉落为空");
                    continue;
                }

                if (!WorldDropId.TryParse(drop.InstanceId, out WorldDropId id))
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, $"{dropPath}.instanceId", "世界掉落实例 ID 非法");
                }
                else
                {
                    if (!ids.Add(id))
                    {
                        Add(issues, SaveDataIssueCode.DuplicateInstanceId, $"{dropPath}.instanceId", "世界掉落实例 ID 重复");
                    }

                    if (expectedRunId.Value != null && id.RunId != expectedRunId)
                    {
                        Add(issues, SaveDataIssueCode.InconsistentState, $"{dropPath}.instanceId", "世界掉落不属于当前 Run");
                    }

                    maximumSequence = Math.Max(maximumSequence, id.Sequence);
                }

                ValidateItemReference(drop.ItemInstanceId, items, ownership, dropPath, issues);
                ValidateVector(drop.Position, $"{dropPath}.position", issues);
            }

            return maximumSequence;
        }

        static void ValidateMerchant(
            MerchantInventoryDto merchant,
            IReadOnlyDictionary<ItemInstanceId, ItemInstanceDto> items,
            IDictionary<ItemInstanceId, string> ownership,
            string path,
            List<SaveDataIssue> issues)
        {
            if (merchant == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "商人状态为空");
                return;
            }

            ValidateContentId(merchant.TraderContentId, ContentNamespaces.Trader, $"{path}.traderContentId", issues);
            ValidateNonNegativeFinite(merchant.BuyMultiplier, $"{path}.buyMultiplier", issues);
            ValidateNonNegativeFinite(merchant.SellMultiplier, $"{path}.sellMultiplier", issues);

            if (merchant.OrderedItemInstanceIds == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, $"{path}.orderedItemInstanceIds", "商人库存为空");
                return;
            }

            for (int i = 0; i < merchant.OrderedItemInstanceIds.Count; i++)
            {
                ValidateItemReference(
                    merchant.OrderedItemInstanceIds[i],
                    items,
                    ownership,
                    $"{path}.orderedItemInstanceIds[{i}]",
                    issues);
            }
        }

        static void ValidateSpawner(
            MonsterSpawnerStateDto spawner,
            string path,
            List<SaveDataIssue> issues,
            IReadOnlyList<MonsterRunStateDto> monsters)
        {
            if (spawner == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "刷怪器状态为空");
                return;
            }

            if (!string.IsNullOrEmpty(spawner.SpawnContentId) || spawner.IsRunning)
            { ValidateContentId(spawner.SpawnContentId, ContentNamespaces.Spawn, $"{path}.spawnContentId", issues); }
            ValidateNonNegativeFinite(spawner.NextSpawnRemainingSeconds, $"{path}.nextSpawnRemainingSeconds", issues);
            if (!Enum.IsDefined(typeof(FiniteSpawnMode), spawner.Mode) || spawner.Batches == null || spawner.Slots == null)
            { Add(issues, SaveDataIssueCode.InvalidValue, path, "刷怪名额模式或集合非法"); return; }
            if (spawner.Mode != FiniteSpawnMode.Finite)
            {
                if (spawner.Batches.Count != 0 || spawner.Slots.Count != 0
                    || spawner.Mode == FiniteSpawnMode.LegacyPreserved && spawner.IsRunning)
                { Add(issues, SaveDataIssueCode.InconsistentState, path, "旧图不能含有限名额或运行补怪循环"); }
                return;
            }
            if (spawner.Batches.Count > MapDocumentLimits.MaxSpawnBatches
                || spawner.Slots.Count > LocalSaveFormat.MaximumMapMonsters)
            { Add(issues, SaveDataIssueCode.LimitExceeded, path, "刷怪批次或名额超限"); return; }
            var batchIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpawnBatchStateDto batch in spawner.Batches)
            {
                if (batch == null || !ContentId.IsValidSegment(batch.BatchId) || !batchIds.Add(batch.BatchId))
                { Add(issues, SaveDataIssueCode.InvalidValue, path + ".batches", "批次 ID 非法或重复"); }
            }
            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpawnSlotStateDto slot in spawner.Slots)
            {
                if (slot == null || string.IsNullOrEmpty(slot.SlotId) || !slotIds.Add(slot.SlotId)
                    || !batchIds.Contains(slot.BatchId) || !Enum.IsDefined(typeof(SpawnSlotStatus), slot.Status))
                { Add(issues, SaveDataIssueCode.InvalidValue, path + ".slots", "名额身份或状态非法"); continue; }
                ValidateContentId(slot.MonsterDefinitionId, ContentNamespaces.Monster, path + ".slots.monsterDefinitionId", issues);
                if (slot.Status == SpawnSlotStatus.Alive)
                {
                    if (!MonsterInstanceId.TryParse(slot.InstanceId, out _) || !instanceIds.Add(slot.InstanceId)
                        || monsters?.Count(monster => monster?.InstanceId == slot.InstanceId) != 1)
                    { Add(issues, SaveDataIssueCode.InconsistentState, path + ".slots.instanceId", "存活名额与怪物记录不一致"); }
                }
                else if (!string.IsNullOrEmpty(slot.InstanceId))
                { Add(issues, SaveDataIssueCode.InconsistentState, path + ".slots.instanceId", "未生成或已消耗名额不能持有实例 ID"); }
            }
            if (monsters != null && spawner.Slots.Count(slot => slot?.Status == SpawnSlotStatus.Alive)
                != monsters.Count(monster => monster != null && monster.Boss == null))
            { Add(issues, SaveDataIssueCode.InconsistentState, path + ".slots", "普通怪物与存活名额数量不一致"); }
        }

        static void ValidateMonsterSeeds(
            MonsterRandomSeedsDto seeds,
            string path,
            List<SaveDataIssue> issues)
        {
            if (seeds == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "怪物随机种子为空");
                return;
            }

            MonsterInstanceRandomSeeds expected = new MonsterInstanceRandomSeeds(seeds.RootSeed);

            if (seeds.HealthSeed != expected.HealthSeed
                || seeds.AffixCountSeed != expected.AffixCountSeed
                || seeds.AffixSelectionSeed != expected.AffixSelectionSeed)
            {
                Add(issues, SaveDataIssueCode.InconsistentState, path, "怪物派生随机种子与根种子不一致");
            }
        }

        static void ValidateStats(
            IReadOnlyList<StatValueDto> stats,
            string path,
            List<SaveDataIssue> issues)
        {
            if (stats == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "属性集合为空");
                return;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < stats.Count; i++)
            {
                StatValueDto stat = stats[i];
                string statPath = $"{path}[{i}]";

                if (stat == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, statPath, "属性为空");
                    continue;
                }

                ValidateContentId(stat.StatContentId, ContentNamespaces.Stat, $"{statPath}.statContentId", issues);

                if (!ids.Add(stat.StatContentId))
                {
                    Add(issues, SaveDataIssueCode.DuplicateInstanceId, $"{statPath}.statContentId", "属性 ID 重复");
                }

                ValidateFinite(stat.Value, $"{statPath}.value", issues);
            }
        }

        static void ValidateMonsterAffixes(
            IReadOnlyList<MonsterAffixInstanceDto> affixes,
            string path,
            List<SaveDataIssue> issues)
        {
            if (affixes == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "怪物词条集合为空");
                return;
            }

            for (int i = 0; i < affixes.Count; i++)
            {
                MonsterAffixInstanceDto affix = affixes[i];
                string affixPath = $"{path}[{i}]";

                if (affix == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, affixPath, "怪物词条为空");
                    continue;
                }

                ValidateContentId(
                    affix.DefinitionContentId,
                    ContentNamespaces.MonsterAffix,
                    $"{affixPath}.definitionContentId",
                    issues);
                ValidateModifiers(affix.Modifiers, $"{affixPath}.modifiers", issues);
            }
        }

        static void ValidateAffixes(
            IReadOnlyList<AffixInstanceDto> affixes,
            string expectedNamespace,
            string path,
            List<SaveDataIssue> issues)
        {
            if (affixes == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "词条集合为空");
                return;
            }

            for (int i = 0; i < affixes.Count; i++)
            {
                AffixInstanceDto affix = affixes[i];
                string affixPath = $"{path}[{i}]";

                if (affix == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, affixPath, "词条为空");
                    continue;
                }

                ValidateContentId(affix.DefinitionContentId, expectedNamespace, $"{affixPath}.definitionContentId", issues);
                ValidateModifiers(affix.Modifiers, $"{affixPath}.modifiers", issues);
            }
        }

        static void ValidateModifiers(
            IReadOnlyList<ModifierInstanceDto> modifiers,
            string path,
            List<SaveDataIssue> issues)
        {
            if (modifiers == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "修改器集合为空");
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierInstanceDto modifier = modifiers[i];
                string modifierPath = $"{path}[{i}]";

                if (modifier == null)
                {
                    Add(issues, SaveDataIssueCode.MissingValue, modifierPath, "修改器为空");
                    continue;
                }

                if (!string.IsNullOrEmpty(modifier.StatContentId))
                {
                    ValidateContentId(
                        modifier.StatContentId,
                        ContentNamespaces.Stat,
                        $"{modifierPath}.statContentId",
                        issues);
                }

                if (!Enum.IsDefined(typeof(ModifierOperation), modifier.Operation)
                    || !Enum.IsDefined(typeof(ModifierScope), modifier.Scope)
                    || !Enum.IsDefined(typeof(DamageType), modifier.FromDamageType)
                    || !Enum.IsDefined(typeof(DamageType), modifier.ToDamageType)
                    || !Enum.IsDefined(typeof(CombatTagScope), modifier.QueryScope))
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, modifierPath, "修改器枚举值非法");
                }

                ValidateFinite(modifier.Value, $"{modifierPath}.value", issues);
                ValidateContentIdList(modifier.RequiredAllTagIds, ContentNamespaces.Tag, $"{modifierPath}.requiredAllTagIds", issues);
                ValidateContentIdList(modifier.RequiredAnyTagIds, ContentNamespaces.Tag, $"{modifierPath}.requiredAnyTagIds", issues);
                ValidateContentIdList(modifier.BlockedTagIds, ContentNamespaces.Tag, $"{modifierPath}.blockedTagIds", issues);
            }
        }

        static void ValidateContentIdList(
            IReadOnlyList<string> values,
            string expectedNamespace,
            string path,
            List<SaveDataIssue> issues)
        {
            if (values == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "ContentId 集合为空");
                return;
            }

            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < values.Count; i++)
            {
                ValidateContentId(values[i], expectedNamespace, $"{path}[{i}]", issues);

                if (!unique.Add(values[i]))
                {
                    Add(issues, SaveDataIssueCode.InvalidValue, $"{path}[{i}]", "ContentId 重复");
                }
            }
        }

        static void ValidateResources(
            CombatResourceStateDto resources,
            string path,
            List<SaveDataIssue> issues)
        {
            if (resources == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "战斗资源为空");
                return;
            }

            ValidateFinite(resources.CurrentHealth, $"{path}.currentHealth", issues);
            ValidateFinite(resources.MaxHealth, $"{path}.maxHealth", issues);
            ValidateFinite(resources.CurrentMana, $"{path}.currentMana", issues);
            ValidateFinite(resources.MaxMana, $"{path}.maxMana", issues);

            if (resources.MaxHealth <= 0f
                || resources.CurrentHealth < 0f
                || resources.CurrentHealth > resources.MaxHealth
                || resources.MaxMana < 0f
                || resources.CurrentMana < 0f
                || resources.CurrentMana > resources.MaxMana)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, path, "战斗资源范围非法");
            }

            if (resources.IsAlive && resources.CurrentHealth <= 0f
                || !resources.IsAlive && resources.CurrentHealth != 0f)
            {
                Add(issues, SaveDataIssueCode.InconsistentState, path, "存活状态与当前生命不一致");
            }
        }

        static void ValidateVector(Vector3Dto value, string path, List<SaveDataIssue> issues)
        {
            if (value == null)
            {
                Add(issues, SaveDataIssueCode.MissingValue, path, "位置为空");
                return;
            }

            ValidateFinite(value.X, $"{path}.x", issues);
            ValidateFinite(value.Y, $"{path}.y", issues);
            ValidateFinite(value.Z, $"{path}.z", issues);
        }

        static void ValidateItemReference(
            string rawId,
            IReadOnlyDictionary<ItemInstanceId, ItemInstanceDto> items,
            IDictionary<ItemInstanceId, string> ownership,
            string path,
            List<SaveDataIssue> issues)
        {
            if (!ItemInstanceId.TryParse(rawId, out ItemInstanceId id))
            {
                Add(issues, SaveDataIssueCode.InvalidValue, path, "物品引用 ID 非法");
                return;
            }

            if (!items.ContainsKey(id))
            {
                Add(issues, SaveDataIssueCode.DanglingReference, path, "物品引用悬空");
                return;
            }

            if (ownership.TryGetValue(id, out string previousPath))
            {
                Add(issues, SaveDataIssueCode.DuplicateOwnership, path, $"物品已由 {previousPath} 持有");
                return;
            }

            ownership.Add(id, path);
        }

        static void ValidateContentId(
            string value,
            string expectedNamespace,
            string path,
            List<SaveDataIssue> issues)
        {
            if (!ContentId.TryParse(value, out ContentId contentId)
                || !string.Equals(contentId.Namespace, expectedNamespace, StringComparison.Ordinal))
            {
                Add(issues, SaveDataIssueCode.InvalidValue, path, $"ContentId 必须属于 {expectedNamespace} 命名空间");
            }
        }

        static void ValidateNonNegativeFinite(
            float value,
            string path,
            List<SaveDataIssue> issues)
        {
            ValidateFinite(value, path, issues);

            if (value < 0f)
            {
                Add(issues, SaveDataIssueCode.InvalidValue, path, "数值不能小于 0");
            }
        }

        static void ValidateFinite(float value, string path, List<SaveDataIssue> issues)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                Add(issues, SaveDataIssueCode.NonFiniteNumber, path, "数值必须是有限浮点数");
            }
        }

        static void ValidateSummary(
            SaveSummaryDto summary,
            SavePayloadDto payload,
            string path,
            List<SaveDataIssue> issues)
        {
            if (summary == null || payload.Profile == null || payload.Run == null)
            {
                return;
            }

            string runId = payload.Run.InstanceIds != null ? payload.Run.InstanceIds.RunId : string.Empty;
            CombatResourceStateDto resources = payload.Run.Player?.Resources;

            if (!string.Equals(summary.RunId, runId, StringComparison.Ordinal)
                || summary.Gold != payload.Profile.Gold
                || summary.ItemCount != (payload.Items?.Count ?? 0)
                || resources != null
                && (summary.CurrentHealth != resources.CurrentHealth
                    || summary.MaxHealth != resources.MaxHealth))
            {
                Add(issues, SaveDataIssueCode.InconsistentState, path, "Header 摘要与 Payload 不一致");
            }

            ValidateFinite(summary.CurrentHealth, $"{path}.currentHealth", issues);
            ValidateFinite(summary.MaxHealth, $"{path}.maxHealth", issues);
        }

        static bool TryParseUtc(string value, out DateTimeOffset result)
        {
            if (!DateTimeOffset.TryParseExact(
                    value,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out result))
            {
                return false;
            }

            return result.Offset == TimeSpan.Zero
                && string.Equals(result.ToString("O", CultureInfo.InvariantCulture), value, StringComparison.Ordinal);
        }

        static bool IsLowerHex(string value, int expectedLength)
        {
            if (value == null || value.Length != expectedLength)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (!(character >= '0' && character <= '9')
                    && !(character >= 'a' && character <= 'f'))
                {
                    return false;
                }
            }

            return true;
        }

        static SaveDataValidationResult Result(List<SaveDataIssue> issues)
        {
            return new SaveDataValidationResult(issues.AsReadOnly());
        }

        static void Add(
            ICollection<SaveDataIssue> issues,
            SaveDataIssueCode code,
            string path,
            string message)
        {
            issues.Add(new SaveDataIssue(code, path, message));
        }
    }
}
