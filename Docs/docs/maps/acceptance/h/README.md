# H：批量导航验收

状态：2026-09-19，H1–H7 完成。版本 `0.5.4-alpha`、core v6、Schema 5；完整回归、性能矩阵和 Windows 构建通过。

## 验收结果

- 最终 [EditMode](./editmode-final.json) **722/722**、[PlayMode](./playmode-final.json) **85/85**，零失败、零跳过。正式内容校验零问题，配置文档 **237/237** 字段覆盖；无 Session 时导航调试窗口可安全打开，见[配置校验](./configuration-validation.json)。
- [Development Player 矩阵](./performance.md)：500 / 1000 × 纯导航、完整场景、战斗负载 × 三轮全部满足计划预算。1000 战斗负载的总帧 P95 为 **15.63–18.29 ms**，导航主线程 P95 **2.45–2.63 ms**；所有正式轮次导航稳态为 **0 B/tick**，GC 正向探针有效。
- [普通 Player 战斗复核](./release-1000-combat.json)：1000 单位，三轮各预热 10 秒、采样 60 秒，总帧 P95 **14.18–16.02 ms**，维持 1000 活动怪物。普通构建的 GC 探针未验证有效，不据其零事件另作 GC 结论。
- [1000 单位 20 轮往返](./player-1000-travel.json)：每轮返回野外并预热后均为 1000 Agent、1001 注册对象、6 owner、46 lease；导航原生字节均为 **51,261,536**，生命周期峰值 **52,420,576**（49.99 MiB），无持续增长。往返采集与 Editor 回归曾并行，elapsedSeconds 包含切图、保存和预热，只用于生命周期验证，不作为独占机器下的切图耗时基线。
- [正式 Windows 构建](./production-build.json)成功，零错误；输出 `Builds/Alpha054/DarkFlare.exe`。产品名恢复 `DarkFlare`，临时采集器已从 Assets 移除；[Development](./development-build.json) 和[普通复核构建](./release-build.json)保留独立测试数据根。

构建中的 Pipeline 缺少运行配置、TMP 旧 shader pragma 为既有工具 / 包警告；具体出现次数以各构建 JSON 为准。Pipeline 在交付 Player 中关闭，不影响游戏导航。

## 方法与环境

Windows，Unity 6000.6.0f1，AMD Ryzen 7 PRO 8845HS（16 逻辑线程），Radeon 780M，1920×1080，Mono，Burst 开启，约 50 Hz 物理步。Development Player 记录 `collectionSafetyChecks=false`；关闭 VSync，targetFrameRate=-1，无 Deep Profile。不得将 Editor 算法基线与完整 Player 帧时间直接相除作为加速倍数。

每种负载分别测试 500（480 Flow / 20 A*）和 1000（950 Flow / 50 A*）。每轮预热 10 秒、采样 60 秒，每项 3 轮。场景保持正式野外、地表、障碍、怪物配置、动画、Rigidbody2D 和 HUD。目标玩家绕半径 4 的轨迹持续移动，特殊单位每秒更新独立固定点目标。怪物生成种子为 71023+i，GameplayRandomSystem 固定根种子 71023；初始位置使用实际地图净空查询。最终矩阵 JSON 标注 `physicsStepSampling=true` 与 `randomSeed=71023`。

- `full`：完整表现 / 物理 / HUD；测试状态仅禁用怪物攻击，防止测试玩家死亡。
- `combat`：完整表现 / 物理 / HUD，保留真实接触攻击；只在隔离测试进程提高玩家生命。每秒施加短暂移动限制，每两秒通过正式伤害入口击杀一只并立即补充，保持验收人数，包含掉落和资源清理。
- `pure`：关闭怪物 Renderer / Animator / Rigidbody2D 模拟，固定位置采样并手动调用同一导航 Tick；只供成本归因，不能替代完整场景结论。

独立进程使用 `DarkFlare-Navigation-Audit` 产品数据根；不接触正式玩家存档。临时采集器只受显式命令行开关启用，最终生产代码中移除。采集本身、状态及补怪成本包含在总帧时间中；导航主线程标记只覆盖网格变更、搜索轮询 / 调度、采样、转向等待和速度提交，战斗命令另计。

## 指标解释

原始 JSON 保存 P50 / P95 / P99 / 最大值、实际最低活动 / 注册人数、队列到完整结果发布的延迟、候选访问与饱和、流场命中 / 缺失、当前和生命周期原生峰值。导航指标逐物理步采集上一已完成步，不在 LateUpdate 合并物理追帧；总帧时间单独在 LateUpdate 采集。路径延迟按有发布的物理步取该步最大请求延迟；合并后的目标不重置先前等待起点。近目标直接安全段不计入流场查询命中率。

导航主线程耗时包含 steering Job 的 Complete 等待，可作为本步提交关键路径的保守上界；跨步搜索工作的响应另由路径可用延迟约束。`steeringWaitMs` 不代表所有 worker 的总 CPU 时间。每 Agent 候选访问上限 96，采用邻居上限 24；饱和代表软避让采样受限，不意味着硬障碍检查跳过。

总帧与物理步是不同采样总体。纯导航模式帧率很高，大量渲染帧不执行固定模拟，因此帧时间 P95 可能小于导航物理步 P95；这不表示完整游戏能达到相同帧率，也不能把两个 P95 相减或相除推导开销。

GC 使用当前主线程 `GC.Alloc` ProfilerRecorder，每轮正式采集前有 512 B 正向探针。只有探针有效且导航区间事件为零时报告 0 B/tick；事件 Value 不解释为分配字节。拓扑变更、配置切换、建池和存档的分配不属于稳态 Tick。

## 证据与边界

长期回归留在模块套件中：`NavigationTests` 保护 Burst 网格 / 净空差分、有向成本与 A* 一致性、预算续算、断路、内存门禁、密集桶、薄墙、贴墙接入及孤立合法节点；`NavigationPlayModeTests` 保护真实混合策略绕障、动态墙、状态限制、句柄失效、攻击遮挡和连续移动目标。`WorldMapPlayModeTests` 扩展原有冻结 / 20 轮往返用例，检查导航意图、Agent 与原生字节回归基线。

`SaveSerializerTests` 保留真实 Schema 4 夹具，并比较迁移前后除新增导航字段外的整个 Payload，覆盖非法模式 / 跨图目标拒绝。原先两个仅测试已退役 `MonsterSteeringCalculator` 的纯辅助函数用例删除，由实际 Burst 转向行为覆盖取代；旧私有接触攻击方法的反射断言改为配置行为断言。眩晕测试从等待一个固定帧改为有界等待首次异步路径就绪，未降低眩晕与恢复移动断言。没有增加忽略项。

- `legacy-steering-baseline.json` 是旧全表分离的 Editor 算法基线（500 / 1000），仅作历史成本说明。
- `smoke-*` 为调优中的短测，不能替代 `player-*` 正式矩阵。
- `editmode-before-collector-cleanup.json` 的 5 项失败均是临时采集器触发基础设施源策略；清理后的最终 722 项回归已全部通过。
- `playmode-final.json` 记录真实物理和地图生命周期回归；记录本次最终代码的 85 项通过结果。

Separation 是有界软避让，不承诺密集群体完全不重叠或严格编队。动态 stamp 为当前图调试阻挡，离场清除；不表示已交付持久化建造玩法。Alpha 0.5 全范围综合交付仍属于后续 I 阶段。

## 调优记录

最初 1000 战斗负载的一轮导航主线程 P95 为 3.03 ms（[原始记录](./combat-before-node-fastpath.json)），超出 3 ms 门槛。首先让合法当前节点直接返回，并将方向归一化移入 Burst。之后以逐物理步采样重新测量，固定位置的纯导航仍有 3.01–3.19 ms（[原始记录](./pure-before-phase-profile.json)）；[分段采样](./pure-phase-profile.json)定位到单位采样中的重复合法节点查询。

最终按准确位置和网格 Revision 缓存当前节点 / 固定目标节点，在传送、意图切换和 Profile 变化时失效。保持原有负载、采样方式、规模和预算，重新完成全部 18 轮矩阵。未通过调整门槛或减少单位数消除超标。早期记录没有 physicsStepSampling 标记的行仅作历史诊断，不能与最终物理步分位数直接比较。

[构建身份](./build-identity.json)记录 G 父提交和用于完整矩阵的 Runtime 程序集摘要；采集构建含临时入口，最终交付构建已清理该入口。长期测试保留行为合同，一次性采集脚本不进入 Assets 或测试程序集。
