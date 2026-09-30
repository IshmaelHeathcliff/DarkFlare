# G：城镇、野外与地图生命周期验收

日期：2026-09-18。Unity 6000.6.0f1，Windows x64 Development Player（Mono），AMD Ryzen 7 PRO 8845HS / Radeon 780M，1920×1080。独立 Player 验收、完整项目回归已通过。正式构建结果见下文。

## 行为与回归

最终 [EditMode 713/713](editmode-final.json)、[PlayMode 83/83](playmode-final.json)，0 失败。包含新版入口编辑、旧存档迁移、地图冻结、失败回退、真实触发器交互及合法贴墙存档恢复。

[地图专项](map-playmode.json)覆盖同一 Session / 玩家内的两图往返、20 轮怪物与掉落身份恢复、状态相位与随机流、两图保存后继续，以及准备取消、快速替代、无效目标、旧 generation 与提交后清理失败。[物理恢复](physics-restore.json)验证城镇交互触发及贴墙位置保存后继续。

故障覆盖分层复用既有长期回归：

| 合同 | 覆盖 |
| --- | --- |
| 候选 / 提交失败 | WorldMapPlayModeTests 在 prepared、source-detached、target-restored、before-commit 注入，验证原地图、玩家和唯一刷怪器；源清理失败进入 Fatal |
| 请求仲裁 | 同一套件验证取消、三次快速请求只保留最后候选、无效目标 / 入口、旧地图 generation 拒绝；Scene Flow 既有测试覆盖一次结算与退出 / 加载仲裁 |
| 资源与场景 | MapObjectPresentationTests 覆盖加载失败与取消后的资源释放；ApplicationLifecyclePlayModeTests 覆盖外部 Main 卸载与连续 Session |
| 持久化故障 | LocalSaveStorage / SaveCoordinator / 平台生命周期既有套件覆盖磁盘故障、原档保护、保存合并与挂起 / 退出；地图专项增加稳定快照与两图 Continue |
| 历史与容量 | 保留历史 Schema / core 夹具；WorldMapStateTests 检查冻结时钟、地图 RNG 隔离、未知图 / 阻挡落点、跨图重复身份与分配器回退 |

1000 怪物样本使用正式怪物词条、1000 个状态层、掉落及商人库存，规范 JSON 为 **3,733,896 字节（3.56 MiB）**，低于 16 MiB。样本通过序列化、反序列化、内容恢复预检和全图身份检查，见[地图状态测试](map-state.json)。这不是 1000 活动怪物的导航性能验收。

[容量内存采样](capacity-memory.json)在 Editor 主线程复用同一完整样本，后台每 1 ms 观察托管堆：基线 220.25 MiB、观察峰值 1065.52 MiB、增量 845.27 MiB，完整过程 11.77 秒。范围包括样本创建、JSON 往返、恢复预检和多次非法身份 / 容量检查，包含尚未收集的分配；不能作为单次恢复的存活内存或 Player 峰值。该结果说明字节容量通过，但大规模 JSON / DTO 临时分配仍有后续优化空间。

[内容、配置与连通性](configuration-connectivity.json)：内容校验 0 项问题，31 类配置的 235/235 字段覆盖。以半径 0.4 的保守网格检查城镇各入口、复活、出口及商人 / 打造接近点，野外入口、复活和回城出口，均有连通路径。商人 / 工作台接近点取对象下方 1.5 单位；下方 1 单位的物理交互点可用，但其所在导航子格不满足此保守半径，诊断如实保留 NoLegalEndpoint，未放松网格通行规则。

## 独立 Player 方法

[新游戏与往返](player/player-new.json)、[野外退出后继续](player/player-wild.json)、[城镇退出后继续](player/player-town.json)三个独立进程均成功。新游戏验证键盘打开商店并交易、手柄打开打造并完成操作、野外实际自动攻击击杀与掉落；回城 **30.000 秒**活动模拟期间野外快照逐项不变，玩家限时状态正常过期。继续游戏保留同一怪物和掉落实例。

20 轮城镇→野外往返耗时平均 **814 ms**，范围 **751–943 ms**。每轮稳定点均为 owner 6、lease 46、entry 46、动态对象登记 5、排序参与者 38、交互目标 1、玩家 1、刷怪器 1。Player 已分配内存为 **291.37–291.58 MiB**，包括采集记录和缓存；此值不是逐帧峰值，也不据此宣称原生分配归零。

已检查[城镇画面](player/town.png)与[野外战斗掉落画面](player/wild.png)，地表、角色、工作台与掉落正常显示。

使用隔离产品名 `DarkFlare-WorldAudit-G`，不读取真实玩家存档。临时采集脚本只进入验收构建，收尾删除。虚拟 Keyboard / Gamepad 通过 Input System 状态事件驱动真实 Interact Action、菜单和出口；隐藏窗口的采集工具使用 IgnoreFocus，正式游戏的后台输入设置不变。本记录不宣称实体硬件测试。

场景截图使用 Main Camera 渲染至 1920×1080 RenderTexture，不包含 Overlay UI；菜单可见性、交易和打造由运行时断言核对。资源稳定点比较 Addressables owner / lease / entry、地图对象登记、排序和交互对象，并记录往返耗时和已分配内存。采集环境运行着 Editor，耗时仅用于本阶段装配观察。

## 验收过程中修复

- MapScope 激活后同步生成 Manual CompositeCollider，避免候选在禁用祖先下得到空碰撞。
- Addressables 与场景目录可能加载不同 SO 实例，地表规则改从权威内容目录编译。
- 恢复使用实际 Prefab 足迹和物理接触容差，避免用出生 / 导航余量拒绝合法贴墙存档；纯预检先检查地图身份及阻挡内部。
- 地图出口不再暴露 Shop 菜单权限；丢弃物品改用当前地图几何；外部卸载后清理兼容已销毁根节点。
- Continue 等待前台摘要预检，退出检查点等待已有保存，保留预检错误原因；切图自动保存失败显示已有本地化错误提示。

测试维护保留地图冻结 / 全局身份、事务回退、历史迁移和输入可达性回归；复用既有 Scene Flow、Session、保存、状态及内容校验套件。对象预览撤销测试改为有上限地等待实际异步刷新，继续检查同一地表实例与准确位置，避免依赖固定 8 帧。临时 Player 采集器在构建完成后移出 Assets，未增加版本专属运行测试。

## 正式构建

[Windows x64 正式构建](production-build.json)成功，0 错误，产物 `Builds/Alpha053/DarkFlare.exe`，版本 `0.5.3-alpha`；已排除临时采集脚本，产品名恢复为 DarkFlare。两条警告分别为未配置 Player Pipeline 调试接口，以及既有 TMP Shader 调试 pragma 已弃用。Editor 恢复 Reload Scene Only 基线。

动态绕障、Flow Field / A*、Spatial Hash / Separation、Job / Burst 和 500 / 1000 活动单位性能留给 0.5.4。
