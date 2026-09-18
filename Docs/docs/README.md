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
- [世界渲染与稳定层级](./world-rendering.md)
- [已完成计划归档](./plan/archive/README.md)

## 计划状态

2026-09-18：**Alpha 0.5.0 的 A 数据导入与 B1–B3 地表规则、分区解析及诊断预览已实现**，见[地图文档模块](./maps/README.md)及[阶段计划](./plan/alpha-0.5.0-map-foundation-plan.md)。下一步为 B4 技术样例、视觉矩阵和压力验收，随后正式地表和静态几何；整个 0.5.0 尚未完成。总范围仍含手工 / AI 编辑、Y 排序、双地图与 Grid / Flow Field / A* / Job / Burst 导航。规模为 500 活动怪物、1000 压测，切图保留状态并暂停离场模拟。运行版本保持 `0.4.2-alpha`，core v5、Schema 3。

A 已提交为 `2bf3843`，B1 已提交为 `0c866f7`；[B 执行计划](./plan/alpha-0.5.0-wang-preview-plan.md)已实现 B3 独立诊断预览，可查看真实地图源或 256 Key 画廊；尚未完成正式地表资产。

本轮完整 EditMode 650/650、PlayMode 75/75 通过，B3 补充 7 项预览资源 / 生命周期 / 导入刷新回归；内容与配置文档覆盖校验通过，见[预览验证](./maps/terrain-preview.md#验证与边界)。本轮未构建 Windows Player。Alpha 0.4 历史记录仍见[随机商店验收](./assets/acceptance/random-shop/README.md)及[消耗品交付](./assets/acceptance/alpha-0.4-item-use-acceptance/README.md)。

Alpha 0.1、0.2 基础设施、0.3 UI 与十槽装备，以及 0.4 已完成计划均见[归档索引](./plan/archive/README.md)。当前功能以各模块文档为准，归档中的“下一阶段”保留历史含义。

## 说明

当前项目已完成首版最小循环、初步体验优化及 Alpha 0.1–0.4 已归档计划，Alpha 0.5 正在执行首阶段，数据与导入切片已完成。已完成的执行计划统一保存在 `Docs/docs/plan/archive/`。
后续新增系统、配置或流程时，应优先更新对应文档，避免 `Docs/docs/` 与实现脱节。游戏设计文档位于 `Docs/design/`，不属于 Agent 的常规维护范围。
