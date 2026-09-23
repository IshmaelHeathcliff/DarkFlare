# 普通怪物行为

Alpha 0.6.1 将正式普通怪物接入既有 Core HFSM 与行为树。核心程序集不依赖本模块；配置、感知、地图驱动、战斗授权和 Unity 表现均在 Runtime。守卫 Boss、固定总量刷怪和手动重置属于后续阶段。

## 配置与状态

三种正式怪物的 `MonsterDefinition.Behavior` 引用 `Assets/Data/Preset/Monsters/普通怪物行为.asset`。可复制配置为个别怪物设置不同感知和动作时序。配置中心识别此依赖资产，它没有独立 ContentId；字段、中文 Inspector 与校验规则见[配置参考](../config-reference/actors-and-monsters.md#monsterbehaviordefinition)。core v7 预检拒绝缺失或无效的行为配置。

| 情况 | HFSM 状态 | 行为树 / 导航 |
| --- | --- | --- |
| 玩家无效或激活范围外 | Sleeping | 清除意图、零速度、不请求路径 |
| 激活范围内，未发现玩家 | Wandering | 出生锚点附近选点、走停与有界等待 |
| 看见玩家 | Chasing | 使用玩家目标、按怪物配置选择 Flow Field / A* |
| 暂时丢失视线 | Chasing | A* 前往最后可见点，记忆过期后恢复闲逛 |
| 范围、视线和许可均有效 | Attacking | 前摇 → 一次命中尝试 → 后摇 |
| Move 与 Attack 同时禁止 | Controlled | 停止移动、取消攻击 |
| 死亡 | Dead | 撤销动作，既有死亡 / 掉落管线接管 |

HFSM 层级为 root / alive，alive 下含 sleep、active、controlled；active 下含 wander、combat，combat 下含 chase、attack。dead 是 root 的另一子状态。树用优先选择器编排攻击、追逐和闲逛；状态退出 Abort 树。共享定义仅存结构，每个实例独立持有执行器和领域状态。

激活 / 退出默认 14 / 16 米，视距 9 米、视角 180°，视线使用当前地图导航的同源遮挡。感知默认每 0.1 秒刷新；首次及几何改变立即更新，随后按稳定种子错峰。朝向来自实际移动速度，攻击接纳时转向目标。未看见玩家时不会持续读取墙后目标作为导航终点。受击不会突破范围或直接授予目标位置。

闲逛默认半径 2 米、等待 0.7–1.8 秒；每次至多采样四点，并检查同源通行。不同点目标采用有界 A* 池，保持原导航的四个 A* 工作区、两个 Flow 工作区、轮转与容量限制；Pending / Busy 继续等待，Unreachable / BudgetExceeded 则退回等待后重试。跑出锚点范围时先正常寻路返回，不传送。

正式行为实例登记时准备路径缓冲，保留至单位注销，等待、闲逛和追逐切换不会重复申请；旧路线和请求身份仍在每次意图切换时清空。容量沿用地图准入时已有的每 Agent 上限。

## 时钟、攻击与动画

`MapScope` 拥有 `MonsterBehaviorMap`，它管理地图唯一 FixedUpdate 异步任务。每步先提交动态障碍、等待当前几何就绪，再感知 / 决策 / 结算，最后由 `NavigationMap` 调度寻路与安全速度提交。暂停和状态快照锁定不推进领域时间；地图停用取消任务、清零速度并注销行为。

Move 禁止只停止导航，允许原地攻击；Attack 禁止立即取消动作，但不停止追逐。正式 `ContactAttackCommand` 只请求动作接纳，不能绕过前摇。默认前后摇各 0.3 秒，接纳时分配动作 ID 并消耗 `ContactDamageInterval` 冷却；取消或挥空均不退还冷却。

到命中点，执行器先标记阶段已结算，再授予仅本次同步调用有效的命中许可。命令重新检查实例句柄、目标、存活、行动许可、距离与当前几何；成功才消费既有 MonsterAttack 随机流并创建伤害快照。保存不能插入同步命中内部。恢复后摇、重复回调或旧 Handle 不会再次造成伤害。

行为随机从实例根种子派生独立序列，仅实际选点 / 等待抽样推进；不改变攻击、掉落和刷怪随机通道。诊断读取不执行条件、不抽样。

正式 Prefab 挂接 `BehaviorAnimatorBridge`，绑定时旧 `ActorAnimatorController` 让出移动 / 攻击参数，仍处理受击、死亡、复活和左右翻转。桥接在前摇开始触发攻击动画，以动作 ID 去重。Animator、动画事件、播放速度和资源是否可用均不决定伤害或动作完成。

表现订阅通过 `BehaviorSystem.RegisterPresentation(handle, callback)` 按实例分发，复用 QFramework `EasyEvent`；句柄采用字典索引。全局事件仍可供少量观察者使用，动画桥和样例对象不全体订阅，避免大量同时攻击时出现平方级回调。注销先撤销身份并移除索引，再发停止事件；旧订阅不能收到替代实例的事件。

## 保存与地图生命周期

Schema 6 的 `MonsterBehaviorStateDto` 保存版本、状态、锚点、朝向、最后可见点 / 剩余记忆、闲逛目标 / 等待、行为 RNG 种子 / 序号、动作 ID、阶段 / 剩余时间 / 结算标记和目标稳定身份。原冷却字段继续保存剩余秒数。不保存 Core 节点、委托、路径、句柄和 Animator Hash。

Schema 5 → 6 为当前及冻结地图补充安全休眠状态：旧位置作为锚点，默认朝右，原实例根种子作为行为种子。原身份、资源、状态、词条、冷却和全部既有随机通道不重掷。core 6 → 7 只升级内容版本。未知行为版本、非有限数、非法阶段组合和外来目标在预检拒绝。

离场冻结不推进计时；回图和继续游戏恢复剩余阶段。恢复目标失效时取消动作并保留冷却。行为状态重新产生正式怪物导航意图，调试接管不跨图延续。候选地图不运行行为；提交失败恢复源图状态，成功后旧注册及旧回调均失效。

## 调试与维护

Editor 菜单 `DarkFlare/调试/行为实时图` 可选正式怪物，查看真实 HFSM / BT、感知可见性、记忆、冷却、动作阶段和转移原因。选中对象时 SceneView 显示激活 / 退出距离、视锥、闲逛锚点及最后可见点。

正式 Development Player 用 F8 / 手柄 R3 打开只读面板，Esc / B 关闭，实例切换与焦点使用既有 UI 输入。样例场景继续使用 Tab / Start。关闭面板释放诊断与输入租约，普通 Player 停用整个诊断对象。

导航调试命令必须显式 `takeBehaviorControl: true` 接管；否则正式行为实例拒绝外部意图修改。使用“归还怪物行为控制”或 `ReleaseMonsterNavigationOverrideCommand` 恢复自主决策。

Profiler 标记包括 `DarkFlare.Behavior.MapTick`、`DarkFlare.Behavior.Perception`、`DarkFlare.Behavior.Decision`、`DarkFlare.Behavior.Presentation`；导航另行计量。表现标记衡量有效语义事件的 Animator 参数应用，不能当作 Animator 系统全部更新成本。快照格式化、UI、注册、路径分配与保存均不能套用纯 Core Tick 零分配结论。

长期回归由 MonsterBehaviorTests、MonsterBehaviorPlayModeTests、SaveSerializerTests 以及原导航 / 地图 / Session 套件共同覆盖。发布采集工具不长期保留；最终当次证据归入 `acceptance/0.6.1/`。
