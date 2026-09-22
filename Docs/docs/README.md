# DarkFlare 文档索引

## 当前文档

- [项目概览](./project.md)
- [架构图索引](./architecture/README.md)
- [Unity 高层架构图与代码依据](./architecture/unity-overview/README.md)
- [装备、词条与伤害作用流程](./architecture/equipment-affix-damage/README.md)
- [Unity 6.6 升级兼容](./infrastructure/unity-6.6-upgrade.md)
- [Domain Reload 兼容检查](./infrastructure/domain-reload-compatibility.md)
- [测试维护规范](./testing/test-maintenance.md)
- [Alpha 0.3 前置测试审计](./testing/test-suite-audit.md)
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
- [alpha 0.2 综合验收记录](./infrastructure/alpha-0.2-acceptance.md)
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
- [世界渲染与稳定层级](./world-rendering.md)
- [已完成计划归档](./plan/archive/README.md)

## 计划状态

2026-09-22：**Alpha 0.6.0 行为基础完成**，交付 `0.6.0-alpha`，core v6 / Schema 5 / 地图 V2 不变。[行为模块](./behavior/README.md)包含 Core 内独立 HFSM 与行为树、组合样例、Animator 桥接、Editor 实时图与开发 Player 面板；完整 EditMode **788/788**、PlayMode **87/87**，独立开发 / 普通 Windows Player 及核心规模采集通过，见[验收](./behavior/acceptance/0.6.0/README.md)。[阶段计划及合同](./plan/archive/alpha-0.6.0-behavior-foundation-plan.md)已归档，[总计划](./plan/alpha-0.6-plan.md)继续推进 0.6.1 普通怪物，后续为守卫 Boss、有限刷怪及手动重置。

2026-09-19：**Alpha 0.5 的 A–I 全部完成**，交付 `0.5.5-alpha`、core v6、Schema 5、地图文档 V2。地图制作、世界排序、双地图玩法、保存冻结和混合批量导航的综合验收见 [I 验收](./maps/acceptance/i/README.md)。

最终 EditMode **722/722**、PlayMode **85/85**；当前 Windows Player 的交易 / 打造 / 绕障 / 冻结 / 跨进程继续、Schema 4 迁移、500 / 1000 单位三轮战斗压力及 1000 单位 20 轮切图通过。生产构建 `Builds/Alpha055/DarkFlare.exe` 已移除临时采集入口。历史 A–H 数据仍保留各自阶段含义。

[总计划](plan/archive/alpha-0.5-plan.md)、[I 执行计划](plan/archive/alpha-0.5.5-integration-plan.md)及配套方案已归档；当前实现入口以模块文档为准。

## 说明

当前项目已完成首版最小循环、初步体验优化及 Alpha 0.1–0.4 已归档计划，Alpha 0.5 的地图基础、编辑器、世界层级、双地图、批量导航与综合交付均已完成。已完成的执行计划统一保存在 `Docs/docs/plan/archive/`。
后续新增系统、配置或流程时，应优先更新对应文档，避免 `Docs/docs/` 与实现脱节。游戏设计文档位于 `Docs/design/`，不属于 Agent 的常规维护范围。
