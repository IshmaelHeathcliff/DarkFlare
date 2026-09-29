# Alpha 0.7 启动准备

状态：2026-09-26，**启动准备完成并归档**。首个目标“简易 SemVer 版本对象”已由用户明确，现由[Alpha 0.7 总计划](../alpha-0.7-plan.md)接管。本记录保留启动时的基线与目标确定过程，不表示功能已经实施；启动源码版本为 `0.6.5-alpha`。

## 当前基线

| 项目 | 核对结果 | 依据 |
| --- | --- | --- |
| 分支与提交 | `main` / `751225c` | Git 当前状态；9 月 26 日怪物动画与测试输入清理修复 |
| 运行版本 | `0.6.5-alpha` | `ProjectSettings/ProjectSettings.asset` |
| Unity / URP | `6000.6.0f1` / `17.6.0` | `ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json` |
| 正式内容目录 | core v10 | `Assets/Data/Preset/Content/正式内容目录.asset` |
| 存档 / 设置 Schema | 9 / 1 | `Assets/Scripts/Runtime/Infrastructure/Persistence/VersionContracts.cs` |
| 地图交换格式 | 当前 DTO 与规范输出 V4 | `Assets/Scripts/Runtime/Maps/MapDocument.cs`、[地图合同](../../maps/README.md) |
| 正式地图源 | 城镇 V2 / r1、64×64；野外 V4 / r9、128×128 | `Assets/Data/Maps/World/town.dfmap`、`wilderness.dfmap`；旧格式在内存升级，不为启动准备改写源文件 |
| 野外名额 | 入图 20、10 处范围触发各 8，共 100 个普通怪物；1 个独立 Boss 点 | 野外源文件的 `spawnBatches` 与 `bossSpawns` |
| Editor 运行选项 | Reload Scene Only，关闭 Domain Reload | `ProjectSettings/EditorSettings.asset`：选项启用且数值为 1 |

启动时工作区已有 `Assets/Settings/UI/Fonts/GameCjkFont.asset` 未提交改动，本次保留原状；代码、Packages 和 ProjectSettings 相对 HEAD 无未提交修改。本次只核对文档、代码与配置，不读取 `Docs/design/`，不运行 Unity 测试或构建 Player。

## 已交付能力

| 范围 | 当前成果 | 维护入口 |
| --- | --- | --- |
| 基础设施 | FrontEnd、新建 / 继续、Session 隔离、auto 本地存档、设置、本地化、输入、日志与 Addressables 生命周期 | [项目概览](../../project.md)、[本地存档](../../infrastructure/local-save.md) |
| 构筑与物品 | 十槽二十件装备、格子背包与统一拖放、属性 / HUD、堆叠材料、随机商店、材料打造 | [装备](../../equipment-system.md)、[物品](../../item-system.md)、[交易](../../trading-system.md)、[打造](../../crafting-system.md) |
| 状态与使用 | 七异常、抗性、叠层 / 时间、来源隔离、状态保存与显示，四种消耗品 | [状态](../../status-system.md)、[消耗品](../../consumables.md) |
| 地图与导航 | 四地表、Dual Grid / Wang、手绘 / AI 原子编辑、稳定排序、城镇 / 野外往返、离场冻结、Flow Field / A* 与 Job / Burst | [地图](../../maps/README.md)、[地图生命周期](../../maps/world-map-lifecycle.md)、[导航](../../maps/navigation.md) |
| 行为基础（0.6.0） | 独立 HFSM / 行为树、Runtime 组合、Animator 桥接、Editor 实时图与开发 Player 诊断面板 | [行为模块](../../behavior/README.md) |
| 普通怪物（0.6.1） | 休眠、闲逛、感知、追逐、前后摇攻击与冻结恢复 | [普通怪物](../../behavior/monsters.md) |
| 守卫 Boss（0.6.2） | 独立名额、挥击、预警冲锋、强化阶段、动作保存与恢复 | [Boss](../../behavior/boss.md) |
| 有限刷怪（0.6.3） | 固定计划、范围批次、Pending / Alive / Defeated、击杀不补怪与旧图迁移 | [有限刷怪](../../behavior/finite-spawning.md) |
| 手动重置（0.6.4） | 传送门二次确认、地图代次、持久化提交与失败回退，普通进出继续保留进度 | [手动重置](../../behavior/map-reset.md) |
| 综合交付（0.6.5） | Editor 正式流程、存档故障边界与 20 轮生命周期回归；计划已归档 | [0.6.5 验收](../../behavior/acceptance/0.6.5/README.md) |

以上作为 0.7 的已有依赖，不重复列为待开发功能。

## 封版后变更与验收边界

- `84b0687`（2026-09-24）：完成 0.6.5 Editor 验收及归档。记录为 EditMode 819/819，PlayMode 102 通过、2 项包内跳过、0 失败。
- `7fedf34`（2026-09-26）：更新 `com.unity.pipeline` 至 `0.8.0-exp.1`。
- `751225c`（2026-09-26）：路径重算为 Pending / Busy 时保留已开始的 Move 表现，避免移动动画反复重播；完善测试输入运行时、原设备与项目 Actions 的切换恢复。模块文档和相关 EditMode / PlayMode 用例已经更新，见[普通怪物](../../behavior/monsters.md)及[输入文档](../../input-ui-system.md)。

本次没有重跑测试；819 / 102 是 0.6.5 归档时的历史结果，不作为 `751225c` 的本轮通过数。开始 0.7 实施时，按改动范围验证相关套件；输入或生命周期改动同时验证停止后再次运行及输入恢复。阶段收尾按[测试维护规范](../../testing/test-maintenance.md)执行项目回归并记录当次实际结果。

当前版本 Player、真实设备、跨进程中断 / 重启和 300 / 600 活动单位性能矩阵仍为未执行。历史 0.6.1 的 Player 与 500 / 1000 单位结果只作对照，不替代 Boss、有限刷怪与重置共存后的当前版本实测。依照现行门槛，仅在用户针对当前任务明确要求 Player 验收时构建；这些未执行项不阻塞 0.7 规划，也不能记为通过。

## 尚未交付的边界

- Profile 选择、手动存档槽管理与云同步。
- 背包旋转 / 重量；装备耐久、套装、完整纸娃娃与唯一装备特效。
- 多商人独立库存 / 回购；打造配方与批量操作。
- 关卡选择、Addressables Scene 与更高内容密度；现有视觉仍是功能原型基线。
- Text Scale、High Contrast、Screen Shake 与 Display Mode 仍是设置数据预留，未开放玩家 UI。

这些是当前边界，**尚未被指定为 0.7 目标**；不以未交付清单自动推定本版范围。

## 0.7 计划入口（启动时记录）

版本规则已由用户明确，长期合同见[版本管理](../../version-management.md)：小版本按 `0.7.0-alpha`、`0.7.1-alpha` 推进，首个小版本各阶段按 `0.7.0-alpha.1`、`0.7.0-alpha.2` 推进；下一个小版本的阶段号从 1 重新开始。无阶段号的 `alpha` 是起始基线或系列名称，实际阶段收尾保留最后一个 `alpha.N`。0.7 目前均预计为 alpha，下一里程碑或明确进入 beta 时转入 0.8。

开发文档目前没有既定的 0.7 功能清单。待明确主要玩家流程、必须交付与暂缓范围后，再建立 `alpha-0.7-plan.md`，从 `0.7.0-alpha` 小版本起拆分可独立验证的阶段，并为每阶段指定完整目标版本。

1. 阅读目标涉及的模块，核对代码 / 配置差距，优先复用现有工具与系统。
2. 明确首阶段的正常及失败路径、可观察成功标准、键鼠 / 手柄可达性和本地化。
3. 分别评估内容目录、存档 Schema 和地图格式影响；需要迁移时保留真实历史夹具，测试使用隔离数据根。
4. 按 QFramework 分层实现，异步任务可统一取消，资源归所属 Application / Session / Map 生命周期。
5. 复用已有模块测试，完成相关验证后交付对应 `alpha.N` 并更新模块文档；小版本完成时归档其执行计划。运行版本与验收记录按版本管理合同同步，内容版本与 Schema 分别评估。
6. 总计划接管后将本启动记录归档到 `plan/archive/`，同步入口链接。

启动准备完成标准：当前版本与已有能力有代码 / 配置依据，历史验收与未执行项区分清楚，封版后修复和工作区改动已记录，版本规则和规划入口可访问。当前已满足；功能实施的下一前置是明确 0.7 的功能目标与 `0.7.0-alpha` 各阶段验收标准。

2026-09-26：用户明确首先加入简易 SemVer 版本对象，由 [Alpha 0.7 总计划](../alpha-0.7-plan.md)接管；上方待明确目标的描述保留启动时含义。本启动准备已完成并归档。

