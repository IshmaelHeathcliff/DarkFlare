# 本地存档与 Session 恢复

> 状态：`alpha 0.2.2` 已实现
> 最近更新：2026-08-19

## 模块目标

本模块为当前 `Main` 单场景玩法提供可验证的本地存档闭环：运行中捕获一致快照、提交 `auto` 槽位、在停止旧 Session 前完成读档预检、以独立 Restore 事务重建 Session，并在应用退出前有界 Flush。

正式存档只写入 `Application.persistentDataPath/DarkFlare/Saves`。玩法 Model、System、Command、Controller 和 Session Initializer 不直接访问文件；UI 只调用当前 Session 的 `SessionSaveFacade`。

## 分层与职责

| 层 | 主要类型 | 职责 |
| --- | --- | --- |
| 数据合同 | `SaveDtos`、`SaveDataValidator` | 定义 Header、Profile、Run、摘要、集合上限与跨字段校验 |
| 序列化 | `NewtonsoftSaveSerializer` | 规范 JSON、严格 UTF-8、Payload SHA-256、Schema 迁移和强类型映射 |
| 路径 | `PersistentSavePathProvider` | 唯一读取 `Application.persistentDataPath` 并生成受控根目录 |
| 存储 | `LocalSaveStorage` | 单操作门禁、临时文件、持久写、提交代际、读后验证、备份回退和清理 |
| 快照 | `SessionSnapshotSource` | 在 Unity 主线程从精确 architecture generation 捕获冻结 DTO |
| 恢复预检 | `SaveRestorePreparer` | 使用当前 `ContentCatalog` 解析稳定 ID，构造不接触旧 Session 的 `PreparedRestore` |
| 恢复提交 | `RestoreGameSessionInitializer` | 预热资源、重建状态、提交新 Session；失败时按初始化事务回滚 |
| 协调 | `SaveCoordinator` | Profile 级单写者、Dirty revision、请求合并、PrepareContinue 和退出 Flush |
| 玩家入口 | `SessionSaveFacade`、`GameMenuController` | 对当前 generation 暴露保存、继续、新游戏与稳定错误反馈 |

`ApplicationHost` 拥有 PathProvider、Serializer、Storage 和 Profile 级 Coordinator；每个 Running Session 只绑定一个 generation-bound Snapshot Source 与 Facade。Session 停止时先解绑这些入口，再释放场景与架构。

## 文件格式与 Schema

V1 文件格式固定为：

- `formatId = "darkflare-save"`
- `formatVersion = 1`
- `saveSchemaVersion = 1`
- 最大文档 16 MiB，最大 JSON 深度 64
- 最大物品 8192、存活怪物 512、世界掉落 4096
- Header 包含游戏版本、内容目录 ID / 版本、槽位、提交序号、UTC 创建 / 更新时间、Payload SHA-256 和显示摘要
- Payload 分为 `ProfileSaveData` 与 `RunSaveData`，所有引用使用稳定 ContentId 或强类型实例 ID

Serializer 使用项目已有 `Unity.Newtonsoft.Json`。写出前先把 Payload 转为按属性名 Ordinal 排序的规范 JSON，再计算小写 SHA-256；Header 不参与校验值。读取顺序为格式 / 槽位 / 提交序号检查、Payload 校验、Schema 兼容与逐级迁移、强类型验证。当前登记了历史 `0 → 1` 迁移；未来 Schema 会被安全拒绝。

禁止把 `UnityEngine.Object`、Asset GUID、Addressables 地址、资源路径、场景对象引用、显示文本或本地化 Key 写入存档身份。

## 存储布局与提交语义

```text
Application.persistentDataPath/
└── DarkFlare/Saves/
    └── auto/
        ├── <commitSequence>.save
        └── <commitSequence>-<unique>.tmp
```

- 槽位目录由 `SaveSlotId` 生成并再次验证不能越出存档根目录。
- 提交序号必须严格递增；目标文件使用 `CreateNew`，不覆盖已有代际。
- 数据先写入同目录唯一 `.tmp`，Flush 到持久介质，反序列化验证成功后再以不覆盖方式重命名为 `.save`。
- 启动加载先清理陈旧 `.tmp`，再从高到低验证代际；最高有效代际为 Current，次高有效代际为 Backup。
- 正常清理保留两份有效代际；无效代际只保留最高一份用于诊断。任何失败都不删除上一份有效存档。
- 当前代际损坏时可以从 Backup 恢复，并通过 `SaveRecoverySource.Backup` 明确告知调用方；全部损坏时返回 `NoValidGeneration`。

`LocalSaveStorage` 本身同步执行，Coordinator 把序列化和文件 IO 切到线程池。测试通过注入 `ILocalSaveFileOperations` 和临时根目录演练故障，不读取或覆盖真实玩家存档。

## 持久化状态

当前快照覆盖：

- Profile：玩家 ID、金币、物品完整关系图、10×6 背包位置、四槽装备。
- Run：Run ID 与怪物 / 世界掉落下一序号、全部登记随机通道及其消费位置。
- 玩家：内容 ID、位置、生命 / 法力、存活状态和有效属性。
- 怪物：实例 ID、内容、随机种子、词条实例、基础 / 有效属性、位置、资源和接触攻击剩余时间。
- 世界掉落：世界实体 ID、所含物品实例 ID 和位置。
- 商人：内容 ID、有序库存和买卖倍率。
- 刷怪器：是否运行、下一次生成剩余时间和当前存活数。

不持久化 UI 焦点、Hover、拖拽状态、展开页、动画播放进度、在途投射物、特效、音频实例或纯缓存。恢复后由持久状态和当前配置重建这些瞬态表现。

## 运行流程

### 保存

1. `SessionSaveFacade.SaveAutoAsync` 校验自己仍属于当前 Running Session。
2. `SaveCoordinator` 在主线程同步调用 `SessionSnapshotSource.Capture()`，快照返回后不再访问架构。
3. 同槽位当前写事务继续运行；密集请求最多形成一次待重捕获，每个调用者保留独立且恰好一次的完成结果。
4. 线程池读取上一有效代际、生成严格递增序号并提交新文件。
5. 只有当前 generation 的成功提交才更新已持久化 Dirty revision。

### 继续游戏

1. Coordinator 先等待已经接受的写事务收敛，再在线程池加载、校验、迁移并准备恢复图。
2. 文件、Schema、Payload 或 ContentId 预检失败时，不停止当前 Session。
3. 预检成功后，Facade 才请求 `ApplicationHost` 以 `RestoreGameSessionInitializer` 重建当前场景 Session。
4. Restore 事务预热资源、重建玩家 / 怪物 / 掉落 / 商人 / 随机 / 生成器状态，最后提交刷怪器并进入 Running。
5. 初始化取消或失败沿用 Session 事务回滚，不能部分提交或回写旧 generation。

### 新游戏

`StartNewGameAsync` 使用当前场景配置重建 NewGame Session。它不会预先删除或覆盖 `auto` 槽位，因此新 Run 在第一次成功保存前仍可继续旧档。

### 退出

正常桌面退出由 `Application.wantsToQuit` 门禁拦住第一次请求。`ApplicationHost` 依次关闭新请求入口、等待读取、捕获最终快照并在 5 秒内 Flush，然后才停止 Session、Profile 和 Application 作用域。Flush 失败或超时会进入结构化关闭结果，但不能无限阻塞退出。

`OnDestroy` / 应急关闭不等待，只关闭入口并保留已经原子提交的代际有效性；不承诺尚未提交的临时文件成为正式存档。

## 错误与调用约束

玩家入口统一返回 `SaveOperationResult`，包含 operation、slot、`SaveErrorCode`、恢复来源、是否可重试、内部异常和提交序号。UI 只显示稳定错误语义，不展示完整存档内容或用户绝对路径。

主要错误类别包括：请求 / Session 不可用、取消、路径 / 权限 / 空间 / IO、提交失败、槽位不存在、格式 / Header / 校验失败、无有效代际、未来 Schema、迁移失败、内容版本 / 内容缺失、Payload 无效、Restore 预检 / 提交失败和 Flush 超时。

后续代码必须遵守：

- Controller 不得直接访问 Storage、Serializer、DTO 或保存根路径。
- Capture 必须在主线程无 await 完成；后台只处理冻结纯数据。
- 任何异步存档操作必须由 Application / Profile 生命周期 TaskGroup 拥有，禁止裸 `Forget()`。
- Session-bound 入口必须校验精确 host、Session、architecture generation；旧 continuation 不得更新新 Session UI。
- Load 与 Save 不并行操作同一 Storage；Continue 必须先完成预检，再停止当前 Session。
- 新增持久字段必须同时更新 DTO、Validator、Snapshot、PreparedRestore、Restore 提交、迁移夹具、测试和本文档。

## 运行时入口

当前玩家入口位于游戏菜单底部：

- 保存：写入 `auto`。
- 继续：只有探测到有效 `auto` 时可用。
- 新游戏：重建当前 Main Session，不删除旧档。
- Busy 期间三个按钮统一禁用，并显示稳定中文反馈。

这些按钮同时支持鼠标、键盘和手柄焦点。相关文本已在 `alpha 0.2.3` 迁入 Unity Localization String Table。

## 验证证据

- Unity `6000.4.3f1` 重编译：0 error。
- EditMode 全量：`310/310` 通过。
- 项目自有 PlayMode：`48/48` 通过。
- 完整 PlayMode：52 项中 50 项通过、0 失败；2 项 Input System 包集成测试因上游 issue 1252825 跳过。
- 存档专项覆盖 DTO / 校验、确定性序列化 / SHA-256 / 迁移、Storage 中断 / 损坏 / 备份、恢复准备、Coordinator 合并 / 超时 / 异常结算，以及 Main Session 捕获与恢复。
- 两次真实 Play 均达到 Application `Ready`、Session `Running`、存档 Facade 有效、玩家 1 个、菜单绑定 1 次；第二次退出后 Console Error 为 0。

## 当前边界

- UI 只有一个 `auto` 槽位，没有手动槽位列表、删除、重命名、Profile 选择或云同步。
- 仍只有 `Main` 单场景，没有 Boot / FrontEnd / Loading 和正式 SceneFlow。
- Settings 文件与 Locale 服务已在 `alpha 0.2.3` 落地；统一 Logger、Toast / 错误页和移动平台挂起恢复仍未实现，其中 UI 错误外壳由 `alpha 0.2.4` 负责。
- 退出门禁只完成当前桌面流程；平台差异由 `alpha 0.2.5` 处理。
- Settings 已使用独立路径域、Schema 和服务，未写入游戏存档；存档 Storage 原语继续只服务 Save Domain。
