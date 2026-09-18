# F：世界对象与稳定排序验收

日期：2026-09-18。Unity 6000.6.0f1、Windows x64 Development Player、Mono、AMD Ryzen 7 PRO 8845HS / Radeon 780M，1920×1080。模块入口：[世界渲染](../../../world-rendering.md)、[对象表现](../../object-presentation.md)。

## 行为与视觉

独立 Player 使用隔离产品名 `DarkFlare-WorldVisual-Audit`，加载正式 Bootstrap → Main，创建新会话。通过 Input System 的虚拟键盘验证移动和 E 交互，逐一检查 Shop / Crafting 页面可见；每次退出同时关闭目标页和背包，避免上一页残留产生假阳性。

[Player 结果](./player-visual/player-visual.json)记录树前 / 后、长石障两端及 0/90/180/270 度旋转的 Order 比较；另验证独立投射物、视觉中心高于地面锚点的落点特效、宿主内部受击效果。附着效果没有嵌套 Group。19 个正式地图排序段通过 Addressables 装配，释放后资源 lease 从 42 回到 42，owner 数也回到基线。

画面使用真实 Player Main Camera 渲染到 1920×1080 RenderTexture，再读取 PNG。隐藏窗口下系统截图不可用；这些图片展示世界 Renderer、Animator 和光照，不包含 Screen Space Overlay 的 UI，交互页面以行为断言确认。

| 样例 | 后方 | 前方 |
| --- | --- | --- |
| 玩家与树 | [树后](./player-visual/tree-body-1.png) | [树前](./player-visual/tree-body--1.png) |
| 石障第一端 | [后方](./player-visual/wall_0-stone_0-1.png) | [前方](./player-visual/wall_0-stone_0--1.png) |
| 石障另一端 | [后方](./player-visual/wall_0-stone_3-1.png) | [前方](./player-visual/wall_0-stone_3--1.png) |
| 90 度石障 | [后方](./player-visual/wall_90-stone_3-1.png) | [前方](./player-visual/wall_90-stone_3--1.png) |
| 180 度石障 | [后方](./player-visual/wall_180-stone_0-1.png) | [前方](./player-visual/wall_180-stone_0--1.png) |
| 270 度石障 | [后方](./player-visual/wall_270-stone_3-1.png) | [前方](./player-visual/wall_270-stone_3--1.png) |
| 投射物 | [后方](./player-visual/projectile-1.png) | [前方](./player-visual/projectile--1.png) |
| 落点特效 | [后方](./player-visual/impact-1.png) | [前方](./player-visual/impact--1.png) |
| 附着受击 | [后方](./player-visual/attached-hit-1.png) | [前方](./player-visual/attached-hit--1.png) |

画面中的 Main 怪物、掉落和战斗反馈仍按真实玩法运行。Main 另连续 10 秒手动渲染相机，600 帧、P50 16.667 ms / P95 16.669 ms；设置了 60 fps 上限，因此该结果是此机器上的集成冒烟，不是无上限吞吐量、完整 UI / GPU 或 500 怪物战斗性能承诺。

## 性能方法与分配

[排序原始记录](./player-sorting.json)采用相同布局、相同移动轨迹的一对系统：基线源为阶段 E 的 `7cf9de1`，改动版使用本次排序实现。两者在同一帧顺序测量，预热 3 秒后各采样 60 秒；500 / 1000 动态单元，另加 1000 静态段和 128 效果角色，总数 1628 / 2128。四种场景各三轮：全静止、动态单元沿正弦轨迹移动、同 Y 密集且仅横向移动、每帧注销并注册最后 64 个单元。注册成本独立记录，不混入 Tick；GC / 原生资源管理仍可能影响帧调度。

这是纯排序压力：使用真实 Transform、WorldSortParticipant 和 SortingGroup，统一类别以制造同类同 Y 比较，未运行 1000 个完整怪物 Animator / AI，也不实例化 128 份真实特效动画。位置更新、注册及计时数据收集在 Tick 测量区外。完整 Main 与效果生命周期另行验证，导航预算留至 0.5.4。

Mono 的 `GC.GetAllocatedBytesForCurrentThread` 在本机不可用，原始数据明确保存 null。改用当前线程的 `GC.Alloc` ProfilerRecorder，并用 512 B 数组分配作正向探针；GC.Alloc 是事件标记，其 Value 是耗时，不能误读成字节。只有正向探针有事件且被测区域事件数为零，才推导为 0 B。先行[Player 探针](./player-gc-probe.json)在两种规模、各 512 次移动 Tick 中均验证了零分配。

优化来自实际瓶颈：去除排序比较中的重复 Unity 原生判空、每 Tick 只解析一次 Layer、缓存上次应用的 Order / Layer、用整数比较类别避免枚举装箱、缓存 Comparison 委托避免 Mono 的 IComparer 适配分配。没有跳过静态对象的位置采样，也没有放宽预算。

`editor-baseline.json` 是早期 Editor 同步采样，只用于定位。`player-sorting-before-optimization.json`、`player-sorting-intermediate.json`、`player-sorting-order-cache.json` 是中止的诊断轮次，含不同优化状态及并发构建 / 测试负载，均不作为最终验收结果。

## 最终结果

EditMode **707/707**、PlayMode **78/78**，无失败 / 跳过，见 [EditMode](./editmode.json)、[PlayMode](./playmode.json)。[内容与配置覆盖](./final-validation.json)为 0 项问题、231/231 字段覆盖。工作台正式表现见[编辑器截图](./editor-object-preview.png)。版本为 `0.5.2-alpha`，core v5、玩家 Schema 3、地图文档 V1 不变。

[最终性能汇总](./performance-summary.json)采用完整矩阵中其余六组，以及 [500/churn](./player-500-churn-quiet.json)、[1000/moving](./player-1000-moving-quiet.json)的全部三轮隔离复测。原矩阵在这两组期间同时有全量回归、构建 / Editor 恢复及已结束但仍驻留的诊断 Player，最高分别为 1.504 / 2.361 ms，不能满足预算。清理诊断进程并停止重负载操作后，依次单进程重测整组，不挑单轮最优值。最终所有 24 个采用轮次均达到 500 ≤1 ms / 1000 ≤2 ms；原记录保留，不覆盖失败数据。

| 动态规模 | 场景 | 三轮 P95（ms） | 对照基线 P95（ms） |
| --- | --- | --- | --- |
| 500 | stationary | 0.338 / 0.406 / 0.339 | 0.333 / 0.345 / 0.296 |
| 500 | moving | 0.990 / 0.868 / 0.835 | 1.878 / 1.743 / 1.696 |
| 500 | same-y | 0.272 / 0.267 / 0.278 | 0.220 / 0.223 / 0.245 |
| 500 | churn | 0.761 / 0.843 / 0.797 | 1.708 / 1.703 / 1.684 |
| 1000 | stationary | 0.374 / 0.549 / 0.480 | 0.359 / 0.506 / 0.494 |
| 1000 | moving | 1.117 / 1.123 / 1.202 | 2.324 / 2.044 / 2.332 |
| 1000 | same-y | 0.420 / 0.460 / 0.357 | 0.419 / 0.427 / 0.347 |
| 1000 | churn | 1.621 / 1.510 / 1.448 | 2.850 / 2.569 / 2.589 |


## 边界与清理

本阶段不实现 MapScope / ChangeMap、离场冻结、正式城镇 / 野外或寻路。新商人与打造台 Prefab 为纯表现，不创建重复交互和领域状态。地图几何仍由 D 模块从源 footprint 生成一次。

临时采集 MonoBehaviour、基线副本和临时场景已通过 AssetDatabase 删除；长期只保留模块规则、事务、取消、资源生命周期、编辑器联动和排序分配回归。验收使用隔离产品目录，不写正式玩家存档。最终恢复 Bootstrap、Reload Scene Only、原产品名和 Profiler 关闭状态。


项目全量回归曾因同步测试桥接超时而重新启动 Editor，备份场景保存在 Library 的恢复目录；正式结果来自恢复后的项目程序集异步运行。旧配置发现测试的菜单假设已更新，诊断失败记录见 editmode-diagnostic.json。临时诊断构建中的过时 API 警告随脚本清理一并移除。纯排序采集器缺少正常前台退出流程，结果落盘后显式结束所属进程；这不作为应用退出生命周期证据，真实 Main 退出另由 Player 冒烟和 PlayMode 覆盖。

正式构建见 [production-build.json](./production-build.json)：0.5.2-alpha、Bootstrap / Main、0 错误，已排除临时采集脚本。两条警告分别为未启用 Player Pipeline 调试配置和既有 TMP Shader 的旧调试 pragma；不影响游戏构建。产物位于 Builds/Alpha052/DarkFlare.exe。本次真实 Main 输入 / 遮挡运行使用隔离的采集 Player；最终纯净构建未另行启动，以免访问正式玩家目录。
