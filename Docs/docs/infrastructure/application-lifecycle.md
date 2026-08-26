# 应用生命周期与会话作用域

> 状态：`alpha 0.2.0` 已完成；`alpha 0.2.5` 已补充 Application 输入、音频、可访问性与平台生命周期；最近更新：2026-08-24
>
> 参考：[归档计划](../plan/archive/alpha-0.2.0-application-lifecycle-plan.md) · [长期运行时契约](./alpha-0.2-runtime-contract.md)

## 核心模型

生命周期系统只回答三个问题：**谁创建、依赖怎样交付、谁负责停止**。

可以把它理解成一组串联的总闸：父作用域拥有子作用域，父作用域断电时会向下取消所有子作用域；清理则从最末端开始，逐层等待任务结束后再回收上层资源。

```mermaid
flowchart TB
  A["Application<br/>整个进程"] -->|拥有| P["Profile<br/>当前玩家档案"]
  P -->|拥有| S["Session<br/>一次游戏"]
  S -->|拥有| SC["Scene<br/>本次会话绑定的场景"]
  SC -->|拥有| C["Component<br/>单个组件的长期任务"]

  START["创建顺序"] -.-> A
  C -.-> STOP["停止顺序：Component → Scene → Session → Profile → Application"]
```

创建从上到下，停止从下到上。短生命周期可以使用长生命周期依赖；长生命周期不得保留已经结束的场景或组件引用。作用域只管理取消、任务等待和父子所有权，不是依赖注入容器。

## 各层作用域

| 作用域 | 所有者 | 主要内容 | 结束时机 |
| --- | --- | --- | --- |
| Application | `ApplicationHost` | Settings、唯一 Input / Audio / Accessibility / Platform / Localization 服务、应用状态、Scene Flow、Time Service、内容目录、存档路径 / 序列化 / Storage、根取消 | 应用退出或测试清理 |
| Profile | `ApplicationHost` | 当前档案生命周期、SaveCoordinator | 应用退出；正式档案切换尚未实现 |
| Session | `GameSessionHost` | `GameArchitecture`、玩法 Model / System、随机状态、运行时对象、generation-bound 存档 Facade | 离开当前游戏或绑定场景卸载 |
| Scene | `GameSessionHost` | 初始化事务、场景任务、场景绑定 | 场景卸载或 Session 停止 |
| Component | `ComponentLifecycle` | 单个组件的长期任务和局部取消 | 组件在禁用时主动停止、销毁 Token 取消或上级作用域停止 |

`LifecycleScope` 的状态很简单：

```mermaid
stateDiagram-v2
  [*] --> Active
  Active --> Stopping: BeginStop
  Stopping --> Stopped: 子作用域与任务全部结束
  Stopping --> Abandoned: 任一后代或任务停止超时
```

默认停止时限为 10 秒。`Abandoned` 表示旧任务可能仍在运行，**不是清理成功**；该状态会粘性传播到祖先，关闭新任务和子作用域入口，并禁止创建下一代 Session。迟到结果不得修改状态或访问新一代架构，此时应结束本次运行或重置测试环境。

## 一次完整生命周期

```mermaid
sequenceDiagram
  participant Unity
  participant Bootstrap as ApplicationBootstrap
  participant App as ApplicationHost
  participant Flow as SceneFlowService
  participant Session as GameSessionHost
  participant Provider as GameArchitectureProvider
  participant Init as IGameSessionInitializer
  participant Binding as SceneSessionBinding
  participant Component as 场景组件

  Unity->>Bootstrap: SubsystemRegistration / BeforeSceneLoad
  Bootstrap->>App: 创建唯一宿主
  App->>App: 创建 Application、Profile 作用域
  Flow->>Flow: 加载并激活 Main，选择 initializer
  Flow->>App: StartSessionAsync(scene, initializer)
  App->>Session: 创建待初始化 Session
  App->>Session: 注入 ApplicationInputService
  Session->>Provider: StartOwnedSession(SessionScope, InputService)
  Provider-->>Session: Architecture + generation lease
  App->>Session: 绑定 Scene 并开始初始化
  Session->>Init: InitializeAsync(context, token)
  Init-->>Session: 初始化完成
  Session-->>App: Running
  App-->>Binding: SessionRunning(session)
  Binding->>Component: Bind(IArchitecture)
  Flow->>App: StopCurrentSessionAsync
  Session->>Session: Component → Scene → Session 停止与回滚
  Session->>Provider: StopOwnedSession(lease)
  Provider->>Provider: Deinit 并清除当前架构
  Flow->>Flow: 激活 Bootstrap 并卸载 Main
```

`ApplicationBootstrap` 在场景脚本前创建唯一宿主。宿主同一时间只允许一个 Session；`SceneFlowService` 负责 active + latest pending 场景请求，宿主的 Session 协调器继续保证已加载场景的初始化 exactly-once。初始化成功后才广播 `SessionRunning`；正常停止必须先解绑当前存档快照入口、收敛 Scene、回滚初始化并停止 Session，最后才能销毁架构。

- Application：`None → Booting → Ready → ShuttingDown → Shutdown`；启动失败进入 `Failed`。
- Session：`Created → Initializing → Running → Stopping → None`；初始化失败或取消进入 `RollingBack`，无法安全收敛则进入 `Abandoned`。

新游戏事务为 `验证 → 预热 → 写状态 → 创建并登记对象 → 绑定场景 → 提交刷怪`；Restore 事务在旧 Session 存活时完成文件和内容预检，再执行 `预热 → 重建完整状态 → 绑定场景 → 提交刷怪`。两者失败或取消都只逆序撤销已经完成的步骤。

正常桌面关闭进入 `ShuttingDown` 后先关闭 Platform 新事务入口并获取 Shutdown 输入 suspension，再关闭存档请求入口、等待读取并执行最终 Capture / 有界 Flush，随后停止 Session 与 Profile。Application 服务按固定顺序关闭：Localization → Accessibility → Platform / Time → Audio / 配置句柄 → Input → Settings；Input Service 在释放 Action Asset 前先通知 Bootstrap Binder 停用并解绑 UI Module。第一次 `Application.wantsToQuit` 会被门禁延后，关闭流程完成后只放行一次；应急销毁不等待，但使用相同服务释放顺序，并保证已经原子提交的存档代际仍有效。

Focus 与 Pause 回调同样只由宿主接收。宿主把事实转发给 `PlatformLifecycleService`，后者使用独立原因集合协调输入 suspension、平台 time pause lease、Audio 暂停和每次挂起 episode 唯一的存档检查点；恢复时先刷新设备，再恢复 Audio，最后释放平台自己的输入与时间 lease。菜单 pause lease 不会被平台恢复误释放。

## 依赖与注入关系

这里没有反射式或全局自动注入。依赖通过构造参数、初始化上下文和场景绑定回调显式交付：

```mermaid
flowchart LR
  App["ApplicationHost"] -->|"ProfileScope + InputService + 受控停止回调"| Session["GameSessionHost"]
  Session -->|"StartOwnedSession(SessionScope, InputService)"| Provider["GameArchitectureProvider"]
  Provider -->|"唯一创建 / 销毁 + generation lease"| Arch["GameArchitecture"]
  Provider -->|"注册非所有权 GameInput Adapter"| Arch

  Session -->|创建| Context["SessionInitializationContext<br/>Architecture / SessionScope / SceneScope / ContentCatalog"]
  Context -->|显式参数注入| Initializer["IGameSessionInitializer"]

  App -->|SessionRunning| Binding["SceneSessionBinding"]
  Session -->|"Running + 同场景 + 有效 lease"| Binding
  Binding -->|"Bind(IArchitecture)"| Controller["场景 Controller"]
  Controller -->|"Command / Query / Event<br/>GetModel / GetSystem / GetUtility"| Arch

  App -->|CurrentSceneScope| ComponentLifecycle["ComponentLifecycle"]
  ComponentLifecycle -->|CreateChild| ComponentScope["Component Scope + TaskGroup"]
```

三种注入各有用途：构造参数表达宿主所有权，并把 Application 级 Input Service 交给 Session Adapter；`SessionInitializationContext` 为 initializer 提供一次事务所需的架构、作用域和内容目录；`SceneSessionBinding` 为场景预置 Controller 延迟注入架构。Session 只能借用输入服务，`GameInput.Dispose()` 不得关闭 Application owner。

`GameArchitectureProvider` 是所有权门卫，不是业务层 Service Locator。它只允许当前 Session 的有效 lease 停止架构，并用 generation 隔离旧任务。

## 场景组件绑定合同

场景预置 Controller 必须遵循 `OnEnable → Enable binding → Bind(IArchitecture) → Unbind → OnDisable`。`GetArchitecture()` 应返回 `_sessionBinding.RequireArchitecture()`，不得在 `Awake` / `OnEnable` 中抢先获取架构。

绑定回调结果：

- `Success`：绑定完成；已绑定同一 Session 时不会因重复通知再次绑定。
- `Retry`：依赖尚未就绪，下一帧在 Scene TaskGroup 中重试。
- `Failed`：配置或依赖不可恢复地缺失，本次不再重试。

只有 Session 为 `Running`、组件与 Session 属于同一场景、lease 有效且 Scene 作用域仍可接收工作时才会绑定。解绑必须注销事件、移除回调并清空所有 Session 级引用。

## 使用规范

| 场景 | 必须 | 禁止 |
| --- | --- | --- |
| 架构所有权 | 由 `ApplicationHost` / `GameSessionHost` 经 Provider 创建和停止 | 业务代码访问 `GameArchitecture.Interface`，或自行创建、销毁架构 |
| 场景依赖 | 通过 `SceneSessionBinding` 获取架构；仅在绑定期间使用 | 在 `Awake` / `OnEnable` 抢先取架构，跨 generation 缓存引用 |
| 组件任务 | 用 `ComponentLifecycle.CreateScope(..., GetCancellationTokenOnDestroy())` 创建，并在 `OnDisable` 主动 `BeginStop` | 把组件任务挂到 Application / Profile 作用域以延长寿命 |
| 长期 UniTask | 登记到所属 `scope.Tasks.Run`，使用传入或链接 Token | 未登记的 `.Forget()`、`UniTaskVoid`、手工散落的 `CancellationTokenSource` |
| 失败处理 | 按需求选择 `Report`、`ReportAndStopScope` 或 `Propagate`；取消视为正常停止 | 用未观察异常代替结构化 `LifecycleResult`，或在停止后提交状态 |
| 运行时对象 | Session 对象登记到 `SessionObjectRegistry`；对象提前销毁时向原 Registry 注销 | 通过“当前 Registry”注销旧代对象，或让 Registry 猜测 Addressables 句柄 |
| 平台回调 | `ApplicationHost` 只转发 Focus / Pause / Quit 事实，`PlatformLifecycleService` 统一编排 | 业务组件自行处理平台挂起、恢复输入、暂停音频或重复保存 |
| Unity API | 场景、GameObject、ScriptableObject 和 QFramework 初始化 / 反初始化均在主线程执行 | 在后台线程直接访问 Unity 对象 |

补充规则：

- 父作用域停止会自动取消子作用域，但每个任务仍必须响应 Token 并尽快退出。
- `LifecycleResult` 用于表达成功、幂等完成、进行中、非法状态、关闭中、验证失败、取消和失败；重复停止必须保持幂等。
- Addressables 资产由 Application Resource Service 通过 owner / lease 精确释放；`SessionObjectRegistry` 只负责 Session 运行时对象。
- 只有不存在 `ApplicationHost` 的独立测试环境，`SceneSessionBinding` 和 `ComponentLifecycle` 才允许回退到 Provider / 独立根作用域。
- `InfrastructurePolicyTests` 会阻止直接访问 `GameArchitecture.Interface`、未登记异步、直接业务文件 IO、`PlayerPrefs` 和散落场景加载；例外必须登记在 `InfrastructurePolicyExceptions.json`，写明原因与移除阶段。

## 当前限制

- 当前 Scene Flow 已覆盖 Bootstrap / Main 的启动、NewGame、Continue、取消、恢复和返回前台；生命周期模块仍不自行加载或卸载场景。
- 当前仍只有 `local-default` Profile；已支持 `auto` 槽位、序列化、两代备份、迁移和 Restore，但没有正式档案切换、手动槽位管理或云同步。
- 新游戏与读档分别使用 `NewGameSessionInitializer` 和 `RestoreGameSessionInitializer`，禁止复用新游戏发放流程伪装恢复。
- Game Flow 状态、场景事务、UI 错误外壳、输入、音频、平台挂起、结构化日志和 Addressables 治理已由独立模块落地；本模块只提供其所有权和注入时机。
- 一旦进入 `Abandoned`，本次运行不得继续创建 Session；应终止运行或执行测试静态重置。

本模块的边界是提供可靠的生命周期、所有权和注入时机；稳定身份、内容目录和迁移合同见[稳定身份、内容目录与迁移框架](./content-identity-migration.md)，文件格式、保存与 Restore 顺序见[本地存档与 Session 恢复](./local-save.md)，本地化见[用户设置与本地化](./user-settings-localization.md)，场景状态与 UI 外壳见[游戏状态、场景流与应用 UI 外壳](./game-state-scene-flow-ui-shell.md)，音频见 [Application Audio](./application-audio.md)，日志与资源见[日志、错误处理与 Addressables 资源治理](./logging-error-addressables-governance.md)，降低动态与平台挂起见[可访问性与平台生命周期](./accessibility-platform-lifecycle.md)。历史验收范围和测试记录见顶部归档计划。
