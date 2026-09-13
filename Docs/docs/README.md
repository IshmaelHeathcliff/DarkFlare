# DarkFlare 文档索引

## 当前文档

- [项目概览](./project.md)
- [Unity 6.6 升级兼容](./infrastructure/unity-6.6-upgrade.md)
- [Domain Reload 兼容检查](./infrastructure/domain-reload-compatibility.md)
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
- [统一物品系统](./item-system.md)
- [状态系统](./status-system.md)
- [状态与角色战斗接入](./status-combat.md)
- [异常、抗性与状态来源](./status-ailments.md)
- [属性定义与调用关系](./stat-system.md)
- [独立属性详情](./attribute-details.md)
- [战斗 HUD](./hud-system.md)
- [随机化与掉落规则](./randomization-system.md)
- [首批内容池](./content-system.md)
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

[alpha 0.4 状态系统](./plan/alpha-0.4-status-system-plan.md)已完成阶段 1 核心、阶段 2 角色战斗与[阶段 3 七异常、抗性及来源接入](./plan/archive/alpha-0.4-status-ailments-plan.md)。EditMode 全量 527/527、随后本地化专项 20/20、PlayMode 68/68 通过，见[验收记录](./assets/acceptance/alpha-0.4-status-ailments/README.md)与[异常模块](./status-ailments.md)。下一步为阶段 4 自动计时、存档和图标 UI。运行版本保持 0.4.0-alpha，core v4、Schema 2。

状态系统完成后实施[统一物品使用与消耗品](./plan/alpha-0.4-usable-items-plan.md)，建议版本 `0.4.2-alpha`：所有主动使用物品共用能力配置与执行事务，首批支持回血、回蓝、临时状态和驱散异常。该阶段已规划，尚未实现。

[alpha 0.4 统一物品系统](./plan/archive/alpha-0.4-item-system-plan.md)已完成：金币与材料入栏、堆叠 / 消耗、材料打造、掉落及旧档迁移。运行版本 `0.4.0-alpha`，EditMode 442/442、PlayMode 67/67；见[验收证据](./assets/acceptance/alpha-0.4-items/README.md)。

[alpha 0.3.4：十槽装备与内容补齐](plan/archive/alpha-0.3.4-equipment-completion-plan.md)已于 2026-09-11 完成并归档：十槽、二十件装备、滚动商店、旧档兼容；434/434 EditMode、63/63 PlayMode 和可见 Windows Player 验收通过。当时版本 `0.3.4-alpha`，见[验收证据及包级退出警告说明](./assets/acceptance/alpha-0.3.4-equipment/README.md)。[alpha 0.3 总计划](plan/archive/alpha-0.3-plan.md)同时归档。

[alpha 0.3.3：统一 UI 视觉组件](./plan/archive/alpha-0.3.3-unified-ui-visual-plan.md)已完成：共享材质、正式按钮 / 槽位、Shell 控件与三语言排版通过验收。当时运行版本 `0.3.3-alpha`，EditMode 432/432、PlayMode 63/63；见[组件规范](./ui-component-style.md)与[验收证据](./assets/acceptance/alpha-0.3.3-ui-style/README.md)。收尾阶段 [0.3.4 十槽装备与内容补齐](plan/archive/alpha-0.3.4-equipment-completion-plan.md)。

[alpha 0.3.2：暗黑像素 HUD 与战斗状态](./plan/archive/alpha-0.3.2-dark-pixel-hud-plan.md)已完成，当时版本 `0.3.2-alpha`；EditMode 432/432、PlayMode 63/63。见[HUD 模块](./hud-system.md)与[验收证据](./assets/acceptance/alpha-0.3.2-hud/README.md)。0.3.3 统一 UI 视觉组件也已完成。

[alpha 0.3.1：独立属性详情与实时解释](./plan/archive/alpha-0.3.1-realtime-attributes-plan.md)已完成，当时运行版本为 `0.3.1-alpha`。模块行为见[独立属性详情](./attribute-details.md)，全量回归与视觉证据见[验收记录](./assets/acceptance/alpha-0.3.1-attributes/README.md)。0.3.2 HUD 也已完成。

Alpha 0.3 前置[测试审计与精简](./testing/test-suite-audit.md)已完成：EditMode 424/424、PlayMode 56/56 通过，一次性 / 过时测试已清理，后续维护规则已建立。[执行计划已归档](./plan/archive/test-suite-maintenance-plan.md)。

`alpha 0.2.0–0.2.7` 已完成。规划期总计划、阶段计划与验收过程已移入[已完成计划归档](./plan/archive/README.md)；后续实现以长期运行时契约和各基础设施模块文档为准。

`alpha 0.3` 已开始 [UI 迭代](plan/archive/alpha-0.3-plan.md)：独立背包 / 商店 / 打造窗口、实时属性详情、暗黑像素 HUD 和统一视觉组件。[alpha 0.3.0 首阶段](plan/archive/alpha-0.3.0-independent-ui-windows-plan.md)已完成设置 / 返回确认与背包的交接修复；独立窗口、单窗买卖 / 打造、共享物品表现及空间导航已完成；统一拖放事务、快捷动作与代表视觉组件均已完成，运行版本更新为 `0.3.0-alpha`。[启动资料梳理](./plan/archive/alpha-0.3-startup.md)已归档。

首阶段优先修复设置 / 返回主菜单与背包的交互阻塞，并按[统一物品交互合同](plan/archive/alpha-0.3-item-interaction-contract.md)验收拖动、右键、手柄、单一高亮与详情排版。

[独立窗口可用闭环已归档](./plan/archive/alpha-0.3.0-window-workspace-plan.md)：EditMode 429/429、PlayMode 59/59 通过，[18 种组合截图与十槽空间线框](./assets/acceptance/alpha-0.3.0-windows/README.md)已留档。[统一物品操作与事务](./plan/archive/alpha-0.3.0-unified-item-actions-plan.md)已接通指定格购买、出售 / 丢弃、打造取回落点和统一快捷动作；[当前验收记录](./assets/acceptance/alpha-0.3.0-actions/README.md)。0.3.0–0.3.4 已全部完成。

[代表视觉组件与布局收尾](plan/archive/alpha-0.3.0-visual-pilot-plan.md)已完成：正式窗口底板、独立属性 / HUD 空间线框、展开属性排版与关闭图标通过验证。完整 EditMode 430/430、PlayMode 61/61，见[阶段验收](./assets/acceptance/alpha-0.3.0-visual-pilot/README.md)。

手柄导航须按实际视觉位置四向移动并支持面板边缘自然跨窗；[alpha 0.3.4 收尾计划](plan/archive/alpha-0.3.4-equipment-completion-plan.md)扩充十槽装备，每个部位至少两件正式配置，并完成旧档兼容与综合验收。

## 说明

当前项目已完成首版最小循环、初步体验优化、`alpha 0.1` 封板和 `alpha 0.2` 基础设施封板。文档以现有目录、包依赖和脚本入口为准整理；已完成的执行计划统一保存在 `Docs/docs/plan/archive/`。
后续新增系统、配置或流程时，应优先更新对应文档，避免 `Docs/docs/` 与实现脱节。游戏设计文档位于 `Docs/design/`，不属于 Agent 的常规维护范围。
