# 行为接入、Animator 与调试

## 模块边界

两个 Core 模块可分别独立使用。Runtime 的 `HfsmBehaviorTreeState<TContext>` 在状态 Enter / Tick / Exit 中转发树的 Start / Tick / Abort；组合依赖只存在于 Runtime。

`BehaviorModel` 保存实例注册；`BehaviorSystem` 接受显式作用域 Tick，通过 Command / Query / Event 对外工作。Handle 包含架构 generation、作用域和注册序号，动画观察另带单调动作序号。注销立即撤销授权，Tick 边界再移除和停止，防止回调内注销跳过下一个实例。全局暂停和实例暂停不推进模拟计时。

`PatrolBehaviorRuntime.Template` 共享 HFSM / BT 定义和配置标量，每个实例自有上下文、计时、位置及动作。动作结束由模拟时间决定。此样例直接提交注入路径上的位置，只用于隔离验证；正式怪物将在 0.6.1 接入已有导航唯一速度提交机制。

## 可运行样例

- 场景：`Assets/Scenes/Diagnostics/BehaviorSample.unity`。
- 创建入口：`DarkFlare/行为/创建诊断样例`；安装器复用既有美术和 Animator，不覆盖已存在场景。
- 配置：`Assets/Data/Diagnostics/BehaviorSampleConfig.asset`，中文 Odin 字段设置巡逻速度、距离、等待和攻击各阶段时间。
- 两个 Actor 共享定义，各自等待、往返巡逻，可单独打断、暂停或请求演示攻击。

直接打开样例进入 Play。项目统一引导创建 ApplicationHost，样例等待 Ready 后申请专属 Session 和子作用域，不替换已有 Session。初始化及调度均归 LifecycleScope；禁用先取消工作，再停止自有 Session。样例不初始化正式内容或保存，不加入生产构建场景。

## 动画表现

`BehaviorAnimatorBridge` 订阅 System 发出的语义事件，`BehaviorAnimatorPresenter` 将 Idle / Move / AttackWindup / Attack / Recovery / Interrupted / Stopped 映射为表现。`BehaviorAnimatorMap` 设置 Moving Bool 与 Attack Trigger 参数名。

桥接只消费当前 Handle 和动作，缺失 Animator、禁用、参数缺失或类型错误不会阻塞行为；问题计数有界。重复绑定同一 Handle 保留已触发动作序号，避免同次攻击再次 Trigger。解除绑定清理 Trigger、移动标记、暂停速度及订阅。

既有 `ActorAnimatorController` 默认继续负责旧战斗表现。需要同时挂行为桥时，必须显式开启“行为驱动攻击”；旧组件让出攻击和移动，继续受伤、死亡及复活表现。其事件绑定现归 SceneSessionBinding，禁用和 Session 结束时注销。

可选 `ReportMarker(handle, actionId)` 通过 Command 记录动画观察，重复或迟到结果被拒绝；它不扣血，不决定动作完成或树节点结果。

## 调试入口

Editor：`DarkFlare/调试/行为实时图`。选择样例 Actor 或使用上一个 / 下一个；窗口显示真实 HFSM 父子边、活动路径、BT 节点状态、条件判断 Tick、最近转移、语义动作进度及身份。图形同时使用文字和颜色。Editor 控制按钮发送受控 Command。

Development Player：默认关闭的摘要面板，Tab / 手柄 Start 打开，方向键 / D-pad 导航，Enter / A 选择，Esc / B 关闭，也支持鼠标。面板只有只读摘要与实例选择；关闭恢复输入上下文和此前焦点。中英文本使用 Localization，伪本地化用于布局验收。普通发布 Player 的诊断入口禁用。

窗口和面板每 100ms 按需 Query 一致快照，图布局按拓扑缓存，读取不重新执行 Guard。选择变更、窗口关闭或 Session 失效释放诊断租约，不持有旧架构 / Actor。快照格式化和 UI 更新有分配，与 Core 稳态 Tick 单独计量。

0.6.0 尚无怪物感知范围、Boss 阶段或刷怪批次；这些数据随对应阶段接入。性能与实际 Player 证据见[验收](acceptance/0.6.0/README.md)。
