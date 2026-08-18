# alpha 0.2.0 应用宿主、作用域与规范验证执行计划

> 状态：已完成并归档
> 建立日期：2026-08-18
> 最近更新：2026-08-18
> 完成日期：2026-08-18
> 基线提交：`926e32e`
> 上位计划：[alpha 0.2 基础设施开发计划](../alpha-0.2-plan.md)
> 强制约束：[alpha 0.2 基础设施约束契约](../alpha-0.2-infrastructure-contract.md)
> 当前模块：[应用生命周期与会话作用域](../../infrastructure/application-lifecycle.md)

## 阶段目标

建立唯一、幂等、可关闭的应用宿主，使 QFramework 玩法架构、异步任务、场景对象和资源都具有明确所有者。当前 `Main.unity` 仍保持唯一启动场景，但不再依靠场景脚本的偶然执行顺序创建全局架构。

本阶段只建立运行时生命周期底座和当前新游戏路径，不实现存档序列化、本地化、通用场景流、输入重绑定、音频或后续错误日志系统。

## 完成摘要与实际偏差

- `ApplicationBootstrap` 已在 `BeforeSceneLoad` 创建唯一 `ApplicationHost`，并在 `SubsystemRegistration` 统一重置静态状态；Application / Profile / Session / Scene 四级作用域按计划落地，并补充了 Component 子作用域。
- `GameArchitectureProvider` 已成为 `GameArchitecture.Interface` 的唯一正式创建、访问和销毁入口；它为每个 Session 签发绑定架构、所有者作用域和单调 generation 的 lease，同时补齐 QFramework 初始化 / 反初始化异常安全，防止部分初始化失败污染下一代 Session。
- 当前场景仍保留 `CombatPrototypeBootstrap` 名称以兼容既有序列化引用，没有另建计划中的 `GameplaySceneBootstrap`；其职责已收缩为组装 `GameplaySceneConfiguration` 并提交 `NewGameSessionInitializer`。
- NewGame 已实现一次提交、外部取消和逐步回滚。Restore 初始化器没有提前实现，存档与读档仍由 `alpha 0.2.2` 负责。
- 场景卸载、直接重新加载 `Main` 和快速重复请求的协调进入当前宿主，并采用 latest-wins；`SceneSessionBinding` 让预置 UI / 世界组件只绑定同场景且已 Running 的有效 Session。没有建立 Boot / FrontEnd / Loading 状态机，因此不视为 `alpha 0.2.4` SceneFlow 提前完成。
- TaskGroup 只保留活动任务，并提供默认 10 秒停止时限和未结束 operation 诊断。超时进入不可逆 `Abandoned`，污点向祖先传播并关闭新工作入口；Session 受控停止后禁止创建下一代。迟到 continuation 的状态结果和失败回调被隔离，应急退出保留旧架构至下一次 Subsystem Registration 静态重置，避免旧任务误用新架构。
- 为闭合所有权合同，实际范围增加了 Prefab Addressables GUID 单飞与精确句柄释放、保存原始所有者并精确注销的 `SessionObjectRegistry` 以及架构异常安全测试；完整资源治理仍留给 `alpha 0.2.6`。

## 当前审计基线

### 架构创建与释放

- `Architecture<T>.Interface` 在首次访问时懒创建静态 `MArchitecture`，同时初始化全部 Model 和 System。
- `Architecture<T>.Deinit()` 会依次调用架构 `OnDeinit()`、System `Deinit()`、Model `Deinit()`，清空容器并将静态实例设为 `null`。
- 当前正式运行路径没有统一调用 `GameArchitecture.Interface.Deinit()`；只有部分测试自行在 SetUp / TearDown 中调用。
- `GameArchitecture.OnDeinit()` 当前释放世界排序、特效池、Sprite / Prefab Loader 和 `GameInput`，但它在各 System 的 `OnDeinit()` 之前执行。
- 当前有 23 个 Runtime 文件直接读取 `GameArchitecture.Interface`，22 个测试文件直接创建或销毁该静态架构。
- `DarkFlare.Core/QFramework.cs` 是稳定框架层，本阶段不修改其内部生命周期顺序。

### 场景启动事务

`CombatPrototypeBootstrap.Start` 当前按以下顺序直接执行：

1. 禁用刷怪器。
2. 配置随机根种子。
3. 预热玩家、技能、怪物、掉落物和物品图标资源。
4. 生成玩家并发放 / 装备初始武器。
5. 启用刷怪器。
6. 初始化商人、发放初始金币并初始化打造。
7. 绑定摄像机。

该流程同时包含场景配置、资源预热、新游戏数据写入和场景表现绑定；失败时没有统一结果或回滚事务。读档阶段若直接复用会重复发放武器、金币和商人库存。

### 异步与事件

- Runtime 当前有 9 个 `UniTaskVoid` 方法和 8 个 `.Forget()` 调用。
- Bootstrap、玩家自动攻击 / 复活、怪物死亡、刷怪、投射物销毁、特效回收、世界排序和资源恢复分别维护自己的取消逻辑。
- 玩家、刷怪器、世界排序和资源恢复共使用四组手工 `CancellationTokenSource`；它们没有统一父作用域。
- 多个场景 Controller 的 QFramework 事件订阅依赖 `OnDestroy`、`OnDisable` 或手工列表释放，因此场景作用域必须先于 Session 结束。
- `ResourceRegenerationSystem` 只有在架构 `Deinit()` 进入 System 清理阶段后才取消循环，不能依靠现有架构 `OnDeinit()` 顺序保证“先停任务、后放资源”。

### 当前可立即冻结的零基线

项目 Runtime 当前没有正式 `PlayerPrefs`、业务文件 IO 或业务场景加载调用。本阶段可以直接把这些行为冻结为零新增，无需建立历史例外。

## 范围边界

### 本阶段完成

- 应用、档案、Session、Scene 四级作用域和父子取消关系。
- 场景加载前创建的唯一 `ApplicationHost`。
- QFramework `GameArchitecture` 的唯一创建、访问和销毁入口。
- 当前本地默认档案和新游戏 Session 的兼容启动路径。
- 当前 Bootstrap 的配置、预热、新游戏写入、场景绑定职责拆分。
- 生命周期相关异步任务的统一观察、取消和等待。
- 重复启动、失败回滚、退出清理和无 Domain Reload 静态重置。
- 本阶段立即生效的本地策略测试及限期例外格式。

### 明确不做

- 不实现存档 DTO、槽位、文件写入和恢复数据；由 `alpha 0.2.1–0.2.2` 完成。
- 不建立 Locale、String Table 或迁移玩家可见字符串；由 `alpha 0.2.3` 完成。
- 不新增前台页、Loading UI、通用 Scene Flow Service 或额外场景；由 `alpha 0.2.4` 完成。
- 不重构 `Time.timeScale`、输入绑定、音频或平台挂起；由 `alpha 0.2.4–0.2.5` 完成。
- 不在本阶段全量替换 `Debug.Log*` 或重组 Addressables 分组；由 `alpha 0.2.6` 完成。
- 不修改 `QFramework.cs`，不扩展玩法内容，不调整战斗和经济数值。

## 冻结架构决策

### 1. 宿主创建方式

- 使用唯一静态 Bootstrap 在 `BeforeSceneLoad` 创建 `ApplicationHost` GameObject。
- `ApplicationHost` 是唯一由项目代码使用 `DontDestroyOnLoad` 的对象；重复实例只保留现有宿主并安全销毁新实例。
- 使用 `SubsystemRegistration` 重置项目自有静态入口，兼容关闭 Domain Reload 的测试环境。
- QFramework 自带的 Runtime Initialize 与场景卸载辅助对象列为框架例外，不计入项目宿主重复。
- 本阶段不新增启动场景。`alpha 0.2.4` 可以在不改变宿主公共契约的前提下改由前台场景驱动 Session。

### 2. QFramework 归属 Session

- `GameArchitecture` 属于 Session，不属于 Application，也不由任一场景 Controller 拥有。
- `GameArchitectureProvider` 是唯一允许访问 `GameArchitecture.Interface` 的生产入口。
- Provider 的 `StartSession()` 触发 QFramework 创建并缓存当前 `IArchitecture`；`RequireCurrent()` 只返回已存在架构，禁止懒创建。
- Provider 的 `StopSession()` 只允许当前 Session 所有者调用，负责且只负责一次 `Deinit()`。
- Session 未创建或已经停止时，Controller 请求架构必须得到包含当前生命周期状态的明确异常，不能静默创建新实例。
- 23 个 Runtime 直接访问文件统一改为从 Provider 获取；`IController.GetArchitecture()` 的 QFramework 使用方式保持不变。
- 22 个既有测试文件统一通过测试 Session Fixture 获取和释放架构，避免测试绕过 Provider 造成双重所有权。

### 3. 四级作用域

| 作用域 | 本阶段真实消费者 | 创建时机 | 结束条件 |
| --- | --- | --- | --- |
| Application | ApplicationHost、根状态、根任务组 | 场景加载前 | 应用退出或宿主销毁 |
| Profile | 当前本地默认档案、Session 父令牌 | Application Ready 后 | 切换档案或应用退出 |
| Session | GameArchitecture、资源恢复、玩法 Model / System | 当前原型启动前 | 离开游戏、初始化失败或应用退出 |
| Scene | Main 配置、场景 Bootstrap、玩家 / 怪物 / UI 任务 | Main 场景绑定 | 场景卸载或 Session 先行关闭 |

- 本阶段的 Profile 仅是具有稳定生命周期的本地默认档案，不含持久数据；`alpha 0.2.2` 将其替换为实际槽位。
- 每个子作用域使用父令牌创建 linked token，停止时按 `Scene → Session → Profile → Application` 顺序取消。
- 每个作用域只能结束一次；重复 Stop 返回已完成结果，不重复释放。

### 4. 当前场景兼容启动

- `ApplicationHost` 在 `Main` 场景加载前创建默认 Profile 和待初始化 Session，因此场景 Controller 的 `Awake` 不会触发架构懒创建。
- 场景中的 Bootstrap 只负责提交一份 `GameplaySceneConfiguration` 并绑定 Scene 作用域。
- 当前只注册 `NewGameSessionInitializer`。未来读档通过新增独立 initializer 接入，同一 Session 只能选择一个 initializer。
- 不创建“空实现读档”或在新游戏初始化器中加入 `if (isLoad)` 分支。
- 本阶段公开的测试停止 / 重启能力不等于新增玩家可见的退出或读档 UI。

### 5. 异步任务所有权

- `LifecycleScope` 拥有 CancellationToken 和 `LifecycleTaskGroup`。
- `LifecycleTaskGroup` 只接收返回 `UniTask` 的委托，统一捕获完成、取消和异常，并可在 Stop 时限时等待。
- Session 任务必须链接 Session Token；场景和组件任务必须同时链接 Scene Token 与组件销毁 Token。
- 本阶段将 9 个 `UniTaskVoid` 改为可观察 `UniTask`，移除 8 个裸 `.Forget()`。
- Bootstrap 初始化和资源恢复归 Session / Scene 任务组；玩家、刷怪、世界排序、投射物和特效归 Scene 或其组件子作用域。
- 正常取消不记录为失败；非取消异常必须上报到 ApplicationHost 并触发所属初始化事务失败或受控停止。

## 状态契约

### Application 状态

| 当前状态 | 操作 | 下一状态 | 规则 |
| --- | --- | --- | --- |
| `None` | Boot | `Booting` | 只允许唯一宿主执行 |
| `Booting` | Boot 成功 | `Ready` | Profile 和待初始化 Session 已可用 |
| `Booting` | Boot 失败 | `Failed` | 释放所有已创建子作用域 |
| `Ready` | Shutdown | `ShuttingDown` | 立即拒绝新请求 |
| `Failed` | Shutdown | `ShuttingDown` | 只清理已创建部分 |
| `ShuttingDown` | 清理完成 | `Shutdown` | 不允许重新进入 Ready |

### Session 状态

| 当前状态 | 操作 | 下一状态 | 规则 |
| --- | --- | --- | --- |
| `None` | Create | `Created` | 创建 Session Scope 与 GameArchitecture |
| `Created` | Initialize | `Initializing` | 只接受一个 initializer |
| `Initializing` | Commit | `Running` | 新游戏事务全部成功后一次提交 |
| `Initializing` | 取消 / 失败 | `RollingBack` | 禁用刷怪并撤销完整 Session |
| `Running` | Stop | `Stopping` | 先关闭 Scene 再关闭 Session |
| `RollingBack` / `Stopping` | 清理完成 | `None` | Provider 中不再存在当前架构 |

- 同一状态上的重复 Boot / Initialize / Stop 必须有确定结果。
- 初始化期间收到第二个请求时返回 `OperationInProgress`，不排队、不交错执行。
- 已经进入 `ShuttingDown` 后的请求统一返回 `ApplicationShuttingDown`。

## 新游戏初始化事务

### 配置输入

`GameplaySceneConfiguration` 保留当前场景已有引用：玩家定义、默认技能、初始武器、怪物生成定义、掉落物引用、商人定义、打造定义、初始金币、玩家出生点、MonsterSpawner 和 CameraFollowTarget。

运行前验证所有最小循环必需引用。出生点可以回退到 Bootstrap Transform；玩家、技能、初始武器、刷怪定义、掉落物、商人、打造、刷怪器和摄像机跟随组件缺失均阻止事务进入 Running。

### 提交顺序

1. 验证场景配置并绑定 Scene Scope。
2. 保持 MonsterSpawner 禁用，阻止初始化中产生玩法写入。
3. 配置 GameplayRandomSystem。
4. 并行预热 SpawnSystem、LootSystem 和 SpriteAssetLoader 所需资源。
5. 生成玩家。
6. 事务性发放并装备初始武器。
7. 初始化商人、发放初始金币并初始化打造。
8. 绑定摄像机目标。
9. 启用 MonsterSpawner。
10. 将 Session 从 `Initializing` 一次切换为 `Running`。

### 失败回滚

- 任一步失败后立即保持 / 恢复刷怪器禁用。
- 取消 Scene Scope，等待已登记场景任务结束。
- 销毁或释放已经生成的玩家、怪物、掉落物、投射物和场景实例。
- 取消 Session Scope，等待 Session 任务结束。
- 调用 Provider `StopSession()`，由 `GameArchitecture.Deinit()` 清理 Model、System、Input 和 Loader。
- 清空当前 Scene / Session Access，返回结构化失败码和原始异常。
- 回滚本身再次失败时保留首个失败为主因，并附加清理错误；不得把半初始化 Session 标为 Running。

## 关闭顺序

1. Application 状态切为 `ShuttingDown`，拒绝新的初始化请求。
2. 禁用刷怪器和所有新的玩法事务入口。
3. 取消 Scene Scope，等待场景 / 组件任务结束并解除场景绑定。
4. 取消 Session Scope，等待资源恢复等 Session 任务结束。
5. 由 Provider 唯一调用 `GameArchitecture.Deinit()`；此时异步消费者已经停止。
6. 清空 Session、结束 Profile Scope。
7. 取消 Application Scope，等待根任务结束。
8. 清空项目自有静态引用并销毁宿主。

该顺序用于规避 QFramework 固定的“架构 OnDeinit 早于 System OnDeinit”顺序，不修改框架内部实现。

## 预计代码结构

```text
Assets/Scripts/Runtime/
  Infrastructure/Lifecycle/
    ApplicationBootstrap.cs
    ApplicationHost.cs
    LifecycleModels.cs
    LifecycleScope.cs
    LifecycleTaskGroup.cs
    GameSessionHost.cs
    GameArchitectureProvider.cs
  Gameplay/Bootstrap/
    GameplaySceneBootstrap.cs
    GameplaySceneConfiguration.cs
    NewGameSessionInitializer.cs

Assets/Scripts/Tests/EditMode/
  ApplicationLifecycleTests.cs
  InfrastructurePolicyTests.cs
  InfrastructurePolicyExceptions.json

Assets/Scripts/Tests/PlayMode/
  ApplicationLifecyclePlayModeTests.cs
```

- 具体文件可以在实现中合并，但不得合并 Application / Session / Scene 所有权或重新引入场景脚本懒创建架构。
- 测试 Session Fixture 可以按测试程序集分别提供；正式 Runtime 不加入只为测试服务的公开后门。
- 完成后建立 `Docs/docs/infrastructure/application-lifecycle.md`，本执行计划归档。

实际实现还增加了 `ComponentLifecycle.cs` 与 `SessionObjectRegistry.cs`；`GameplaySceneBootstrap.cs` 则由保留序列化兼容名的 `CombatPrototypeBootstrap.cs` 承担。测试另增加 `ArchitectureExceptionSafetyTests.cs` 与 `PrefabAssetLoaderTests.cs`。

## 本阶段立即生效的规范验证

| 规则 | 允许位置 | 验证方式 |
| --- | --- | --- |
| `GameArchitecture.Interface` 创建 / 销毁 | `GameArchitectureProvider` | Runtime 调用点扫描 |
| 项目 `RuntimeInitializeOnLoadMethod` | `ApplicationBootstrap`；QFramework `ComparerAutoRegister` 为框架例外 | Runtime 调用点扫描 |
| 项目 `DontDestroyOnLoad` | `ApplicationHost`；QFramework 场景卸载辅助对象为框架例外 | Runtime 调用点扫描 |
| `PlayerPrefs` | 无 | Runtime 零调用扫描 |
| 业务 `File` / `Directory` | 无 | Runtime 零调用扫描；QFramework / Editor 测试例外 |
| 业务 `SceneManager.Load*` / `Unload*` | 无 | Runtime 零调用扫描；QFramework 场景卸载辅助例外 |
| `UniTaskVoid` / 裸 `.Forget()` | 无 | Runtime 零调用扫描 |
| 手工长期 `CancellationTokenSource` | `LifecycleScope` / 组件子作用域实现 | Runtime 调用点扫描 |

### 例外格式

`InfrastructurePolicyExceptions.json` 的每项例外必须包含：

- `ruleId`
- `path`
- `reason`
- `removeByStage`

QFramework 框架文件可以作为长期外部框架例外；项目业务代码例外必须指定本版本内移除阶段。`Debug.Log*`、`Time.timeScale` 和直接 Addressables 规则在各自后续阶段启用，本阶段不建立大规模历史白名单。

## 执行切片

### A. 特征测试与纯生命周期合同

- 为 QFramework 静态创建 / Deinit、现有资源释放和冷启动行为补特征测试。
- 实现纯状态模型、LifecycleScope、TaskGroup 和结构化结果。
- 覆盖重复 Boot、重复 Stop、初始化中并发请求、父子取消顺序和部分创建失败。

### B. ApplicationHost 与 Session Provider

- 建立场景加载前宿主和静态重置。
- 建立默认 Profile、待初始化 Session 和 GameArchitectureProvider。
- 将 23 个 Runtime 直接访问文件迁移到 `RequireCurrent()`。
- 将 22 个既有测试迁移到统一测试 Session Fixture。

### C. 新游戏事务拆分

- 将场景序列化引用集中到 GameplaySceneConfiguration。
- 把预热与新游戏数据写入迁入 NewGameSessionInitializer。
- GameplaySceneBootstrap 只绑定 Scene Scope、提交 initializer 和显示结构化结果。
- 为每个失败点补回滚测试，确保刷怪器只在最终 Commit 后启用。

### D. 异步所有权迁移

- 迁移 9 个 `UniTaskVoid` 和 8 个裸 `.Forget()`。
- 将四组手工长期 CTS 纳入 Session / Scene / Component 作用域。
- 验证场景销毁、Session Stop 和应用关闭都先取消并观察任务，再释放资源。

### E. 场景迁移与规范验证

- Unity Editor 可连接时使用 Unity CLI / Editor API 更新 `Main.unity` 的 Bootstrap 组件和引用，不手改场景 YAML。
- 保留现有序列化配置值和最小循环行为。
- 建立策略扫描与例外文件，复跑直到零未登记违规。

### F. 综合回归与文档

- 运行全量 EditMode / PlayMode。
- 验证冷启动、初始化中取消、失败回滚、重复 Session、退出 Play Mode 和再次进入 Play Mode。
- 更新项目结构、玩法循环和生命周期模块文档。
- 将本阶段状态改为完成并归档执行计划。

## 测试矩阵

### EditMode

- Application / Session 所有合法和非法状态转换。
- LifecycleScope 父子取消顺序、幂等 Stop 和 TaskGroup 异常聚合。
- Provider 未启动访问、重复启动、唯一 Deinit 和停止后重新创建。
- NewGame initializer 固定顺序、一次提交和各步骤失败回滚。
- InfrastructurePolicyTests 对每条禁止调用的正反样本。
- 既有 22 个测试文件使用 Fixture 后保持原断言结果。

### PlayMode

- 冷启动 Main 时只存在一个 ApplicationHost、一个当前 GameArchitecture 和一个玩家。
- 初始武器、金币、商人库存和打造配置只初始化一次。
- 初始化未完成时 MonsterSpawner 保持禁用；成功后才启用。
- 预热期间销毁 Scene Bootstrap 时任务取消，零玩家、零刷怪和零悬挂句柄。
- 缺少必需配置时进入失败 / 回滚，不留下半初始化 Session。
- 连续三次测试 Session 创建 / 停止后，事件、输入 Action、任务和架构实例不重复。
- 退出 Play Mode 再进入时，项目自有静态宿主和 Provider 不保留旧引用。

### 回归

- 当前战斗 → 掉落 → 拾取 → 装备 → 商店 / 打造 → 再战斗流程保持可玩。
- 键鼠与手柄现有输入模式切换保持一致。
- 当前固定随机种子序列不因生命周期重构改变。
- 全量 EditMode 零失败；全量 PlayMode 零失败，既有 Input System 忽略项按当前上游条件记录。
- Unity Console 无新增错误，`Main.unity` 和 Prefab 引用完整。

## 最终验证记录

- Unity 脚本编译：0 error。
- EditMode：`255/255` 通过。
- 项目自有 PlayMode：`45/45` 通过；完整运行共 `49` 项，`47` 项通过、`0` 失败，另有 `2` 项 Input System 包集成测试因上游 issue 1252825 按既有标记跳过。
- 最终修复后两次真实 Play 均达到 Application `Ready`、Session `Running` 且架构 lease 有效，`ApplicationHost`、玩家、已提交刷怪器每次各 `1` 个。
- 两次真实 Play 退出后的 Console Error 均为 0。
- 直接重新加载 `Main`、连续三个快速请求 latest-wins、同场景组件重绑、旧 Registry 精确注销和旧 generation 迟到 continuation 隔离合同通过。
- 自动覆盖还包括初始化取消 / 回滚、挂起回滚有界收敛、并发初始化 / 停止、连续 Session、后代 Abandoned 污点、作用域停止超时诊断、受控停止 / Shutdown / Emergency 终态隔离、架构异常安全、Prefab Addressables 单飞 / 精确句柄、场景三态重绑、对象注销和策略扫描。新增专项为 `AbandonedGenerationIsolationTests.cs`、`ApplicationHostSceneTransitionPlayModeTests.cs`、`SceneSessionComponentBindingPlayModeTests.cs` 与 `SessionObjectRegistryOwnershipPlayModeTests.cs`。

## 完成定义

- ApplicationHost 是项目唯一应用宿主，场景加载前创建，关闭后不留项目静态状态。
- GameArchitecture 只由 GameArchitectureProvider 创建和 Deinit，Runtime 零绕过调用；所有 Session 操作验证精确 lease / generation，旧代不得触碰新代架构。
- Application / Profile / Session / Scene 四级核心作用域及 Component 子作用域进入正式代码和测试，关闭顺序为 `Component → Scene → Session → Profile → Application`。
- 当前新游戏初始化为可取消、可回滚、一次提交的独立事务；未来读档无需复用初始发放逻辑。
- 当前单场景快速请求遵守 latest-wins，场景预置组件只绑定同场景 Running Session，`SessionRunning` 对每个有效 generation 只通知一次。
- `Abandoned` 保持不可逆，迟到 continuation 不改写终态、不触发结束作用域回调；运行时对象只向登记时的原始 Registry 注销。
- Runtime 零 `UniTaskVoid`、零裸 `.Forget()`，长期任务全部属于明确作用域。
- 本阶段启用的策略扫描零未登记违规，例外均有原因和移除阶段。
- Main 最小循环、全量测试和再次进入 Play Mode 回归通过。
- 生命周期模块文档已建立，总计划与文档索引同步，本计划完成后归档。
