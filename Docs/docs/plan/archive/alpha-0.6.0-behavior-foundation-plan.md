# Alpha 0.6.0：轻量行为基础

状态：2026-09-22，F1–F6 全部完成，交付 `0.6.0-alpha`。上位范围见[总计划](../alpha-0.6-plan.md)。本阶段交付通用基础与可观察样例，不提前接管正式怪物与刷怪规则。

## 下一步入口

2026-09-22：上位计划已提交为 `de60427`，完成 0.6.0 细化后获用户授权开始执行。F1 已在现有 Core 中交付独立 HFSM，编译通过，EditMode **35/35**，诊断开 / 关的预热切换均通过零分配验证。实际 API 见[模块文档](../../behavior/hfsm.md)，验证范围见[F1 记录](../../behavior/acceptance/f1-hfsm.md)。

- [核心 API 与执行语义](alpha-0.6.0/behavior-core-contract.md)：明确两模块的定义 / 实例、HFSM 转移顺序、BT 中断 / 时间 / 预算、诊断与异常清理。
- [接入顺序与验收](alpha-0.6.0/behavior-integration-validation.md)：基于现有代码拆分 F1–F6，明确隔离样例、Animator 唯一入口、运行时调试与测试矩阵。

F2–F6 已完成：独立 BT、组合样例、Animator 与实际可视化；完整 EditMode 788/788、PlayMode 87/87，独立开发 / 普通 Windows Player 和核心规模采集通过。见[阶段验收](../../behavior/acceptance/0.6.0/README.md)。下一阶段为 0.6.1 普通怪物。

## 合同

- HFSM：泛型上下文、显式父子关系与初始子状态；Enter / Tick / Exit 生命周期。父级紧急转移（死亡 / 禁用）配置最高优先级，同优先级父级优先，再按稳定声明顺序选一次转移；沿最低共同祖先退出 / 进入。自转移必须显式指定重入或忽略，不能模糊处理；停止逆序退出，禁止回调内重入转移。复合状态无初始子状态、未知节点、重复 ID、环路在构建时拒绝。
- 行为树：Success / Failure / Running，动作、条件、记忆序列、反应式优先选择、有限等待及必要条件装饰器；高优先行为抢占时先 Abort 旧 Running 分支，再进入新动作。停止和重入重置节点状态，每次进入对应且仅对应一次退出 / 取消。每 Tick 有节点访问上限，超限报告而不是卡住主线程；首版不做并行节点与无限循环装饰器。
- HFSM 与行为树是两个独立模块，放入现有 `DarkFlare.Core` 程序集，各自可单独使用、测试，不相互引用；不另建行为核心程序集。核心只依赖 .NET 基础类型，不引用 UnityEngine、Animator、怪物或场景。不可变定义允许共享，节点运行态归单个实例，不在共享配置上保存游标与计时。上下文采用类型化数据和能力接口，不能通过全局查找玩家或访问单例绑定到某一类 Actor。
- 诊断提供稳定节点 ID、父子边、活动路径、节点状态与有界转移记录；默认不在每 Tick 创建字符串或复制整棵树，按需采集供运行中 Editor 窗口查看。
- 动画桥接消费行为语义消息 / 战斗事件；动画参数只在表现端映射。行为完成由领域计时与结果决定，动画缺失、禁用或切换不阻塞模拟。ActionInstanceId 与 generation 过滤迟到动画回调；可选动画标记只提供表现同步，不能重复结算命中。

## 分层与调度

目录与程序集边界固定为：

| 模块 | 目录 | 所属程序集与依赖 |
| --- | --- | --- |
| HFSM 核心 | `Assets/Scripts/Runtime/Core/HFSM/` | `DarkFlare.Core`；状态、层级、转移、执行实例与自身诊断合同 |
| 行为树核心 | `Assets/Scripts/Runtime/Core/BehaviorTree/` | `DarkFlare.Core`；节点、组合 / 装饰、执行实例、中断与自身诊断合同 |
| 玩法及组合适配 | `Assets/Scripts/Runtime/Behavior/` | `DarkFlare.Runtime` → `DarkFlare.Core`；感知、动作、HFSM 内运行树的适配、QFramework 接入 |
| Animator 桥接与开发面板 | 现有 Runtime 表现 / UI 目录 | `DarkFlare.Runtime`；消费核心诊断与玩法语义，不将 Unity 类型引入核心 |
| Editor 可视化 | `Assets/Scripts/Editor/Behavior/` | `DarkFlare.Editor` → Runtime / Core；只读展示与调试工具 |

HFSM 不保存行为树专有引用，行为树不要求状态机宿主；需要在状态中执行树时，由 Runtime 适配器负责 Tick / Abort 转发。Core 中只保留各模块通用状态与诊断信息；Actor 身份、地图代次、战斗语义、存档 DTO、ScriptableObject、UniTask 调度及 Animator 参数映射均归适配层。共处 Core 不要求核心算法使用 QFramework 的全局架构入口。

当前 `DarkFlare.Core.asmdef` 已存在且无显式程序集引用，Runtime 已引用 Core。本次不改变程序集文件；实施时也不为这两个模块在子目录新增 asmdef。Core 现有其他代码允许 Unity 引用，因此不通过修改整个程序集的 `noEngineReferences` 来强制迁移无关代码；新增两模块保持自身纯 C# 边界。

| 层 | 拟定职责 |
| --- | --- |
| Core / HFSM | 状态定义、层级、运行实例、生命周期、转移及只读诊断 |
| Core / 行为树 | 节点定义、树运行实例、执行结果、中断及只读诊断 |
| Model | Actor 稳定身份对应的行为运行态与只读快照，不持有编辑器对象 |
| System | 感知 / 决策的批量调度、动作请求、状态门禁、实例注销和领域事件 |
| Command / Query | 注册 / 取消、行为控制与快照查询；Controller 不直接修改 Model |
| Controller / 表现桥 | 绑定 Actor / Animator、订阅语义事件、显示调试，不反向决定 AI 状态 |
| 配置与 Editor | ScriptableObject / Odin 参数及验证、运行图可视化；配置资产不持有运行游标 |

由 MapScope 统一拥有调度和取消，顺序为收集感知 → 行为决策 → 动作许可 / 请求 → 导航求解与唯一速度提交 → 表现事件消费。反应 / 感知频率可配置并错峰，攻击计时使用统一模拟时间；死亡、暂停、离场和 Session 结束立即取消，不等待低频感知轮询。

纯内存动作优先用有界 Tick 状态，不给每个节点创建 UniTask。确需异步的叶节点持有归属 Actor / Map 的 token、动作序号及完成回执；Abort 后释放订阅 / 资源，只接受当前动作代次回执。异常上报既有日志 / 生命周期渠道，进入安全停止，不能吞掉后继续造成伤害。

## Animator 桥接

1. Runtime 适配层定义 Idle / Move / AttackWindup / Attack / Recovery / PhaseChange / Dead 等玩法语义，配置映射到已有 Animator 参数及可选状态；Core 只提供通用状态 / 节点结果，不内置怪物动作枚举。
2. 调整现有 `ActorAnimatorController` 的职责，复用已有受伤 / 死亡事件，防止两个组件重复发 Attack Trigger。移动表现消费最终速度，不以 Animator Root Motion 绕过导航。
3. 桥接表校验 Animator 参数是否存在、类型是否匹配；缺失参数只在表现侧报告有界诊断，不修改 AI 结果。
4. 控制解除、禁用、对象重用与换 Session 后清理 Trigger 和旧订阅。攻击被打断后不得由旧动画完成通知恢复移动或扣血。

## 运行时可视化

- Editor Play 模式：选择 Actor，显示 HFSM 父子结构与活动路径、行为树拓扑与 Running / Success / Failure / Aborted 标记、转移条件结果、最近转移原因、动作进度、关键上下文。不同状态同时采用颜色与文字标记。
- SceneView：显示激活圈、视野扇形 / 遮挡射线、攻击范围、出生锚点与导航目标；复用现有路径诊断，避免重复绘制和查询整场角色。
- Development Player：提供默认关闭的只读诊断面板，至少显示选中 Actor 的状态路径、活动节点、动作阶段与触发批次；键鼠 / 手柄可打开、选择和关闭。常规发布构建不启用诊断入口。
- 调试只读快照按需、限频采样，有界环形记录；关闭窗口不停止 AI，切图后不显示旧对象数据。UI 不直接读取可变集合；先复用 Odin 工具约定，拓扑绘制可用 UI Toolkit 专用区域，通过 Unity 工具验收实际布局。
- 首版不做行为图拖拽编排、远程调试协议或任意运行态热编辑；不实现会使单个 Actor 绕过世界时间的“单步”按钮。

上述为 Alpha 0.6 各阶段共同使用的调试合同。0.6.0 验收真实样例的图、计时和动作，感知 / Boss / 触发批次数据随对应功能阶段接入；具体边界见[接入与验收方案](alpha-0.6.0/behavior-integration-validation.md)。

## 工作与验证

1. 在 Core 内分别实现两模块并扩展现有 EditMode 测试：层级顺序、父级抢占、无效图拒绝、Running 延续、中断一次、实例隔离与停止后重启。核心用例仅使用对应模块和普通 C# 上下文，分别验证单独运行；组合适配、Animator 和地图生命周期另做集成测试。检查 Core 不引用 Runtime / Editor，两个核心模块不互相依赖。
2. 提供可运行的最小 NPC 巡逻 / 等待样例，组合 HFSM 与树；另用不含 Unity 对象的上下文验证核心复用。样例不接入正式商人逻辑。
3. 桥接真实 Animator：正常、缺失参数、禁用、打断、迟到回调；角色行为结果应相同，表现变化可观察。
4. 创建运行中调试窗口与开发 Player 诊断，显示样例真实状态，不以静态图或日志替代可视化；窗口关闭及 Session 退出不残留引用。
5. 性能探针验证预热后纯 Tick 成本和分配，异常 / 高频切换 / 中断有界。编译、核心 EditMode 与必要 PlayMode / Player 验证通过后才能标记阶段完成。

预计长期测试按 `Behavior` 模块管理，覆盖子树退出、中断一次、错误时资源清理、重用隔离、时间门禁与语义桥接，不测试固定节点数量或硬编码布局。临时视觉验收脚本完成后删除，截图与结果归档到 `Docs/docs/`。
