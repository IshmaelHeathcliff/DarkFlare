# I：Alpha 0.5 综合交付验收

状态：2026-09-19，**I1–I5 全部完成**，交付 `0.5.5-alpha`，core v6、Schema 5、地图文档 V2。[I 执行计划](../../../plan/archive/alpha-0.5.5-integration-plan.md)与[Alpha 0.5 总计划](../../../plan/archive/alpha-0.5-plan.md)及配套方案一并归档。

## 交付结果

- 当前地图制作链：从空白图手绘并应用 AI 补丁，四地表与五类数据、变体、入口、导入导出、撤销重做、冲突和失败回退通过，见[操作证据与可导入样图](./workflow.md)。
- 当前 Player：交易、打造、混合寻路绕过动态墙、战斗掉落、离场 30 秒冻结、城镇 / 野外跨进程继续及真实 Schema 4 迁移通过；树、帐篷和四段长障碍的前后遮挡已查看实际截图。
- [当前组合压力](./performance.md)：500 / 1000 活动怪物各三轮，每轮预热 10 秒、采样 60 秒，全部满足 H 预算。500 总帧 / 导航主线程 P95 为 **7.05–8.53 / 1.21–1.33 ms**；1000 为 **15.01–17.23 / 2.30–2.47 ms**。导航 GC 正向探针有效，六轮稳态分配事件均为零。
- 1000 单位经过一次往返预热后，20 轮切图全部维持 1000 Agent、1001 注册对象、6 owner、46 lease；回城 / 回野外的原生容量分别保持固定基线。快照、序列化、持久保存与继续恢复的耗时及托管内存观察峰值单独记录。
- 最终 [EditMode](./editmode-final.json) **722/722**、[PlayMode](./playmode-final.json) **85/85**，零失败、零跳过；[正式配置校验](./configuration-final.json)内容零问题、237/237 字段覆盖。
- [Windows 生产构建](./production-build.json)成功，输出 `Builds/Alpha055/DarkFlare.exe`。正式产品名 `DarkFlare`，临时采集器已删除；最终源码 / 程序集与 Editor 状态见[交付核验](./delivery-validation.json)。

## 覆盖矩阵

| 目标 | 本轮证据 | 长期行为覆盖 |
| --- | --- | --- |
| Dual Grid / Wang、四地表与变体 | [新地图与正式预览](./editor/result.json) | TerrainResolver / TextureCompiler / TextureBuild |
| 地表、障碍、装饰、交互、区域，手绘 / AI 与交换 | [SceneView 与 AI 联验](./workflow.md) | MapDocument / Importer / MapEdit |
| 渲染、碰撞、导航同源 | 新地图水 / 道路查询、当前 Player 动态墙绕障 | MapGeometry / Navigation |
| 对象 Y 排序与分段 | [Player 近景与排序值](./player/player-visual.json) | WorldSorting / MapObjectPresentation / WorldSortingPlayMode |
| 城镇交易打造、野外战斗掉落 | [当前新游戏链](./player/player-new.json) | WorldMapPlayMode、交易 / 打造 / 战斗 / 输入套件 |
| 离场冻结、身份、库存、导航意图、旧档 | [野外继续](./player/player-wild.json)、[城镇继续](./player/player-town.json)、[Schema 4 迁移](./player/player-legacy.json) | WorldMapState / WorldMapPlayMode / SaveSerializer |
| Flow / A*、Job / Burst、500 / 1000 | [独立 Player 六轮矩阵和 20 轮往返](./performance.md) | Navigation / NavigationPlayMode |
| 失败回退、取消、外部卸载、旧句柄、存档故障 | [最终完整回归](./playmode-final.json) | ApplicationLifecycle / WorldMapPlayMode / LocalSaveStorage / SaveCoordinator |

## 身份、诊断与边界

本轮基线为 `65429e0`，包含 H 导航提交 `259c27d`，见[基线身份](./baseline-identity.json)、[初始配置](./configuration-baseline.json)和[初始 EditMode](./editmode-baseline.json)。A–H 历史数据保留原阶段含义，不能代替 I 本轮结果。

采集 Player 运行版本为 `0.5.4-alpha`，产品名为独立的 `DarkFlare-IntegrationAudit-I`；通过验收并移除采集器后，生产版本更新为 `0.5.5-alpha`。没有改变生产寻路、存档或玩法算法，也没有新增长期测试套件。当前模块文档修正了地表、静态几何、对象表现、世界层级中尚未接入后续阶段的旧描述。

- 输入使用 Input System 虚拟键盘 / 手柄；实际物品操作调用正式 Command，未宣称实体手柄全按钮人工验收。
- 重叠截图为可见遮挡诊断，拍摄时临时关闭角色物理；没有修改排序号，不表示角色可以穿过碰撞体。
- 1000 单位大规模切图 / 保存仍有明显耗时和托管分配，见性能报告；此项与导航稳态预算分开。内存峰值是定时观察值，不是保证捕获所有瞬时峰值。
- Separation 是软避让；临时 stamp 离场清除，未扩展为严格无重叠群体规划或持久化建造。
- 初次变体样点不兼容、采集器 float / double 比较误报、冷启动搜索工作区尚未建立三项诊断均保留在对应报告，修正采样方法后重新验证，没有调整产品规则或性能门槛。
- 构建警告按实际 JSON 分类：Pipeline 无运行配置时在 Player 关闭；TMP 旧 shader pragma 属既有包警告。初始临时采集器 nullable 警告已随工具清理移除。生产包不包含自动建档 / 采集入口。

正式玩家存档与正式地图源未改动。临时地图、采集脚本和自建恢复副本已清理；本轮仅保留交付版本、模块文档、归档计划与验收证据。
