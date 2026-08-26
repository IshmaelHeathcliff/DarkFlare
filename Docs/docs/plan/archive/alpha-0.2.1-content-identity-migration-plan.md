# alpha 0.2.1 稳定身份、内容目录与迁移框架执行计划

> 状态：已完成并归档
> 建立日期：2026-08-18
> 最近更新：2026-08-18
> 完成日期：2026-08-18
> 基线提交：`b56a190`
> 上位计划：[alpha 0.2 基础设施开发计划](./alpha-0.2-plan.md)
> 强制约束：[alpha 0.2 长期运行时契约](../../infrastructure/alpha-0.2-runtime-contract.md)
> 前置模块：[应用生命周期与会话作用域](../../infrastructure/application-lifecycle.md)
> 实现总结：[稳定身份、内容目录与迁移框架](../../infrastructure/content-identity-migration.md)

## 阶段目标

建立可以长期写入存档的稳定业务身份、只读内容目录、纯 DTO 映射边界和逐版本迁移框架。完成后，运行时对象可以继续使用适合玩法事务的对象引用，但任何准备持久化的数据都只能通过稳定 ID 描述内容、实例、所有者和槽位。

本阶段为 `alpha 0.2.2` 本地存档提供数据与迁移底座，不实现文件路径、序列化文件格式、存档槽位 UI、原子写入、备份、自动保存或正式 Restore Session。迁移 Pipeline 只处理调用方提供的内存文档，不直接访问文件。

## 当前审计基线

### 内容配置身份

当前已有本地稳定字符串 ID 的配置类型：

- `TagDefinition`
- `StatDefinition`
- `AffixDefinition`
- `ItemBaseDefinition`
- `CharacterDefinition`
- `ProjectileSkillDefinition`
- `MonsterDefinition`
- `MonsterAffixDefinition`
- `TraderDefinition`

当前缺少稳定 ID 的正式配置类型：

- `LootTableDefinition`
- `MonsterSpawnDefinition`
- `CraftingDefinition`

现有 ID 使用小写 `snake_case`，但只是各类型上的普通字符串；没有统一命名空间、值对象、运行时内容目录或跨类型查询契约。`ContentConfigurationValidator` 已能检查部分类型的空值、格式和同类型重复，但仍依赖硬编码类型、目录与正式 ID 清单，新配置类型不会自动进入持久化身份检查。

### 运行时实例与对象引用

- `ItemInstance` 已有字符串 `InstanceId`，但生成规则分散在初始发放、商人和掉落流程中，没有统一来源、格式或重复检测。
- `ItemInstance` 直接保存 `ItemBaseDefinition`，`AffixInstance` 直接保存 `AffixDefinition`；这些引用适合运行时计算，但不能直接进入 DTO。
- `InventoryGrid` 使用 `ItemInstance` 作为格子映射键，`EquipmentLoadout` 保存槽位到 `ItemInstance`，`EconomyModel` 保存 `List<ItemInstance>`。
- `EquipmentModel` 使用 `CombatActor` 作为 Loadout 所有者键；当前 Actor 没有独立、可持久化的身份契约。
- `MonsterInstanceData` 只有随机种子和属性，没有怪物实例 ID；世界掉落只有所含物品实例，没有独立世界实体 ID。
- `GameplayRandomSystem` 维护根种子和各通道序号，但没有导出 / 恢复状态；本阶段只冻结 DTO 边界，正式保存与恢复由 `alpha 0.2.2` 接入。

### 版本与迁移

- Runtime 当前没有统一的 `GameVersion`、`ContentVersion`、`SaveSchemaVersion` 或 `SettingsSchemaVersion`。
- Editor 中的视觉资产迁移和配置文档清单各自拥有局部 Schema，不是运行时保存数据的通用迁移入口。
- 项目已安装 `Newtonsoft.Json`，可以在不绑定具体存储实现的前提下，以分离的 JSON 文档进行确定性逐级迁移。

## 范围边界

### 本阶段完成

- 冻结 `ContentId` 的命名空间、格式、比较和序列化规则。
- 为全部正式内容配置建立自动注册契约，并补齐三个缺失 ID 的配置类型和正式资产。
- 建立 Application 作用域拥有的不可变内容目录，支持按稳定 ID 和预期类型解析配置。
- 冻结玩家、物品、怪物、世界掉落和存档槽位的实例 ID 语义及生成所有权。
- 建立物品、背包布局、装备槽、商人库存和 Actor 所有权的纯 DTO / Mapper 边界。
- 建立独立版本类型和只允许逐级执行的内存迁移 Pipeline。
- 建立缺失内容、类型冲突、未知未来版本和迁移失败的结构化结果。
- 扩展配置中心校验、反射规则和策略测试，使新增配置或 DTO 自动进入检查。
- 完成模块文档、执行记录和回归验证。

### 明确不做

- 不读写 `Application.persistentDataPath`，不建立 Storage、Serializer 或文件锁。
- 不实现主存档、备份、校验和损坏恢复；由 `alpha 0.2.2` 完成。
- 不实现正式读档 Session、继续游戏按钮或存档槽位页面。
- 不保存或恢复完整随机通道、刷怪计时、存活怪物和世界场景状态。
- 不实现 Settings Schema 的真实设置迁移；本阶段只冻结独立版本域和通用 Pipeline。
- 不建立 Locale、String Table 或迁移玩家可见文本；由 `alpha 0.2.3` 完成。
- 不建立 Boot / FrontEnd / Loading 场景流；内容目录的来源将在 `alpha 0.2.4` 迁移到正式启动流程。
- 不重组 Addressables 分组，不替换日志系统，不调整玩法数值或增加玩法内容。

## 稳定身份契约

### ContentId

持久化形式固定为：

```text
<namespace>:<local_id>
```

规则：

- `namespace` 和 `local_id` 只允许小写 ASCII 字母、数字与下划线，必须以字母开头。
- 使用 `StringComparer.Ordinal` 比较；不自动转小写、不裁剪、不进行文化相关归一化，非法输入直接拒绝。
- ID 不得来源于资产路径、文件名、Asset GUID、显示名称、Localization Key 或运行时 Instance ID。
- 资产重命名、移动目录、替换显示文本或 Addressables 地址不得改变 `ContentId`。
- 现有配置的 `_id` 保留为本地 ID，避免无意义改写正式内容；运行时由已登记命名空间组合为完整 `ContentId`。
- CLR 类型名不是命名空间来源。类型重命名时，已冻结命名空间保持不变。

首批命名空间：

| 命名空间 | 配置类型 | 缺失内容默认策略 |
| --- | --- | --- |
| `tag` | `TagDefinition` | 阻止加载 |
| `stat` | `StatDefinition` | 阻止加载 |
| `affix` | `AffixDefinition` | 阻止加载 |
| `item` | `ItemBaseDefinition` | 阻止加载 |
| `actor` | `CharacterDefinition` | 阻止加载 |
| `skill` | `ProjectileSkillDefinition` | 阻止加载 |
| `monster` | `MonsterDefinition` | 根对象阻止加载；明确可选的 Run 条目可跳过 |
| `monster_affix` | `MonsterAffixDefinition` | 阻止加载 |
| `loot` | `LootTableDefinition` | 阻止加载 |
| `spawn` | `MonsterSpawnDefinition` | 阻止加载 |
| `trader` | `TraderDefinition` | 根对象阻止加载；明确可选库存条目可跳过 |
| `crafting` | `CraftingDefinition` | 阻止加载 |

`ContentId` 使用只读值对象表达，提供显式 `Parse` / `TryParse`、值相等、稳定哈希和规范字符串输出。不得在业务代码中通过字符串拼接临时制造 ID。

### 内容类型登记

- 使用显式内容类型元数据登记“CLR 类型、稳定命名空间、缺失策略”；不通过文件夹或类名猜测。
- 内容配置实现统一只读接口，但不迁移到新的共同 ScriptableObject 基类，避免移动现有序列化字段。
- 所有 `DarkFlare/Data/` 下可创建的 ScriptableObject 必须被登记为内容，或显式标记为不持久化配置；没有隐式第三类。
- 同一命名空间只能对应一个正式配置类型；继承类型的解析规则必须明确登记，不能依赖 `is` 的偶然顺序。
- 新增配置类型但未登记时，Editor 校验和 EditMode 策略测试必须失败。

### 运行时实例 ID

| 身份 | 作用域 | 生成规则 | 生命周期语义 |
| --- | --- | --- | --- |
| `PlayerId` | Profile | 当前兼容档案固定为 `local_player`，未来由档案创建 | 同一档案保持稳定，不使用场景对象 ID |
| `ItemInstanceId` | Profile / Run | Profile 所有的可注入生成器创建 32 位小写 UUID；测试使用确定性生成器 | 物品在背包、装备、商店和世界间转移时保持不变 |
| `MonsterInstanceId` | Run | `RunId` 与单调序号组合；序号状态进入未来 Run DTO | 不消费 `GameplayRandomSystem`，重载后不与旧实例冲突 |
| `WorldDropId` | Run | `RunId` 与独立单调序号组合 | 标识世界实体；所含物品继续使用自己的 `ItemInstanceId` |
| `SaveSlotId` | Application Storage | 小写稳定 slug，由 Storage 映射文件名 | 与玩家可见槽位名称分离，不直接接受任意路径文本 |

- Asset GUID 与运行时 UUID 是不同概念：禁止保存 Asset GUID，但允许由受控生成器创建不指向 Unity 资产的实例 UUID。
- 生成器必须可注入、可测试，并由对应作用域拥有；业务模块不得自行调用 `Guid.NewGuid()` 或拼接时间戳。
- 同类 ID 不允许空值或重复；不同身份类型不能依靠裸字符串隐式互换。
- 本阶段保留必要的字符串兼容访问器，迁移完成后新代码只使用强类型 ID。

## 内容目录契约

### 目录资产与运行时目录

- 新增唯一正式 `ContentCatalogDefinition` 资产，保存目录 ID、`ContentVersion` 和显式 ScriptableObject 条目。
- 目录资产只负责声明正式内容；构建后的 `ContentCatalog` 是不可变运行时对象，不暴露增删接口。
- 目录构建时一次性验证空引用、未登记类型、非法 ID、同命名空间重复、同资产重复和类型冲突。
- 正式查询入口至少提供 `TryResolve<T>`、`Resolve<T>` 与 `GetAll<T>`；调用方必须同时给出 `ContentId` 和预期类型。
- 解析失败返回结构化错误，包含 ContentId、预期类型、实际登记类型和缺失策略；不得静默返回同名其他类型。
- 目录不保存 Asset GUID、路径或显示名作为运行时键；这些信息只允许出现在 Editor 诊断中。

### 生命周期与接入

- `ContentCatalog` 属于 Application 作用域，不注册进 Session 级 `GameArchitecture`，也不允许短生命周期对象拥有它。
- 当前没有 Boot 场景，因此 `Main` 的 `GameplaySceneConfiguration` 暂时提供唯一目录资产引用；`ApplicationHost` 在创建 / 初始化 Session 前安装并冻结目录。
- 同一目录 ID 与版本的重复安装是幂等成功；Application 存活期间提交不同目录或不同版本必须失败，不能让并存 Session 看到不同内容定义。
- Session Initializer 和 DTO Mapper 通过显式依赖取得目录，不使用新的静态全局单例。
- `alpha 0.2.4` 将目录来源迁移到正式 Boot 流程，但保持 `ContentCatalog` 查询接口和 Application 所有权不变。

## DTO 与映射边界

### DTO 结构约束

DTO 只允许：

- 原始数值、布尔、字符串和已冻结枚举值。
- 其他 DTO、数组或列表。
- 规范化后的 ContentId / 实例 ID 字符串表示。

DTO 禁止：

- `UnityEngine.Object`、`ScriptableObject`、`GameObject`、组件和场景引用。
- `AssetReference`、资产 GUID、资源路径和 Addressables 地址。
- 委托、事件、Task / UniTask、CancellationToken、服务或 QFramework 接口。
- 运行时集合键对象、运行时缓存和只用于 UI 的选择 / 焦点状态。

反射测试必须递归扫描 DTO 字段与集合元素类型，新增违规字段时立即失败。

### 首批 DTO / Mapper

本阶段建立最小但真实的领域映射：

- `ItemInstanceDto`：物品实例 ID、基础物品 ContentId、稀有度、等级、生成种子、耐久和当前词条状态。
- `AffixInstanceDto` / `ModifierInstanceDto`：词条 ContentId 与已经掷出的精确修改器值；不能仅依赖当前配置重新随机，避免内容调整后悄然改变已有物品。
- `InventoryLayoutDto`：物品 ID 与格子原点 / 尺寸，不保存 `ItemInstance` 字典键。
- `EquipmentLoadoutDto`：Actor ID、稳定装备槽枚举与物品 ID，不保存 `CombatActor`。
- `MerchantInventoryDto`：Trader ContentId 与有序物品 ID，不保存 `ItemInstance` 列表引用。
- `ActorIdentityDto`：Player / Monster 的强类型身份和内容定义 ID；完整战斗状态留给 `alpha 0.2.2`。

映射顺序固定为：

1. 从运行时取得不可变快照，不在 Mapper 中修改 Model。
2. 生成纯 DTO，并验证实例 ID 唯一、引用闭合和槽位合法。
3. 反向映射时先在分离对象图中解析全部 ContentId、实例和交叉引用。
4. 任一内容缺失、重复实例、非法槽位或字段错误时返回失败，不提交部分运行时状态。
5. 全部验证成功后返回可提交对象图；真正写入 Model 的 Restore 事务由 `alpha 0.2.2` 负责。

运行时内部的 `InventoryGrid`、`EquipmentModel` 和 `EconomyModel` 可以继续使用对象引用保证玩法事务简洁；禁止的是对象引用跨越 DTO 边界，而不是为了序列化重写所有运行时集合。

## 版本契约

| 版本 | 类型 | 所有者 | 用途 |
| --- | --- | --- | --- |
| `GameVersion` | 字符串 | 应用 | 玩家显示、诊断和兼容信息；默认来自 `Application.version` |
| `ContentVersion` | 正整数 | 内容目录 | 内容身份或语义兼容性变化；不是存档结构版本 |
| `SaveSchemaVersion` | 正整数 | Save 根 DTO | 存档字段结构与解释方式；由 `alpha 0.2.2` 建立首个正式版本 |
| `SettingsSchemaVersion` | 正整数 | Settings 根 DTO | 用户设置结构；由 `alpha 0.2.3` 建立首个正式版本 |

- 四个版本不得共用一个整数或互相替代。
- 版本类型使用强类型值对象或明确封装，业务代码不能裸比较无来源的 `int`。
- ContentVersion 变化不自动触发 SaveSchemaVersion 变化；是否需要数据迁移由显式兼容规则决定。
- 未知未来 Schema 一律拒绝；不能按当前结构尝试解析。

## 迁移 Pipeline 契约

- 迁移输入为调用方提供的分离 JSON 文档；Pipeline 首先深复制，永不原地修改调用方对象。
- 每个迁移步骤声明稳定 ID、数据域、`FromVersion` 和 `ToVersion`，并且只允许 `N → N + 1`。
- 注册时拒绝重复起点、链路空洞、倒退、跳级和循环。
- 执行顺序完全由版本决定，不依赖程序集反射顺序。
- 每一步必须确定性执行；相同输入得到等价输出，不读取时间、随机数、文件或 Unity 场景状态。
- 任一步失败时返回原始版本、失败步骤、异常和已完成步骤列表；不返回可提交的部分文档。
- 当前版本输入是幂等成功；未来版本返回 `FutureVersionUnsupported`；缺少链路返回 `MigrationPathMissing`。
- Pipeline 不写文件。`alpha 0.2.2` 的 Storage 只有在解析、校验和全部迁移成功后才允许提交新文件。
- 本阶段提供至少一条 `0 → 1` 历史夹具迁移，验证字段重命名、ContentId 规范化和失败不修改输入；它是迁移合同样本，不冒充正式存档格式。

## 缺失内容与兼容策略

- 内容重命名必须新增显式迁移或旧 ID 别名迁移；运行时目录不自动猜测相近 ID。
- 删除玩家持有物品、装备、词条、角色根配置或结构性 Stat / Tag 时默认阻止加载，保留原数据等待恢复，不静默丢弃经济状态。
- Run 中明确标记为可选的怪物、世界掉落或商人库存条目可以按字段策略跳过，但必须产生结构化 Warning 和恢复摘要。
- 占位内容只有在目录显式登记了对应占位资产、且 DTO 字段策略允许时才能使用；本阶段不创建通用“万能占位物品”。
- 回滚备份是 Storage 行为，属于 `alpha 0.2.2`；本阶段只返回足够的失败原因和建议恢复动作。
- 同一缺失内容在同一映射事务中只记录一次根问题，避免生成无界重复诊断。

## Editor 校验与规范冻结

- 配置中心增加内容目录状态：目录版本、登记类型、条目数、重复 / 缺失 / 未收录内容和定位入口。
- 现有 `ContentConfigurationValidator` 复用统一类型登记与 ID 解析器，不再各自实现不同格式规则。
- Editor 扫描所有 `DarkFlare/Data/` 配置类型和正式资产，保证每个可持久化资产恰好进入目录一次。
- 正式资产必须达到：非法 ID 0、同命名空间重复 0、空 ID 0、目录遗漏 0、未登记类型 0。
- Infrastructure 策略新增 DTO 结构和实例 ID 生成入口规则；业务代码新增 Asset GUID / 路径持久化字段或自行生成实例 ID 时测试失败。
- 例外清单仍使用现有严格 Schema；本阶段不为新增违规建立无期限白名单。

## 执行切片

### 阶段 A：合同与特征测试

交付：

- 为现有 12 类正式配置、现有 ID 和对象引用建立特征测试。
- 冻结 ContentId、实例 ID、四类版本和缺失内容错误码。
- 建立 DTO 反射禁用类型测试，先用正反样本证明规则有效。

退出条件：

- 当前资产身份清单可重复生成。
- 新增非法 ContentId、未登记配置类型或含 Unity 引用 DTO 时测试能稳定失败。
- 不改变当前玩法运行结果。

### 阶段 B：内容身份与目录

交付：

- 实现 ContentId、内容类型登记、目录定义和不可变运行时目录。
- 为 Loot、Spawn、Crafting 配置补充 `_id`，为全部正式资产登记语义稳定 ID。
- 将目录资产接入 `GameplaySceneConfiguration` 和 ApplicationHost，冻结 Application 级唯一目录。
- 配置中心展示目录验证结果。

退出条件：

- 全部正式内容可按类型和 ContentId 解析，零空值、零重复、零遗漏。
- 场景重载和连续 Session 复用同一目录实例，不产生 Session 级副本或静态残留。
- 不同目录 / 版本在同一 Application 中被明确拒绝。

### 阶段 C：实例身份与 DTO Mapper

交付：

- 实现强类型 Player、Item、Monster、WorldDrop 和 SaveSlot ID，以及受控生成器。
- 迁移当前物品实例 ID 创建入口；为怪物和世界掉落建立独立实例身份。
- 建立物品、词条、修改器、背包、装备、商人和 Actor 身份 DTO。
- 建立运行时 → DTO → 分离运行时对象图的 Mapper 与结构化验证结果。

退出条件：

- 同一物品跨背包、装备、交易和世界状态保持同一个实例 ID。
- DTO 不含 Unity / Addressables / QFramework / 异步对象引用。
- 正式物品经 round-trip 后，所有规定持久字段等价；重复 ID、悬空引用和非法槽位均在提交前被拒绝。

### 阶段 D：版本与迁移 Pipeline

交付：

- 实现四类版本的独立表达和版本兼容结果。
- 实现内存 JSON 迁移注册表、链路验证、深复制执行与结构化报告。
- 增加历史夹具、缺链、重复步骤、未来版本、步骤异常和输入不变测试。

退出条件：

- 只存在唯一完整迁移链时才能升级。
- 成功迁移可重复，失败迁移不改变输入。
- 业务 Mapper 中不存在散落的旧版本 `if` 分支。

### 阶段 E：集成回归与文档收尾

交付：

- NewGame 真实路径使用目录解析至少一组场景配置与初始物品，证明目录不是仅测试使用的空接口。
- 连续 Session、场景直接重载和退出路径验证目录所有权、实例生成器与 Mapper 无累积。
- 更新内容系统、配置中心、项目结构和项目概览文档。
- 总结为稳定身份 / 内容目录 / 迁移模块文档，并将本计划归档。

退出条件：

- Unity 编译 0 error，新增专项全部通过。
- 全量 EditMode 不低于当前 `255/255` 基线且零失败。
- 项目自有 PlayMode 不低于当前 `45/45` 基线且零失败；Input System 上游跳过项保持已知状态。
- 两次真实 NewGame / 退出的内容目录版本、实例 ID 唯一性和生命周期计数一致，Console 无新增 Error。

## 自动测试矩阵

| 测试类别 | 核心断言 |
| --- | --- |
| ContentId 单元测试 | 合法解析、非法字符、大小写拒绝、Ordinal 相等、稳定哈希、round-trip |
| 内容类型登记测试 | 命名空间唯一、类型唯一、未登记新配置失败、Transient 例外明确 |
| 正式资产校验 | 12 类配置零空 ID、零重复、零目录遗漏、引用类型正确 |
| 内容目录测试 | 强类型解析、错误类型、缺失内容、重复安装、不同版本拒绝、不可变性 |
| 实例 ID 测试 | 生成入口唯一、测试可复现、转移不换 ID、不同身份不可混用 |
| DTO 反射测试 | 递归拒绝 Unity Object、AssetReference、路径 / GUID 字段和运行时服务 |
| Mapper round-trip | 物品掷值、词条、耐久、背包位置、装备槽、商店顺序和 Actor 所有权等价 |
| Mapper 失败测试 | 重复实例、悬空物品、非法槽位、缺失内容、类型冲突均不返回可提交图 |
| Migration 单元测试 | 唯一逐级链、深复制、确定性、缺链、未来版本、异常与输入不变 |
| 生命周期 PlayMode | Main 重载、连续 Session 与退出时目录唯一、无静态残留、无 ID 重复 |
| 真实路径 PlayMode | NewGame 至少通过 ContentId 解析初始内容并保持现有玩法结果 |

## 预期文件与模块位置

计划中的目录职责如下，实际文件名可在不改变契约的前提下精简：

```text
Assets/Scripts/Runtime/Infrastructure/
├── Content/
│   ├── ContentId.cs
│   ├── ContentDefinitionMetadata.cs
│   ├── ContentCatalogDefinition.cs
│   ├── ContentCatalog.cs
│   └── ContentCatalogResult.cs
└── Persistence/
    ├── Identity/
    ├── Dto/
    ├── Mapping/
    ├── Versioning/
    └── Migration/

Assets/Scripts/Editor/
├── ContentCatalogValidator.cs
└── ContentCatalogEditorIntegration.cs

Assets/Scripts/Tests/EditMode/
├── ContentIdentityTests.cs
├── ContentCatalogTests.cs
├── PersistenceDtoContractTests.cs
├── PersistenceMappingTests.cs
└── MigrationPipelineTests.cs

Assets/Scripts/Tests/PlayMode/
└── ContentCatalogLifecyclePlayModeTests.cs
```

不建立新的基础设施总管理器。内容目录归 ApplicationHost 生命周期管理；映射器和迁移器使用显式依赖、纯输入和结构化结果。

## 风险与控制

| 风险 | 控制 |
| --- | --- |
| 为序列化重写全部玩法集合，扩大回归范围 | 保留运行时对象引用，只在 DTO 边界映射稳定 ID |
| ContentId 与本地化 Key、Addressables 地址混用 | 使用独立值对象和类型登记，策略测试拒绝 GUID / 路径持久化 |
| 资产移动或重命名导致身份变化 | ID 独立序列化，Editor 校验不从路径生成 ID |
| 实例 ID 消耗玩法随机序列，改变固定种子结果 | 独立 ID 生成器与 Run 序号，不调用 GameplayRandomSystem |
| 目录属于 Session，重载后内容不一致 | Application 作用域冻结唯一目录，重复安装必须同 ID / 版本 |
| 迁移原地修改或中途失败留下半成品 | 深复制输入、逐级链和失败无提交结果 |
| 过早实现完整存档导致 0.2.1 / 0.2.2 边界失控 | 本阶段只做 DTO、Mapper 和内存迁移，不做文件与 Restore 提交 |
| 新配置类型绕过身份规范 | 自动类型发现要求登记或显式 Transient，零默认例外 |

## 实际交付与偏差

- 12 类正式配置全部接入 `IContentDefinition` 与类型登记；为 Loot、Spawn、Crafting 补齐稳定 `_id`。
- 新建唯一正式目录 `core` v1，实际收录 90 个配置；`ApplicationHost` 冻结运行时目录，同 ID / 版本重复安装幂等，不同 ID / 版本拒绝。
- `ContentId`、Player / Item / Run / Monster / WorldDrop / SaveSlot 强类型 ID 及可注入生成器均已落地；Profile 物品生成器由 Application 拥有，Run 生成器由 Session 拥有。
- DTO 实际覆盖修改器、词条、物品、背包、装备、商人和 Actor；Mapper 先构造分离图，再验证重复、悬空、非法格子 / 槽位和所有权冲突。
- 版本与迁移实现使用项目已有 `Unity.Newtonsoft.Json`；迁移注册表额外拒绝越过当前版本的未来步骤。
- 策略实现采用 DTO 反射合同与源码扫描组合：DTO 字段名拒绝 GUID / 地址 / 路径，`Guid.NewGuid()` 只允许基础设施身份生成器和玩法随机根种子。
- 未新增 Transient 内容类型或策略例外；当前 12 类缺失策略均为 `BlockLoad`，占位内容仍只是扩展合同。
- 严格遵守阶段边界：未实现文件 IO、存档路径、序列化封装、槽位 UI、原子写入、备份、自动保存或 Restore Session。

## 最终验收证据

- Unity `6000.4.3f1` 重编译：0 error。
- EditMode 全量：`287/287` 通过。
- 项目自有 PlayMode：`45/45` 通过。
- PlayMode 完整运行：49 项中 47 项通过、0 失败；2 项 Input System 包测试因上游 issue 1252825 跳过，状态与基线一致。
- 专项：内容身份 16/16、实例身份与 DTO 7/7、迁移 7/7、基础设施策略 22/22。
- 两次真实 Play 均为 Application `Ready`、Session `Running`、目录 `core` v1 / 90 项、玩家 `local_player` 1 个、已运行刷怪器 1 个；退出后 Console Error 为 0。
- 配置参考当前覆盖 12 个顶层类型、6 个嵌套结构、153 个序列化字段。

## 完成定义

- ContentId、内容类型、目录、实例 ID、DTO 和迁移公共合同均有运行时代码、真实消费者和自动测试。
- 全部正式可持久化内容具有合法、唯一、可解释的稳定 ID，并被唯一目录收录。
- 物品、背包、装备、商人和 Actor 所有权能转换为不含 Unity 引用的纯 DTO，并通过 round-trip。
- 缺失内容、类型冲突、重复实例和未知未来版本得到结构化失败，不静默修复或部分提交。
- 迁移只能按登记链逐级执行，失败不改变输入，也不在业务 Mapper 中散落兼容分支。
- 现有 NewGame、连续 Session、场景重载和退出行为无回归。
- 形成正式模块文档，本执行计划移动至 `Docs/docs/plan/archive/`，上位计划将 `alpha 0.2.1` 标记为已完成并把下一阶段切换为 `alpha 0.2.2`。
