# alpha 0.2.6 日志、错误处理与 Addressables 资源治理执行计划

> 状态：已完成并归档
> 建立日期：2026-08-25
> 规划基线：`601f0fd`（读档后怪物资源预热修复提交）
> 实施前置：`alpha 0.2.5` 已完成并归档，工作区干净
> 上位计划：[alpha 0.2 基础设施开发计划](./alpha-0.2-plan.md)
> 强制契约：[alpha 0.2 长期运行时契约](../../infrastructure/alpha-0.2-runtime-contract.md)
> 前置模块：[应用生命周期与会话作用域](../../infrastructure/application-lifecycle.md) · [本地存档与 Session 恢复](../../infrastructure/local-save.md) · [游戏状态、场景流与应用 UI 外壳](../../infrastructure/game-state-scene-flow-ui-shell.md) · [Application Audio](../../infrastructure/application-audio.md)

## 阶段结论

`alpha 0.2.6` 只补齐运行故障的可定位性、玩家错误反馈和资源所有权，不扩充玩法内容。实施顺序固定为“先冻结合同与债务基线，再建立 Logger，再接入异常和玩家反馈，最后统一资源服务与 Addressables 配置”。Logger 必须早于其他 Application 服务可用；资源服务由 Application 拥有，Session 和具体 Loader 只持有有主租约。

本阶段不把所有异常都当作可恢复错误。已处理的领域失败继续由 `SaveOperationResult`、`SceneFlowResult` 等结构化结果表达；未处理异常进入全局捕获并保留原始异常链。只有能够证明恢复动作的失败才向玩家提供重试或返回前台，进程级致命错误保持不可恢复且 first-failure-wins。

Addressables 治理不引入远端内容或热更新。现有 15 个 DarkFlare 条目按生命周期和资源类型迁出默认组，统一地址与 Label；Localization 自动管理组和 Unity 包生成组只验证、不接管。存档身份仍使用 `ContentId` / `InstanceId`，Addressables 地址和 GUID 都不得进入持久化身份。

## 已确认基线

- 规划基线提交为 `601f0fd`，工作区干净；`alpha 0.2.5` 验收基线为 EditMode `409/409`、项目 PlayMode `53/53`，完整 PlayMode 57 项中 55 项通过、0 失败、2 项因 Input System 上游问题忽略。
- `Assets/Scripts/Runtime` 有 37 个文件、110 处可执行的直接 `Debug.Log`、`LogWarning`、`LogError` 或 `LogException` 调用；生成输入类中的示例注释与 `Debug.Assert` 不计入迁移债务。
- 项目没有正式 Logger、稳定日志事件目录、有限内存日志缓冲或 Unity / UniTask 全局异常捕获。`LifecycleTaskGroup` 能报告受监控任务失败，但最终仍由各调用方直接写 Console。
- 存档、场景、输入、音频和平台模块已有各自错误码 / 结果；`SceneFlowResult` 已含本地化消息与恢复动作。当前缺口是统一的日志上下文、异常入口和资源错误到 Shell 表现的映射，不重做这些领域结果。
- Runtime 的 Addressables 静态 API 分布在 4 个文件共 9 处：`PrefabAssetLoader`、`SpriteAssetLoader`、`AudioClipLoader` 和 `ApplicationHost`。Prefab Loader 已有单飞与迟到释放测试，Sprite Loader 和 Application 音频配置仍各自管理原始句柄。
- `Default Local Group` 中有 15 个 DarkFlare 条目：6 个玩法 Prefab、7 个物品图标、1 个音频配置和 1 个 Audio Clip；其中只有 `audio/ui.confirm` 与 `infrastructure/audio` 使用 `Audio` Label。
- 当前没有 `Application.logMessageReceivedThreaded`、`AppDomain.UnhandledException` 或 `UniTaskScheduler.UnobservedTaskException` 订阅。项目使用的 UniTask 版本提供可注册的 `UnobservedTaskException` 事件。
- 新游戏与 Restore 已从场景配置、刷怪池和内容实例收集预热引用，但 Prefab、Sprite 与 Audio 使用三套所有权实现；Session 对象实例由 `SessionObjectRegistry` 负责，不使用 `Addressables.InstantiateAsync`。

## 目标

- 建立 Application 级结构化 Logger，冻结等级、类别、稳定事件 ID、上下文、异常和 Sink 合同。
- Unity Console 与固定容量内存环形缓冲成为首批真实 Sink；任何 Sink 失败都不能递归拖垮 Logger。
- Runtime 除唯一 Unity Console Adapter 外零直接 `Debug.Log*`，并由源码策略测试持续冻结。
- 捕获 Unity 未处理日志异常、AppDomain 未处理异常和未观察 UniTask 异常，保证订阅 / 退订幂等、递归受控、首个致命原因不被覆盖。
- 复用现有领域错误码，为存档、场景和资源失败提供稳定、本地化、可操作的 Shell 表现。
- 建立 Application 级 Addressable Asset Service，把静态 API、原始句柄、单飞、引用计数、取消和迟到完成收口到唯一后端。
- 用有主 Asset Lease 连接 Application、Session、Prefab、Sprite 和 Audio；所有 owner 关闭后资源计数回到基线。
- 冻结 DarkFlare Addressables 分组、Label、地址和预热声明规则，并建立 Editor 自动验证。
- 通过故障注入和三轮 FrontEnd → Main → FrontEnd / Continue 验证无句柄、实例、订阅或常驻对象累积。

## 非目标

- 不实现远端 Catalog、CDN、内容更新、下载进度、磁盘缓存策略或 DLC。
- 不上传日志、遥测或崩溃报告，不新增日志文件、开发者 Console 页面或玩家日志导出 UI。
- 不新增 Addressables Scene，不改变现有 Bootstrap + additive Main 场景拓扑。
- 不建立无真实消费者的通用对象池、`Addressables.InstantiateAsync` 封装或资源依赖注入框架。
- 不重写存档、场景流、输入、音频和平台结果类型；只补齐统一日志与玩家表现映射。
- 不改变 Save、Settings、Content Schema，不新增内容、玩法、手动槽位或云同步。
- 不读取或修改 `Docs/design/`。

## 冻结架构

### Logger 所有权与数据合同

| 组件 | 作用域 | 职责 |
| --- | --- | --- |
| `ApplicationLog` | 进程入口 | Runtime 唯一静态写入门面；不拥有 Sink，测试和 Subsystem Registration 可重置 |
| `ApplicationLogger` | Application | 分配单调序号、过滤等级、隔离 Sink、创建显式上下文 Logger |
| `UnityConsoleLogSink` | Application Adapter | 唯一允许调用 `Debug.Log*` 的 Runtime 文件；保留 Unity Object context |
| `RingBufferLogSink` | Application | 固定容量保存最近日志快照；覆盖最旧条目，不写磁盘 |
| `ApplicationExceptionMonitor` | Application | 安装 / 移除全局异常钩子，去重、抑制递归并上报致命候选 |

- `ApplicationLog` 在 Host 创建前使用可重置的早期 Logger：通过同一个 Console Adapter 保留 Editor `OnValidate` 反馈，并把条目存入小型固定缓冲；`ApplicationHost` 安装正式 Logger 后一次性转移。Shutdown 后恢复到早期状态，不保留上一轮测试或 Domain Reload 的订阅。
- `LogEntry` 固定包含 sequence、UTC 时间、level、category、eventId、scope、technical message、exception 和有限结构化字段。允许的上下文键包括 Application state、Flow state、Session generation、SceneId、SaveSlotId、ContentId、InstanceId、operation 与 phase。
- 禁止记录完整存档 JSON、Binding Overrides JSON、绝对存档路径和任意大对象 `ToString()`。路径异常只记录错误类别与槽位，不记录用户目录。
- 事件 ID 使用小写点分层级与短横线动作，例如 `lifecycle.application.boot-failed`、`persistence.save.commit-failed`、`resource.asset.load-failed`；运行时代码只能引用集中目录常量，不能现场拼接事件 ID。
- 技术日志不本地化。玩家消息只保存 String Table key 与参数，原始异常链只进入 Logger。
- Editor / Development Build 默认允许 Info 进入 Console；非 Development Player 的 Console 最低为 Warning。环形缓冲保留完整等级但容量固定为 256，测试可注入其他容量。
- Sink 逐个隔离；失败 Sink 在本次写入中禁用，不能再次通过 Logger 报告自身失败。Unity Console Adapter 使用线程递归守卫，避免被全局日志回调重新摄取。

### 异常分类与致命路径

| 来源 | 处理 |
| --- | --- |
| `UniTaskScheduler.UnobservedTaskException` | 忽略正常取消；其余记录完整异常并提交未处理异步失败 |
| `Application.logMessageReceivedThreaded` | 只接收非 Logger Adapter 产生的 Error / Assert / Exception；Error 记录，Assert / Exception 作为致命候选排队回主线程 |
| `AppDomain.CurrentDomain.UnhandledException` | 记录 `IsTerminating`，作为不可恢复致命候选；不宣称已经恢复 |
| 已有领域结果 | 在事务边界记录一次；继续走既有重试、回退或返回前台，不送入全局未处理异常路径 |

- `ApplicationFailureCoordinator` first-failure-wins，保留首个致命异常、事件 ID 和阶段。重复回调只增加去重计数，不覆盖根因、不重复打开 Fatal UI。
- 工作线程回调只创建不可变报告并排队；Application 状态和 UI 只能在主线程更新。
- Host 尚未 Ready 时的致命失败转为 `ApplicationLifecycleState.Failed`；Shell 已 Ready 时转为现有 `FatalError` 表现并只保留安全退出。
- 捕获器不调用 `throw`、不吞掉 AppDomain 致命异常，也不把 Unity Console 中普通 Error 自动标记为可恢复。

### 玩家错误表现

- 保留 `SaveErrorCode`、`SceneFlowErrorCode` 和新增 `ResourceErrorCode` 作为领域事实；`PlayerErrorCatalog` 只负责映射到本地化 key、参数、严重度和允许动作。
- Shell 使用统一 `PlayerErrorPresentation`，动作限定为 Dismiss、Retry、ReturnFrontEnd 和 Quit。领域结果没有证明恢复能力时不得显示 Retry。
- Scene Flow 继续拥有状态恢复；错误表现不能自行卸载场景、停止 Session 或写存档。Retry 必须重新提交原领域请求，而不是复用已完成的 UniTask。
- 资源预热失败在进入 Running Session 前回滚并显示 Modal；Application 必需资源失败进入 Fatal。非必需图标失败记录并使用现有视觉后备，不伪装成加载成功。
- `system` / `ui` String Table 为首批错误补齐 `zh-Hans`、`en` 和 Pseudo Locale，策略测试冻结三种 Locale 的 key 与参数一致。

### Resource Service 与所有权

| 层 | 合同 |
| --- | --- |
| `IAddressableAssetBackend` | 唯一静态 `Addressables.LoadAssetAsync` / `Release` Adapter，测试可替换 |
| `AddressableAssetService` | Application 级单飞、类型校验、owner 引用计数、迟到完成回收和诊断快照 |
| `AssetOwnerScope` | Application 或 Session 有主作用域；Close 幂等并释放该 owner 的全部 Asset Lease |
| `AssetLease<T>` | 成功加载的唯一消费票据；Dispose 幂等，不能跨 owner 转让 |
| Prefab / Sprite / Audio Loader | 领域适配器，只整理预热与缓存语义，不接触 Addressables 静态 API 或原始 handle |

- 资源键由 AssetReference GUID 或稳定地址归一化，并与请求类型共同组成单飞键；同键不同类型返回 `TypeMismatch`，不能共享错误结果。
- 每个请求返回结构化 `ResourceLoadResult<T>`。取消、owner 已关闭、无效引用、加载失败和类型不符使用稳定 `ResourceErrorCode`，原异常仅进入日志和结果内部技术字段。
- 单个等待者取消只结束该等待，不破坏其他 owner 的共享加载；最后一个 owner / 等待者离开后释放后端句柄。Owner Close 与加载完成竞争时，迟到结果必须立即释放且不能进入缓存。
- Application 音频配置和 Audio Cue 使用 Application owner；Prefab 与 Sprite 预热使用当前 Session owner。Session stop、初始化回滚、Abandoned 和 Application Shutdown 都走同一个 Close 路径。
- 当前 Prefab 实例仍由 `Object.Instantiate` + `SessionObjectRegistry` 管理，不伪装成 Addressables Instance Handle；策略测试禁止业务代码新增 `Addressables.InstantiateAsync` / `ReleaseInstance`。若以后引入实例 API，必须另建独立租约合同。
- `GameplaySceneConfiguration`、Content Catalog 和 Prepared Restore 继续是依赖事实来源；新增单一 `SessionResourcePreloadPlan` 收集器，Initializer 不再各自手工扩展隐式列表。

### Addressables 分组、Label 与地址

DarkFlare 自有条目按下表迁移；`Default Local Group` 保持空默认组。Localization 自动管理组与 `unifiedraytracing` 包组不改名、不移动，只检查重复地址和无效引用。

| 分组 | 条目 | 必需 Label |
| --- | --- | --- |
| `DarkFlare-Application` | Audio Service 配置 | `df.application`、`df.configuration` |
| `DarkFlare-Session-Prefabs` | 玩家、三种怪物、投射物、掉落物 Prefab | `df.session`、`df.prefab` |
| `DarkFlare-Session-Sprites` | 七个正式物品图标 | `df.session`、`df.sprite` |
| `DarkFlare-Audio` | `ui.confirm` Clip | `df.application`、`df.audio` |

地址统一为小写 `/` 分段和 kebab-case：

| 当前地址 | 目标地址 |
| --- | --- |
| `Combat/Player` | `gameplay/prefabs/player` |
| `Combat/Monster/{Basic,Swift,Heavy}` | `gameplay/prefabs/monsters/{basic,swift,heavy}` |
| `Combat/Projectile/Default` | `gameplay/prefabs/projectiles/default` |
| `Loot/Pickup` | `gameplay/prefabs/loot/pickup` |
| `ItemIcons/<snake_case>` | `ui/icons/items/<kebab-case>` |
| `audio/ui.confirm` | `audio/ui/confirm` |
| `infrastructure/audio` | `infrastructure/audio/configuration` |

- AssetReference 继续按 GUID 解析，因此地址迁移不得改动资源 GUID；字符串加载的 Audio Service 配置常量、测试和模块文档必须同步更新。
- Editor 验证检查自有条目所属组、必需 Label、地址格式、重复地址、空 GUID、丢失资源、类型与 Label 一致性、默认组误放和内容配置中的空 AssetReference。
- 分组和条目迁移使用 Unity Editor Addressables API 执行，不直接手改 YAML；执行后保存资产并运行 Addressables Analyze / EditMode 契约测试。

## 实施进度

- [x] 切片 0：合同、债务清单与特征测试冻结
- [x] 切片 1：结构化 Logger、Sink 与 Runtime 日志迁移
- [x] 切片 2：全局异常捕获与玩家错误表现
- [x] 切片 3：Application Resource Service 与 Loader 迁移
- [x] 切片 4：Addressables 分组、Label、地址与生命周期验收
- [x] 切片 5：综合故障演练、模块文档与计划归档

## 实施切片

### 切片 0：合同、债务清单与特征测试冻结

- 新增纯日志、异常、玩家错误、资源结果与 owner / lease 合同，不接入运行行为。
- 用源码测试冻结 37 个文件 / 110 处直接日志债务和 4 个文件 / 9 处 Addressables 静态调用；只允许精确路径、`removeByStage = alpha 0.2.6` 的临时例外。
- 冻结 15 个 DarkFlare Addressables 条目的 GUID、当前地址、目标分组、目标地址和 Label 清单。
- 为既有 Prefab 单飞 / 迟到释放、Sprite 取消、Audio owner 释放和 Application Shutdown 建立迁移前特征测试。
- 成功标准：新增合同测试通过，现有全量回归不变，运行行为和 Addressables 配置零改动。

### 切片 1：结构化 Logger、Sink 与 Runtime 日志迁移

- 实现 `ApplicationLog`、`ApplicationLogger`、集中事件目录、Console Sink、256 条 Ring Buffer 和测试 Sink。
- 在 Subsystem Registration、Application Boot、Shutdown 与测试 TearDown 中建立确定的安装 / 重置顺序。
- 先迁移 Lifecycle、Flow、Persistence、Settings、UI Shell，再迁移 Gameplay、数据验证和视觉模块；去掉无价值的逐帧 / 高频成功日志，保留 Debug 级诊断事件。
- 启用 `runtime-direct-debug-log` 策略，只允许 `UnityConsoleLogSink`；生成输入类中的 `Debug.Assert` 不属于该规则。
- 为稳定事件 ID、上下文字段白名单、环形覆盖顺序、Sink 隔离、递归保护、早期日志转移和敏感字段拒绝建立 EditMode 测试。
- 成功标准：Runtime 零未登记直接 `Debug.Log*`，事件 ID 零重复 / 零非法，完整回归通过且正常 Bootstrap Console Error 为 0。

### 切片 2：全局异常捕获与玩家错误表现

- 实现 `ApplicationExceptionMonitor` 与 `ApplicationFailureCoordinator`，接入 Unity、AppDomain 和 UniTask 三类异常源。
- 将 `LifecycleTaskFailure`、启动失败、Scene Flow 失败和关闭失败在事务边界记录一次，消除重复异常输出。
- 建立 `PlayerErrorCatalog` / `PlayerErrorPresentation`，接入 FrontEnd、Busy、Modal、Toast、Fatal 现有 Shell，不创建第二套 UI 外壳。
- 为存档、场景和首批资源错误增加三语言条目；故障注入验证 Retry、ReturnFrontEnd、Dismiss 与 Quit 的可达性和禁止组合。
- 成功标准：全局订阅 / 退订 exactly-once，递归日志只产生一条根事件，首个 Fatal 不被覆盖，可恢复失败不污染 Application Fatal 状态。

### 切片 3：Application Resource Service 与 Loader 迁移

- 实现 Addressables 唯一后端、Application 级 Service、owner scope、asset lease、结构化结果和诊断快照。
- `ApplicationHost` 通过 Application owner 加载 Audio 配置；`AudioService`、`PrefabAssetLoader`、`SpriteAssetLoader` 改为 Service Adapter。
- 新建 `SessionResourcePreloadPlan`，统一 NewGame / Restore 对 Prefab 与 Sprite 的依赖收集；保留 Restore 全刷怪池预热回归。
- 启用 `runtime-addressables-static-api` 策略，只允许唯一后端；禁止业务 `InstantiateAsync` / `ReleaseInstance`。
- 覆盖单飞、双 owner、取消、失败、类型冲突、owner 先关闭、迟到完成、重复 Dispose、Session 回滚与 Shutdown 释放。
- 成功标准：Runtime 只有一个 Addressables 静态入口，原三类 Loader 行为保持，所有故障路径后 active handle / lease 计数归零。

### 切片 4：Addressables 分组、Label、地址与生命周期验收

- 通过 Unity Editor API 创建四个 DarkFlare 分组，迁移 15 个条目并清空默认组。
- 更新 Audio 配置字符串地址、资产测试与相关模块文档；所有 AssetReference GUID 保持不变。
- 新增 Editor 验证器和 EditMode 测试，冻结组、Label、地址、类型、重复、缺失、默认组误放和包 / Localization 例外。
- 三轮执行 FrontEnd → NewGame / Continue → Main → FrontEnd，记录 Application / Session owner、asset lease、后端 handle、SessionObjectRegistry 和常驻对象快照。
- 注入 Prefab、Sprite、Audio 配置 / Cue 缺失与取消，验证回滚、视觉后备、玩家提示和零迟到泄漏。
- 成功标准：Addressables 验证零问题；每轮返回 FrontEnd 后 Session 资源归零，Application 资源回到固定基线；Shutdown 后全部归零。

### 切片 5：综合故障演练、模块文档与计划归档

- 运行读档损坏、场景加载失败、资源加载失败、未观察 UniTask 异常、重叠挂起 / 退出和 Session Abandoned 故障矩阵。
- 全量运行 Unity 脚本校验、EditMode、项目 PlayMode 和完整 PlayMode；记录既有 Input System Ignore，不新增跳过项。
- 真实从 Bootstrap 启动，完成 NewGame、保存、返回、Continue、退出，确认玩家错误 UI、日志事件与资源快照符合合同，最终 Console Error 为 0。
- 新增 `Docs/docs/infrastructure/logging-error-addressables-governance.md`，同步项目概览、目录结构、玩法循环和相关模块文档。
- 完成后把本计划移入 `Docs/docs/plan/archive/`，更新归档索引与总计划；`alpha 0.2.7` 只登记为下一阶段，不提前执行。
- 成功标准：全部完成定义满足且工作区仅包含本阶段预期改动。

## 测试矩阵

| 层级 | 必测内容 |
| --- | --- |
| 纯 EditMode | 日志合同、事件目录、Ring Buffer、Sink 故障、异常去重、错误映射、Resource Service 单飞 / owner / lease / 取消 / 迟到释放 |
| Editor 资产测试 | Addressables 分组、Label、地址、GUID、资源类型、重复 / 缺失引用、Localization / 包组例外 |
| 源码策略 | 直接 `Debug.Log*`、Addressables 静态 API、实例 API、事件 ID 拼接、现有 IO / Scene / Input / Audio / Platform 规则回归 |
| PlayMode | Host 唯一 Logger / Monitor / Resource Service、Shell 错误动作、NewGame / Continue、Session 回滚、三轮场景往返、Shutdown |
| 故障注入 | Unity / UniTask 异常递归、存档损坏、场景失败、Prefab / Sprite / Audio 失败、取消、owner 提前关闭、迟到完成 |
| 人工验收 | Bootstrap → NewGame → 保存 → FrontEnd → Continue → Quit；日志可定位、提示可操作、Console Error 0 |

## 风险与控制

| 风险 | 控制 |
| --- | --- |
| Logger 写 Console 后被全局回调再次摄取 | Console Sink 线程递归守卫 + 来源标记 + first-failure 去重测试 |
| 测试故障日志被误判 Fatal | 可注入异常源与 Sink；测试明确安装 / 卸载 Monitor，不依赖全局残留状态 |
| 110 处日志机械替换后事件语义失真 | 按事务边界归并事件；集中事件目录；删除高频噪声，不把显示字符串当上下文 |
| 共享加载中单个取消释放其他 owner 资源 | 请求等待与后端操作分离；最后 owner / waiter 才释放；双 owner 竞争测试 |
| Session 回滚和迟到完成跨代污染 | owner generation + lease 身份 + late completion 立即释放；沿用现有 lifecycle generation 语义 |
| 分组 / 地址迁移破坏音频配置字符串加载 | 先冻结 GUID / 地址清单，通过 Editor API 迁移，同一切片更新常量并运行真实加载测试 |
| Localization 或 Unity 包组被自有规则误改 | 验证器按受管组分类；只管理 `DarkFlare-*`，其他组只做只读通用校验 |
| 日志泄露用户路径或大对象造成内存压力 | 上下文键白名单、值长度上限、禁止绝对路径、固定 256 条 Ring Buffer |

## 预计文件边界

- 新增 `Assets/Scripts/Runtime/Infrastructure/Diagnostics/`：日志合同、事件目录、Logger、Sink、异常监控、失败协调和玩家错误映射。
- 新增 `Assets/Scripts/Runtime/Infrastructure/Resources/`：资源合同、Addressables Backend、Service、owner / lease、预热计划与诊断快照。
- 修改 `ApplicationBootstrap`、`ApplicationHost`、生命周期任务回调和 Application Shell 控制器，接入创建、表现与逆序释放。
- 修改 `PrefabAssetLoader`、`SpriteAssetLoader`、Audio Loader / Service，以及 NewGame / Restore Initializer，迁移到统一资源服务。
- 精准迁移当前 37 个 Runtime 日志债务文件；不顺带重构玩法或 UI。
- 修改 `Assets/AddressableAssetsData/` 中 DarkFlare 自有分组、Schema、Label 与条目；Localization 和包组不做结构迁移。
- 新增 / 修改 EditMode、PlayMode、策略和资产测试；临时策略例外在阶段完成前清零。
- 完成阶段时新增基础设施模块文档并同步 `Docs/docs/README.md`、总计划、项目概览、目录结构和归档索引。

## 完成定义

- Runtime 除 `UnityConsoleLogSink` 外零直接 `Debug.Log*`；除唯一 Addressables Backend 外零静态 Addressables API。
- 所有生产日志使用合法、唯一、稳定事件 ID；关键存档 / 场景 / 资源失败可由事件 ID、状态、槽位 / Session 和原始异常链定位。
- Unity、AppDomain、UniTask 异常钩子 exactly-once，递归受控，首个致命原因不可被覆盖或标记为已恢复。
- 存档、场景和资源错误均有三语言、可操作且不越权的玩家反馈；重试与返回前台只在领域合同允许时出现。
- DarkFlare 15 个 Addressables 条目全部位于目标组、目标地址和必需 Label；默认组无自有条目，验证器零问题。
- 三轮 NewGame / Continue 场景往返后 Session asset lease、handle、实例和常驻对象回到同一基线；Application Shutdown 后全部归零。
- 加载失败、取消、owner 先关闭、迟到完成、Session 回滚和对象提前销毁均无悬挂句柄或半初始化状态。
- Unity 脚本编译 0 error；全量 EditMode、项目 PlayMode、完整 PlayMode 零失败，除既有 2 个 Input System Ignore 外不新增跳过。
- 模块文档完成，总计划和索引同步，本计划归档；`alpha 0.2.7` 尚未开始。
