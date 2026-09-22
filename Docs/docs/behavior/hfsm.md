# HFSM 核心

实现位于 `Assets/Scripts/Runtime/Core/HFSM/`，命名空间 `DarkFlare.HFSM`，归现有 `DarkFlare.Core` 程序集。仅引用 .NET，不依赖行为树、Unity、Runtime 或 Editor。

## 构建与所有权

`HfsmBuilder<TContext>` 添加状态工厂、父关系、初始子状态和转移，`Build()` 冻结为可共享的 `HfsmDefinition<TContext>`。示例中 `IdleState`、`WorkingState` 和 `RootState` 均由调用方实现 `IHfsmState<WorkContext>`：

```csharp
HfsmDefinition<WorkContext> definition = new HfsmBuilder<WorkContext>()
    .AddState("root", () => new RootState())
    .AddState("idle", () => new IdleState(), "root")
    .AddState("working", () => new WorkingState(), "root")
    .SetInitialChild("root", "idle")
    .AddTransition("idle", "working", context => context.HasWork, reason: "work available")
    .AddTransition("working", "idle", context => !context.HasWork)
    .Build();
HfsmInstance<WorkContext> machine = definition.CreateInstance(context);
HfsmResult started = machine.Start();
// 所属调度器检查结果，并在允许模拟时传入时间步。
HfsmResult step = machine.Tick(deltaTime);
HfsmResult stopped = machine.Stop();
```

每个实例独占处理器、活动路径和诊断缓冲。工厂在创建实例及 Reset 时执行，必须返回全新的非空处理器；同实例不同状态、不同实例之间共享同一处理器会被拒绝。弱引用登记不保留处理器或场景所有者。工厂仅构造对象，资源在 Enter 取得、Exit 清理；工厂异常直接抛给调用方。调用方须分别提供上下文，Guard 和工厂不得捕获跨实例共享的可变业务数据。

Build 不执行 Guard 或工厂。它拒绝重复 ID、未登记引用、父子环、缺少或多个根、非法初始子状态、不可达状态及容量超限。可达性按初始路径和转移边检查，不推测 Guard 是否可能成立。默认上限为 256 状态、1024 转移、32 层，可在 Builder 构造时指定正数；根深度为 0，MaximumDepth 为所需路径容量。

## 执行语义

状态实现 `Enter(context)`、`Tick(context, deltaTime)`、`Exit(context, reason)`。Exit 原因区分 Transition、Stopped、Faulted。实例由单个调用方串行驱动，不提供线程安全或异步调度。

1. Start 进入根并沿初始子状态到叶，不执行 Tick。
2. 有效 Tick 只检查当前活动路径来源的转移：优先级从高到低，同优先级父状态优先，再按声明顺序。Guard 应无副作用，每个候选至多判定一次。
3. 首个成立且实际生效的转移退出旧路径至最低共同祖先，再进入目标及其初始子链。每步最多提交一次；公共父状态保留。
4. 转移后的路径由根到叶各 Tick 一次。回调导致的业务变化留到下一步判断。

目标已是当前叶，或来源与目标为同一个活动复合状态时，默认 Ignore，不增加转移计数并继续检查其他候选。指定 `HfsmSelfTransition.Reenter` 后，目标已活动则连同其活动子树退出并重新进入；外层公共祖先保留。转向其他复合状态时按其初始子链进入。

零时间步、未启动、已停止和故障状态不推进模拟。负数、NaN、Infinity 直接抛参数异常，不改变执行状态。重复 Start 拒绝，Stop 幂等并从叶到根退出。正常 Stop 后可 Start 回到初始路径，继续使用原处理器和累计统计；处理器应在 Enter 初始化单次行为数据。需要全新处理器和清零统计时，在 Stop 后显式 Reset。Reset 不修改调用方上下文。

## 故障与清理

Enter、Tick、Guard 或 Exit 抛异常时，返回 `HfsmResult` 的 Faulted 及 `HfsmFault`，尝试由叶到根清理全部已进入状态。即使 Enter 只执行了一部分，也配对一次 Exit；抛错的 Exit 不重复执行。失败进入不增加成功转移计数，清理后活动路径为空，不再运行旧行为。

`Fault.Cause` 保留触发异常，`CleanupErrors` 保留退出失败，`CleanupSucceeded` 表示退出回调是否均成功。调用方必须检查结果并上报故障。清理成功后才允许 Reset 恢复；退出失败的实例拒绝重用，由所属作用域处理残留资源后创建新实例。核心不回滚回调已经产生的外部副作用。

回调内禁止重入 Start / Tick / Stop / Reset、诊断租约操作或读取活动路径快照；异常未被回调自行处理时同样进入 Faulted。外部注销应先撤销动作许可，再在 Tick 边界调用 Stop。回调必须自行保持有界，核心不能抢占阻塞的用户代码。

## 只读诊断

定义公开只读 `States`、`Transitions`，包含稳定 ID、父 / 初始子索引、优先级、自转移策略与 Reason。实例提供 Status、Fault、TickSequence、ElapsedSeconds、TransitionCount、LastTransitionIndex；无活动叶或无已提交转移时相应索引为 -1。

`CopyActivePath(Span<int>)` 按根到叶复制到调用方缓冲。详细诊断通过 `AcquireDiagnostics()` 的 IDisposable 租约开启：

- `CopyGuardObservations` 按定义转移索引复制判定结果。TickSequence 为 0 表示未采集，不等于当前步表示旧结果；读取不会重算 Guard。
- `CopyTransitionTrace` 从旧到新复制最近成功转移，包含时间步、转移索引、前后叶索引。容量由 CreateInstance 的 traceCapacity 指定，默认 64；满后覆盖最旧记录。
- 多个租约独立释放，重复 Dispose 安全；最后一个释放时停止详细采集并清空记录，保留缓冲供下次重用。Reset 清空记录但保留租约。关闭诊断时两项复制返回 0。

复制只在执行边界可用，缓冲不足会抛参数异常，不向外暴露内部可变数组。构建、创建、Reset、首次启用诊断、租约和异常可以分配；预热后的正常 Tick、状态切换和缓冲复制不分配。此 API 是后续可视化的数据基础，当前没有 Editor 图窗口或 Player 面板。

长期回归位于 `Assets/Scripts/Tests/EditMode/HfsmTests.cs`，结果与测量边界见[F1 验收](acceptance/f1-hfsm.md)。
