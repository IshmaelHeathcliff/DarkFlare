# 地图静态几何与通行查询

Alpha 0.5.0 D 的当前实现。地图布局仍以 `.dfmap` 为唯一源；同一份 [64×64 分区](./terrain-partition.md)派生视觉与静态阻挡。该模块提供静态查询和 Physics2D 投影，供后续编辑器、刷怪及导航复用；Main 尚未迁移，怪物当前仍使用原移动逻辑。

## 调用与所有权

`MapDocumentCompiler.Compile` → `MapGeometryInput.Capture(map, lookup, settings)` → `MapGeometryCompiler.Compile(input, token)` → 不可变 `MapGeometrySnapshot`。

Capture 在主线程读取 / 校验 SO，复制地表属性、对象矩形与区域；Compile 只访问纯值，不读取 SO / 场景，可在线程池执行。快照没有原生容器或 Unity 对象，调用者释放引用即可；`ArrayBytes` 仅计常驻核心数组，不含区域对象、包装器、构建临时内存或物理引擎资源。

`SourceHash` 跟踪规范文档；`GeometryHash` 独立包含分区 / 几何修订、地图身份 / 范围 / 细分、地表许可 / 代价、实际阻挡、区域及代价数组。seed、文档 revision、外观变体不改变通行结果；定义的 Walkable / MovementCost 改变会使签名失效。旧快照不因源 SO 变动而被修改。当前每次完整编译，没有增量几何重建或全局缓存。

## 分区、精度与容量

- 默认每逻辑格 **4×4 导航节点**，可显式选择 2 或 8。节点在子格中心，世界坐标使用 double。渲染分区每轴 64 像素，逻辑格、分区像素、导航节点不能互换。
- 分区像素 `(i,j)` 仍按 `i/63,j/63` 采样；几何覆盖实际纹理像素区间 `[i/64,(i+1)/64]`。不可步行材质的连续水平跨度与相同跨度的相邻行合并为矩形，再合并跨显示格的精确共边矩形。最终保留像素台阶、孔洞和孤岛，不从颜色 / Alpha 反推。
- 显示格的外侧半格裁到逻辑矩形，地图外始终不可通行。对象 footprint 使用世界单位、直角旋转、局部 position 加一次 origin；区域点使用逻辑坐标乘 cellSize 再加 origin。树冠、装饰及交互身份不扩大阻挡。
- 静态障碍索引按逻辑格分桶，查询只检查覆盖桶；同一矩形在一个查询内只计算一次距离。桶访问本身也受预算约束。没有为全图建立 64² 倍像素节点，线程间没有共享可变查询缓存。

| 限制 | 默认值 / 行为 |
| --- | --- |
| 导航节点 | 4,194,304；Capture 在分配前检查 |
| 阻挡矩形 | 合并前及对象加入后最多 262,144 |
| 索引条目 | 最多 4,194,304 |
| 不同 Wang 模板 | 最多 1,024 |
| 物理投影 | 最多 32,768 个矩形，另加四条边界 |
| 单次线段查询 | 默认最多 65,536 次桶 / 条目访问，可显式提供预算 |

前三项可由 `MapGeometrySettings` 限定。超限抛 `MapGeometryCapacityException`，查询预算耗尽返回 `BudgetExceeded`，不截断地图、不发布部分结果。源文档合法不等于满足指定派生预算；逐格随机四材质的 256² 压力源超过矩形上限，连续区域 256² 样例已通过，详见[验收](./acceptance/d/README.md)。

## 足迹与查询

`MapGroundProfile` 表示当前地面行走圆形足迹：Radius 必须为正，Margin 非负。默认物理余量 **0.0125 世界单位**，用于覆盖实测 Polygon/Composite 约 0.01 的形状扩展和投影精度余量。`SupportsPhysics` 表明余量是否达到此基线；低于基线的显式 profile 用于纯几何诊断，不保证物理无重叠。未来其他通行能力还需匹配物理层，并非更换寻路策略就可穿水。

1 单位逻辑格下，1 单位宽标准直道对当前最大半径 0.36，在本轮四种网格偏移中均有合法节点。0.75 宽直道的连续空间可容纳该足迹，但子格中心未必落在狭窄余量内；4×4 与 8×8 都存在保守拒绝，不能宣称所有连续可达通道都可被离散图发现。地图绘制应给常规通路至少 1 单位宽，并单独验证转弯。

| API | 合同 |
| --- | --- |
| `Bounds` / `GetNode` | 逻辑地图矩形及合法子格中心；不静默夹越界节点 |
| `CheckPoint` | 圆足迹加 Margin，完全处于边界内，且不与任何阻挡接触 |
| `CheckSegment` | 整段 swept circle；端点合法不能绕过中间薄墙；接触阻挡判失败 |
| `CheckEdge` | 相邻八方向，斜边还要求两侧正交节点合法；整段净空检查，输出正向边成本 |
| `FindPoint` | 先查原点，再在搜索圆内按距离、y、x 稳定选子格中心；候选包围矩形超过请求数量预算时完整拒绝 |
| `Regions` | 按 priority 降序、regionId 升序的不可变集合；Contains / BoundaryDistanceSquared 可查询覆盖 |

结果区分 `Valid / InvalidInput / Outside / Blocked / RegionRejected / NoCandidate / BudgetExceeded`；取消抛 `OperationCanceledException`。FindPoint 只承诺落点合法，**不承诺与起点连通**；未来搜索必须继续判断连通性。未知刷怪区不回退到全图。

移动成本为目标节点像素所属材质的 MovementCost × 直行 10 / 斜行 14；这是离散有向图成本，不是连续路径积分。未来 Flow Field 反向扩展必须使用对应的正向边成本；A* 共用同一邻接 / 成本 / 半径规则。两种算法、动态障碍和 Jobs 尚未在本模块实现。

Named 只标记位置；Safe 不生成墙。请求指定 Spawn 区时，整个圆足迹必须在所选区内，且不与任意 Safe 区相交，安全区排除不受 priority 压过；重叠 Spawn 保留各自身份及 spawnDefinitionId，调用者指定本次区域。本模块不触发怪物生成。

## 物理投影与预览

主线程创建 `MapGeometryProjection(snapshot, loadedScene)`，再 `await PopulateAsync(token)`。投影根在指定场景创建，使用 WorldObstacle 层、Static Rigidbody2D、每批 128 条矩形路径的 PolygonCollider2D 与单个 CompositeCollider2D。原始障碍不按角色半径膨胀；轮廓合并消除内部边，并保留孔洞。生成分批让出，最后的 Unity `GenerateGeometry` 是同步调用，不能由 CancellationToken 中止，故单独限制投影容量。

填充期间根禁用；完成才启用并生成复合轮廓。失败 / 取消会释放自身根对象；Dispose 幂等，PlayMode 先禁用再延迟 Destroy，EditMode 立即销毁。不能对同一投影重复填充。碰撞精度检查保守估算 float 世界坐标误差，超过 `min(0.001, cellSize/512)` 时拒绝投影，纯 double 查询仍可用；大 origin 应通过后续世界原点管理处理，不静默损失精度。

菜单 **DarkFlare → 地图 → 打开同源几何样例** 创建缺失的开发样例并打开正式材质预览。源在 `Assets/Data/Development/Maps/terrain_geometry.dfmap`；辅助对象 / 区域定义也在该目录，没有加入 core 内容池。样例复用四材质拓扑，加入 U 形障碍、Spawn / Safe 交叠。

现有预览窗口新增“构建碰撞与通行”和“检查半径”。红线为原始阻挡矩形，放大后绿点为合法节点；左键检查足迹，Shift+左键检查移动线段，白圆为半径，彩色圆为加余量后的净空。每次重绘最多显示 4,096 个阻挡 / 节点，缩放查看细节；窗口显示几何签名、容量和失败原因。叠加不表示怪物正在寻路。

Session 在同一次构建中捕获显示和几何输入；完整帧连同碰撞一起发布，源变动 / 取消期间保留旧帧并标记过期、禁用检查，不能混用两个修订。关闭 / 重载 / PlayMode 切换取消构建，释放显示帧和投影；迟到结果不会发布。没有创建逐单位循环或新的游戏 Session。

## 验证

长期规则与生命周期覆盖位于 `MapGeometryTests`，实际移动与释放位于 `MapGeometryPlayModeTests`；复用现有预览、文档、导入及旧 Main 回归。[D 验收记录](./acceptance/d/README.md)保留尺度矩阵、物理对照、数据往返、取消和性能证据。运行时地图作用域、刷怪消费者和寻路留在后续阶段接入。
