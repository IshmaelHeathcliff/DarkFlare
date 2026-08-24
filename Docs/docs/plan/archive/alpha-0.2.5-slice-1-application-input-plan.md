# alpha 0.2.5 切片 1：Application Input Service 与 Session 适配执行计划

> 状态：已完成（2026-08-24）
> 规划日期：2026-08-24
> 实施基线：`82b8513`（alpha 0.2.5 切片 0 基础契约）
> 上位计划：[alpha 0.2.5 输入、音频、可访问性与平台生命周期执行计划](./alpha-0.2.5-input-audio-accessibility-platform-lifecycle-plan.md)
> 相关模块：[应用生命周期与会话作用域](../../infrastructure/application-lifecycle.md)、[游戏状态、场景流与应用 UI 外壳](../../infrastructure/game-state-scene-flow-ui-shell.md)、[输入与运行时 UI](../../input-ui-system.md)

## 本切片结论

本切片只迁移输入所有权，不实现玩家重绑定和设置页面。`ApplicationHost` 在 Settings 之后创建唯一 `ApplicationInputService`，该服务持有唯一运行时 `InputSystem_Actions`、输入上下文和可叠加暂停租约。每代 Session 的 `GameInput` 只转发既有 QFramework 输入 API，不再创建或销毁 Action Asset。

Bootstrap 的 `InputSystemUIInputModule` 在运行时改绑 `ApplicationInputService` 的同一 `InputActionAsset`。FrontEnd 默认使用 UI Context；Session 创建时进入 Gameplay，打开菜单仍切换 UI，Session 结束后恢复 FrontEnd UI。Application Shutdown 才最终禁用并释放 Actions。

## 成功标准

- Application 冷启动后只有一个 `ApplicationInputService` 和一个运行时 Action Asset。
- Bootstrap `InputSystemUIInputModule.actionsAsset` 与 `ApplicationHost.Input.ActionAsset` 引用相同实例。
- 连续三次 FrontEnd → InGame → FrontEnd 不更换 Action Asset，也不累积输入回调。
- Session `GameInput.Dispose()` 只解除 Adapter 订阅并恢复 UI Context，不释放 Application Action Asset。
- Gameplay / UI Action Map 继续互斥；任一暂停原因有效时两个 Map 都禁用，最后一个租约释放后才恢复当前 Context。
- Tab / Start、Escape / 手柄东键、移动、交互、UI 导航和 Rearrange 的既有行为保持不变。
- Application 启动失败、正常关闭和应急关闭都能幂等释放输入服务。
- 不修改 Input Actions 资产、Settings Schema、场景流状态、菜单功能或玩家可见 UI。

## 已确认基线

- 仓库位于干净提交 `82b8513`；Unity `6000.4.3f1`，目标平台 `StandaloneWindows64`。
- Unity Editor 当前停在 `Bootstrap.unity`，未运行、未编译、无资源刷新，MCP 可执行工具。
- Input System 包版本为 `1.19.0`；运行时反射确认 `InputSystemUIInputModule.actionsAsset` 是可读写的 `InputActionAsset` 属性。
- 包源码确认替换 `actionsAsset` 时会解除旧 Action 回调；只有旧 ActionReference 仍存在时才会按 Map / Action 名重建引用，因此正式 Binder 必须显式绑定并释放 Point、Move、Submit、Cancel 等全部引用。
- 当前 `GameArchitecture.Init()` 每代执行 `new GameInput()`，`GameInput` 每次再创建 `InputSystem_Actions`；`GameArchitecture.OnDeinit()` 会释放该实例。
- Bootstrap EventSystem 的 UI Module 当前序列化引用原始 `InputSystem_Actions.inputactions`，尚未绑定 Session 使用的运行时实例。
- 现有 `GameInput`、Application Lifecycle、alpha 0.2.4 Flow Contract 定向 EditMode 基线为 `27/27` 通过。

## 范围边界

### 本切片包含

- Application 级输入服务与幂等关闭。
- Gameplay / UI Context 切换。
- 同一暂停原因可重复获取的引用计数租约，以及不同原因的重叠恢复。
- ApplicationHost、GameSessionHost、GameArchitectureProvider 和 QFramework `GameInput` 的显式依赖交付。
- Bootstrap UI Module 的运行时 Action Asset 绑定。
- 独立 EditMode / PlayMode 测试所需的显式 standalone 输入 owner。
- 所有权、Action Map、Session 循环和 Bootstrap 导航回归测试。

### 本切片不包含

- Binding Overrides JSON 的加载、保存、冲突检测或恢复默认。
- 活动设备族识别、噪声过滤、设备拔出处理、Glyph Resolver 或 Glyph 资产。
- Settings Page、Rebind Modal、本地化 Key 或 UXML / USS 布局修改。
- SceneTransition、FocusLost、PlatformSuspended 和 Rebinding 的真实消费者；本切片只提供暂停租约，并在 Shutdown 使用。
- Audio、Reduce Motion、平台挂起 / 恢复和退出检查点。
- `InputSystem_Actions.inputactions` 或生成类 `InputSystem_Actions.cs` 的修改。

## 冻结设计

### ApplicationInputService

`ApplicationInputService` 是 Action Asset 的唯一所有者，提供以下最小能力：

- 只读 `ActionAsset`，供 Bootstrap UI Module 绑定。
- 只读 `CurrentContext`、`SuspensionReasons`、`IsSuspended` 和关闭状态。
- `SwitchContext(InputContext)`，只在状态实际变化时更新 Action Map 并发布事件。
- `AcquireSuspension(InputSuspensionReason)`，返回幂等 `IDisposable` 租约。
- 供 Session Adapter 使用的 Move 读取和 Interact、ToggleMenu、Navigate、Rearrange、Cancel 回调入口。
- 幂等 `Dispose()`：先禁用 Actions、解除所有 Input System 回调，再销毁运行时 Action Asset。

初始 Context 固定为 `UI`，保证无 Session 的 FrontEnd 可导航。未暂停时只启用当前 Context 对应的 Map；暂停时两个 Map 全部禁用。关闭后的读操作返回安全空值，写操作返回结构化失败或抛出 `ObjectDisposedException`，具体风格与现有 `GameInput` 保持一致。

每个 suspension lease 只接受一个非 `None` 的已定义 reason。服务按 reason 计数，同一原因的多个 lease 与不同原因可以重叠；重复释放不减少第二次，服务关闭后释放旧 lease 也不得访问已销毁 Actions。

### 依赖交付与所有权

| 位置 | 变更 | 所有权结果 |
| --- | --- | --- |
| `ApplicationHost.Boot()` | Settings 成功后、Localization 创建前构造 Input Service | Application 唯一 owner |
| `ApplicationHost` 关闭路径 | Localization 关闭后、Settings 关闭前释放 Input Service | 与创建顺序逆序 |
| `GameSessionHost` | 构造时接收 Application Input Service 并交给 Provider | 只借用，不关闭 |
| `GameArchitectureProvider` | 创建 Architecture 后注册 `GameInput` Adapter | 显式 Composition Root 注入 |
| `GameArchitecture` | 不再自行 `new GameInput()`；Deinit 只 Dispose Adapter | 不触碰 Action Asset owner |
| standalone 测试 fixture | 先创建测试 Input Service，再创建 Session；停止 Session 后释放 owner | 测试拥有完整生命周期 |

生产代码不得在 ApplicationHost 缺失时隐式创建输入 owner，也不增加静态 `Current` 输入服务。只有测试 fixture 显式持有 standalone owner，避免掩盖 Composition Root 缺失。

### GameInput Adapter

`GameInput` 保留当前 Controller 已消费的公开表面：`Move`、模式状态、Interact / Navigate / Rearrange / Cancel / ModeChanged 事件、模式切换和交互绑定显示文本。内部改为订阅 `ApplicationInputService`，不直接订阅或销毁生成 Actions。

- 构造时进入 Gameplay Context 并挂接一次 Session 回调。
- ToggleMenu 继续切换 UI Context。
- Cancel 仍按现有回调顺序尝试消费；未消费时在当前 Input System 更新的 `onAfterUpdate` 阶段切回 Gameplay，避免共享 Action Asset 时在回调分发中途禁用 UI Map。
- Dispose 幂等解除 Adapter 事件，将 Context 恢复为 UI，但不关闭 Input Service。
- `GameInputMode` 暂时保留以避免扩散修改，内部与 `InputContext` 做唯一映射；阶段完成文档明确两者职责。

### Bootstrap UI Module Binder

新增一个挂在 Bootstrap EventSystem 上的最小 Binder：

- `[RequireComponent(typeof(InputSystemUIInputModule))]`，`InputSystemUIInputModule` 使用 `[SerializeField]` 缓存。
- `OnValidate()` 检查并获取同对象组件，不在运行时搜索整个场景。
- `OnEnable()` 订阅 `ApplicationHost.InputReady`，并从当前 Host 取得 Input Service；同场景 Host 重建后自动改绑新服务。
- 绑定前先停用 UI Module，显式从唯一运行时 Asset 创建 Point、Navigate、Submit、Cancel、Click 等全部 ActionReference，再恢复模块启用状态。
- Input Service 关闭前先发布 `Closing`；Binder 在 Asset 释放前停用模块、解除回调、清空 ActionReference 和 `actionsAsset`，避免 Input System 残留状态。
- 重复绑定同一实例幂等；Host 或服务缺失时不创建第二份 Actions。

执行时通过 Unity MCP 向当前打开的 `Bootstrap.unity/EventSystem` 添加 Binder 并保存场景，不直接编辑场景 YAML。原始序列化 Action 引用只作为 Editor 配置，运行后的真实引用必须由测试证明已替换。

## 执行顺序

### 1. 先写所有权与行为测试

- 新增 Input Service EditMode 测试，覆盖初始 UI、Context 互斥、重复切换、暂停叠加、重复释放和关闭。
- 改造 `GameInputTests`，由 fixture 显式创建和释放 Input Service，冻结现有键鼠 / 手柄行为。
- 为 Provider / GameSessionHost 增加“缺少显式输入依赖即失败”的契约测试。
- 测试先失败，确认失败原因只来自尚未实现的所有权迁移。

### 2. 实现 Application 输入核心

- 新增 `ApplicationInputService` 和 suspension lease。
- 把 Action 回调注册、Map 启停和运行时 Action Asset 销毁集中到服务。
- 保持 `ApplicationInputContracts.cs` 的切片 0 枚举与结果语义不变；只有确认缺失时才补充最小合同。
- 先跑 Input Service 与 `GameInputTests`，再进入生命周期改造。

### 3. 完成显式注入与 Session 适配

- 调整 ApplicationHost 的创建、公开入口、启动失败、正常关闭和应急关闭顺序。
- 调整 GameSessionHost / Provider 的构造链，禁止生产隐式 standalone owner。
- 将 GameInput 收缩为非所有权 Adapter，并移除 GameArchitecture 自建输入。
- 更新 EditMode / PlayMode 的 `GameArchitectureTestFixture` 与直接构造 `GameSessionHost` 的生命周期测试。
- 回归 Application Lifecycle 与 alpha 0.2.4 Flow Contract，确认 Session 事务语义未变化。

### 4. 绑定 Bootstrap EventSystem

- 创建并校验 Binder 脚本，等待 Unity 编译完成且 Console Error 为 0。
- 使用 Unity MCP 添加 Binder、填充组件引用并保存 Bootstrap 场景。
- 在 PlayMode 验证 UI Module 与 Host 引用同一 Action Asset，FrontEnd UI 可导航。
- 连续三次进入和退出 Session，验证 Action Asset Instance ID 稳定、Session Adapter 更新且 FrontEnd 恢复 UI Context。

### 5. 收尾回归与文档同步

- 运行新增专项、全量 EditMode、Application / Scene Flow / 输入相关项目 PlayMode，再运行完整 PlayMode。
- 保留原有两项 Input System 上游跳过；新增测试不得跳过或不稳定。
- 更新输入与生命周期模块文档，并在父计划记录切片 1 的实际文件、测试数量和偏差。
- 确认场景、Input Actions、生成代码、EditorSettings 和非目标资源没有意外修改。

## 预计文件边界

预计新增：

- `Assets/Scripts/Runtime/Infrastructure/Input/ApplicationInputService.cs`
- `Assets/Scripts/Runtime/Infrastructure/Input/ApplicationInputModuleBinder.cs`
- `Assets/Scripts/Tests/EditMode/ApplicationInputServiceTests.cs`
- `Assets/Scripts/Tests/PlayMode/Alpha025InputOwnershipPlayModeTests.cs`

预计修改：

- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationHost.cs`
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/GameSessionHost.cs`
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/GameArchitectureProvider.cs`
- `Assets/Scripts/Runtime/GameArchitecture.cs`
- `Assets/Scripts/Runtime/Gameplay/Input/GameInput.cs`
- `Assets/Scenes/Bootstrap.unity`（只通过 Unity MCP）
- `Assets/Scripts/Tests/EditMode/GameInputTests.cs`
- `Assets/Scripts/Tests/EditMode/GameArchitectureTestFixture.cs`
- `Assets/Scripts/Tests/EditMode/ApplicationLifecycleTests.cs`
- `Assets/Scripts/Tests/PlayMode/GameArchitectureTestFixture.cs`
- 受构造签名影响的精确生命周期测试
- `Docs/docs/input-ui-system.md`
- `Docs/docs/infrastructure/application-lifecycle.md`
- 父计划与文档索引

明确不修改：

- `Assets/Settings/InputSystem_Actions.inputactions`
- `Assets/Scripts/Runtime/Gameplay/Input/InputSystem_Actions.cs`
- `ProjectSettings/EditorSettings.asset`
- Settings 数据结构与 Schema
- UXML、USS、本地化表、Audio 与 Glyph 资产
- `Docs/design/`

## 验证矩阵

| 层级 | 必须覆盖 |
| --- | --- |
| EditMode：输入核心 | 初始 UI、Gameplay / UI 互斥、暂停原因叠加、同原因计数、租约幂等、关闭后安全 |
| EditMode：Adapter | 键盘 / 手柄移动、菜单开关、Cancel 消费、交互、导航、Rearrange、模式事件次数 |
| EditMode：生命周期 | 显式依赖、启动失败清理、Session 正常 / 应急停止、generation 与 abandoned 语义不变 |
| PlayMode：Bootstrap | UI Module 与 Host 共用 Action Asset，FrontEnd 键鼠 / 手柄导航可用 |
| PlayMode：三次 Session | 同一 Asset Instance ID、每代一个 Adapter、退出后 UI Context、无重复回调 |
| PlayMode：行为回归 | Tab / Start、Escape / 东键、移动、交互、暂停菜单与返回 FrontEnd |
| 全量 | Unity 编译、EditMode、项目 PlayMode、完整 PlayMode、Console Error 0 |

## 风险与控制

- **Provider 在 Architecture 初始化后才注册 Adapter**：先确认现有 Init 中没有 Model / System 读取 `GameInput`；用契约测试锁定注册完成后才能向外发布 Session。
- **Session Dispose 意外释放 Application Actions**：Adapter 不接收 owner 标记，类型上不提供关闭服务入口；测试在 Session 停止后继续使用 FrontEnd UI。
- **UI Module 短暂挂接序列化资产**：Binder 在 Awake 立即替换；PlayMode 首帧断言运行时引用，Console 记录绑定失败。
- **重叠暂停提前恢复**：每个 reason 使用引用计数，Map 状态只由统一 `ApplyState()` 计算。
- **测试偷偷走生产 Service Locator**：standalone owner 必须由 fixture 字段显式创建和释放，生产路径不提供无参 Provider overload。
- **关闭顺序破坏现有 Flush**：BeginShutdown 只先获取 Shutdown suspension；Input Service 最终释放仍位于 Session 停止和 Localization 关闭之后、Settings 关闭之前。

## 实施完成记录（2026-08-24）

- `ApplicationInputService` 已成为唯一运行时 `InputSystem_Actions` owner，初始 Context 为 UI，并实现 Context 切换、同原因引用计数 suspension lease、重叠原因恢复、关闭前通知和幂等释放。
- `ApplicationHost` 在 Settings 后创建输入服务，经 `GameSessionHost → GameArchitectureProvider` 显式交给每代 `GameInput` Adapter；`GameArchitecture` 不再创建 Action Asset，Session 结束只解除 Adapter 并恢复 UI Context。
- Bootstrap EventSystem 已通过 Unity MCP 添加 `ApplicationInputModuleBinder`。Binder 显式绑定全部 UI ActionReference，并保证解绑动作先于 Asset 释放；同场景 Host 重建也会自动重新绑定。
- 共享 Asset 暴露的 Cancel 回调顺序问题已收敛：未消费的 Cancel 在 `InputSystem.onAfterUpdate` 切回 Gameplay，不会在 UI Module 仍处理同一 CallbackContext 时禁用 Map。
- PlayMode 输入模拟 Guard 已适配 Application 级 owner：首次替换测试 Input System runtime 前结束旧 Host，替换后重建 Host 与 Bootstrap；后续设备清理只临时停用 UI Module，不再把它改绑到默认 Actions。
- 输入核心与 Adapter 专项 EditMode `15/15`、基础设施策略 `27/27`、全量 EditMode `378/378` 通过；项目 PlayMode `51/51` 通过；完整 PlayMode 55 项中 53 项通过、0 失败，2 项为 Input System 上游既有 Ignore。
- 未修改 `InputSystem_Actions.inputactions`、生成类、Settings Schema、UXML、USS、本地化表、Audio、Glyph 或 `Docs/design/`。

## 完成定义

- 新增与改造脚本通过 Unity 标准校验，0 warning / 0 error。
- 所有成功标准都有自动测试，不以日志数量代替所有权断言。
- 定向 EditMode 基线 `27/27` 不退化；新增专项和全量测试零失败。
- Bootstrap 实机键鼠和手柄均可从 FrontEnd 进入 Main、打开 / 关闭菜单并返回 FrontEnd。
- 三次 Session 循环后 Action Asset、事件次数、Context 和 suspension 状态符合预期。
- 最终 Console Error 为 0，`EnterPlayModeOptions = 0`，`git diff --check` 通过。
- 父计划已完成切片 2–6；本计划随 `alpha 0.2.5` 阶段计划统一归档。
