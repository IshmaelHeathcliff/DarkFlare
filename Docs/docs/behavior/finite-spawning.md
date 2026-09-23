# 有限刷怪与地图名额

Alpha 0.6.3 将正式地图的普通怪物从“按存活数定时补满”改为固定名额。地图 V4 的 `spawnBatches` 记录稳定批次 ID、Spawn 区域 ID、数量及 `OnEnter` / `PlayerRange` 激活方式；区域引用 `MonsterSpawnDefinition` 加权池。正式野外图 r9 配置 `initial` 入图 20 只，以及分散在地图各处的 10 个范围触发点、每点 8 只，共 100 个普通怪物名额。每个触发批次绑定附近的独立 Spawn 区域，SceneView 用触发圆、中心标记、批次 ID 和数量标识。Boss 点独立，城镇无普通刷怪批次。

`FiniteSpawnPlan` 在首次创建地图实例时按稳定批次 ID 排序，以地图随机根种子预选怪物内容、实例种子与落点种子。每个名额依次为 `Pending`、`Alive`、`Defeated`；批次单独保存激活标记。预选不分配怪物实例 ID；实际生成成功后才关联 ID。入图批次在地图可交互前同步生成，范围批次用玩家上一位置至当前位置的线段命中触发圆，每帧最多处理四个待生成名额。暂停、切图停用和取消不会继续触发；几何或动态占用暂时无合法落点时保留 Pending 并重试。已击败名额绝不补怪。

位置候选必须完整落入关联 Spawn 区域，经过实际 Prefab 圆足迹、地表障碍及 Safe 排除检查，并避开现存怪物与玩家。入图批次无法完成会中止候选地图并回滚；范围批次暂时受阻时在 `MonsterSpawner` Inspector 中显示失败原因与累计次数。Inspector 还显示计划总数、Pending / Alive / Defeated 及已激活批次。`MaxAliveCount` 和 `SpawnInterval` 只供无正式地图批次的旧示例刷怪器使用。

活动图与冻结图都用 Schema 8 起保存相同计划及状态。正常离场和进程继续保持未触发名额、活怪身份与已击败名额；同一图不重掷计划。Schema 7→8 把已访问旧图设为 `LegacyPreserved`，保留现有怪物、Boss 与掉落，停掉旧式补满，不推断历史击杀。野外旧 r1 / r4 在旧布局语义哈希匹配时升为 r9；r7 的 12 名额有限计划按原种子扩为 100 名额，原名额的类型、种子、激活、存活及击败进度保持不变。未知修订拒绝。Alpha 0.6.4 的[手动重置](map-reset.md)新增 Schema 9 地图实例代次，普通离场仍不刷新。

长期回归复用 `WorldMapStateTests`、`MapEditTests`、`WorldMapPlayModeTests` 和原存档迁移套件，覆盖固定计划、编辑事务、暂停 / 高速穿越触发、击杀不补怪与切图恢复。Windows Player 与 300 / 600 单位综合实测若未执行，单独记录，不以 Editor PlayMode 代替。
