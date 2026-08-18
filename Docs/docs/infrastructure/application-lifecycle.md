# 应用生命周期与会话作用域

> 状态：`alpha 0.2.0` 已完成
> 最近更新：2026-08-18
> 对应归档计划：[alpha 0.2.0 应用宿主、作用域与规范验证执行计划](../plan/archive/alpha-0.2.0-application-lifecycle-plan.md)
> 上位约束：[alpha 0.2 基础设施约束契约](../plan/alpha-0.2-infrastructure-contract.md)

## 职责与边界

本模块为当前单场景玩法提供唯一应用宿主、分层作用域、Session 事务边界、统一取消与 QFramework 架构所有权。它解决“谁创建、谁停止、谁释放”的基础问题，不实现存档、本地化或完整 SceneFlow。

当前兼容路径仍从唯一构建场景 `Main.unity` 启动，但应用宿主在场景脚本执行前创建。`CombatPrototypeBootstrap` 只负责读取场景配置并请求宿主启动当前场景的新游戏事务，不再直接拥有全局架构的创建与销毁。

## 主要入口

| 入口 | 职责 |
| --- | --- |
| `ApplicationBootstrap` | 在 `SubsystemRegistration` 重置静态引用，并在 `BeforeSceneLoad` 创建唯一 `ApplicationHost`；架构 generation 不复用旧值 |
| `ApplicationHost` | 拥有 Application / Profile 作用域，按 latest-wins 协调 Session / 场景请求，并在每个有效 generation 进入 Running 时通知一次 |
| `GameSessionHost` | 拥有 Session / Scene 作用域，以架构 lease 校验当前 generation，协调初始化、停止与回滚 |
| `GameArchitectureProvider` | `GameArchitecture.Interface` 的唯一正式创建、访问和销毁入口，签发绑定所有者与单调 generation 的 lease |
| `LifecycleScope` | 表达父子所有权、取消、停止顺序和终止状态 |
| `LifecycleTaskGroup` | 只跟踪仍在运行的长期 UniTask，统一观察异常、取消与有界停止；超时诊断保留未结束 operation 名称 |
| `ComponentLifecycle` | 为组件建立从属 Scene 的 Component 子作用域 |
| `SceneSessionBinding` | 让场景预置组件只绑定同场景且已 Running 的 Session，并在 Scene 作用域结束时解除绑定 |
| `SessionObjectRegistry` | 登记 Session 运行时对象与清理动作，支持统一释放和对象自行注销 |
| `GameplaySceneConfiguration` | 收集 `Main` 场景的新游戏配置和场景绑定引用 |
| `NewGameSessionInitializer` | 执行新游戏验证、资源预热、状态写入、玩家创建、场景绑定与失败回滚 |

正式业务代码不得直接创建或销毁 `GameArchitecture`。需要架构时由 `GameArchitectureProvider` 提供当前 Session 的实例；场景脚本只通过 `ApplicationHost` 请求生命周期操作。

场景预置 Controller 不得在 `Awake` / `OnEnable` 中直接取得当前架构。HUD、菜单、背包、商店、打造、交互提示和世界交互表现统一通过 `SceneSessionBinding` 等待 `SessionRunning`；绑定时必须同时满足 Session 为 `Running`、绑定场景与组件场景一致、架构 lease 仍有效且 Scene 作用域仍可接收工作。绑定结果区分成功、下一帧重试与永久失败，只有真实成功才增加绑定计数并暴露架构。同一组件对同一 generation 只绑定一次，Scene 作用域取消或被 Abandoned 后代污染时立即解除或拒绝重绑。只有不存在 `ApplicationHost` 的独立测试环境可以回退到 Provider 的当前实例。

## 作用域与所有权

| 作用域 | 当前所有者 | 典型内容 | 结束时机 |
| --- | --- | --- | --- |
| Application | `ApplicationHost` | 应用根取消、宿主状态 | 应用退出或测试清理 |
| Profile | `ApplicationHost` | 当前本地默认档案的生命周期位置 | 应用退出；正式档案切换留给后续阶段 |
| Session | `GameSessionHost` | `GameArchitecture`、玩法 Model / System、随机状态、Session 对象 | 离开当前游戏或绑定场景卸载 |
| Scene | `GameSessionHost` | 当前场景任务、Bootstrap、刷怪器、摄像机绑定与场景对象 | 场景卸载或 Session 停止 |
| Component | `ComponentLifecycle` | 单个组件的长期任务与局部取消 | 组件禁用或销毁 |

父作用域停止会取消并等待子作用域。正常关闭顺序是 `Component → Scene → Session → Profile → Application`；短生命周期对象不得反向拥有长生命周期对象。

`GameSessionHost` 记录实际绑定的 Unity `Scene`。绑定场景卸载时，宿主停止对应 Session；直接重新加载 `Main` 时，旧场景卸载、旧 Session 停止、新 Session 创建与绑定由同一宿主协调。场景请求使用 Application 作用域内的 latest-wins 协调器：新请求会停止正在执行请求的子作用域，替换尚未执行的旧 pending 请求；被替换请求以 `Cancelled` 完成且每个回调只完成一次，最新请求最终执行。该兼容合同只保证当前 `Main` 重载安全，不等同于 `alpha 0.2.4` 的通用场景状态机或 SceneFlow。

## 状态与结果

- Application 状态：`None → Booting → Ready → ShuttingDown → Shutdown`；启动失败进入 `Failed`。
- Session 正常路径：`Created → Initializing → Running → Stopping`。
- 初始化失败会进入 `RollingBack`，只撤销本次事务已经完成的步骤，再结束当前 Session。
- `LifecycleScope` 正常结束为 `Stopped`；停止超时进入 `Abandoned`。
- `GameArchitectureSessionLease` 同时绑定架构实例、所有者 Session 作用域与单调 generation；初始化、停止、回滚和 `SessionRunning` 通知都必须验证精确 lease。
- `Abandoned` 是不可逆终态；后代作用域的 Abandoned 污点会粘性传播到全部祖先并关闭新任务 / 子作用域入口。迟到的初始化、停止或回滚结果不得把状态改回 `Running` / `None`，也不得访问下一代架构。
- 生命周期 API 返回 `LifecycleResult`，区分成功、幂等完成、操作进行中、非法状态、应用关闭中、验证失败、取消和失败。

重复停止是幂等操作；并发初始化、并发停止及应用关闭期间的新请求会收到结构化结果，不依赖异常作为正常控制流。

## 新游戏初始化事务

当前 `NewGameSessionInitializer` 按事务处理新游戏：

1. 验证场景引用、配置资产和当前生命周期状态，并保持刷怪器关闭。
2. 配置本次 Session 的随机根种子。
3. 预热玩家、技能、怪物、掉落与物品图标等 Addressables 资源。
4. 创建玩家，发放并装备初始武器。
5. 初始化商人、初始金币和打造配置。
6. 绑定摄像机并登记 Session 运行时对象。
7. 最后提交刷怪循环；成功后 Session 进入 `Running`。

任何步骤失败或收到取消时，只回滚已经完成的步骤：停止刷怪、解除场景绑定、撤销本次初始状态、释放已登记对象与资源，随后由 Session 停止流程销毁架构。未成功提交的事务不会留下玩家、刷怪器或重复初始发放。

本阶段只有 NewGame 初始化器。Restore 初始化器、存档 DTO 和“继续游戏”路径属于 `alpha 0.2.2`，不能通过复用新游戏发放步骤伪装成读档。

## 异步、取消与异常

- 长期 UniTask 必须登记到所属 `LifecycleTaskGroup`，并使用作用域 Token 或其链接 Token。
- 任务失败策略为 `Report`、`ReportAndStopScope` 或 `Propagate`；取消属于正常停止，不报告为未处理异常。
- TaskGroup 只保留活动任务；同步完成、正常异步完成和迟到完成都会从活动集合移除，不长期保留已完成 runner 或闭包。
- 作用域默认停止时限为 10 秒。到期后进入 `Abandoned`，旧代仍被隔离，宿主禁止创建下一代 Session。
- Session 预登记回滚协调任务。停止时先等待 Scene 初始化任务收敛，再执行尽力回滚；回滚或任一祖先作用域超时会有界返回失败并保留旧 lease，不会阻塞 latest-wins 回调或应用关闭。
- 超时后的迟到任务只保留失败记录，不再触发全局或作用域失败回调；迟到 continuation 的状态结果一律丢弃。
- Component 等后代任务超时会触发当前 Session 的一次性受控停止；Application 进入 `Failed`，已有绑定解除，污染作用域拒绝继续扩张工作。
- 正常停止必须先完成任务与对象清理，再销毁 `GameArchitecture`。
- 应急退出不会把仍可能运行的旧任务切到新架构；旧架构和 Provider 保留至下一次 `SubsystemRegistration` 静态重置。

`Abandoned` 是安全隔离状态，不表示清理成功。发生后应终止本次运行或重置测试环境，而不是继续创建 Session。

## Addressables 与运行时对象

- Prefab 加载按资源 GUID 建立跨调用单飞任务；Sprite 在单次预热请求内按 GUID 去重。
- 加载器保存并释放实际返回的 Addressables 句柄，不通过重新构造 key 猜测释放目标。
- `SessionObjectRegistry` 为 Session 创建的 GameObject、组件级清理和其他运行时对象提供统一登记点；对象登记时保存其所属 Registry，对象提前销毁时精确向该实例注销，禁止通过“当前 Registry”注销旧对象。Session 停止时释放剩余登记项。
- 资源句柄仍由对应加载器拥有，Registry 负责 Session 对象，不取代 Addressables 加载器的精确释放职责。

更完整的 Addressables 分组、标签、诊断和资源治理仍属于 `alpha 0.2.6`。

## 线程与配置

Unity 场景、GameObject、ScriptableObject、QFramework 初始化与反初始化都在主线程执行。可并行等待的资源预热由 UniTask 编排，但不得在后台线程直接访问 Unity 对象。

当前场景配置由 `GameplaySceneConfiguration` 从 `CombatPrototypeBootstrap` 的序列化引用构建；默认 Profile 仅是内存中的本地兼容身份，不持久化。存档路径、Schema、Locale 和用户设置均未在本阶段建立。

## 规范验证

`InfrastructurePolicyTests` 扫描新增运行时代码并执行基础设施策略。当前冻结的规则包括：

- 禁止业务代码直接使用 `GameArchitecture.Interface`，唯一例外为 Provider 实现。
- 禁止新增未登记的 `.Forget()`、`UniTaskVoid` 与手工 `CancellationTokenSource`。
- 禁止直接业务文件 IO、`PlayerPrefs` 和散落场景加载；`Debug.Log*` 治理由 `alpha 0.2.6` 启用对应规则。
- 机器可读例外位于 `InfrastructurePolicyExceptions.json`，每项必须说明原因与移除阶段。

规则使用正反向夹具验证，确保扫描器既能接受合规样例，也能拒绝违规样例。后续阶段应收紧规则，不能用白名单绕过正式入口。

## 验证记录

- Unity 脚本编译：0 error。
- EditMode：`255/255` 通过。
- 项目自有 PlayMode：`45/45` 通过；完整运行共 `49` 项，`47` 项通过、`0` 失败，另有 `2` 项 Input System 包集成测试因上游 issue 1252825 按既有标记跳过。
- 最终修复后两次真实 Play 均达到 Application `Ready`、Session `Running` 且架构 lease 有效；`ApplicationHost`、玩家和已提交刷怪器每次各 `1` 个。
- 两次退出后的 Console Error 均为 0。
- 直接重新加载 `Main`、连续三个快速场景请求、无 Provider 场景进入 `Main`、旧 Registry 对象延迟销毁和旧 generation 迟到 continuation 合同通过。

覆盖范围包括唯一宿主、连续 Session、并发初始化 / 停止、取消与回滚、挂起回滚有界收敛、作用域超时诊断、后代 Abandoned 污点传播、受控停止与应急终态隔离、架构初始化 / 反初始化异常安全、Prefab Addressables 单飞与精确句柄、场景卸载、latest-wins、场景组件三态重绑、lease / generation 隔离、旧 Registry 精确注销和策略扫描。新增专项位于 `AbandonedGenerationIsolationTests.cs`、`ApplicationHostSceneTransitionPlayModeTests.cs`、`SceneSessionComponentBindingPlayModeTests.cs` 与 `SessionObjectRegistryOwnershipPlayModeTests.cs`。

## 当前限制与后续扩展

- 当前仍只有 `Main.unity`，没有 Boot / FrontEnd / Loading / Recovering / FatalError 状态机，也没有通用 SceneFlow。
- 当前没有正式 Profile、存档槽位、序列化、备份、迁移或恢复路径。
- Unity Localization 仍未建立 Locale、String Table、运行时切换和文本迁移。
- 应用级结构化日志、统一玩家错误反馈和完整 Addressables 治理尚未实现。

这些能力分别由 `alpha 0.2.1` 至 `alpha 0.2.6` 继续交付；本模块只提供它们可依赖的生命周期与所有权底座。
