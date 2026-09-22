# Alpha 0.6.0 验收

2026-09-22，F1–F6 完成，交付 `0.6.0-alpha`。内容目录仍为 core v6、SaveSchema 5、地图 V2。正式场景及怪物规则未切换；0.6.1 起接入普通怪物。

## 交付与回归

- HFSM 与 BT 独立放入既有 Core，各自纯 C# 上下文验证，不互相引用。
- 组合样例有两个共享定义、独立运行态的巡逻 Actor；HFSM 状态退出中断树。暂停、注销、异常清理、旧代次与重复动画回执有长期回归。
- Animator 映射只影响表现；缺失 / 错误参数 / 禁用不阻塞行为，旧表现组件的 Session 订阅归属已修正。
- Editor 显示真实父子边、活动节点、条件结果、轨迹和动作计时；Player 默认关闭，只读摘要与选择可通过键鼠 / 手柄操作。

| 检查 | 结果 / 证据 |
| --- | --- |
| 完整 EditMode | [788/788](editmode.json)，含 HFSM 35、BT 25、Runtime 6 |
| 完整 PlayMode | [87/87](playmode.json) |
| 最后只读面板修正 | [PlayMode 2/2](panel-final-check.json)，包括隐藏控制、双输入、语言、Animator 和清理 |
| 独立 Windows Development | [构建成功](development-build.json)，[实际 Player 检查](player-check.json) |
| 普通 Windows Player | [构建成功](release-build.json)，[Debug.isDebugBuild=false、面板停用](release-check.json) |

首轮全量检查暴露样例初始化使用裸 Forget / CTS 等策略偏差，已统一到 LifecycleScope，策略 39/39。地图对象 Redo 测试曾间歇失败，隔离复查及最终完整 EditMode 通过，未改地图代码或放宽断言。构建保留既有 TMP Shader 废弃 pragma 警告；普通包另提示远程诊断关闭，均无构建错误。

## Player 与可视化

样例只构建 `Assets/Scenes/Diagnostics/BehaviorSample.unity`，独立产品名 `DarkFlareBehaviorSample`，数据位于该产品自己的 LocalLow 目录，不写正式存档。生产构建列表保持 Bootstrap / Main。验收后恢复项目产品名和 Pipeline 默认关闭设置。

实际 Windows Player 中使用 Input System 虚拟 Keyboard / Gamepad：Tab / Start 打开，方向导航及 A 切换选中实例，B / Esc 关闭；关闭恢复 Gameplay 输入。中英与 `qps-ploc` 切换后检查文本 / 布局，关闭面板继续模拟，停止并重新启用样例后旧 Handle 无效。此为虚拟设备验证，不声称实体手柄人工验收。

- [Editor 实时 HFSM / BT 图](editor-graph.png)
- Player 面板：[中文](player-zh-Hans.png)、[英文](player-en.png)、[伪本地化](player-qps-ploc.png)

Player 后台隐藏运行，直接背缓冲截图不可用；上述面板图来自实际 Player 的 PanelSettings RenderTexture，呈现真实 UI，不包含场景背景。Editor 图取自 Editor Play 中与调试窗口共用的实时拓扑视图，以固定像素 RenderTexture 采集，避免系统对话框遮挡。伪本地化提示已分行，控制栏保持可达；玩家面板不暴露 Editor 的暂停 / 打断 / 攻击控制。

## 核心规模与分配

[原始规模记录](core-performance.json)：Unity 6000.6.0f1、Editor Mono、AMD Ryzen 7 PRO 8845HS。500 / 1000 对 HFSM＋BT，共享定义，HFSM 3 状态 / 2 转移，BT 3 节点 / 2 入口条件；交替高低分支并定期进入 hold，覆盖转移与 Abort。每组预热 100 Tick、采样 600 Tick、三轮，dt=.02；这是同步核心微基准，不是完整怪物场景的 60 秒实时帧预算。

| 实例对数 | 诊断 | 三轮 Tick P95 范围 | 每轮树访问数 | GC.Alloc 事件 |
| --- | --- | --- | --- | --- |
| 500 | 关 | 0.137–0.147ms | 840,000 | 0 |
| 500 | 开 | 0.171–0.173ms | 840,000 | 0 |
| 1000 | 关 | 0.278–0.287ms | 1,680,000 | 0 |
| 1000 | 开 | 0.340–0.344ms | 1,680,000 | 0 |

ProfilerRecorder 限定当前线程、GC.Alloc，采样前用 512B 分配正向探针确认有效。计时不含构造、快照格式化、UI 或故障；不能外推整场零分配，也不能替代 Alpha 0.6 后续活动怪物压力验收。

实例内存按实际运行时元素尺寸统计自有数组有效载荷：HFSM 40B / BT 111B；诊断开启后 HFSM 1608B / BT 1189B（双方各 64 条轨迹）。记录列出每个数组长度与元素大小。该值不含对象 / 数组头、实例字段、动作、上下文、共享定义及弱引用工厂表；全局 GC 堆差值不可靠，未据此声称总实例堆大小。诊断缓冲首次观察时分配，释放最后租约停采并保留固定容量。

临时性能、截图、Player 驱动和发布门禁探针完成后移除；长期保留模块行为测试、隔离诊断场景及文档证据。
