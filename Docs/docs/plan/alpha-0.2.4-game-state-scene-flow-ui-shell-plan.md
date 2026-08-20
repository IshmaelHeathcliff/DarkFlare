# alpha 0.2.4 游戏状态、场景流与 UI 外壳执行计划

> 状态：规划完成，待实施
> 建立日期：2026-08-20
> 规划基线：`16d67c6`（`alpha 0.2.3` 完成提交）
> 实施前置：`alpha 0.2.3` 已独立提交；后续实现不得回写或混入上一阶段
> 上位计划：[alpha 0.2 基础设施开发计划](./alpha-0.2-plan.md)
> 强制契约：[alpha 0.2 基础设施约束契约](./alpha-0.2-infrastructure-contract.md)
> 前置模块：[应用生命周期与会话作用域](../infrastructure/application-lifecycle.md)、[本地存档与 Session 恢复](../infrastructure/local-save.md)、[用户设置与本地化](../infrastructure/user-settings-localization.md)、[输入与运行时 UI](../input-ui-system.md)

## 阶段结论

本阶段在现有 `ApplicationHost`、latest-wins Session 初始化协调器、Restore 事务和 Settings / Localization 启动门禁之上，增加唯一的游戏流状态机、真实场景加载服务和应用级 UI 外壳。现有生命周期底座继续负责 Session 创建、初始化、回滚、停止和 generation 隔离；新的 Scene Flow 负责“加载哪个场景、何时激活、使用何种 Session initializer、失败后回到哪里”。两者不得合并为新的巨型宿主，也不得出现两个可以直接调用 `SceneManager` 的入口。

本阶段采用最小双场景拓扑：常驻 `Bootstrap.unity` 提供 FrontEnd、Loading、Modal、Toast、Busy Overlay 和唯一 EventSystem；`Main.unity` 作为 additive 玩法场景加载与卸载。新游戏、继续游戏、返回前台和加载失败重试全部经过同一 `SceneFlowService`。完整多地图、选角、Profile 管理、手动槽位、云同步、输入重绑定、全局异常捕获和 Addressables 场景治理不在本阶段扩张。

## 已确认基线

- Unity 实时状态为 `6000.4.3f1`，当前只加载 `Assets/Scenes/Main.unity`；Build Settings 也只有 `Main`，build index 为 0。
- `Main` 有 12 个根对象；`UIRoot` 同时承载 HUD、背包、商店、打造、存档和语言 UI，场景内还有唯一 `EventSystem`。
- `ApplicationBootstrap` 在 `BeforeSceneLoad` 创建唯一 `ApplicationHost`；Application Host 已拥有 Application / Profile / Session / Scene 作用域、Settings、Localization、Content Catalog、Save Coordinator 和退出 Flush。
- `ApplicationHost.BeginSceneSessionInitialization` 只接受“已加载场景”，并以 active + latest pending 请求完成 exactly-once、latest-wins Session 初始化；它不加载、激活或卸载场景。
- `CombatPrototypeBootstrap.Start` 仍安装内容目录并自动为当前 `Main` 提交 `NewGameSessionInitializer`；这使冷启动必然直接进入游戏，无法停留 FrontEnd。
- `SessionSaveFacade` 当前同时负责 Save、Continue 和 NewGame；后两者只能在已有 Running Session 中工作，FrontEnd 没有可用入口。
- Runtime 没有业务 `SceneManager.Load*` / `Unload*` 调用，现有策略测试已经冻结这一基线；`GameSessionHost` 只订阅 `sceneUnloaded` 处理外部卸载。
- `GameplayPauseSystem` 是 Runtime 中唯一直接写 `Time.timeScale` 的项目模块，共有暂停、恢复和 Deinit 恢复三个写入点。
- 当前只有技术态 `ApplicationLifecycleState` 和 `GameSessionState`，没有独立 `Boot / FrontEnd / Loading / InGame / Paused / Recovering / FatalError` 游戏流状态。
- 当前 UI 没有通用 Page、Modal、Toast、Busy Overlay 或焦点栈；`GameMenuController` 自行维护 Save / Language busy 标记，并把 Input Mode 变化直接映射为玩法暂停。
- `alpha 0.2.3` 完成基线为 EditMode `334/334`、项目 PlayMode `48/48`、完整 PlayMode 52 项中 50 项通过、0 失败、2 项因 Input System 上游 issue 1252825 跳过。
- 规划开始时工作区含 186 个 `alpha 0.2.3` 变更；现已由 `16d67c6` 独立提交，0.2.4 具备清晰实施边界。

## 目标

- 建立与 `ApplicationLifecycleState` 分离的唯一 `GameFlowState`，冻结合法转换、稳定态和事务态。
- 新增常驻 Bootstrap / FrontEnd 场景，并让 Main 成为可加载、可卸载、可失败恢复的 additive 玩法场景。
- 让新游戏、继续游戏、返回前台、重试和取消都只经过 `SceneFlowService`。
- 将内容目录安装和 Continue 预检前移到 Bootstrap，使没有 Running Session 时也能安全显示 FrontEnd 和准备 Restore。
- 建立 Application UI Shell：FrontEnd Page、Loading / Busy、Modal、Toast、FatalError Page 与焦点栈。
- 建立唯一 `GameTimeService` 写入 `Time.timeScale`；保留 QFramework Command / Event 调用方式，但玩法 System 只作适配。
- 让所有阶段、错误和恢复动作使用结构化结果与本地化消息，不把异常、场景路径或伪进度暴露给玩家。
- 用策略测试和 PlayMode 故障演练冻结单场景实例、唯一 EventSystem、latest-wins、焦点恢复和退出顺序。

## 非目标

- 不新增第二张玩法地图，不建立关卡选择、快速旅行或开放世界流送。
- 不实现 Profile 选择、手动存档槽位、删除存档、云同步或跨设备冲突处理。
- 不实现输入重绑定、设备 Glyph、AudioMixer、可访问性消费者或平台挂起恢复；这些属于 `alpha 0.2.5`。
- 不建立全局 Logger、全局异常捕获、遥测或完整 Addressables 分组 / 句柄治理；这些属于 `alpha 0.2.6`。
- 不重写 `ApplicationHost`、`GameSessionHost`、Save DTO、Settings Schema 或当前玩法初始化器。
- 不把 Main 世界、HUD 或物品工作台搬入 Bootstrap，也不新增无真实消费者的抽象 UI 框架。
- 不读取或修改 `Docs/design/`。

## 冻结架构

### 游戏流状态与转换

`GameFlowState` 与 `ApplicationLifecycleState`、`GameSessionState` 分开。Application 生命周期表达进程服务是否可用，Session 状态表达单次玩法实例是否已提交，Game Flow 只表达玩家所处的应用路径。

| 状态 | 场景 / Session 合同 | 可接受的玩家动作 |
| --- | --- | --- |
| `Boot` | Bootstrap 已加载；等待 Application Ready、UI Shell 注册和内容目录安装；无 Session | 无，显示启动 Busy |
| `FrontEnd` | 仅 Bootstrap；无 Main、无 Session | 新游戏、继续、语言、退出 |
| `Loading` | Scene Flow 事务独占；Busy Overlay 阻止底层输入 | 仅在阶段允许时取消 |
| `InGame` | Bootstrap + active Main；Session 为 Running 且 lease 有效 | 玩法输入、打开暂停 / 功能菜单 |
| `Paused` | 场景和 Session 保持；至少一个 pause lease 生效 | 关闭菜单、保存、返回前台 |
| `Recovering` | 正在回滚 Session、卸载残留场景或恢复上一个稳定态 | 无，显示恢复 Busy |
| `FatalError` | 无法安全回到 FrontEnd / InGame；拒绝新流请求 | 重试安全启动或退出（仅在动作可用时） |

冻结转换：

```text
Boot → FrontEnd
FrontEnd → Loading → InGame
InGame ⇄ Paused
InGame / Paused → Loading → FrontEnd
Loading → Recovering → FrontEnd / InGame / Paused
Boot / Loading / Recovering → FatalError
```

- `Loading`、`Recovering` 是事务态；`FrontEnd`、`InGame`、`Paused` 是可交互稳定态。
- 非法转换返回结构化 `InvalidState`，不静默修正，也不直接改写多个状态字段。
- 应用关闭继续由 `ApplicationLifecycleState.ShuttingDown` 表达，不新增重复的 `Quitting` 游戏流态。

### 场景拓扑与注册

- 新建 `Assets/Scenes/Bootstrap.unity` 并放到 Build Settings index 0；`Main.unity` 改为 index 1。
- Bootstrap 在应用运行期保持加载，包含 Application UI Shell、唯一 `EventSystem` 和 `InputSystemUIInputModule`，不包含玩法架构、玩家、怪物或世界相机。
- Main 只作为 additive gameplay scene 加载。进入游戏后将 Main 设为 active scene；返回前台前先恢复 Bootstrap 为 active，再卸载 Main。
- 从 Main 移除 EventSystem，任意稳定态和事务态都必须恰好存在一个 EventSystem。
- 新建 `SceneId` 与 `SceneFlowConfiguration`，唯一映射 `SceneId → build scene path`。场景名 / 路径不得散落在 Controller、Command 或测试正文。
- `SceneFlowConfiguration` 放在 `Assets/Settings/Scenes/`，使用 ScriptableObject / Odin 中文字段；Editor 验证器检查唯一 ID、非空路径、Build Settings 登记、Bootstrap index 0 和 Main index 1。
- `CombatPrototypeBootstrap` 保留现有类名和资产 GUID，避免无收益的场景序列化迁移；移除自动初始化的 `Start` 职责，改为 Main 场景内唯一的 `GameplaySceneConfiguration` 提供者。
- Scene Flow 只在目标 Scene 的根对象中寻找配置提供者；禁止全局 `FindAnyObjectByType` 把旧场景或测试场景对象误当目标。

### 所有权与唯一入口

| 所有者 | 责任 | 禁止事项 |
| --- | --- | --- |
| `ApplicationHost` | 构造并持有 Scene Flow、Time Service、Settings、Localization、Save Coordinator；继续持有 Session 底层事务 | 不直接操作 VisualElement，不重新实现场景事务 |
| `SceneFlowService` | GameFlowState、请求仲裁、场景事务、Session initializer 选择、恢复与进度 | 不直接读写玩法 Model，不直接拼玩家文本 |
| `UnitySceneLoader` | Runtime 唯一 `SceneManager.LoadSceneAsync` / `UnloadSceneAsync` / `SetActiveScene` 调用点 | 不决定 GameFlowState，不创建 Session |
| `ApplicationShellController` | Bootstrap UIDocument、Page / Modal / Toast / Busy 表现、焦点栈 | 不加载场景、不访问 Save 文件、不依赖 GameArchitecture |
| `ApplicationFrontEndController` | 新游戏、继续、语言和退出的玩家入口 | 不调用 SceneManager、Storage 或 Session initializer |
| `GameTimeService` | Runtime 唯一 `Time.timeScale` 写入与 pause lease | 不依赖场景 Controller 或 QFramework Model |
| `GameplayPauseSystem` | QFramework Command / Event 到 GameTimeService 的适配 | 不再直接写 `Time.timeScale` |

- `ApplicationHost.BeginSceneSessionInitialization` 保持为“已加载场景的 Session 底层入口”，由 Scene Flow 调用；业务 UI 不再直接调用。
- `SessionSaveFacade` 保留当前 Session 的 Save 能力；Continue / NewGame 转移到 Scene Flow，不能继续存在第二条跨 Session 入口。
- Application 级 FrontEnd Controller 不实现 `IController`，因为 FrontEnd 没有 GameArchitecture；Main 场景 Controller 继续遵守 Query / Command / Event 规则。

### Bootstrap、内容目录与 FrontEnd

- Bootstrap 场景中的 `ApplicationShellBootstrap` 序列化引用 `SceneFlowConfiguration`、正式 `ContentCatalogDefinition`、Application Shell UXML 和 Panel Settings。
- Application Ready 后先安装并验证正式 Content Catalog，再注册 UI Shell 与 Scene Flow；三者全部成功才从 Boot 提交 FrontEnd。
- 内容目录不再由 Main 的 `CombatPrototypeBootstrap.Start` 首次安装。Main 提供者仍引用同一目录，验证其与 Application Host 已安装目录一致。
- FrontEnd 至少提供：新游戏、继续、语言、退出。Continue 只有 Profile 级 `SaveCoordinator.PrepareContinueAsync(auto)` 预检成功时启用。
- 当前 Game Menu 的 Continue / NewGame / Language 迁移到 FrontEnd；游戏内菜单保留 Save、关闭和“返回前台”。不同时维护两套同义入口。
- 新游戏不预先删除或覆盖 `auto`；沿用 `alpha 0.2.2` 的旧档保护语义。

### 新游戏与继续游戏事务

Scene Flow 请求使用明确的 `GameStartIntent.NewGame` 或 `GameStartIntent.Continue`，结果使用 `SceneFlowResult`；不得用 bool 或异常猜测阶段。

```text
验证请求与稳定态
→ 进入 Loading，显示真实阶段并阻止底层输入
→ 确认 Application / Content Catalog / UI Shell Ready
→ Continue 时在 Profile Scope 完成 auto 预检与 PreparedRestore
→ additive 加载 Main
→ 验证目标 Scene 和唯一 Gameplay 配置提供者
→ 设 Main 为 active scene
→ 创建 NewGameSessionInitializer 或 RestoreGameSessionInitializer
→ 调用 ApplicationHost 的 Session 初始化协调器并等待 exactly-once 结果
→ 验证 Running + lease + 同场景绑定
→ 隐藏 Busy，提交 InGame
```

- Session 初始化失败时，先沿用现有 initializer 回滚，再卸载未提交的 Main，最后回到 FrontEnd。
- Continue 预检失败发生在 Main 加载前；保留 FrontEnd 并打开可恢复 Modal，不停止任何有效 Session。
- Main 加载成功但配置缺失、重复或无效属于加载事务失败，不允许以默认配置继续。

### 返回前台与场景退出

```text
Paused 中确认返回
→ 进入 Loading 并阻止新请求
→ 当前 Session Dirty 时请求 auto 保存
→ 保存失败：进入 Recovering，提供重试或取消返回
→ 停止 Session，等待 Scene / Component / Addressables 任务收敛
→ 将 Bootstrap 设为 active scene
→ 卸载 Main
→ 清除所有 pause lease 和 Session UI 状态
→ 提交 FrontEnd 并恢复焦点
```

- 不提供静默丢弃失败保存的“强制离开”；本阶段只允许重试或取消返回。
- Session 停止或 Scene 卸载出现 Abandoned 时不得创建下一代 Session，转入 FatalError 并保留诊断。
- 桌面退出仍走现有 `Application.wantsToQuit → Flush → Stop Session → Stop Profile / Application` 顺序；FrontEnd 退出只请求宿主，不直接调用 `Application.Quit`。

### latest-wins、取消与 Unity AsyncOperation

- Scene Flow 使用一个 active request + 一个 latest pending request；新请求替代旧 pending，并取消 active 的可取消步骤。
- 每个请求恰好完成一次。被替代请求返回 `Cancelled`，最新请求最终执行或返回自己的失败结果。
- Unity `AsyncOperation` 本身不视为可硬取消。取消发生在加载开始后时，服务记录 superseded，等待操作进入可清理状态，禁止创建 Session，随后卸载残留 Scene，再执行最新请求。
- `allowSceneActivation` 只用于将“加载完成”和“允许激活”分开；取消后仍必须完成 Unity 要求的激活 / 清理步骤，不能把停在未激活状态的操作永久悬挂。
- 取消按钮只在 Prepare、Load 和可补偿阶段启用；Session commit、最终保存和 Scene unload 关键区间显示不可取消 Busy。
- 超时必须指明 phase。超时后进入 Recovering，执行与取消相同的补偿清理。

### Loading、进度与错误模型

`SceneFlowPhase` 至少包含：`Preparing`、`PreparingSave`、`LoadingScene`、`ActivatingScene`、`StartingSession`、`SavingBeforeExit`、`StoppingSession`、`UnloadingScene`、`Recovering`、`Completed`。

- UI 显示当前真实阶段、目标和取消能力；不把多个不同权重步骤伪装成一个总百分比。
- 只有底层提供可测量值时，`SceneFlowProgress` 才携带 `Progress01`；否则显示不确定进度。
- `SceneFlowResult` 至少包含 operation、code、from / target state、phase、SceneId、是否可重试、玩家 `LocalizedMessage` 和内部 Exception。
- 稳定错误码至少覆盖：InvalidState、Cancelled、Superseded、ConfigurationInvalid、SceneNotInBuild、LoadFailed、ActivationFailed、EntryMissing、EntryDuplicate、SavePrepareFailed、SaveBeforeExitFailed、SessionStartFailed、SessionStopFailed、UnloadFailed、Timeout、ApplicationUnavailable、FatalCleanupFailed。
- Recovering 只负责回滚和恢复稳定态；可恢复错误回到原稳定态后打开 Modal。只有无法证明场景 / Session / lease 唯一性时进入 FatalError。
- 不建立 0.2.6 的全局异常捕获；本阶段只捕获并结构化 Scene Flow 自己拥有的事务异常。

### Application UI Shell 与焦点

- 新建 `ApplicationShell.uxml/.uss`，使用现有 Theme、字体与 Localization，不生成新美术资源。
- Application Shell UIDocument 使用高于 Main UIRoot 的 sorting order；Busy / Modal 能覆盖玩法 UI，Toast 位于可见层但不阻止操作。
- 层级固定为：Page → Toast → Modal → Busy / Fatal。Page 只显示一个主页面；Modal 和 Busy 阻止底层 picking。
- FrontEnd 是本阶段唯一新增 Page 真实消费者；加载 / 退出确认使用 Modal，场景事务使用 Busy，保存成功或非阻塞提示使用 Toast。
- 打开 Modal 前记录当前有效 `focusedElement`；打开后聚焦显式默认按钮；关闭后优先恢复原元素，否则恢复当前 Page 默认焦点。
- Modal 打开时禁用底层 Page / Game UI，Cancel 只关闭允许取消的顶层 Modal；Toast 不聚焦、不改变焦点栈。
- Busy Overlay 打开时阻止键鼠、指针和手柄提交；有取消动作时只聚焦取消按钮，否则不暴露可交互子项。
- Bootstrap 持有唯一 EventSystem。FrontEnd 直接使用其 `InputSystemUIInputModule` 与 UI Action 引用；本阶段不另建重绑定或 Glyph 服务。
- 新 Session 的 `GameInput` 继续默认进入 Gameplay；游戏内菜单打开后使用 UI mode。返回 FrontEnd 后 Session Input 被释放，焦点交回 Application Shell。

### 时间与暂停

- 新建 Application 级 `GameTimeService`，成为 Runtime 唯一 `Time.timeScale` 写入点。
- 服务使用有所有者的 pause lease；第一个 lease 将 time scale 置 0，最后一个 lease 释放后恢复受服务保存的基础倍率。重复释放幂等，Application Shutdown 强制恢复。
- `GameplayPauseSystem` 保留 `SetGameplayPausedCommand` 和 `GameplayPauseChangedEvent` 兼容面，但内部只申请 / 释放 Menu pause lease。
- `GameFlowState.Paused` 由菜单 pause lease 与 InGame 稳定态共同决定；关闭菜单回到 InGame，不创建或销毁 Session。
- Loading、保存、Localization、UI 动画和取消等待使用 unscaled time / realtime，不得因 time scale 为 0 停止。
- 新增策略规则：除 `GameTimeService` 和测试 Adapter 外，Runtime 不得写 `Time.timeScale`。

## 迁移清单

| 当前入口 | 0.2.4 目标 |
| --- | --- |
| `Main` 是唯一 build scene | `Bootstrap` index 0 常驻，`Main` index 1 additive |
| Main 场景 `EventSystem` | 移到 Bootstrap，跨状态保持唯一 |
| `CombatPrototypeBootstrap.Start` 自动 NewGame | 只提供 Gameplay 配置，由 Scene Flow 选择 initializer |
| Main 首次安装 Content Catalog | Bootstrap 在 FrontEnd 前安装并验证 |
| `SessionSaveFacade.ContinueAutoAsync` / `StartNewGameAsync` | Scene Flow 的 Profile 级 FrontEnd 请求 |
| Game Menu 的 Continue / NewGame / Language | FrontEnd；Game Menu 改为 Save / Return FrontEnd / Close |
| `GameplayPauseSystem` 写 `Time.timeScale` | 适配 Application `GameTimeService` |
| Game Menu 私有 busy 文本与按钮禁用 | Save 局部状态保留；跨场景 Busy / Modal / Toast 进入 Application Shell |
| 测试直接 `SceneManager.LoadSceneAsync("Main")` | 共用 `SceneFlowPlayModeFixture`，仅底层 loader / 场景故障测试允许直接 API |

## 实施切片

### 切片 0：特征测试与合同冻结

- 为当前 ApplicationHost Session 协调、SessionSaveFacade、GameMenu、暂停和 Main 直接启动补齐必要特征测试。
- 冻结 `GameFlowState`、`SceneId`、请求、phase、progress、result、error code 和恢复动作合同。
- 冻结 Bootstrap / Main Build Settings、唯一 EventSystem、Content Catalog 所有权和测试 API 例外。
- 记录 0.2.3 提交作为实施基线；实施开始时工作区必须只包含 0.2.4 变更。

验收：合同可在不加载场景的 EditMode 中验证；没有未决定的场景所有权或 Continue 数据来源。

### 切片 1：Scene Flow、Scene Loader 与 Time Service 纯底座

- 建立 `SceneFlowService` 状态转换、latest-wins 请求协调、结构化结果和进度事件。
- 建立 `ISceneLoader` 测试接口与 `UnitySceneLoader` 唯一 Runtime 实现。
- 建立 `GameTimeService` pause lease，并把 `GameplayPauseSystem` 改为适配层。
- 扩展策略测试，允许 Scene API 只出现在 UnitySceneLoader，允许 timeScale 写入只出现在 GameTimeService。

验收：Fake loader 覆盖成功、取消、替代、超时、异常、补偿失败和 exactly-once；Runtime 唯一 API 扫描通过。

### 切片 2：Bootstrap 场景与 Application UI Shell

- 使用 unityMCP 新建并验证 `Bootstrap.unity`，创建 ApplicationShell UIDocument、唯一 EventSystem 和 Shell Bootstrap。
- 创建 `SceneFlowConfiguration.asset`，更新 Build Settings 为 Bootstrap 0 / Main 1。
- 从 Main 移除 EventSystem；Main 其余世界、UI 和序列化配置保持不变。
- 建立 FrontEnd、Loading / Busy、Modal、Toast、Fatal 层及焦点栈。
- 接入中英 / Pseudo 表与当前字体链，补齐三分辨率布局测试。

验收：冷启动只显示 FrontEnd；ApplicationHost、Bootstrap、UIDocument、EventSystem 各一个，无 Session、玩家或 Main。

### 切片 3：新游戏、继续游戏与 Main additive 加载

- 将 Content Catalog 安装迁移到 Bootstrap；Main 配置提供者移除自动 Start。
- FrontEnd 接入 NewGame / Continue / Language / Quit，Continue 使用 Profile SaveCoordinator 预检。
- Scene Flow 完成 Main load → activate → 配置验证 → Session initializer → InGame 事务。
- `SessionSaveFacade` 收缩为当前 Session Save；删除或改造旧跨 Session 调用点。
- 迁移现有 PlayMode fixture，使玩法测试从 Bootstrap 经 Scene Flow 进入 Main。

验收：新游戏和 Continue 都从无 Session 的 FrontEnd 成功进入唯一 Running Session；Restore 不重复发放新游戏资源。

### 切片 4：暂停、返回前台与场景退出

- Game Menu 迁移为 Save / Return FrontEnd / Close，返回使用确认 Modal。
- 接入保存前置、Session 停止、Bootstrap 激活、Main unload 和 FrontEnd 焦点恢复。
- 验证保存失败重试 / 取消返回、Session stop 失败、unload 失败和 Abandoned 终态。
- 验证 InGame ⇄ Paused 不重建 Session，unscaled UI / Save 在暂停中继续。

验收：连续三次 FrontEnd → InGame → Paused → FrontEnd 不累积场景、玩家、架构、输入、EventSystem、订阅或句柄。

### 切片 5：恢复、故障演练与 UI 栈收口

- 完成 Recovering / FatalError 状态和玩家恢复动作。
- 覆盖取消加载、快速 NewGame / Continue、配置缺失、Session 初始化失败、回滚失败和残留场景清理。
- 验证 Modal 阻断、Toast 不抢焦点、Busy 全局阻断及键鼠 / 手柄焦点恢复。
- 把 Save / Localization 等适合的非阻塞结果接入 Toast；不把领域失败强制升级为 Modal。

验收：所有可注入故障都回到可证明唯一性的稳定态，或明确进入 FatalError；没有半加载 Main 或半提交 Session。

### 切片 6：策略、全量回归与文档归档

- 更新项目概览、目录结构、生命周期、存档、输入 UI 和新增 Game Flow 模块文档。
- 配置验证覆盖 Build Settings、Scene Flow 配置、唯一 Scene Entry / EventSystem 和本地化 Key。
- 运行全量编译、EditMode、项目 PlayMode、完整 PlayMode、三语言 / 三分辨率 UI 和真实 Play / Stop 回归。
- 恢复 `ProjectSettings/EditorSettings.asset` 的 `EnterPlayModeOptions = 0`，确认 Console Error 为 0、`git diff --check` 通过。
- 完成后归档本计划并将上位计划推进到 `alpha 0.2.5`。

验收：完成定义全部满足；既有两项 Input System 跳过只允许保留同一上游 issue 1252825，不得新增跳过。

## 测试矩阵

| 层级 | 必须覆盖 |
| --- | --- |
| EditMode：状态 | 全部合法 / 非法转换、稳定态、事务态、Recovering / Fatal 判定 |
| EditMode：协调 | latest-wins、pending 替代、active 取消、exactly-once、超时与补偿顺序 |
| EditMode：Scene Loader | 路径解析、Build Settings 校验、load / activate / unload 失败与残留清理 |
| EditMode：时间 | 多 pause lease、重复释放、基础倍率恢复、Shutdown 恢复、唯一 timeScale 写入扫描 |
| EditMode：UI 状态 | Page 唯一、Modal 栈、Busy 优先级、Toast 队列、焦点保存 / 后备恢复 |
| PlayMode：冷启动 | Bootstrap / Shell / EventSystem / Host 唯一；FrontEnd 无 Main、Session、玩家 |
| PlayMode：新游戏 | FrontEnd → Loading → Main active → Running Session → InGame |
| PlayMode：继续 | FrontEnd 预检 → Restore → 状态 / 随机 / 实例恢复且不重复初始发放 |
| PlayMode：取消与并发 | 快速 NewGame / Continue / Cancel，只有最新请求提交，无残留 Main |
| PlayMode：返回前台 | 保存 → Stop Session → unload Main → FrontEnd；失败可重试或取消 |
| PlayMode：暂停 | InGame ⇄ Paused 保留 Session；玩法停止，unscaled UI / Save 继续 |
| PlayMode：UI | 键鼠 / 手柄 Page、Modal、Toast、Busy；焦点与底层输入阻断 |
| PlayMode：布局 | `zh-Hans` / `en` / `qps-ploc` × 1280×720 / 1920×1080 / 2560×1440 |
| 回归 | 存档、Settings、Localization、背包、装备、商店、打造、退出 Flush、连续 Session |

## 风险与控制

- **Unity 场景加载不可硬取消**：使用 superseded 标记和补偿卸载；不得把取消等价为 AsyncOperation 已停止。
- **Bootstrap 与 Main 各自携带 EventSystem**：在切片 2 物理迁移并用场景资产测试锁定唯一性。
- **Main Bootstrap 自动创建 Session**：保留类 / GUID，只移除 Start 副作用；Scene Flow 在 Scene 局部显式取得配置。
- **FrontEnd 无 SessionSaveFacade**：Continue 预检前移到 Profile SaveCoordinator，跨 Session 操作只由 Scene Flow 编排。
- **ApplicationHost 继续膨胀**：Host 只构造 / 持有服务；Scene Flow、Scene Loader、Time 和 UI Shell 分文件且依赖单向。
- **UI Shell 与 Main GameInput 抢输入**：Bootstrap 保留唯一 EventSystem；Shell 只在 FrontEnd / Modal / Busy 可交互，InGame 普通状态不占焦点。
- **暂停导致加载 / 保存停住**：所有基础设施等待和 UI 动画使用 realtime / unscaled time，自动测试在 timeScale 0 下覆盖。
- **失败后同时存在旧新 Main**：恢复提交前后都检查 SceneId、Scene handle、Session lease 和 generation；无法证明唯一性即 FatalError。
- **伪进度误导玩家**：只报告真实 phase 和可测量子进度，不生成综合百分比。
- **0.2.4 扩张为完整前端产品**：只交付一个 FrontEnd Page 与三个通用覆盖层的真实闭环，视觉复用现有 Theme。
- **测试继续直接加载 Main 绕过正式路径**：共用 SceneFlow fixture；只有底层 loader 与故障注入测试登记精确例外。
- **0.2.3 未提交导致阶段混杂**：实施第一步必须先提交当前完成工作，再记录 0.2.4 实施基线。

## 预计文件边界

新增或重点修改范围：

- `Assets/Scenes/Bootstrap.unity`
- `Assets/Scenes/Main.unity`
- `Assets/Settings/Scenes/SceneFlowConfiguration.asset`
- `Assets/Scripts/Runtime/Infrastructure/Flow/`
- `Assets/Scripts/Runtime/Infrastructure/Time/`
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationHost.cs`
- `Assets/Scripts/Runtime/Infrastructure/Persistence/SessionSaveFacade.cs`
- `Assets/Scripts/Runtime/Gameplay/Bootstrap/CombatPrototypeBootstrap.cs`
- `Assets/Scripts/Runtime/Gameplay/Interaction/GameplayPauseSystem.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/GameMenuController.cs`
- `Assets/UI/ApplicationShell.uxml`
- `Assets/UI/ApplicationShell.uss`
- `Assets/Localization/Tables/ui_*` 与 `system_*`
- `Assets/Scripts/Tests/EditMode/` 的 Scene Flow / Time / UI Shell / 策略测试
- `Assets/Scripts/Tests/PlayMode/` 的统一 SceneFlow fixture 与跨场景回归

不应修改 Save Schema、Settings Schema、正式玩法内容数量、随机算法或 `Docs/design/`。

## 完成定义

- 冷启动稳定停在 FrontEnd，Settings / Localization / Content Catalog / UI Shell 均已 Ready，首帧无语言闪切。
- Bootstrap 与 Main 拓扑、Build Settings、唯一 EventSystem 和 SceneId 注册均由自动验证保护。
- NewGame、Continue、Cancel、Retry、Return FrontEnd 和 Quit 通过唯一 Scene Flow / Application Host 路径。
- latest-wins、超时、回滚和补偿保证每个请求完成一次，任何稳定态最多一个 Main、一个 Running Session、一个玩家和一个架构 lease。
- Page / Modal / Toast / Busy / Fatal 有真实消费者；键鼠与手柄焦点、底层输入阻断和返回焦点符合合同。
- Runtime 场景加载 / 卸载 API 只存在于 UnitySceneLoader；`Time.timeScale` 写入只存在于 GameTimeService。
- 暂停不销毁 Session，Loading / Save / Localization / UI 不受 time scale 0 阻塞。
- 可恢复错误回到 FrontEnd / InGame / Paused 并提供本地化恢复动作；不可恢复错误进入 FatalError。
- Unity 编译、全量 EditMode、项目 PlayMode、完整 PlayMode与三语言三分辨率布局零失败；只保留既有两项上游 Input System 跳过。
- 新增 Game Flow 模块文档并同步项目索引；本计划归档，上位计划推进到 `alpha 0.2.5`。
