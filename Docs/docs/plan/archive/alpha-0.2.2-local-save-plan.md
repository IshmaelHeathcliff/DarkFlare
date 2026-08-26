# alpha 0.2.2 本地存档闭环执行计划

> 状态：已完成并归档
> 建立日期：2026-08-19
> 最近更新：2026-08-19
> 完成日期：2026-08-19
> 基线提交：`21360c7`
> 上位计划：[alpha 0.2 基础设施开发计划](./alpha-0.2-plan.md)
> 强制约束：[alpha 0.2 长期运行时契约](../../infrastructure/alpha-0.2-runtime-contract.md)
> 前置模块：[应用生命周期与会话作用域](../../infrastructure/application-lifecycle.md) · [稳定身份、内容目录与迁移框架](../../infrastructure/content-identity-migration.md)
> 实现总结：[本地存档与 Session 恢复](../../infrastructure/local-save.md)

## 阶段目标

建立可恢复、可迁移、可验证且有明确所有权的本地存档闭环，让当前 `Main` 单场景玩法真正完成：

```text
运行中取得一致快照
  → 写入 auto 槽位
  → 停止并重建 Session
  → 预检、迁移并恢复
  → 继续同一 Run
```

完成后，新游戏与恢复存档必须使用两条互斥的 Session 初始化事务。恢复流程不得再次发放初始武器、金币或商人库存；任何文件、迁移、内容解析或运行时提交失败都不得留下半恢复状态。

## 完成定义

本阶段只有同时满足以下条件才可标记完成：

- 正式存档只写入 `Application.persistentDataPath` 下的受控目录，Gameplay、Model、System、Controller 不直接访问文件。
- `auto` 槽位具有真实运行时消费者；玩家可以在当前游戏菜单中保存、恢复自动存档或开始新游戏。
- 存档覆盖玩家物品关系图、金币、位置、资源、随机通道、实例 ID 序号、刷怪进度、存活怪物、世界掉落和商人运行时库存。
- 主线程同步取得冻结 DTO，序列化、校验和文件 IO 只处理纯数据；后台任务全部由生命周期作用域拥有。
- 同一槽位同时最多一个写事务；密集请求可合并但每个调用者都恰好完成一次。
- 临时写入、提交代际、校验和有限备份均有故障测试；损坏存档只恢复已验证的上一代或安全拒绝。
- 读档在停止当前 Session 前完成文件、Schema、迁移、结构和 ContentId 预检；预检失败时当前游戏继续运行。
- Restore 提交失败时释放新建对象与资源，Session 按既有生命周期合同停止或进入不可逆 `Abandoned`，不得回写旧代际。
- 退出前 Flush 有成功、失败和超时结果；无论结果如何，关闭流程都能有界收敛。
- 完成 Unity 编译、EditMode、项目自有 PlayMode、完整 PlayMode 与两次真实 Play / 退出验证，Console 无新增 Error。
- 新增模块文档，更新项目概览、目录结构、玩法流程与总计划，并归档本执行计划。

## 当前审计基线

### 已可复用的基础

- `alpha 0.2.0` 已建立唯一 `ApplicationHost`、Profile / Session / Scene 作用域、latest-wins 场景 Session 协调、可回滚初始化和不可逆 `Abandoned` 隔离。
- `alpha 0.2.1` 已建立 `ContentId`、`SaveSlotId`、Player / Item / Run / Monster / WorldDrop 强类型身份、Application 级内容目录和四类版本合同。
- `RuntimeStateDto` 与 `RuntimeStateMapper` 已能表达并验证物品、背包、装备、商人和 Actor 身份关系图，迁移 Pipeline 已能在内存 `JObject` 上执行确定性逐级迁移。
- `PrefabAssetLoader`、`SpriteAssetLoader` 与 `SessionObjectRegistry` 已有精确所有权和回滚路径，可供 Restore 预热与失败清理复用。
- 当前验证基线为 Unity 编译 0 error、EditMode `287/287`、项目自有 PlayMode `45/45`；完整 PlayMode `49` 项中 `47` 项通过、`0` 失败，另有 `2` 项 Input System 包测试因上游 issue 1252825 跳过。

### 尚未形成存档闭环的缺口

| 状态所有者 | 当前能力 | 本阶段缺口 |
| --- | --- | --- |
| `RuntimeStateDto` / Mapper | 物品关系图和身份映射 | 没有 Profile / Run 顶层边界、金币、位置、资源、随机、刷怪、怪物、掉落和计时器 |
| `InventoryModel` | 背包和金币玩法事务 | 没有静默、一次性、全量 Restore 提交接口 |
| `EquipmentModel` | 以 `CombatActor` 为键维护装备 | 没有稳定所有者枚举和完整替换接口 |
| `EconomyModel` | 商人有序库存与倍率 | 没有 Trader 身份和一次性恢复接口 |
| `GameplayRandomSystem` | 根种子与每通道序号 | 只暴露根种子，不能导出或恢复各通道继续位置 |
| `RunInstanceIdGenerator` | 为怪物和世界掉落分配单调 ID | 不暴露序号，不支持从已保存 Run 恢复；Monster / WorldDrop ID 也缺少正式 Parse |
| `CombatActor` / Player / Monster | 创建时配置满资源，运行中扣除资源 | 没有精确恢复生命、法力、存活状态和剩余逻辑时间的入口 |
| `SpawnSystem` / `LootSystem` | 创建新怪物与新掉落 | 只能生成新随机实例，不能按保存的 ID、实例数据和资源状态精确重建 |
| `MonsterSpawner` | Session 作用域拥有刷怪循环 | 不暴露下次刷怪剩余时间，恢复后只能从头开始延迟 |
| `ApplicationHost` | 有界停止 Session 和作用域 | `BeginShutdown` 当前立即停止 Application 根作用域，无法在取消前完成最终快照与 Flush |
| `GameMenuController` | 暂停和三类玩法面板 | 没有保存、继续或新游戏的真实入口及忙碌 / 错误反馈 |
| 策略测试 | Runtime 文件 IO 当前冻结为零 | 尚未把唯一 Storage 实现登记为正式所有者，也没有冻结 `persistentDataPath` 唯一入口 |

## 范围边界

### 本阶段完成

- 冻结存档 V1 文档结构、Header、Profile / Run 数据边界、校验规则和 `SaveSchemaVersion` 当前值。
- 扩展纯 DTO / Mapper，覆盖当前玩法继续运行所需的完整持久状态。
- 建立 Serializer、Storage、Snapshot、Restore Preparation、Save Coordinator 六层边界。
- 建立同目录临时文件、唯一提交代际、SHA-256 校验、两代有效文件保留和损坏回退。
- 建立 `auto` 自动槽位；接口接受任意合法 `SaveSlotId`，但本阶段不制作手动槽位管理页面。
- 建立 Session 绑定的 Snapshot Source 和独立 `RestoreGameSessionInitializer`。
- 为运行时 Model、随机系统、实例 ID 生成器、Actor、Spawner、Spawn / Loot 补齐最小 Capture / Restore 接口。
- 建立集中 Dirty revision、检查点合并、单写者队列和退出前有界 Flush。
- 在当前游戏菜单加入保存、读取自动存档、新游戏和状态反馈，保持键鼠与手柄导航可用。
- 加固策略测试，使只有 Storage 访问运行时文件和存档根路径。
- 建立历史 Schema 夹具、损坏夹具、故障注入和端到端恢复测试。

### 明确不做

- 不实现 `UserSettingsData`、语言、音频、输入、显示或可访问性设置文件；共享存储原语可以复用，但正式 Settings 由 `alpha 0.2.3` 完成。
- 不建立 Boot / FrontEnd / Loading 场景、通用 SceneFlow、完整存档选择页、删除 / 重命名槽位 UI 或云存档；由 `alpha 0.2.4` 及后续阶段完成。
- 不建立通用平台焦点 / 挂起恢复系统；本阶段只处理本地存档所必需的桌面退出前有界 Flush，完整平台生命周期由 `alpha 0.2.5` 完成。
- 不替换现有 `Debug.Log*`、不建立全局 Logger / 错误页 / Toast 框架；本阶段只返回稳定错误码并在现有菜单显示最小反馈，统一日志与错误治理由 `alpha 0.2.6` 完成。
- 不恢复 UI 焦点、Hover、拖拽、展开页、动画、在途投射物、特效、音频播放实例或纯缓存。
- 不修改玩法数值、掉落概率、商人生成规则、配置内容或美术资源。
- 不增加构建、发布、CI / CIA、性能测试、遥测、崩溃上报、云同步或上线后运营内容。

## 冻结数据契约

### 存档文档

V1 顶层结构固定为：

```text
SaveDocumentDto
├─ Header: SaveHeaderDto
└─ Payload: SavePayloadDto
   ├─ Items: ItemInstanceDto[]
   ├─ Profile: ProfileSaveData
   └─ Run: RunSaveData
```

`Items` 是整个槽位的唯一物品实例表。Profile 背包 / 装备、Run 商人库存和世界掉落只保存 `ItemInstanceId` 引用，不重复嵌入同一物品。Mapper 必须证明每个物品实例恰好属于背包、装备、商人、世界掉落之一；游离、重复拥有和悬空引用均阻止加载。

本阶段把 `SaveSchemaVersion.Current` 固定为 `1`，并保留 `0 → 1` 历史迁移夹具。文件封装版本和业务 Schema 各自独立递增，禁止因为 JSON 排版或存储文件名变化而修改业务 Schema。

### Header

`SaveHeaderDto` 至少包含：

- `formatId`：固定为 `darkflare-save`。
- `formatVersion`：文件封装版本，V1 固定为 `1`，与业务 Schema 分离。
- `saveSchemaVersion`：当前存档 Schema。
- `gameVersion`：写入时 `Application.version`。
- `catalogId` 与 `contentVersion`：写入时唯一内容目录身份。
- `slotId`：规范化后的 `SaveSlotId`。
- `commitSequence`：槽位内从 1 开始严格递增的提交代际。
- `createdUtc`、`updatedUtc`：ISO 8601 UTC。
- `payloadSha256`：规范化 Payload UTF-8 字节的小写 SHA-256。
- `summary`：只包含菜单所需的安全摘要，不复制完整运行状态。

Header 不参与自身校验值计算。读取顺序固定为格式验证 → 槽位验证 → Payload 校验 → Schema 兼容 → 迁移 → 当前 DTO 反序列化。

### ProfileSaveData

V1 Profile 数据包含：

- `profileId` 与 `PlayerId`。
- 金币。
- 背包宽高、物品位置和尺寸。
- 玩家装备槽与物品实例引用。

Profile 不保存场景对象、`CombatActor` 引用或商人 / 世界掉落所有权。当前只有 `local-default` Profile，但字段不得写死进文件路径。

### RunSaveData

V1 Run 数据包含：

- `RunId`、下一个怪物序号和下一个世界掉落序号。
- 随机根种子及每个 `GameplayRandomChannel` 的下一个序号；未知或重复通道阻止加载。
- 玩家定义、技能、位置、当前 / 最大生命与法力、存活状态、复活剩余秒数和自动攻击冷却剩余秒数。
- 每个存活怪物的 `MonsterInstanceId`、定义 ContentId、完整随机种子组、基础属性、精确词条 / 修改器、位置、资源和接触伤害冷却剩余时间。
- 每个世界掉落的 `WorldDropId`、物品实例引用和位置。
- Trader ContentId、有序库存引用及当前买卖倍率。
- Spawner 是否运行、下次生成尝试剩余秒数。

位置使用纯数值 DTO；计时器只保存非负“剩余逻辑秒数”，禁止保存进程内 `Time.time` 或墙钟绝对时间。死亡且已产生掉落的怪物不保存；恢复后也不重建在途投射物和视觉表现。玩家死亡时保存 `IsAlive = false` 和复活剩余秒数，存活时复活剩余秒数必须为 0；Restore 必须延续该窗口，禁止默默恢复为满生命。

### 精确恢复与兼容

- 怪物保存已掷出的词条和修改器，不在读档时重新掷骰；配置只用于解析定义、Prefab 和当前规则依赖。
- `RunInstanceIdGenerator` 从保存的 Run 与序号恢复，下一次分配不得与存档内实例冲突。
- `GameplayRandomSystem` 恢复每个通道的继续位置；恢复后的后续种子序列必须与不中断运行完全一致。
- 当前 Schema 只接受当前 ContentCatalog 的 ID 与版本；内容迁移必须先由已登记步骤完成，不能猜测替换缺失内容。
- `GameVersion` 用于诊断和显式兼容策略，不替代 `SaveSchemaVersion` 或 `ContentVersion`。
- 迁移只修改内存副本。只有 Restore 成功后，Coordinator 才可把迁移后的当前 Schema 作为下一提交代际写回。

## 存储与序列化契约

### 唯一路径入口

- `ISavePathProvider` 是 `Application.persistentDataPath` 的唯一读取者；正式实现返回 `<persistentDataPath>/DarkFlare/Saves`。
- `ILocalSaveStorage` 是 Runtime 中 `File` / `Directory` 的唯一所有者；路径拼接只接受强类型 `SaveSlotId` 和内部生成的文件名。
- 测试注入临时根目录，TearDown 只清理该精确目录，不读取或覆盖真实玩家存档。
- 策略测试把 Storage 文件登记为规则所有者，而不是加入长期例外清单；Gameplay、UI 和其他 Infrastructure 文件继续零直接 IO。

### 代际文件布局

每个槽位使用独立目录：

```text
<root>/<slotId>/
├─ <commitSequence>.save
├─ <previousCommitSequence>.save
├─ <unique>.tmp
└─ latest-corrupt.save   # 仅在需要保留诊断副本时存在
```

不使用容易中断的可变 `current` 指针。提交过程固定为：

1. 在目标槽位目录创建唯一临时文件。
2. 写入 UTF-8 无 BOM 的紧凑 JSON，并 Flush 到文件系统。
3. 重新读取临时文件，验证格式、槽位、代际和 SHA-256。
4. 在同一目录内把临时文件原子重命名为唯一 `<commitSequence>.save`；目标名禁止覆盖。
5. 再次枚举并确认新代际有效后，最多保留最近两份有效提交和一份最新损坏诊断副本。

任何一步失败都不得删除上一有效代际。启动时 `.tmp` 不视为已提交；Storage 清理陈旧临时文件并从高到低验证提交代际，第一份有效文件为当前，下一份有效文件为备份。若没有有效代际则返回安全失败，不创建空白存档冒充恢复成功。

### Serializer

- 使用项目现有 Newtonsoft.Json，但通过独立 `ISaveSerializer` 封装，不在领域代码散落设置。
- 禁用类型元数据和任意 CLR 类型创建；使用不变文化、UTC、紧凑格式、确定性属性顺序和有界深度。
- 序列化输入必须是已经冻结的纯 DTO；Serializer 不访问 Unity Object、QFramework 或内容目录。
- 反序列化先得到受限 `JObject`，完成 Header / 校验 / 迁移后再映射到当前强类型 DTO。
- 未知未来 Format 或 Schema 直接拒绝；缺少必填字段、非有限浮点值、负计时器、重复 ID 和越界集合均返回结构化验证失败。
- V1 读取上限固定为单文件 16 MiB、JSON 深度 64、物品 8192、存活怪物 512、世界掉落 4096；随机通道必须与当前已登记枚举一一对应。调整上限属于显式合同变更，不能由任意文件自行扩大。

## Coordinator 与生命周期契约

### 所有权

| 组件 | 所有者 | 职责 |
| --- | --- | --- |
| PathProvider / Serializer / Storage / Migration Registry | Application | 无 Session 引用的进程级存档能力 |
| `SaveCoordinator` | Profile | 槽位队列、Dirty revision、检查点、读取预检和 Flush |
| `SessionSnapshotSource` | 当前 Session | 同步捕获当前 generation 的运行状态；Session 停止前失效 |
| `PreparedRestore` | 单次加载事务 | 已校验、迁移、解析内容的冻结恢复计划，不持有旧 Session 对象 |
| `SessionSaveFacade` | 当前 Session | 给 UI / Gameplay 的唯一 Save、Continue、NewGame 入口 |

任何 Profile 服务不得长期缓存 `IArchitecture`、场景组件或旧 generation。Session Running 时注册 Snapshot Source，Scene / Session 停止前先注销；迟到保存请求必须以 `SessionUnavailable` 完成，不得访问下一代架构。

### 单写者和请求仲裁

- 每槽位一个受 ProfileScope 拥有的 UniTask 单写者队列；本阶段只有 `auto` 槽位，但实现不得依赖全局单例锁。
- 同一时刻最多一个 Capture / Serialize / Commit 事务。活动写入期间的新检查点合并为最多一次待重捕获；每个调用者拥有独立 completion，恰好完成一次。
- 显式保存等待自己的提交结果；被合并请求共享最终提交结果，但保留各自请求原因用于诊断。
- Dirty revision 单调递增。捕获 revision `N` 成功后，只在当前 revision 仍为 `N` 时清除 Dirty；捕获后发生的新事件必须保留待保存状态。
- Load 与 Save 不并行修改同一槽位。Continue 先等待已接受的写事务收敛，再读取；NewGame 不删除旧存档，直到新 Run 后续成功提交才形成新代际。

### 检查点

- 背包、装备、金币、交易、打造、Actor 资源、怪物 / 掉落增删和随机状态推进只标记 Dirty，不直接写文件。
- 成功拾取、装备、交易、打造等关键事务可以请求检查点；短时间内请求由 Coordinator 合并和去抖，不为每个事件各写一次。
- 打开暂停菜单、显式保存、Continue 前以及正常退出前请求一次一致快照。
- Capture 是主线程上的无 await 同步操作；Unity 主线程执行期间不会与 Update / Command 交错。Capture 完成后 DTO 深拷贝冻结，后续序列化和 IO 可切到后台。
- 位置即使没有独立事件，也会在暂停、显式保存和退出的强制 Capture 中写入，不依赖 Dirty 才决定是否捕获。

### 关闭顺序

当前 `BeginShutdown` 的“先取消 ApplicationScope”顺序必须调整为：

```text
Application 进入 ShuttingDown，拒绝新请求
  → 主线程同步取得最终快照
  → 关闭 Save 请求入口并有界 Flush 已接受写入
  → 停止当前 Session
  → 停止 Profile / Application 作用域
  → 释放存档服务与其他资源
  → Application 进入 Shutdown
```

- 最终快照失败或 Flush 超时会使 Shutdown 返回 Failed，但不得阻止后续作用域停止。
- 桌面退出使用最小 quit gate：第一次退出请求启动有界 Shutdown 并暂缓真正退出；Shutdown 完成后只允许一次最终退出。更完整的焦点、移动平台挂起和恢复策略留给 `alpha 0.2.5`。
- `OnDestroy` / 应急关闭不能等待；只保证已经原子提交的代际有效，不承诺临时文件成为正式存档。
- 忽略取消的 IO 或 Flush 会使所属作用域进入 `Abandoned`；迟到任务只能完成内部诊断，不能再改写 UI、宿主状态、槽位元数据或新 generation。

## 读档与 Restore 事务

### 预检阶段

Continue 必须在当前 Session 仍可运行时完成：

1. 枚举 `auto` 槽位提交代际并验证校验和。
2. 选择最新有效提交；若回退上一代，在结果中标记 `RecoverySource.Backup`。
3. 检查 Format / Schema / Catalog 兼容性并对内存副本逐级迁移。
4. 反序列化当前 DTO，验证 ID、物品所有权、集合上限、数值和计时器。
5. 用当前 ContentCatalog 解析全部 ContentId，生成不含场景对象的 `PreparedRestore`。

任一步失败都返回结构化错误，当前 Session、玩家、架构和场景保持不变。

### 提交阶段

- `PreparedRestore` 成功后，通过现有 ApplicationHost scene-session latest-wins 协调器为当前已加载场景提交 `RestoreGameSessionInitializer`。
- Restore 与 NewGame 是不同类，不通过布尔参数复用同一初始化器。
- Restore 先验证场景配置与全部 Prefab / Sprite 依赖，再按冻结顺序提交：随机 / ID 生成器 → 玩家 → Profile 物品关系图 → 商人 → 存活怪物 → 世界掉落 → Spawner → 摄像机。
- 玩家、怪物和世界掉落使用保存的强类型实例 ID；不得调用会重新掷骰或重新分配身份的新生成入口。
- 恢复资源、冷却和 Spawner 逻辑时间后才进入 `Running`；Spawner 必须最后启动。
- 任一步失败由 initializer Rollback 释放所有 SessionObject、加载句柄和场景绑定。若作用域停止超时，则保持旧 lease 隔离，不允许创建下一代。

### 当前单场景的真实入口

在 `GameRoot.uxml` / `GameMenuController` 增加最小存档操作区：

- `保存进度`：写入 `auto` 槽位。
- `读取自动存档`：先预检，成功后重建当前 Main Session；无有效存档时禁用。
- `开始新游戏`：使用当前场景配置重建 NewGame Session，不先删除旧自动存档。
- 状态 Label：显示保存中、成功、已从备份恢复或稳定错误码对应的临时中文说明。

操作进行时三个按钮进入 Busy 状态，重复点击不得提交并发事务；关闭 / 重绑定时取消 UI 回调但不取消已经由 ProfileScope 接受的写入。成功重建后菜单绑定新的 Session generation，焦点回到可用首项。键鼠点击、键盘导航和手柄导航必须共用 UI Toolkit 焦点链。

这些入口是当前单场景兼容消费者，不替代 `alpha 0.2.4` 的 FrontEnd、Loading、Modal 和正式槽位页面；临时可见文本在 `alpha 0.2.3` 迁移到 Localization String Table。

## 结构化结果

### SaveErrorCode

至少冻结以下稳定错误码：

| 类别 | 错误码 |
| --- | --- |
| 请求 / 状态 | `InvalidRequest`、`OperationInProgress`、`SessionUnavailable`、`SnapshotUnavailable`、`Cancelled` |
| 路径 / IO | `RootUnavailable`、`PermissionDenied`、`StorageFull`、`IoFailure`、`CommitFailed` |
| 文件 / 完整性 | `SlotNotFound`、`FormatUnsupported`、`HeaderInvalid`、`ChecksumMismatch`、`NoValidGeneration` |
| 版本 / 内容 | `FutureSchemaUnsupported`、`MigrationFailed`、`ContentVersionMismatch`、`ContentMissing` |
| 数据 / 恢复 | `PayloadInvalid`、`RestorePreparationFailed`、`RestoreCommitFailed`、`FlushTimedOut` |

`SaveOperationResult` 同时包含 operation、slot、错误码、恢复来源、是否可重试和内部 Exception。UI 只消费稳定错误码与恢复动作；内部异常不得把完整存档内容或用户绝对路径写入玩家反馈。

## 策略与禁止事项

- Runtime 中只有 PathProvider 可读取 `Application.persistentDataPath`，只有 Storage 可调用 `File` / `Directory`。
- 任何 Model、System、Command、Query、Controller 和 Session Initializer 都不得直接序列化或访问文件。
- 不使用 `PlayerPrefs` 保存游戏或槽位元数据。
- 不把 `UnityEngine.Object`、Asset GUID、资源地址、场景名、显示文本或本地化 Key 作为存档身份。
- 不在 Restore 中调用 `GrantStartingWeaponCommand`、`GrantGold`、`SetupMerchant` 的随机建档路径或重新生成怪物 / 物品的路径。
- 不用 `UniTaskVoid`、裸 `Forget()` 或无作用域后台线程执行保存。
- 不在 Capture 之后修改 DTO；每个请求使用独立冻结快照。
- 不直接覆盖唯一有效存档，不在新代际验证前删除上一代。
- 不把取消当成功，不把备份恢复伪装为正常主文件读取。
- 不通过增加长期策略例外掩盖新 Storage 所有权；规则本身必须表达唯一入口。

## 执行阶段

### 阶段一：DTO、版本和纯数据验证

目标：先冻结文件外的完整数据合同，并证明映射可逆。

- 新增 Header、Payload、Profile、Run、随机、Actor、Monster、WorldDrop、Spawner、计时器和摘要 DTO。
- 为 Monster / WorldDrop ID 增加严格 Parse / TryParse，为 Run ID 生成器增加可导出 / 恢复状态。
- 扩展 `RuntimeStateMapper` 或拆分 `SaveSnapshotMapper`，建立跨 Profile / Run 的唯一物品所有权验证。
- 建立当前 Save Schema 注册与至少一个 `0 → 1` 历史迁移夹具；迁移不修改输入。
- 扩展 DTO 反射策略，继续保证纯数据边界。

阶段验收：纯 DTO round-trip、非法 ID、重复所有权、未来 Schema、缺失迁移链、随机 / ID 序列继续测试全部通过。

### 阶段二：Serializer 与代际 Storage

目标：在不接触玩法的情况下完成可故障注入的本地文件事务。

- 实现 PathProvider、Serializer、SHA-256、Storage 和槽位元数据读取。
- 实现临时写入、重读验证、唯一代际提交、两份有效代际保留、损坏回退和陈旧临时文件清理。
- 使用可注入文件操作边界模拟权限、空间不足、中断、重命名失败和取消，不依赖真实磁盘故障。
- 更新 Infrastructure Policy，冻结 Storage / PathProvider 唯一所有权。

阶段验收：首次提交、覆盖提交、中断恢复、当前损坏回退、全部损坏拒绝、并发调用拒绝 / 排队、临时根目录隔离测试全部通过。

### 阶段三：运行时 Capture / Restore Adapter

目标：补齐现有玩法状态的精确快照和无随机恢复能力。

- 为 Inventory、Equipment、Economy 增加静默全量 Capture / Restore 提交接口，提交后只发送一次恢复完成事件。
- 为 GameplayRandomSystem 和 RunInstanceIdGenerator 增加状态导出 / 恢复。
- 为 CombatActor、PlayerController、MonsterController、MonsterSpawner 增加资源、位置和剩余逻辑时间快照。
- 为 SpawnSystem / LootSystem 增加“按已准备实例精确重建”入口，保留现有 NewGame 生成入口不变。
- 建立 SessionSnapshotSource、PreparedRestore 和资源依赖收集。

阶段验收：同一内存状态 Capture → PreparedRestore → 新架构提交后，所有持久字段等价；后续随机种子和新实例 ID 与不中断运行一致。

### 阶段四：Coordinator、Restore Initializer 与关闭顺序

目标：把文件事务、Session 事务和 Application 生命周期组成一个有界闭环。

- Application Boot 创建存档基础服务，Profile 创建 SaveCoordinator。
- Session Running 注册 generation-bound Snapshot Source / Facade，Session 停止前注销。
- 实现单写者队列、Dirty revision、检查点合并、Save / PrepareContinue / Flush。
- 实现独立 Restore initializer 和当前场景继续 / 新游戏请求。
- 调整 BeginShutdown / ShutdownCore 顺序并接入桌面 quit gate。
- 对所有 completion、取消、失败和 latest-wins 竞争进行 exactly-once 验证。

阶段验收：保存中继续、继续中再次新游戏、退出中保存、Flush 超时、Restore 回滚和 Session generation 隔离测试全部通过。

### 阶段五：游戏菜单真实消费者

目标：让键鼠和手柄玩家能在当前 Main 循环中使用存档。

- 在 UXML / USS 增加存档操作区和 Busy / 状态样式。
- GameMenu 只通过 SessionSaveFacade 发起请求，不直接访问 Storage、DTO 或 Application 路径。
- 绑定有效 Session 时刷新槽位可用性；重建 Session 后只绑定一次新 generation。
- 增加键鼠、手柄焦点、密集点击和 UI 解绑测试。

阶段验收：真实 Main 中完成保存 → 修改状态 → 读取 → 恢复保存点，以及新游戏不重复发放、UI 不重复注册。

### 阶段六：故障演练、回归与文档

目标：用完整证据封板，而不是只证明成功路径。

- 运行 EditMode、项目自有 PlayMode、完整 PlayMode 和策略测试。
- 完成两次真实 Play / 退出；检查 Application / Session / lease、Player、Spawner、对象注册、任务和 Console。
- 在临时目录演练中断写入、当前损坏、备份恢复、全部损坏、迁移失败和内容缺失。
- 新增本地存档模块文档，更新项目概览、目录结构、玩法循环、文档索引和总计划。
- 记录最终测试数字、已知限制和与计划偏差，将本计划移动到 `plan/archive/`。

## 测试矩阵

### EditMode

- Header / Payload 确定性序列化、SHA-256 和非有限数值拒绝。
- Profile / Run DTO 与运行时对象图 round-trip。
- 物品唯一所有权、悬空引用、重复 Actor / Monster / Drop / 随机通道和越界集合拒绝。
- 每个随机通道恢复后连续输出与未中断基准一致。
- Run / Monster / WorldDrop ID 序号恢复后无冲突。
- Schema `0 → 1` 迁移、未知未来版本拒绝、迁移失败不修改输入。
- Storage 首次提交、连续三代保留两代、临时文件中断、当前损坏回退、全损坏拒绝。
- 权限、空间不足、Flush、重命名和取消故障注入。
- 单写者密集请求、合并后各 completion exactly-once、Dirty revision 不误清除。
- Runtime 仅 PathProvider / Storage 命中路径和文件 API；Gameplay 继续零直接 IO / PlayerPrefs。

### PlayMode

- Main 中改变位置、生命、金币、背包、四槽装备、商人库存、随机通道和刷怪状态后保存，重建 Session 并逐项等价恢复。
- 恢复后不发放初始武器 / 金币，不重新生成商人库存，不重掷怪物词条。
- 存活怪物和世界掉落保持实例 ID、内容、位置与资源；下一次怪物 / 掉落 ID 不冲突。
- 保存前后的随机通道后续输出与不中断基准相同。
- 当前文件损坏时从备份恢复并明确标记；全部损坏时当前 Session 保持运行且零部分提交。
- Restore 预热失败、内容缺失和提交中取消时，释放对象、句柄、任务与架构 lease。
- 连续三轮保存 → 继续 → 新游戏不累积 ApplicationHost、Player、Spawner、SessionObjectRegistry、事件或 UI 绑定。
- 保存 / 继续 / 新游戏按钮支持键鼠和手柄焦点；密集点击只有一个实际事务。
- Shutdown Flush 成功、失败、超时均有界，迟到 continuation 不改写 Shutdown 或下一 generation。

### 手工冒烟

1. 进入 Main，拾取并装备物品，交易 / 打造，受伤并等待生成怪物。
2. 打开菜单保存，记录金币、生命、位置、装备、怪物和地面掉落。
3. 继续游玩改变状态，再读取自动存档，确认恢复保存点且随机流程继续。
4. 开始新游戏，确认初始资源只发放一次；再读取旧自动存档，确认旧档在新 Run 首次成功保存前仍可用。
5. 连续两次退出并重新进入 Play，确认 Console Error 为 0，Application / Session / lease 与对象计数回到基线。

## 文件变更预期

预计新增或修改以下职责区域；实际文件数可以因保持内聚而调整，但不得跨越范围边界：

- `Assets/Scripts/Runtime/Infrastructure/Persistence/`：Save DTO、Serializer、Storage、Coordinator、Snapshot / Restore Preparation、结果与错误码。
- `Assets/Scripts/Runtime/Infrastructure/Identity/StableInstanceIds.cs`：Monster / WorldDrop 解析与 Run 生成器状态。
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationHost.cs`：存档服务所有权、当前场景 Continue / NewGame、退出 Flush 顺序。
- `Assets/Scripts/Runtime/Gameplay/Bootstrap/`：Restore initializer 与 Session Save Facade 配置。
- `Assets/Scripts/Runtime/Gameplay/Actors/`、`Combat/`、`Inventory/`、`Trading/`、`Spawning/`、`Loot/`：最小 Capture / Restore 接口。
- `Assets/UI/GameRoot.uxml`、`Assets/UI/GameMenu.uss`、`GameMenuController.cs`：真实玩家入口。
- `Assets/Scripts/Tests/EditMode/`：序列化、Storage、迁移、Mapper、Coordinator 与策略合同。
- `Assets/Scripts/Tests/PlayMode/`：Main 端到端继续游戏、故障恢复、UI 和生命周期回归。
- `Assets/Data/Saves/`：只允许历史 Schema / 损坏样例等只读 Editor 夹具；正式运行不读取该目录。

## 提交切片

建议按可独立回退的最小闭环提交：

1. `feat(save): 冻结存档 DTO 与恢复状态合同`
2. `feat(save): 实现代际存储与损坏恢复`
3. `feat(save): 接入运行时快照与精确恢复`
4. `feat(save): 建立协调器与继续游戏事务`
5. `feat(ui): 接入自动存档操作入口`
6. `docs(infrastructure): 总结本地存档闭环`

每个切片提交前至少通过对应专项测试和 Unity 重编译；只有阶段六完整回归完成后，才更新总计划为 `alpha 0.2.2` 已完成。

## 风险与对策

| 风险 | 对策 |
| --- | --- |
| 保存时跨 Model / 场景对象状态不一致 | 主线程无 await 同步 Capture，结束后只处理深拷贝 DTO |
| 文件替换中断损坏唯一存档 | 唯一提交代际 + 同目录原子重命名；验证新代际后才清理旧代际 |
| 当前存档损坏导致玩家无法继续 | 从高到低验证两份代际，明确 Backup 恢复；全损坏安全拒绝 |
| 旧档迁移后内容缺失 | 停止旧 Session 前完成迁移和 ContentId 全量解析 |
| Restore 重复生成身份或随机状态 | 精确重建入口 + 保存生成器序号 + 各随机通道继续位置 |
| 保存任务跨 Session generation 访问旧架构 | Session-bound Snapshot Source，停止前注销；DTO 冻结后不再访问架构 |
| 退出先取消根作用域导致无法落盘 | 重排 ShuttingDown → Capture / Flush → Session / Scope Stop 顺序 |
| 密集请求覆盖或 completion 丢失 | 每槽位单写者、至多一次待重捕获、每调用者独立 exactly-once completion |
| 读取中失败破坏当前游戏 | 预检全部在当前 Session 存活时完成，成功后才提交 Restore Session |
| UI 随场景重建重复订阅 | 复用 `SceneSessionBinding`，只绑定同场景 Running generation |

## 已知后续边界

- `alpha 0.2.3` 将存档错误和菜单操作文本迁入本地化表，并让 Settings 复用 Storage 原语但使用独立 Schema / 文件域。
- `alpha 0.2.4` 将当前 Main 内操作迁移到 Boot / FrontEnd / Loading / InGame 状态和正式槽位页面；SaveCoordinator、Storage 和 Restore initializer 接口保持不变。
- `alpha 0.2.5` 补齐移动平台挂起、焦点、后台恢复和平台退出差异。
- `alpha 0.2.6` 把存档诊断接入统一 Logger、玩家错误反馈和 Addressables 治理。
- 云存档、跨设备冲突、账号、多 Profile UI 和手动槽位管理均不属于当前阶段。

## 实际交付与偏差

- V1 文档、校验和恢复图集中在 `Infrastructure/Persistence` 扁平目录中，没有按计划拆成六个物理子目录；职责仍由 DTO、Serializer、Storage、Snapshot、Preparation、Initializer、Coordinator 与 Facade 类型分离。
- 正式格式为 `darkflare-save` V1 / Save Schema 1，最大 16 MiB；Header 保存游戏 / 内容版本、槽位、提交序号、UTC 时间、摘要与 Payload SHA-256。
- Storage 实际采用 `<slot>/<commitSequence>.save` 与同目录唯一 `.tmp`；提交后保留两份有效代际，另最多保留最高无效代际用于诊断。当前损坏从 Backup 恢复，全损坏安全拒绝。
- 快照实际覆盖计划中的 Profile、物品、背包、四槽装备、金币、玩家、随机通道、Run 实例序号、Spawner、存活怪物、世界掉落和商人状态；UI / 动画 / 在途投射物等瞬态数据保持不持久化。
- Coordinator 采用 Profile 级单 writer：运行中的一批与最多一次待重捕获，调用者各自 exactly-once completion。异常路径额外保证先退休 worker，再结算当前批和待处理批。
- Continue 在旧 Session 存活时完成文件、Schema、迁移、Payload 和 ContentId 预检；成功后才通过 Restore initializer 重建 Session。NewGame 不预先删除或覆盖旧 `auto`。
- 退出顺序调整为停止新请求、等待 load、最终 Capture / 5 秒 Flush、再停止 Session / Profile / Application；桌面第一次 quit 请求由门禁延后，完成关闭后只放行一次。
- 菜单增加保存、继续和新游戏三项真实入口、Busy 门禁和状态反馈；当前文本仍为中文硬编码，按边界留给 `alpha 0.2.3` 本地化。
- 没有在 `Assets/Data/Saves` 落历史或损坏实体文件；这些场景由内存文件系统、受控字节夹具和注入故障完成，避免测试接触开发者真实存档。

## 最终验收证据

- Unity `6000.4.3f1` 重编译：0 error。
- EditMode 全量：`310/310` 通过；基础设施策略：`24/24` 通过。
- 项目自有 PlayMode：`48/48` 通过。
- PlayMode 完整运行：52 项中 50 项通过、0 失败；2 项 Input System 包集成测试因上游 issue 1252825 跳过。
- 存档专项覆盖确定性 JSON、SHA-256、V0 → V1、未来版本拒绝、非法 / 非有限 Payload、三代保留两代、临时写中断、当前损坏回退、全部损坏拒绝、内容预检、请求合并、Flush 超时、unexpected storage exception 结算和 Main 完整状态恢复。
- Main 端到端验证保存 → Continue → NewGame → Continue 保留旧提交代际，且 Restore 不重复发放初始资源。
- 两次真实 Play 均达到 Application `Ready`、Session `Running`、玩家 1 个、存档 Facade 有效、菜单绑定 1 次；第二次退出后 Console Error 为 0。
