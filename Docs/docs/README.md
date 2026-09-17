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
- [世界渲染与稳定层级](./world-rendering.md)
- [已完成计划归档](./plan/archive/README.md)

## 计划状态

2026-09-17：**Alpha 0.5 总计划已建立，待实施**，见[地图、地图编辑器与动态寻路总计划](./plan/alpha-0.5-plan.md)及[0.5.0 地图基础执行计划](./plan/alpha-0.5.0-map-foundation-plan.md)。范围包含 Dual Grid + Wang Tile 四种地表、五类地图数据、手工 / AI 编辑、Y 排序、城镇 / 野外与 Job / Burst 批量寻路。用户确认 500 活动怪物、1000 压力测试，以及切图保留状态、离场暂停。尚未开始功能实现，运行版本保持 `0.4.2-alpha`。

Alpha 0.4 已完成统一物品、状态、统一使用与四类消耗品，并补充状态 HUD 和随机商店。当前 core v5、Schema 3；最近记录为 2026-09-16 EditMode 568/568、PlayMode 75/75 通过，见[随机商店验收](./assets/acceptance/random-shop/README.md)。该轮未重建 Windows Player；最近的独立 Player 验收属于 2026-09-15 [消耗品交付](./assets/acceptance/alpha-0.4-item-use-acceptance/README.md)，不能代替随机商店实机验收。本次文档梳理未重跑测试。

Alpha 0.1、0.2 基础设施、0.3 UI 与十槽装备，以及 0.4 已完成计划均见[归档索引](./plan/archive/README.md)。当前功能以各模块文档为准，归档中的“下一阶段”保留历史含义。

## 说明

当前项目已完成首版最小循环、初步体验优化及 Alpha 0.1–0.4 已归档计划，现已建立 Alpha 0.5 总计划及首阶段执行计划。已完成的执行计划统一保存在 `Docs/docs/plan/archive/`。
后续新增系统、配置或流程时，应优先更新对应文档，避免 `Docs/docs/` 与实现脱节。游戏设计文档位于 `Docs/design/`，不属于 Agent 的常规维护范围。
