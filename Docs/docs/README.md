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
- [世界渲染与稳定层级](./world-rendering.md)
- [已完成计划归档](./plan/archive/README.md)

## 计划状态

2026-09-19：**Alpha 0.5 的 A–H 已完成**。当前版本 `0.5.4-alpha`、core v6、Schema 5、地图文档 V2。H 的 Grid / Flow Field / A* / Spatial Hash / Separation / Job / Burst 已接入正式怪物，支持导航意图冻结恢复和动态阻挡，见[模块说明](./maps/navigation.md)及 [H 验收](./maps/acceptance/h/README.md)。

最终 EditMode **722/722**、PlayMode **85/85**；500 / 1000 单位 Player 三轮矩阵、普通 Player 复核、1000 单位 20 轮往返与 Windows 构建通过。[H 执行计划](./plan/archive/alpha-0.5.4-navigation-plan.md)已归档，下一阶段为 0.5.5 全范围综合交付。

G 已提交 `9e6f2f2`；A–G 的历史数字与证据见[计划归档](./plan/archive/README.md)。

## 说明

当前项目已完成首版最小循环、初步体验优化及 Alpha 0.1–0.4 已归档计划，Alpha 0.5 的地图基础、编辑器、世界层级与双地图已完成，批量导航已完成，后续进入综合交付。已完成的执行计划统一保存在 `Docs/docs/plan/archive/`。
后续新增系统、配置或流程时，应优先更新对应文档，避免 `Docs/docs/` 与实现脱节。游戏设计文档位于 `Docs/design/`，不属于 Agent 的常规维护范围。
