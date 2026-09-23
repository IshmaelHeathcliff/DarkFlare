# 双地图与运行时作用域

阶段 G 完成于 `0.5.3-alpha`，原有冻结合同见 [G 验收](./acceptance/g/README.md)。H 已接入[地图导航所有权](./navigation.md)及 Schema 5 意图恢复；当前 `0.5.5-alpha` 的 Player 跨进程继续、历史档迁移、30 秒离场冻结与 1000 单位切图见 [I 综合验收](./acceptance/i/README.md)。

## 数据与入口

初始安装完成后，`DarkFlare/地图/安装城镇与野外` 菜单已移除；日常修改通过地图工作台与配置中心完成。`WorldMapSetup.Ensure()` 保留为显式脚本安装入口，调用仍会更新正式配置并保存 Main 场景。

正式源位于 `Assets/Data/Maps/World/`：城镇 64×64、野外 128×128，格宽 1。两图复用四地表发布资源及对象表现目录，当前 core 内容版本为 7。新游戏由 `WorldMapConfiguration` 的默认地图与入口决定。

地图交换格式 V2 增加 `entries`：稳定 `entryId`、用途 `Arrival / NewGame / Respawn`、局部世界坐标。每图最多 256 个入口，正式图必须有唯一复活入口。`Portal` 对象交互保存目标 `targetMapId / targetEntryId`，`contentId` 为空；商人和打造绑定相反。整套地图编译后交叉检查目标引用。V1 读取时只在内存补空入口和空出口字段，保存输出 V2，不在读取时改写原文件。

地图工作台的入口页、SceneView 的 P 工具及 `UpsertEntry / RemoveEntry` AI Patch 共用原子编辑事务、Undo / Redo 和规范 JSON。地图源仍是唯一布局，不在 SO 另存入口坐标。

## 所有权与切图

`WorldMapRuntime` 属于 Session，玩家、背包、装备、全局实例分配器及共享服务持续存在。`MapScope` 持有本次装配的地表、几何、表现、交互、刷怪器和 Addressables owner。怪物、掉落、投射物通过 `SessionObjectRegistry.RegisterMapObject` 归当前地图；玩家保留 Session 所有权。

`ChangeMap` 复用 Scene Flow 的活动请求与最新待处理请求仲裁。地图在 Main 中替换，不反复加载 Main：

1. 等待保存捕获释放状态时钟，暂停并排空状态任务，捕获源图及玩家回退状态。
2. 在禁用根节点下准备地表、碰撞、对象及目标恢复数据。资源与落点失败不会停用源图。
3. 主线程同步停用源图、安装候选、恢复 Actor 与掉落、移动玩家和相机，交换冻结状态集合。
4. 提交后清理源图对象、生命周期与资源，再发布完成事件并安排自动保存。提交后的取消不撤销已完成切换。

提交前异常恢复源图、玩家资源、状态、随机流和刷怪倒计时。回退或提交后清理无法完成时进入 Fatal 路径。保存捕获等待稳定地图；退出检查点等待已在进行的保存，避免重复获取捕获门禁。

出口沿用 Interact 输入与本地化提示，目标包含地图 generation，失效入口不能提交请求；按住交互键时须释放一次才重新开放。

## 冻结与存档

离场状态是纯 DTO，不保留隐藏的模拟对象。怪物生命 / 魔力、位置、词条掷值、攻击冷却，掉落与物品身份，刷怪倒计时和地图随机流均保存。恢复时一起平移状态的应用、过期及下跳时间，保留层 ID、顺序和周期序号；玩家状态继续使用活动 Session 时钟。

0.6.1 的 Schema 6 还冻结行为锚点、感知记忆、闲逛序列及攻击阶段。`MapScope.Behavior` 持有行为注册和唯一地图模拟任务；停用源图时先注销行为，再停止导航。候选不 Tick，失败回退恢复资源、行为阶段与 RNG，成功回图使用新的句柄恢复剩余计时，不补发离场攻击。后续手动重置尚未接入，当前离开再进入保持原状态。

Schema 4 的 `Run` 保存活动 `ActiveMapId / MapRevision / MapRandom`，原 `Monsters / WorldDrops / Spawner / Statuses` 表示当前图；`FrozenMaps` 只保存非活动图。物品仍在 `Payload.Items` 唯一总表，内存冻结态的 `Items` 子集不重复写盘。当前只支持一个共享商人库存，仍由 Session 的 `Run.Merchant` 保存；城镇和野外切换不补货。每图 Spawn 区域当前须使用同一刷怪定义；商人和打造绑定须与 Session 共享服务一致，不静默忽略不同内容 ID。

随机通道 `SpawnPosition / MonsterInstance / MonsterAttack / Loot` 随地图保存；`PlayerAttack / Crafting` 属于 Session。新地图根种子以固定算法从运行种子与 MapId 派生。Schema 3→4 将历史单图映射到野外，复制原随机根及序号，不更改下一次随机结果；core 5→6 为追加内容。野外中央保留旧 Main 范围；纯数据预检拒绝阻挡内部、未知图、修订不匹配和全图重复身份；资源预热后按实际 Prefab 圆形足迹与物理接触容差进一步校验恢复位置，不移动或丢弃存档对象。

容量合同为每图 1024 怪物、全档 2048，物品 8192、掉落 4096、全图状态层 16384、单 Actor 4096，文档 16 MiB。冻结图也参与实例分配器与物品归属校验，超限拒绝，不截断。

## 几何与后续边界

刷怪使用区域与圆足迹检查，Safe 始终排除；采样失败跳过并累计诊断计数。相机使用当前图 Bounds，复活使用当前图唯一 Respawn 入口。

阶段 H 已接入[批量导航](./navigation.md)：`MapScope` 同时持有 Native 网格、搜索池、共享场和固定步驱动。停用时立即取消驱动并完成 Job，提交成功后释放，回退重新激活。Schema 5 增加怪物导航意图；只冻结目标 / 模式和停止规则，路径与缓存重建。1000 怪物存档容量验证仍不能替代独立导航性能验收，性能结论见 H 记录。
