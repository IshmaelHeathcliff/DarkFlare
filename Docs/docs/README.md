# DarkFlare 文档索引

## 当前文档

- [项目概览](./project.md)
- [版本管理与 SemVer 迭代规则](./version-management.md)
- [架构图索引](./architecture/README.md)
- [Unity 高层架构图与代码依据](./architecture/unity-overview/README.md)
- [装备、词条与伤害作用流程](./architecture/equipment-affix-damage/README.md)
- [Unity 6.6 升级兼容](./infrastructure/unity-6.6-upgrade.md)
- [Domain Reload 兼容检查](./infrastructure/domain-reload-compatibility.md)
- [测试维护规范](./testing/test-maintenance.md)
- [目录结构](./project-structure.md)
- [应用生命周期与会话作用域](./infrastructure/application-lifecycle.md)
- [稳定身份、内容目录与迁移框架](./infrastructure/content-identity-migration.md)
- [状态时间、保存与查看](./status-lifecycle-ui.md)
- [本地存档与 Session 恢复](./infrastructure/local-save.md)
- [用户设置与本地化](./infrastructure/user-settings-localization.md)
- [游戏状态、场景流与应用 UI 外壳](./infrastructure/game-state-scene-flow-ui-shell.md)
- [Application Audio](./infrastructure/application-audio.md)
- [日志、错误处理与 Addressables 资源治理](./infrastructure/logging-error-addressables-governance.md)
- [可访问性与平台生命周期](./infrastructure/accessibility-platform-lifecycle.md)
- [alpha 0.2 长期运行时契约](./infrastructure/alpha-0.2-runtime-contract.md)
- [首版玩法循环](./gameplay-loop.md)
- [输入与运行时 UI](./input-ui-system.md)
- [物品 UI 工作台](./item-ui-workbench.md)
- [装备系统](./equipment-system.md)
- [统一物品系统](./item-system.md)
- [统一物品使用](./item-use-system.md)
- [正式消耗品与使用交互](./consumables.md)
- [状态系统](./status-system.md)
- [状态与角色战斗接入](./status-combat.md)
- [异常、抗性与状态来源](./status-ailments.md)
- [属性定义与调用关系](./stat-system.md)
- [独立属性详情](./attribute-details.md)
- [战斗 HUD](./hud-system.md)
- [随机化与掉落规则](./randomization-system.md)
- [首批内容池](./content-system.md)
- [商店与随机库存](./trading-system.md)
- [打造系统](./crafting-system.md)
- [伤害系统与词条系统设计](./damage-affix-system.md)
- [战斗标签系统](./combat-tag-system.md)
- [配置中心](./config-center.md)
- [配置参考索引](./config-reference/README.md)
- [美术资产生成规范](./visual-asset-generation.md)
- [视觉规范](./visual-style.md)
- [UI 组件视觉规范](./ui-component-style.md)
- [视觉资产清单](./visual-assets.md)
- [双层地表 Tilemap](./ground-tilemap.md)
- [地图文档与导入](./maps/README.md)
- [Dual Grid 坐标与地表规则](./maps/terrain-resolution.md)
- [地表分区与增量解析](./maps/terrain-partition.md)
- [地表诊断预览](./maps/terrain-preview.md)
- [地表诊断样例](./maps/terrain-samples.md)
- [正式地表材质家族合同](./maps/terrain-art-contract.md)
- [正式地表纹理编译](./maps/terrain-texture-compilation.md)
- [四材质与运行时地表资源](./maps/terrain-materials.md)
- [地图静态几何与通行查询](./maps/map-geometry.md)
- [地图编辑器与 AI 事务](./maps/map-editor.md)
- [地图对象表现与分段遮挡](./maps/object-presentation.md)
- [城镇、野外与地图生命周期](./maps/world-map-lifecycle.md)
- [批量导航与怪物移动](./maps/navigation.md)
- [行为管理与独立 HFSM](./behavior/README.md)
- [有限刷怪与地图名额](./behavior/finite-spawning.md)
- [刷怪地图手动重置](./behavior/map-reset.md)
- [世界渲染与稳定层级](./world-rendering.md)

## 计划与验收

- [Alpha 0.7 活动总计划](./plan/alpha-0.7-plan.md)
- [0.7.1 启动准备](./plan/alpha-0.7.1-startup.md)
- [0.7.1 技能面板与技能重构执行计划](./plan/alpha-0.7.1-skill-board-plan.md)
- [0.7.1 六键手动施法、输入与存档合同](./plan/alpha-0.7.1/manual-skills.md)
- [0.7.1 能量瓶、充能兑换与天赋成长合同](./plan/alpha-0.7.1/energy-progression.md)
- [已完成版本的计划与验收归档](./plan/archive/README.md)

完成的执行计划与验收文档、截图、日志和结果统一按版本收纳到 `plan/archive/<版本号>/`，各版本提供 `plans/`、`acceptance/` 与 `README.md` 入口。模块文档继续保留在对应职责目录。

## 计划状态

2026-10-06：**0.7.1 技能面板、技能重构与能量成长规划完成，待实施**。几何与六键手动施法已确认；新增三档 2×1 能量瓶、专栏积蓄、中心整瓶充能、余量跨瓶累计、1:1 属性 / 天赋点产出及柴薪 / 普通天赋独立点亮，见[执行计划](./plan/alpha-0.7.1-skill-board-plan.md)、[几何合同](./plan/alpha-0.7.1/geometry.md)、[六键合同](./plan/alpha-0.7.1/manual-skills.md)、[成长合同](./plan/alpha-0.7.1/energy-progression.md)与[验收清单](./plan/alpha-0.7.1/acceptance.md)。计划拆为五阶段，当前 `main` / `bd01a4c`、Unity `6000.6.4f1`，运行版本仍为 `0.7.0-alpha.2`；尚未开始实现，也未重跑 Unity 测试。

2026-09-30：**0.7.1 启动基线已核对**，`main` / `e2b97d2`，当前运行版本保持 `0.7.0-alpha.2`。已有能力、验收身份、6 个文件哈希匹配、工作区字体改动及未执行项已记录在[启动准备](./plan/alpha-0.7.1-startup.md)，[0.7 总计划](./plan/alpha-0.7-plan.md)已同步。功能范围与阶段验收标准待明确，本次未重跑 Unity 测试或构建 Player。

2026-09-29：**Alpha 0.7.0 两阶段完成**，运行版本 `0.7.0-alpha.2` / core v10 / Schema 9 / 1 / 地图 V4。不可变 SemVer 对象、`GameVersion` 解析桥接与历史字符串兼容已完成，核心定向 52/52、接入与存档定向 32/32、完整 EditMode 882/882、PlayMode 102/102，0 失败 / 跳过。见[执行与验收归档](plan/archive/0.7.0/plans/alpha-0.7.0-semver-plan.md)及[版本管理与 API](./version-management.md)；[0.7 总计划](./plan/alpha-0.7-plan.md)继续维护，后续 `0.7.1-alpha` 范围待明确。本版 Player、真实设备、跨进程及性能矩阵未执行。

2026-09-24：**Alpha 0.6.5 综合交付按调整门槛完成**，当前版本 `0.6.5-alpha` / core v10 / Schema 9 / 地图 V4。正式玩法、存档故障及 20 轮生命周期的 Editor 回归通过：EditMode 819/819，PlayMode 102 通过／2 包内跳过／0 失败。按用户要求，只有明确指定 Player 验收时才构建 Player；本阶段 Player、真实设备、跨进程及当前版本 300／600 性能矩阵未执行，见[验收记录](plan/archive/0.6.5/acceptance/behavior/README.md)及[归档计划](plan/archive/0.6.5/plans/alpha-0.6.5-integration-plan.md)。

2026-09-23：**Alpha 0.6.4 手动重置完成**，该阶段版本 `0.6.4-alpha` / 地图 V4 / core v10 / Schema 9。玩家可在刷怪地图传送门选择普通进入或二次确认后重置并进入；正常离场继续保留原进度。见[模块](./behavior/map-reset.md)、[验收](plan/archive/0.6.4/acceptance/behavior/README.md)和[归档计划](plan/archive/0.6.4/plans/alpha-0.6.4-manual-map-reset-plan.md)。

2026-09-23：**Alpha 0.6.3 有限刷怪完成**，当前交付 `0.6.3-alpha` / 地图 V4 / core v10 / Schema 8。正式野外图为 20 个入图名额、10 个分散触发点各 8 个名额及独立 Boss；旧已访问图保留进度。扩容后完整 EditMode 818/818、PlayMode 97 通过 / 2 包内跳过 / 0 失败；Windows Player 与 300 / 600 实测未执行。见[模块](./behavior/finite-spawning.md)、[验收](plan/archive/0.6.3/acceptance/behavior/README.md)及[已归档计划](plan/archive/0.6.3/plans/alpha-0.6.3-finite-spawning-plan.md)。下一阶段为 0.6.4 手动重置。

2026-09-23：**Alpha 0.6.1 普通怪物完成**，交付 `0.6.1-alpha` / core v7 / Schema 6 / 地图 V2。[模块](./behavior/monsters.md)覆盖休眠、闲逛、感知追逐、攻击与冻结恢复；最终 EditMode 808/808、PlayMode 92/92，Windows Player 和 500 / 1000 单位实测通过。历史规模超过后续 300 / 600 目标，但不替代当前版本实测，见[证据](plan/archive/0.6.1/acceptance/behavior/README.md)。[M1–M6 计划](plan/archive/0.6.1/plans/alpha-0.6.1-monster-behavior-plan.md)已归档。

2026-09-22：**Alpha 0.6.0 行为基础完成**，交付 `0.6.0-alpha`，core v6 / Schema 5 / 地图 V2 不变。[行为模块](./behavior/README.md)包含 Core 内独立 HFSM 与行为树、组合样例、Animator 桥接、Editor 实时图与开发 Player 面板；完整 EditMode **788/788**、PlayMode **87/87**，独立开发 / 普通 Windows Player 及核心规模采集通过，见[验收](plan/archive/0.6.0/acceptance/behavior/README.md)。[阶段计划及合同](plan/archive/0.6.0/plans/alpha-0.6.0-behavior-foundation-plan.md)与[总计划](plan/archive/0.6/plans/alpha-0.6-plan.md)均已归档。

2026-09-19：**Alpha 0.5 的 A–I 全部完成**，交付 `0.5.5-alpha`、core v6、Schema 5、地图文档 V2。地图制作、世界排序、双地图玩法、保存冻结和混合批量导航的综合验收见 [I 验收](plan/archive/0.5.5/acceptance/maps/i/README.md)。

最终 EditMode **722/722**、PlayMode **85/85**；当前 Windows Player 的交易 / 打造 / 绕障 / 冻结 / 跨进程继续、Schema 4 迁移、500 / 1000 单位三轮战斗压力及 1000 单位 20 轮切图通过。生产构建 `Builds/Alpha055/DarkFlare.exe` 已移除临时采集入口。历史 A–H 数据仍保留各自阶段含义。

[总计划](plan/archive/0.5/plans/alpha-0.5-plan.md)、[I 执行计划](plan/archive/0.5.5/plans/alpha-0.5.5-integration-plan.md)及配套方案已归档；当前实现入口以模块文档为准。

## 说明

当前项目已完成首版最小循环、初步体验优化及 Alpha 0.1–0.4 已归档计划，Alpha 0.5 的地图基础、编辑器、世界层级、双地图、批量导航与综合交付，以及 Alpha 0.6 的行为、Boss、有限刷怪、手动重置与 Editor 综合交付均已完成。Alpha 0.7 的首个小版本已完成 SemVer 对象与应用接入，运行版本为 `0.7.0-alpha.2`，后续小版本范围待明确。已完成的执行计划统一保存在 `Docs/docs/plan/archive/`。
后续新增系统、配置或流程时，应优先更新对应文档，避免 `Docs/docs/` 与实现脱节。游戏设计文档位于 `Docs/design/`，不属于 Agent 的常规维护范围。
