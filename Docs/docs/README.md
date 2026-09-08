# DarkFlare 文档索引

## 当前文档

- [项目概览](./project.md)
- [测试维护规范](./testing/test-maintenance.md)
- [Alpha 0.3 前置测试审计](./testing/test-suite-audit.md)
- [目录结构](./project-structure.md)
- [应用生命周期与会话作用域](./infrastructure/application-lifecycle.md)
- [稳定身份、内容目录与迁移框架](./infrastructure/content-identity-migration.md)
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

## 计划状态

Alpha 0.3 前置[测试审计与精简](./testing/test-suite-audit.md)已完成：EditMode 424/424、PlayMode 56/56 通过，一次性 / 过时测试已清理，后续维护规则已建立。[执行计划已归档](./plan/archive/test-suite-maintenance-plan.md)。

`alpha 0.2.0–0.2.7` 已完成。规划期总计划、阶段计划与验收过程已移入[已完成计划归档](./plan/archive/README.md)；后续实现以长期运行时契约和各基础设施模块文档为准。

`alpha 0.3` 已开始 [UI 迭代](./plan/alpha-0.3-plan.md)：独立背包 / 商店 / 打造窗口、实时属性详情、暗黑像素 HUD 和统一视觉组件。[alpha 0.3.0 首阶段](./plan/alpha-0.3.0-independent-ui-windows-plan.md)已完成设置 / 返回确认与背包的交接修复；独立窗口、共享物品操作和视觉切片仍待实施。[启动资料梳理](./plan/archive/alpha-0.3-startup.md)已归档。

首阶段优先修复设置 / 返回主菜单与背包的交互阻塞，并按[统一物品交互合同](./plan/alpha-0.3-item-interaction-contract.md)验收拖动、右键、手柄、单一高亮与详情排版。

手柄导航须按实际视觉位置四向移动并支持面板边缘自然跨窗；[alpha 0.3.4 收尾计划](./plan/alpha-0.3.4-equipment-completion-plan.md)扩充十槽装备，每个部位至少两件正式配置，并完成旧档兼容与综合验收。

## 说明

当前项目已完成首版最小循环、初步体验优化、`alpha 0.1` 封板和 `alpha 0.2` 基础设施封板。文档以现有目录、包依赖和脚本入口为准整理；已完成的执行计划统一保存在 `Docs/docs/plan/archive/`。
后续新增系统、配置或流程时，应优先更新对应文档，避免 `Docs/docs/` 与实现脱节。游戏设计文档位于 `Docs/design/`，不属于 Agent 的常规维护范围。
