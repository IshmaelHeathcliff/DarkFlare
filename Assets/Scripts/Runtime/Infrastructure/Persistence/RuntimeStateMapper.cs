using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkFlare
{
    public enum DtoMapIssueCode
    {
        NullInput,
        InvalidInstanceId,
        DuplicateInstanceId,
        InvalidContentId,
        MissingContent,
        ContentTypeMismatch,
        InvalidValue,
        DanglingReference,
        InvalidEquipmentSlot,
        InventoryPlacementInvalid,
        OwnershipConflict,
    }

    public sealed class DtoMapIssue
    {
        public DtoMapIssueCode Code { get; }

        public string Path { get; }

        public string Message { get; }

        public DtoMapIssue(DtoMapIssueCode code, string path, string message)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public sealed class DtoMapResult<T>
    {
        public T Value { get; }

        public IReadOnlyList<DtoMapIssue> Issues { get; }

        public bool Succeeded => Issues.Count == 0 && Value != null;

        internal DtoMapResult(T value, IReadOnlyList<DtoMapIssue> issues)
        {
            Value = value;
            Issues = issues;
        }
    }

    public sealed class DetachedEquipmentLoadout
    {
        public string ActorInstanceId { get; }

        public EquipmentLoadout Loadout { get; }

        internal DetachedEquipmentLoadout(string actorInstanceId, EquipmentLoadout loadout)
        {
            ActorInstanceId = actorInstanceId;
            Loadout = loadout;
        }
    }

    public sealed class DetachedRuntimeState
    {
        public IReadOnlyDictionary<ItemInstanceId, ItemInstance> Items { get; }

        public InventoryGrid Inventory { get; }

        public IReadOnlyList<DetachedEquipmentLoadout> Equipment { get; }

        public TraderDefinition Trader { get; }

        public IReadOnlyList<ItemInstance> MerchantStock { get; }

        public IReadOnlyList<ActorIdentityDto> Actors { get; }

        internal DetachedRuntimeState(
            IReadOnlyDictionary<ItemInstanceId, ItemInstance> items,
            InventoryGrid inventory,
            IReadOnlyList<DetachedEquipmentLoadout> equipment,
            TraderDefinition trader,
            IReadOnlyList<ItemInstance> merchantStock,
            IReadOnlyList<ActorIdentityDto> actors)
        {
            Items = items;
            Inventory = inventory;
            Equipment = equipment;
            Trader = trader;
            MerchantStock = merchantStock;
            Actors = actors;
        }
    }

    public static class RuntimeStateMapper
    {
        public static DtoMapResult<ItemInstanceDto> ToDto(ItemInstance item, ContentCatalog catalog)
        {
            List<DtoMapIssue> issues = new List<DtoMapIssue>();

            if (item == null)
            {
                Add(issues, DtoMapIssueCode.NullInput, "item", "物品实例为空");
                return Failed<ItemInstanceDto>(issues);
            }

            if (catalog == null)
            {
                Add(issues, DtoMapIssueCode.NullInput, "catalog", "内容目录为空");
                return Failed<ItemInstanceDto>(issues);
            }

            if (!item.Id.IsCanonical)
            {
                Add(issues, DtoMapIssueCode.InvalidInstanceId, "item.instanceId", $"物品实例 ID 非规范 UUID：{item.Id}");
            }

            string baseContentId = GetContentId(item.BaseDefinition, catalog, "item.baseContentId", issues);
            ItemInstanceDto dto = new ItemInstanceDto
            {
                InstanceId = item.Id.Value,
                BaseContentId = baseContentId,
                Rarity = item.Rarity,
                ItemLevel = item.ItemLevel,
                Seed = item.Seed,
                Quality = item.Quality,
                Durability = item.Durability,
                ImplicitModifiers = MapModifiersToDto(item.ImplicitModifiers, catalog, "item.implicitModifiers", issues),
                Prefixes = MapAffixesToDto(item.Prefixes, catalog, "item.prefixes", issues),
                Suffixes = MapAffixesToDto(item.Suffixes, catalog, "item.suffixes", issues),
            };
            return issues.Count == 0 ? Success(dto) : Failed<ItemInstanceDto>(issues);
        }

        public static DtoMapResult<ItemInstance> FromDto(ItemInstanceDto dto, ContentCatalog catalog)
        {
            List<DtoMapIssue> issues = new List<DtoMapIssue>();
            ItemInstance item = RestoreItem(dto, catalog, "item", issues);
            return issues.Count == 0 ? Success(item) : Failed<ItemInstance>(issues);
        }

        public static InventoryLayoutDto ToDto(InventoryGrid grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            InventoryLayoutDto dto = new InventoryLayoutDto
            {
                Width = grid.Width,
                Height = grid.Height,
            };

            foreach (KeyValuePair<ItemInstance, RectInt> pair in grid.Placements)
            {
                dto.Placements.Add(new InventoryPlacementDto
                {
                    ItemInstanceId = pair.Key.Id.Value,
                    X = pair.Value.x,
                    Y = pair.Value.y,
                    Width = pair.Value.width,
                    Height = pair.Value.height,
                });
            }

            dto.Placements.Sort((left, right) =>
            {
                int row = left.Y.CompareTo(right.Y);
                return row != 0 ? row : left.X.CompareTo(right.X);
            });
            return dto;
        }

        public static EquipmentLoadoutDto ToDto(string actorInstanceId, EquipmentLoadout loadout)
        {
            EquipmentLoadoutDto dto = new EquipmentLoadoutDto
            {
                ActorInstanceId = actorInstanceId ?? string.Empty,
            };

            if (loadout == null)
            {
                return dto;
            }

            foreach (KeyValuePair<EquipmentSlot, ItemInstance> pair in loadout.Slots)
            {
                dto.Entries.Add(new EquipmentEntryDto
                {
                    Slot = pair.Key,
                    ItemInstanceId = pair.Value.Id.Value,
                });
            }

            dto.Entries.Sort((left, right) => left.Slot.CompareTo(right.Slot));
            return dto;
        }

        public static MerchantInventoryDto ToDto(
            TraderDefinition trader,
            IReadOnlyList<ItemInstance> stock,
            ContentCatalog catalog)
        {
            MerchantInventoryDto dto = new MerchantInventoryDto
            {
                TraderContentId = GetRequiredContentId(trader, catalog),
            };

            if (stock != null)
            {
                for (int i = 0; i < stock.Count; i++)
                {
                    dto.OrderedItemInstanceIds.Add(stock[i].Id.Value);
                }
            }

            return dto;
        }

        public static DtoMapResult<ActorIdentityDto> ToDto(
            PlayerController player,
            ContentCatalog catalog)
        {
            List<DtoMapIssue> issues = new List<DtoMapIssue>();

            if (player == null || player.Definition == null)
            {
                Add(issues, DtoMapIssueCode.NullInput, "actor.player", "玩家或玩家定义为空");
                return Failed<ActorIdentityDto>(issues);
            }

            ActorIdentityDto dto = new ActorIdentityDto
            {
                Kind = ActorIdentityKind.Player,
                InstanceId = player.Id.Value,
                DefinitionContentId = GetContentId(player.Definition, catalog, "actor.definitionContentId", issues),
            };
            return issues.Count == 0 ? Success(dto) : Failed<ActorIdentityDto>(issues);
        }

        public static DtoMapResult<ActorIdentityDto> ToDto(
            MonsterController monster,
            ContentCatalog catalog)
        {
            List<DtoMapIssue> issues = new List<DtoMapIssue>();

            if (monster == null || monster.Definition == null || monster.Instance == null)
            {
                Add(issues, DtoMapIssueCode.NullInput, "actor.monster", "怪物、怪物定义或实例为空");
                return Failed<ActorIdentityDto>(issues);
            }

            ActorIdentityDto dto = new ActorIdentityDto
            {
                Kind = ActorIdentityKind.Monster,
                InstanceId = monster.Instance.Id.Value,
                DefinitionContentId = GetContentId(monster.Definition, catalog, "actor.definitionContentId", issues),
            };
            return issues.Count == 0 ? Success(dto) : Failed<ActorIdentityDto>(issues);
        }

        public static DtoMapResult<DetachedRuntimeState> Restore(
            RuntimeStateDto dto,
            ContentCatalog catalog)
        {
            List<DtoMapIssue> issues = new List<DtoMapIssue>();

            if (dto == null || catalog == null)
            {
                Add(issues, DtoMapIssueCode.NullInput, "root", "DTO 或内容目录为空");
                return Failed<DetachedRuntimeState>(issues);
            }

            Dictionary<ItemInstanceId, ItemInstance> items = RestoreItems(dto.Items, catalog, issues);
            HashSet<ItemInstanceId> ownedItems = new HashSet<ItemInstanceId>();
            InventoryGrid inventory = RestoreInventory(dto.Inventory, items, ownedItems, issues);
            List<ActorIdentityDto> actors = ValidateActors(dto.Actors, catalog, issues);
            HashSet<string> actorIds = new HashSet<string>(actors.Select(actor => actor.InstanceId), StringComparer.Ordinal);
            List<DetachedEquipmentLoadout> equipment = RestoreEquipment(
                dto.Equipment,
                items,
                actorIds,
                ownedItems,
                issues);
            TraderDefinition trader = RestoreTrader(dto.Merchant, catalog, issues);
            List<ItemInstance> merchantStock = RestoreMerchantStock(
                dto.Merchant,
                items,
                ownedItems,
                issues);

            if (issues.Count > 0)
            {
                return Failed<DetachedRuntimeState>(issues);
            }

            DetachedRuntimeState state = new DetachedRuntimeState(
                new Dictionary<ItemInstanceId, ItemInstance>(items),
                inventory,
                equipment.AsReadOnly(),
                trader,
                merchantStock.AsReadOnly(),
                actors.AsReadOnly());
            return Success(state);
        }

        static Dictionary<ItemInstanceId, ItemInstance> RestoreItems(
            IReadOnlyList<ItemInstanceDto> itemDtos,
            ContentCatalog catalog,
            List<DtoMapIssue> issues)
        {
            Dictionary<ItemInstanceId, ItemInstance> items = new Dictionary<ItemInstanceId, ItemInstance>();
            IReadOnlyList<ItemInstanceDto> safeItems = itemDtos ?? Array.Empty<ItemInstanceDto>();

            for (int i = 0; i < safeItems.Count; i++)
            {
                string path = $"items[{i}]";
                ItemInstance item = RestoreItem(safeItems[i], catalog, path, issues);

                if (item == null)
                {
                    continue;
                }

                if (!items.TryAdd(item.Id, item))
                {
                    Add(issues, DtoMapIssueCode.DuplicateInstanceId, path, $"物品实例 ID 重复：{item.Id}");
                }
            }

            return items;
        }

        static ItemInstance RestoreItem(
            ItemInstanceDto dto,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            if (dto == null)
            {
                Add(issues, DtoMapIssueCode.NullInput, path, "物品 DTO 为空");
                return null;
            }

            if (!ItemInstanceId.TryParse(dto.InstanceId, out ItemInstanceId id))
            {
                Add(issues, DtoMapIssueCode.InvalidInstanceId, $"{path}.instanceId", $"非法物品实例 ID：{dto.InstanceId}");
                return null;
            }

            ItemBaseDefinition definition = Resolve<ItemBaseDefinition>(dto.BaseContentId, catalog, $"{path}.baseContentId", issues);

            if (definition == null)
            {
                return null;
            }

            if (!Enum.IsDefined(typeof(ItemRarity), dto.Rarity)
                || dto.ItemLevel <= 0
                || dto.Quality < 0
                || dto.Durability < 0f
                || dto.Durability > 1f)
            {
                Add(issues, DtoMapIssueCode.InvalidValue, path, "物品稀有度、等级、品质或耐久非法");
                return null;
            }

            List<ModifierInstance> implicitModifiers = RestoreModifiers(
                dto.ImplicitModifiers,
                catalog,
                $"{path}.implicitModifiers",
                issues);
            ItemInstance item = new ItemInstance(
                id,
                definition,
                dto.Rarity,
                dto.ItemLevel,
                dto.Seed,
                implicitModifiers)
            {
                Quality = dto.Quality,
                Durability = dto.Durability,
            };
            RestoreAffixes(item, dto.Prefixes, catalog, $"{path}.prefixes", issues);
            RestoreAffixes(item, dto.Suffixes, catalog, $"{path}.suffixes", issues);
            return item;
        }

        internal static List<ModifierInstanceDto> MapModifiersToDto(
            IReadOnlyList<ModifierInstance> modifiers,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            List<ModifierInstanceDto> result = new List<ModifierInstanceDto>();

            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierInstance modifier = modifiers[i];

                if (modifier == null)
                {
                    Add(issues, DtoMapIssueCode.NullInput, $"{path}[{i}]", "修改器为空");
                    continue;
                }

                result.Add(new ModifierInstanceDto
                {
                    StatContentId = MapOptionalLocalContentId(
                        ContentNamespaces.Stat,
                        modifier.StatId,
                        catalog,
                        $"{path}[{i}].statContentId",
                        issues),
                    Operation = modifier.Operation,
                    Scope = modifier.Scope,
                    Value = modifier.Value,
                    FromDamageType = modifier.FromDamageType,
                    ToDamageType = modifier.ToDamageType,
                    QueryScope = modifier.Query.ScopeMask,
                    UsesLegacyTagMatching = modifier.UsesLegacyTagMatching,
                    RequiredAllTagIds = MapTagIds(modifier.Query.RequiredAll, catalog, path, issues),
                    RequiredAnyTagIds = MapTagIds(modifier.Query.RequiredAny, catalog, path, issues),
                    BlockedTagIds = MapTagIds(modifier.Query.BlockedAny, catalog, path, issues),
                });
            }

            return result;
        }

        static List<AffixInstanceDto> MapAffixesToDto(
            IReadOnlyList<AffixInstance> affixes,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            List<AffixInstanceDto> result = new List<AffixInstanceDto>();

            for (int i = 0; i < affixes.Count; i++)
            {
                AffixInstance affix = affixes[i];

                if (affix == null)
                {
                    Add(issues, DtoMapIssueCode.NullInput, $"{path}[{i}]", "词条实例为空");
                    continue;
                }

                result.Add(new AffixInstanceDto
                {
                    DefinitionContentId = GetContentId(
                        affix.Definition,
                        catalog,
                        $"{path}[{i}].definitionContentId",
                        issues),
                    Modifiers = MapModifiersToDto(
                        affix.Modifiers,
                        catalog,
                        $"{path}[{i}].modifiers",
                        issues),
                });
            }

            return result;
        }

        internal static List<ModifierInstance> RestoreModifiers(
            IReadOnlyList<ModifierInstanceDto> dtos,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            List<ModifierInstance> result = new List<ModifierInstance>();
            IReadOnlyList<ModifierInstanceDto> safeDtos = dtos ?? Array.Empty<ModifierInstanceDto>();

            for (int i = 0; i < safeDtos.Count; i++)
            {
                ModifierInstanceDto dto = safeDtos[i];
                string itemPath = $"{path}[{i}]";

                if (dto == null)
                {
                    Add(issues, DtoMapIssueCode.NullInput, itemPath, "修改器 DTO 为空");
                    continue;
                }

                string statId = ResolveOptionalLocalId<StatDefinition>(
                    dto.StatContentId,
                    catalog,
                    itemPath,
                    issues);
                TagSet requiredAll = RestoreTags(dto.RequiredAllTagIds, catalog, $"{itemPath}.requiredAll", issues);
                TagSet requiredAny = RestoreTags(dto.RequiredAnyTagIds, catalog, $"{itemPath}.requiredAny", issues);
                TagSet blocked = RestoreTags(dto.BlockedTagIds, catalog, $"{itemPath}.blocked", issues);

                if (dto.UsesLegacyTagMatching)
                {
                    if (!requiredAny.IsEmpty || dto.QueryScope != CombatTagScope.All)
                    {
                        Add(issues, DtoMapIssueCode.InvalidValue, itemPath, "旧标签匹配不能包含 RequiredAny 或非 All Scope");
                        continue;
                    }

                    result.Add(new ModifierInstance(
                        statId,
                        dto.Operation,
                        dto.Scope,
                        dto.Value,
                        dto.FromDamageType,
                        dto.ToDamageType,
                        requiredAll,
                        blocked));
                }
                else
                {
                    result.Add(new ModifierInstance(
                        statId,
                        dto.Operation,
                        dto.Scope,
                        dto.Value,
                        dto.FromDamageType,
                        dto.ToDamageType,
                        new TagQuery(dto.QueryScope, requiredAll, requiredAny, blocked)));
                }
            }

            return result;
        }

        static void RestoreAffixes(
            ItemInstance item,
            IReadOnlyList<AffixInstanceDto> dtos,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            IReadOnlyList<AffixInstanceDto> safeDtos = dtos ?? Array.Empty<AffixInstanceDto>();

            for (int i = 0; i < safeDtos.Count; i++)
            {
                AffixInstanceDto dto = safeDtos[i];
                string itemPath = $"{path}[{i}]";

                if (dto == null)
                {
                    Add(issues, DtoMapIssueCode.NullInput, itemPath, "词条 DTO 为空");
                    continue;
                }

                AffixDefinition definition = Resolve<AffixDefinition>(
                    dto.DefinitionContentId,
                    catalog,
                    $"{itemPath}.definitionContentId",
                    issues);
                List<ModifierInstance> modifiers = RestoreModifiers(
                    dto.Modifiers,
                    catalog,
                    $"{itemPath}.modifiers",
                    issues);

                if (definition != null && !item.TryAddAffix(new AffixInstance(definition, modifiers)))
                {
                    Add(issues, DtoMapIssueCode.InvalidValue, itemPath, "词条与物品类型、分组或容量不兼容");
                }
            }
        }

        static InventoryGrid RestoreInventory(
            InventoryLayoutDto dto,
            IReadOnlyDictionary<ItemInstanceId, ItemInstance> items,
            ISet<ItemInstanceId> ownedItems,
            List<DtoMapIssue> issues)
        {
            if (dto == null || dto.Width <= 0 || dto.Height <= 0)
            {
                Add(issues, DtoMapIssueCode.InvalidValue, "inventory", "背包尺寸非法");
                return new InventoryGrid(1, 1);
            }

            InventoryGrid grid = new InventoryGrid(dto.Width, dto.Height);
            IReadOnlyList<InventoryPlacementDto> placements = dto.Placements ?? new List<InventoryPlacementDto>();

            for (int i = 0; i < placements.Count; i++)
            {
                InventoryPlacementDto placement = placements[i];
                string path = $"inventory.placements[{i}]";

                if (placement == null
                    || !ItemInstanceId.TryParse(placement.ItemInstanceId, out ItemInstanceId id)
                    || !items.TryGetValue(id, out ItemInstance item))
                {
                    Add(issues, DtoMapIssueCode.DanglingReference, path, "背包位置引用了不存在的物品");
                    continue;
                }

                Vector2Int expectedSize = item.BaseDefinition.GridSize;

                if (placement.Width != expectedSize.x
                    || placement.Height != expectedSize.y
                    || !grid.TryAddAt(item, new Vector2Int(placement.X, placement.Y)))
                {
                    Add(issues, DtoMapIssueCode.InventoryPlacementInvalid, path, "背包位置尺寸、边界或占用冲突");
                    continue;
                }

                ownedItems.Add(id);
            }

            return grid;
        }

        static List<DetachedEquipmentLoadout> RestoreEquipment(
            IReadOnlyList<EquipmentLoadoutDto> dtos,
            IReadOnlyDictionary<ItemInstanceId, ItemInstance> items,
            ISet<string> actorIds,
            ISet<ItemInstanceId> ownedItems,
            List<DtoMapIssue> issues)
        {
            List<DetachedEquipmentLoadout> result = new List<DetachedEquipmentLoadout>();
            HashSet<string> seenActors = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<EquipmentLoadoutDto> safeDtos = dtos ?? Array.Empty<EquipmentLoadoutDto>();

            for (int i = 0; i < safeDtos.Count; i++)
            {
                EquipmentLoadoutDto dto = safeDtos[i];
                string path = $"equipment[{i}]";

                if (dto == null
                    || string.IsNullOrWhiteSpace(dto.ActorInstanceId)
                    || !actorIds.Contains(dto.ActorInstanceId)
                    || !seenActors.Add(dto.ActorInstanceId))
                {
                    Add(issues, DtoMapIssueCode.DanglingReference, path, "装备栏 Actor 引用缺失或重复");
                    continue;
                }

                EquipmentLoadout loadout = new EquipmentLoadout();
                IReadOnlyList<EquipmentEntryDto> entries = dto.Entries
                    ?? (IReadOnlyList<EquipmentEntryDto>)Array.Empty<EquipmentEntryDto>();

                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    EquipmentEntryDto entry = entries[entryIndex];
                    string entryPath = $"{path}.entries[{entryIndex}]";

                    if (entry == null || !Enum.IsDefined(typeof(EquipmentSlot), entry.Slot))
                    {
                        Add(issues, DtoMapIssueCode.InvalidEquipmentSlot, entryPath, "装备槽非法");
                        continue;
                    }

                    if (!ItemInstanceId.TryParse(entry.ItemInstanceId, out ItemInstanceId id)
                        || !items.TryGetValue(id, out ItemInstance item))
                    {
                        Add(issues, DtoMapIssueCode.DanglingReference, entryPath, "装备槽引用了不存在的物品");
                        continue;
                    }

                    if (ownedItems.Contains(id))
                    {
                        Add(issues, DtoMapIssueCode.OwnershipConflict, entryPath, "同一物品同时属于背包、装备或商店");
                        continue;
                    }

                    if (!loadout.TrySet(entry.Slot, item))
                    {
                        Add(issues, DtoMapIssueCode.InvalidEquipmentSlot, entryPath, "物品与装备槽不兼容或槽位重复");
                        continue;
                    }

                    ownedItems.Add(id);
                }

                result.Add(new DetachedEquipmentLoadout(dto.ActorInstanceId, loadout));
            }

            return result;
        }

        static TraderDefinition RestoreTrader(
            MerchantInventoryDto dto,
            ContentCatalog catalog,
            List<DtoMapIssue> issues)
        {
            if (dto == null)
            {
                Add(issues, DtoMapIssueCode.NullInput, "merchant", "商店 DTO 为空");
                return null;
            }

            return Resolve<TraderDefinition>(dto.TraderContentId, catalog, "merchant.traderContentId", issues);
        }

        static List<ItemInstance> RestoreMerchantStock(
            MerchantInventoryDto dto,
            IReadOnlyDictionary<ItemInstanceId, ItemInstance> items,
            ISet<ItemInstanceId> ownedItems,
            List<DtoMapIssue> issues)
        {
            List<ItemInstance> result = new List<ItemInstance>();

            if (dto == null)
            {
                return result;
            }

            IReadOnlyList<string> ids = dto.OrderedItemInstanceIds
                ?? (IReadOnlyList<string>)Array.Empty<string>();

            for (int i = 0; i < ids.Count; i++)
            {
                string path = $"merchant.orderedItemInstanceIds[{i}]";

                if (!ItemInstanceId.TryParse(ids[i], out ItemInstanceId id)
                    || !items.TryGetValue(id, out ItemInstance item))
                {
                    Add(issues, DtoMapIssueCode.DanglingReference, path, "商店引用了不存在的物品");
                    continue;
                }

                if (!ownedItems.Add(id))
                {
                    Add(issues, DtoMapIssueCode.OwnershipConflict, path, "同一物品同时属于背包、装备或商店");
                    continue;
                }

                result.Add(item);
            }

            return result;
        }

        static List<ActorIdentityDto> ValidateActors(
            IReadOnlyList<ActorIdentityDto> dtos,
            ContentCatalog catalog,
            List<DtoMapIssue> issues)
        {
            List<ActorIdentityDto> result = new List<ActorIdentityDto>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<ActorIdentityDto> safeDtos = dtos ?? Array.Empty<ActorIdentityDto>();

            for (int i = 0; i < safeDtos.Count; i++)
            {
                ActorIdentityDto dto = safeDtos[i];
                string path = $"actors[{i}]";

                if (dto == null
                    || string.IsNullOrWhiteSpace(dto.InstanceId)
                    || !ids.Add(dto.InstanceId)
                    || !Enum.IsDefined(typeof(ActorIdentityKind), dto.Kind))
                {
                    Add(issues, DtoMapIssueCode.DuplicateInstanceId, path, "Actor 身份为空、重复或类型非法");
                    continue;
                }

                if (dto.Kind == ActorIdentityKind.Player)
                {
                    if (dto.InstanceId != PlayerId.LocalPlayer.Value)
                    {
                        Add(issues, DtoMapIssueCode.InvalidInstanceId, path, "当前 Profile 玩家 ID 必须为 local_player");
                    }

                    Resolve<CharacterDefinition>(dto.DefinitionContentId, catalog, $"{path}.definitionContentId", issues);
                }
                else
                {
                    Resolve<MonsterDefinition>(dto.DefinitionContentId, catalog, $"{path}.definitionContentId", issues);
                }

                result.Add(dto);
            }

            return result;
        }

        static List<string> MapTagIds(
            TagSet tags,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            List<string> result = new List<string>();

            foreach (string localId in tags.Ids)
            {
                string contentId = MapOptionalLocalContentId(
                    ContentNamespaces.Tag,
                    localId,
                    catalog,
                    path,
                    issues);

                if (!string.IsNullOrEmpty(contentId))
                {
                    result.Add(contentId);
                }
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        static TagSet RestoreTags(
            IReadOnlyList<string> contentIds,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            List<string> localIds = new List<string>();
            IReadOnlyList<string> safeIds = contentIds ?? Array.Empty<string>();

            for (int i = 0; i < safeIds.Count; i++)
            {
                string localId = ResolveOptionalLocalId<TagDefinition>(safeIds[i], catalog, $"{path}[{i}]", issues);

                if (!string.IsNullOrEmpty(localId))
                {
                    localIds.Add(localId);
                }
            }

            return new TagSet(localIds);
        }

        internal static string ResolveOptionalLocalId<T>(
            string raw,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
            where T : ScriptableObject, IContentDefinition
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            T definition = Resolve<T>(raw, catalog, path, issues);
            return definition != null ? definition.Id : string.Empty;
        }

        internal static string MapOptionalLocalContentId(
            string contentNamespace,
            string localId,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            if (string.IsNullOrEmpty(localId))
            {
                return string.Empty;
            }

            if (!ContentId.TryCreate(contentNamespace, localId, out ContentId contentId))
            {
                Add(issues, DtoMapIssueCode.InvalidContentId, path, $"非法内容 ID：{contentNamespace}:{localId}");
                return string.Empty;
            }

            ContentResolveCode code = contentNamespace == ContentNamespaces.Stat
                ? catalog.Resolve<StatDefinition>(contentId).Code
                : catalog.Resolve<TagDefinition>(contentId).Code;

            if (code != ContentResolveCode.Success)
            {
                Add(issues, DtoMapIssueCode.MissingContent, path, $"目录无法解析内容：{contentId}");
                return string.Empty;
            }

            return contentId.ToString();
        }

        internal static T Resolve<T>(
            string raw,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
            where T : ScriptableObject, IContentDefinition
        {
            if (!ContentId.TryParse(raw, out ContentId contentId))
            {
                Add(issues, DtoMapIssueCode.InvalidContentId, path, $"非法内容 ID：{raw}");
                return null;
            }

            ContentResolveResult<T> result = catalog.Resolve<T>(contentId);

            if (result.Code == ContentResolveCode.Missing)
            {
                Add(issues, DtoMapIssueCode.MissingContent, path, $"内容不存在：{contentId}");
                return null;
            }

            if (result.Code != ContentResolveCode.Success)
            {
                Add(issues, DtoMapIssueCode.ContentTypeMismatch, path, $"内容类型不匹配：{contentId}");
                return null;
            }

            return result.Value;
        }

        static string GetRequiredContentId(ScriptableObject definition, ContentCatalog catalog)
        {
            if (definition == null || catalog == null || !catalog.TryGetContentId(definition, out ContentId contentId))
            {
                throw new InvalidOperationException("运行时定义不在内容目录中");
            }

            return contentId.ToString();
        }

        internal static string GetContentId(
            ScriptableObject definition,
            ContentCatalog catalog,
            string path,
            List<DtoMapIssue> issues)
        {
            if (definition == null || !catalog.TryGetContentId(definition, out ContentId contentId))
            {
                Add(issues, DtoMapIssueCode.MissingContent, path, "运行时定义不在内容目录中");
                return string.Empty;
            }

            return contentId.ToString();
        }

        static void Add(List<DtoMapIssue> issues, DtoMapIssueCode code, string path, string message)
        {
            issues.Add(new DtoMapIssue(code, path, message));
        }

        static DtoMapResult<T> Success<T>(T value)
        {
            return new DtoMapResult<T>(value, Array.Empty<DtoMapIssue>());
        }

        static DtoMapResult<T> Failed<T>(List<DtoMapIssue> issues)
        {
            return new DtoMapResult<T>(default, issues.AsReadOnly());
        }
    }
}
