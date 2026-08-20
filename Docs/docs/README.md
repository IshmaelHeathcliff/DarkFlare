# DarkFlare 文档索引

## 当前文档

- [项目概览](./project.md)
- [目录结构](./project-structure.md)
- [应用生命周期与会话作用域](./infrastructure/application-lifecycle.md)
- [稳定身份、内容目录与迁移框架](./infrastructure/content-identity-migration.md)
- [本地存档与 Session 恢复](./infrastructure/local-save.md)
- [用户设置与本地化](./infrastructure/user-settings-localization.md)
- [首版玩法循环](./gameplay-loop.md)
- [输入与运行时 UI](./input-ui-system.md)
- [物品 UI 工作台](./item-ui-workbench.md)
- [装备系统](./equipment-system.md)
- [属性定义与调用关系](./stat-system.md)
- [随机化与掉落规则](./randomization-system.md)
- [首批内容池](./content-system.md)
- [打造系统](./crafting-system.md)
- [伤害系统与词条系统设计](./damage-affix-system.md)
- [战斗标签系统](./combat-tag-system.md)
- [配置中心](./config-center.md)
- [配置参考索引](./config-reference/README.md)
- [美术资产生成规范](./visual-asset-generation.md)
- [视觉规范](./visual-style.md)
- [视觉资产清单](./visual-assets.md)
- [双层地表 Tilemap](./ground-tilemap.md)
- [世界渲染与稳定层级](./world-rendering.md)
- [已完成计划归档](./plan/archive/README.md)

## 进行中的计划

- [alpha 0.2 基础设施开发计划](./plan/alpha-0.2-plan.md)
- [alpha 0.2 基础设施约束契约](./plan/alpha-0.2-infrastructure-contract.md)

`alpha 0.2.0–0.2.3` 已完成，当前实现见[应用生命周期与会话作用域](./infrastructure/application-lifecycle.md)、[稳定身份、内容目录与迁移框架](./infrastructure/content-identity-migration.md)、[本地存档与 Session 恢复](./infrastructure/local-save.md)和[用户设置与本地化](./infrastructure/user-settings-localization.md)，阶段执行计划已移入[已完成计划归档](./plan/archive/README.md)。下一阶段为 `alpha 0.2.4` 游戏状态、场景流与 UI 外壳。

## 说明

当前项目已完成首版单场景最小循环、初步体验优化、`alpha 0.1` 封板，以及 `alpha 0.2.0–0.2.3` 生命周期、稳定身份、内容目录、迁移、本地存档、用户设置和运行时本地化，正在继续推进 `alpha 0.2` 基础设施。文档以现有目录、包依赖和脚本入口为准整理；已完成的执行计划统一保存在 `Docs/docs/plan/archive/`。
后续新增系统、配置或流程时，应优先更新对应文档，避免 `Docs/docs/` 与实现脱节。游戏设计文档位于 `Docs/design/`，不属于 Agent 的常规维护范围。
