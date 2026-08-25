# 游戏状态、场景流与应用 UI 外壳

> 状态：`alpha 0.2.4` 已完成；`alpha 0.2.5` 已扩展共享 Settings Page；最近更新：2026-08-24
>
> 参考：[归档计划](../plan/archive/alpha-0.2.4-game-state-scene-flow-ui-shell-plan.md) · [基础设施约束契约](../plan/alpha-0.2-infrastructure-contract.md) · [应用生命周期与会话作用域](./application-lifecycle.md)

## 模块目标

本模块把应用前台、玩法场景和单次 Session 串成唯一、可取消、可恢复的玩家路径。`SceneFlowService` 只决定场景与 Session 事务，`ApplicationHost` 继续负责生命周期和底层 Session 提交，Application UI Shell 只负责玩家表现；三者不互相越权。

当前场景拓扑固定为：

- `Bootstrap.unity`：Build index 0，运行期常驻，承载 Application Shell 与唯一 `EventSystem`。
- `Main.unity`：Build index 1，仅作为 additive 玩法场景加载；包含世界、玩法 UI 和唯一 `CombatPrototypeBootstrap` 配置提供者。
- `ApplicationBootstrap`：仍在 `BeforeSceneLoad` 创建唯一 `ApplicationHost`，不依赖场景序列化宿主。

## 状态与职责

`GameFlowState` 与 `ApplicationLifecycleState`、`GameSessionState` 分离：

| 状态 | 稳定性 | 场景与 Session 合同 |
| --- | --- | --- |
| `Boot` | 启动态 | Bootstrap 正在等待 Application、内容目录、Scene Flow 与 UI Shell Ready |
| `FrontEnd` | 可交互稳定态 | 仅 Bootstrap；无 Main、Session、玩家和架构 lease |
| `Loading` | 事务态 | 正在预检、加载、保存、停止或卸载，Busy 阻止底层输入 |
| `InGame` | 可交互稳定态 | Bootstrap + active Main；唯一 Running Session |
| `Paused` | 可交互稳定态 | Main 与 Session 保持，至少一个游戏暂停 lease 生效 |
| `Recovering` | 事务态 | 正在回滚 Session 或清理残留 Main |
| `FatalError` | 终态 | 无法证明场景或 Session 唯一性，拒绝新的流请求 |

`SceneFlowRequest` 只提供 `EnterFrontEnd`、`StartGame(NewGame / Continue)` 和 `ReturnToFrontEnd`。结果统一使用 `SceneFlowResult`，包含 operation、from / target state、phase、稳定错误码、`SceneId`、恢复动作、本地化消息和内部异常。

## 所有权与入口

| 类型 | 责任 |
| --- | --- |
| `SceneFlowConfiguration` | 集中维护稳定 `SceneId → build scene path` 与事务超时；正式资产位于 `Assets/Settings/Scenes/` |
| `UnitySceneLoader` | Runtime 唯一场景加载、激活和卸载 API 入口 |
| `SceneFlowService` | 状态转换、active + latest pending 仲裁、阶段进度、Session initializer 选择、补偿恢复 |
| `ApplicationHost` | 安装内容目录、创建 Scene Flow / Time Service、Continue 预检、Session 启停和退出门禁 |
| `CombatPrototypeBootstrap` | 只组装 `GameplaySceneConfiguration`，不再在 `Start` 自动创建新游戏 |
| `ApplicationShellBootstrap` | 等待 Application Ready，安装正式目录、配置 Scene Flow、绑定 Shell 并进入 FrontEnd |
| `ApplicationShellController` | FrontEnd、Settings、Busy、Modal、Toast、Fatal 表现、UI Confirm Cue 和焦点恢复 |
| `ApplicationFrontEndController` | 新游戏、继续、语言、设置和退出入口；不依赖 `GameArchitecture` |
| `ApplicationSettingsController` | FrontEnd / Paused 共用音频、输入、Glyph 与 Reduce Motion 设置事务 |

业务 Controller 不得调用 `SceneManager`、存档 Storage 或 Session initializer。游戏内菜单只保留保存、返回前台和关闭；跨 Session 的 NewGame / Continue 由 FrontEnd 通过 Scene Flow 发起。

## 运行流程

### 冷启动

1. Bootstrap 加载，`ApplicationBootstrap` 创建宿主。
2. Application 完成 Settings、Input、Audio、Accessibility、Platform、Localization、Profile 和持久化服务启动。
3. `ApplicationShellBootstrap` 安装正式 Content Catalog，配置 Scene Flow 并绑定 UIDocument。
4. Scene Flow 提交 `Boot → FrontEnd`。此时 Main 未加载，也不会隐式创建 Session。

### 新游戏与继续游戏

1. FrontEnd 提交 `StartGame`，Scene Flow 进入 `Loading`。
2. Continue 在加载 Main 前完成 `auto` 槽位预检并冻结 `PreparedRestore`；失败仍留在 FrontEnd。
3. `UnitySceneLoader` additive 加载并激活 Main，Scene Flow 只在该 Scene 根对象中查找唯一配置提供者。
4. ApplicationHost 使用 NewGame 或 Restore initializer 创建、绑定并提交 Session。
5. 仅在 Session 为 Running、lease 有效且场景一致后提交 `InGame`。

### 暂停与返回前台

`GameplayPauseSystem` 通过 `GameTimeService` 的有主 pause lease 适配现有 Command / Event。打开菜单只在 `InGame ⇄ Paused` 间转换，不重建 Session。

确认返回后先保存 `auto`，保存失败保持原 Session 并提供重试 / 取消；成功后依次停止 Session、激活 Bootstrap、卸载 Main、释放全部 pause lease，最后提交 FrontEnd。没有静默丢弃失败保存的强制离开路径。

## 取消、进度与恢复

- Scene Flow 使用一个 active request 和一个 latest pending request。显式取消返回 `Cancelled`，被更新请求替代返回 `Superseded`，每个请求只完成一次。
- Unity 场景加载不能硬取消；取消后等待底层操作到达可清理点，再补偿卸载残留场景。
- 进度只报告真实 `SceneFlowPhase` 和底层可测量值。加载进度同步转发，保证 `Completed` 后不会收到过期 Loading 回调。
- 可恢复错误先回滚到 FrontEnd / InGame / Paused，再由 Shell 显示本地化 Modal；只有 Session stop、场景卸载或清理唯一性无法保证时进入 `FatalError`。
- Loading、存档、恢复和 Shell 提示使用 realtime / unscaled time，不受暂停阻塞。

## UI 外壳与焦点

`ApplicationShell.uxml/.uss` 使用现有 Theme、Localization 和字体链，固定包含 FrontEnd Page、共享 Settings Page、Toast、Modal、Busy 与 Fatal 层。Bootstrap 的 Panel sorting order 高于 Main UI：

- FrontEnd 只在 `FrontEnd` 可交互，Continue 由 Profile 级存档预检决定是否启用。
- Settings 可从 FrontEnd 与暂停菜单打开；关闭后恢复来源焦点，从 Main 打开时不释放菜单 pause lease 或切回 Gameplay Context。
- Modal 保存打开前焦点，关闭后优先恢复原元素；确认返回和可恢复错误均使用此层。
- Busy 只在阶段可取消时暴露取消按钮；隐藏层不能在后续帧抢占 Main UI 焦点。
- Toast 不聚焦、不阻止底层输入；Fatal 只保留安全退出入口。
- 唯一 `EventSystem` 位于 Bootstrap，Main 不再拥有 EventSystem。

## 时间服务

`GameTimeService` 是 Runtime 唯一 `Time.timeScale` 写入点。第一个 pause lease 将时间倍率置零，最后一个 lease 释放后恢复基础倍率；重复释放幂等，返回 FrontEnd 和 Application Shutdown 都会强制 `RestoreAll()`。

## 持久化集成

- `SessionSaveFacade` 只服务当前 Running Session 的保存，不再提供跨 Session 的 Continue / NewGame。
- Continue 由 `ApplicationHost.PrepareContinueAsync` 在 Profile Scope 完成文件校验、迁移、内容解析和恢复图准备。
- 返回前台必须先调用 Application 级 `SaveBeforeExitAsync`，再停止 Session。
- 规范 JSON 在计算校验值前把单精度数值归一为 double 表示，避免同一数值因 Single / Double 文本差异产生伪校验失败。

## 验证与策略

`alpha 0.2.4` 完成验收：

- Unity 编译与 Bootstrap 实机启动均为 0 Console Error。
- EditMode `360/360` 通过，覆盖状态合同、配置、loader、latest-wins、超时、恢复、时间、策略、本地化和存档回归。
- 项目 PlayMode `52/52` 通过；完整 PlayMode 54 项中 52 项通过、0 失败，2 项 Input System 包测试因既有 issue 1252825 跳过。
- 三次 FrontEnd → InGame → Paused → FrontEnd、Continue、取消 / 替代、失败恢复、焦点、唯一场景 / Session / EventSystem 和三语言三分辨率布局均有自动验证。
- 从 Bootstrap 真实 Play 后为 Application `Ready`、Flow `FrontEnd`、Shell 唯一，Main 未加载且无 Session；停止后控制台无错误。
- 策略扫描保证 Runtime 场景 API 只在 `UnitySceneLoader`，`Time.timeScale` 写入只在 `GameTimeService`；底层场景故障测试保留精确白名单。

## alpha 0.2.6 扩展验证

- Settings Page 的资产、控件、本地化与两入口由 EditMode / PlayMode 契约覆盖；音频确认通过真实 Addressables Cue 播放。
- Scene Flow 失败通过 `PlayerErrorCatalog` 映射严重度和 Retry / ReturnFrontEnd / Quit，未处理异常进入现有 Fatal 覆盖层。
- 全量 EditMode `423/423`、项目 PlayMode `53/53`；完整 PlayMode 57 项中 55 项通过、0 失败，2 项为 Input System 上游既有 Ignore。

## 当前边界

- 当前只有 Bootstrap 与 Main，不包含多地图、关卡选择、快速旅行或 Addressables Scene。
- FrontEnd 提供新游戏、继续、语言、设置和退出；仍没有 Profile 选择、手动槽位、删除、重命名或云同步。
- Shell 是当前真实消费者所需的最小闭环，不是通用多 Page 导航框架；设置页只开放已有真实消费者的 Audio、Input、Glyph 与 Reduce Motion。
- Shell 不拥有领域恢复事务；日志、异常和资源错误的映射与所有权见[日志、错误处理与 Addressables 资源治理](./logging-error-addressables-governance.md)。
