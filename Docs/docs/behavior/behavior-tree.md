# 行为树

行为树位于 `Assets/Scripts/Runtime/Core/BehaviorTree/`，属于现有 `DarkFlare.Core`。只依赖 .NET，不引用 HFSM、Unity、Animator 或玩法程序集；可独立创建和 Tick。

## 定义与实例

通过 `BehaviorTreeBuilder<TContext>` 添加节点，再 `Build(rootId)` 得到可共享的不可变定义。`CreateInstance(context)` 创建独立的动作处理器、游标、计时和诊断缓冲。动作工厂必须每次返回新实例；复用处理器会被拒绝，不能把运行态放在共享定义或 ScriptableObject 上。

| 节点 | 执行规则 |
| --- | --- |
| Action | `IBehaviorAction<TContext>.Enter / Tick / Exit`，Tick 返回 Success、Failure 或 Running |
| Condition | 当次判断，直接成功或失败 |
| Wait | 从首次访问节点开始计算截止时间；采用实例时钟，不重复消费同一 Tick 的 dt |
| MemorySequence | 已成功子节点不重复执行，Running 保留游标；失败立即结束 |
| PrioritySelector | 每步重新按声明顺序判断分支入口条件；优先分支抢占前先 Abort 原分支；失败继续后续分支 |
| Guard | 条件失败时中断正在运行的子树，条件成功时执行子树 |

定义拒绝重复 / 未知 ID、环路、多父节点、不可达节点、空组合节点，以及超过节点数、深度或访问预算的图。默认上限分别为 256、32、1024，可由 Builder 显式配置；构建阶段先验证保守最坏访问量。

## 生命周期与时间

`Start()` 开始一轮；第一次动作 Enter 发生在 Tick。`Tick(deltaTime, visitBudget = 0)` 推进统一非负有限时钟，0 预算使用定义预算；调用者可提供更严格的单次上限。预算计入节点与选择器分支判断，超限转 Faulted，不延续半步动作。

Success / Failure 为终态，需要显式再次 Start 才重跑。`Abort()` 只退出已进入的节点，重复调用无额外 Exit。`Reset()` 重新创建动作处理器；上一次异常清理失败时拒绝复用。重入执行被拒绝。

异常保留原始原因，并独立按深度逆序清理已进入节点；某个 Exit 抛错仍继续清理其他节点，已经尝试的 Exit 不会重试。清理不再消耗已耗尽的 Tick 预算。故障路径允许分配，不能与正常 Tick 的零分配目标混淆。

## 诊断

`AcquireDiagnostics()` 返回可释放租约，首次观察才分配条件记录与有界轨迹。最后一个租约释放后停采；再次打开复用容量。通过 `CopyObservations`、`CopyTrace` 复制到调用者提供的 Span；定义提供稳定 ID 与父子拓扑。

节点 Guard 与选择器分支入口条件分别记录，携带 Tick 序号；过期结果不能当作本步结论。诊断读取不会再次执行 Predicate，也不会影响分支选择。普通 Tick 只写预分配内存。

长期回归见 `BehaviorTreeTests`：共享定义隔离、Running / 抢占 / Abort、时间、预算、无效图、故障清理及诊断开关分配。组合与实际使用见[接入和调试](integration-debugging.md)。
