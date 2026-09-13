# 稳定身份、内容目录与迁移框架

> 对应阶段：`alpha 0.2.1`
> 状态：已完成
> 当前正式目录：`Assets/Data/Preset/Content/正式内容目录.asset`

## 职责与边界

本模块为后续存档、设置和内容演进提供三条稳定边界：

- 用 `ContentId` 标识可持久化配置，不保存资产路径、Asset GUID 或显示名称。
- 用不同强类型 ID 标识玩家、物品、怪物、世界掉落和存档槽位，不复用 Unity Instance ID。
- 用纯 DTO、显式 Mapper 和逐版本 Migration Pipeline 隔离运行时对象图与持久化数据。

本模块自身只定义身份、DTO、映射和 JSON 迁移能力，不拥有文件或 Session 流程。`alpha 0.2.2` 已在这些合同之上实现文件路径、序列化格式、存档槽位、代际提交、备份、保存协调和 Restore Session，见[本地存档与 Session 恢复](./local-save.md)。

## 内容身份

`ContentId` 的规范形式为 `<namespace>:<local_id>`。命名空间和本地 ID 均使用小写 `snake_case`，比较、排序与查找全部使用 Ordinal 语义。大小写变体、连字符、空段、多余冒号和连续下划线均非法。

当前登记 13 个命名空间（状态类型已登记，正式状态资产尚未创建）：

| 命名空间 | 配置类型 |
| --- | --- |
| `tag` | `TagDefinition` |
| `stat` | `StatDefinition` |
| `affix` | `AffixDefinition` |
| `item` | `ItemBaseDefinition` |
| `actor` | `CharacterDefinition` |
| `skill` | `ProjectileSkillDefinition` |
| `monster` | `MonsterDefinition` |
| `monster_affix` | `MonsterAffixDefinition` |
| `loot` | `LootTableDefinition` |
| `spawn` | `MonsterSpawnDefinition` |
| `trader` | `TraderDefinition` |
| `crafting` | `CraftingDefinition` |
| `status` | `StatusDefinition` |

所有正式配置实现 `IContentDefinition` 并声明 `ContentDefinitionAttribute`。`ContentDefinitionRegistry` 通过运行时程序集反射建立唯一的“类型 ↔ 命名空间”登记；新增类型若未登记、登记重复或不是 `ScriptableObject`，测试和验证器会失败。

## 内容目录与所有权

`ContentCatalogDefinition` 是可编辑的目录资产；`ContentCatalog.Build` 一次性验证后生成不可变运行时目录，支持强类型正向解析、反向查询和结构化错误。当前正式目录为 `core`、内容版本 `3`，收录 106 个正式配置。`SaveRestorePreparer` 支持已发布 `core` v1 → v2 → v3 路径，复制 DTO 后迁移：v1 的旧四槽和已掷装备保持不变，新槽为空；v2 的独立金币按当前堆叠上限转换为背包物品，空间不足时扩行。之后仍执行严格目录、数量、金币摘要和所有权校验。v1 文档含新增槽、未知目录及未来版本一律拒绝。SaveSchemaVersion 为 2，v0 → v1 → v2 结构迁移独立保留；新增数量字段对历史物品初始化为 1。

目录由 `ApplicationHost` 拥有：

1. `Main` 的 `CombatPrototypeBootstrap` 提交场景请求前安装目录。
2. `ApplicationHost` 构建并冻结唯一 `ContentCatalog`。
3. 同目录 ID 与版本的重复安装幂等成功，但不会替换已安装实例。
4. 不同目录 ID 或内容版本在同一 Application 中被拒绝。
5. `GameSessionHost` 将同一目录注入 `SessionInitializationContext`；Session 不创建副本。
6. Application 正常或应急关闭时清除目录引用。

`GameplaySceneConfiguration` 会验证目录存在，并检查场景直接配置及怪物—掉落嵌套引用均属于该目录。`ContentConfigurationValidator` 同时检查非法 ID、同命名空间重复、空 ID、正式目录遗漏和未登记配置类型。

缺失内容策略由类型元数据声明为 `BlockLoad`、`SkipOptionalEntry` 或 `UseExplicitPlaceholder`。当前正式 12 类内容均采用默认 `BlockLoad`；占位替换尚未作为正式加载行为实现。

## 运行时实例身份

| 类型 | 格式与所有者 | 规则 |
| --- | --- | --- |
| `PlayerId` | Profile；当前为 `local_player` | 不使用场景对象 ID |
| `ItemInstanceId` | Profile 生成器；32 位小写十六进制 UUID | 跨背包、装备、商人和世界转移保持不变 |
| `RunId` | Session；32 位小写十六进制 UUID | 每次运行唯一 |
| `MonsterInstanceId` | `<run>:monster:<sequence>` | Session 内单调递增 |
| `WorldDropId` | `<run>:drop:<sequence>` | 与物品实例 ID 分离 |
| `SaveSlotId` | 小写稳定 slug | 只描述逻辑槽位，不描述文件路径 |

生产物品 ID 由 Profile 所有的 `IItemInstanceIdGenerator` 统一生成并注入每个 Session；怪物与世界掉落由 Session 的 `RunInstanceIdGenerator` 生成。测试可使用确定性生成器。业务代码不得自行调用 `Guid.NewGuid()` 或拼接时间戳；源码策略只允许身份生成器和玩法随机根种子拥有该调用。

## DTO 与 Mapper

`IdentityDtos.cs` 定义纯数据边界：修改器、词条、物品、背包布局、装备、商人库存、Actor 身份和聚合 `RuntimeStateDto`。DTO 只允许基础类型、枚举、字符串、列表和其他 `IPersistenceDto`；禁止 Unity Object、Addressables 引用、QFramework 对象、异步对象、委托、Asset GUID、地址和资源路径。

`RuntimeStateMapper` 遵循“解析到分离图，再由调用方提交”的顺序：

1. 通过 `ContentCatalog` 解析所有 ContentId，并验证期望类型。
2. 恢复物品已掷出的精确修改器值，不按新配置重新随机。
3. 建立独立的物品、背包、装备、商人和 Actor 图。
4. 检查重复实例、悬空引用、非法格子、非法装备槽和所有权冲突。
5. 只有零问题时返回可提交图；失败不会修改当前运行时状态。

旧字符串入口只保留为运行时兼容适配，新的基础设施和持久化代码应使用强类型 ID。

## 版本与迁移

四个版本域相互独立：

- `GameVersion`：当前应用版本。
- `ContentVersion`：内容目录语义版本，必须大于 0。
- `SaveSchemaVersion`：存档结构版本，可从 0 开始。
- `SettingsSchemaVersion`：设置结构版本，可从 0 开始。

`VersionCompatibility` 只返回 `Compatible`、`MigrationRequired` 或 `FutureVersionUnsupported`，不把未知未来版本当成当前数据解析。

`JsonMigrationRegistry` 要求同一数据域存在唯一、完整的 `0 → current` 链；每步只能执行 `N → N+1`，步骤 ID、来源版本和目标版本均唯一且不得越过当前版本。`JsonMigrationPipeline` 对输入 `JObject` 深复制后逐步执行：

- 当前版本幂等成功，但仍返回分离副本。
- 历史版本按登记顺序确定性升级，并报告已完成步骤。
- 未来版本、缺链和步骤异常返回结构化失败。
- 失败不修改调用方输入，也不返回部分文档。

当前迁移 `LegacySaveV0ToV1Migration` 将旧 `baseItemId` 规范化为 `item:<local_id>` 的 `baseContentId`。正式文件加载、校验和备份回退已经由本地存档模块组装，Migration 仍保持纯内存且不直接访问文件。

## 扩展规范

新增可持久化配置时必须同时完成：

1. 实现 `IContentDefinition`，声明唯一 `ContentDefinitionAttribute` 和稳定 `_id`。
2. 将正式资产加入唯一内容目录，并更新相关配置参考文档。
3. 运行配置验证器、`ContentIdentityTests` 和全量回归。

修改 DTO 或 Schema 时必须：

1. 保持 DTO 纯数据，不加入运行时服务或资源定位信息。
2. 为每次 Schema 变化新增唯一 `N → N+1` 迁移步骤，不改写历史步骤。
3. 增加历史夹具、当前版本、未来版本和失败输入不变测试。
4. 先迁移、再解析、再映射到分离图，最后由上层事务提交。

## 禁止事项

- 禁止使用文件路径、Asset GUID、Addressables 地址、显示名称或 Unity Instance ID 作为持久化身份。
- 禁止不同身份域复用同一个裸字符串类型处理业务规则。
- 禁止业务模块自行生成 UUID 或时间戳 ID。
- 禁止 DTO 保存 `UnityEngine.Object`、运行时 Model/System、场景对象或异步对象。
- 禁止在 Mapper 中散落旧版本兼容分支；版本兼容必须由迁移步骤负责。
- 禁止迁移步骤直接读写文件或原地修改调用方文档。

## 验证证据

- Unity `6000.4.3f1` 重编译：0 error。
- EditMode 全量：`287/287` 通过。
- 项目自有 PlayMode：`45/45` 通过。
- PlayMode 完整运行：49 项中 47 项通过、0 失败，2 项 Input System 包测试因上游 issue 1252825 跳过。
- 专项：`ContentIdentityTests` 16/16、`InstanceIdentityAndDtoTests` 7/7、`MigrationPipelineTests` 7/7、`InfrastructurePolicyTests` 22/22。
- 冷启动验证目录 `core` v1、90 个条目，并覆盖等价目录重复安装与不同版本拒绝。

实现过程、审计基线和偏差记录见[归档执行计划](../plan/archive/alpha-0.2.1-content-identity-migration-plan.md)。
