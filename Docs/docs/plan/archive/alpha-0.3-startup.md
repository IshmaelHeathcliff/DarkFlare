# alpha 0.3 启动准备

> 状态：启动资料梳理已完成并归档；版本目标由 [alpha 0.3 UI 迭代计划](alpha-0.3-plan.md) 承接，功能尚未实施。
> 梳理日期：2026-09-06
> 当前提交：`7d2ec22`（alpha 0.2.7 综合验收与封板）
> 当前版本：`0.2.7-alpha`；Unity：`6000.4.3f1`

## 已有基线

| 范围 | 当前成果 | 维护入口 |
| --- | --- | --- |
| 玩法循环 | 战斗 → 掉落 / 拾取 → 背包 / 四槽装备 → 交易 / 随机打造 → 再战斗 | [玩法循环](../../gameplay-loop.md) |
| 战斗与内容 | 攻击快照、命中 / 闪避 / 暴击、伤害转换、生命 / 法力恢复；23 项属性、7 件装备、25 个物品词条、10 个怪物词条、3 种怪物 | [属性](../../stat-system.md)、[伤害](../../damage-affix-system.md)、[内容池](../../content-system.md) |
| 物品交互 | 10×6 背包、拖拽移动 / 交换、精确穿脱装备、双戒指交换、键鼠 / 手柄导航、共享物品浮窗 | [物品工作台](../../item-ui-workbench.md)、[装备](../../equipment-system.md)、[打造](../../crafting-system.md) |
| 应用闭环 | Bootstrap 前台 → 新建 / 继续 → additive Main → 暂停 / 保存 → 返回前台；删除自动档、恢复默认设置 | [场景流与 UI 外壳](../../infrastructure/game-state-scene-flow-ui-shell.md)、[存档](../../infrastructure/local-save.md) |
| 基础服务 | 生命周期、稳定身份 / 迁移、本地化、输入、音频、时间、随机、平台挂起、日志 / 错误、资源所有权 | [长期运行时契约](../../infrastructure/alpha-0.2-runtime-contract.md) |

`alpha 0.1` 与 `alpha 0.2.0–0.2.7` 的计划已归档。2026-09-06 梳理时，常规开发文档中尚无既定的 alpha 0.3 功能清单；上述已完成能力不应重新列作待实现功能。

## 验收证据与未闭合事项

以下为 [alpha 0.2 综合验收记录](../../infrastructure/alpha-0.2-acceptance.md) 中 2026-08-26 的历史结果，本次未重新运行 Unity 测试或 Player：

- EditMode `443/443`、项目 PlayMode `56/56` 通过；完整 PlayMode 为 58 通过、0 失败、2 项已登记 Input System Ignore。
- Addressables clean build 成功、配置验证 0 issue；Windows Development Player 构建成功，0 Error、12 条包 / 构建管线 Warning。
- Player 冷启动成功；完整点击路径受 Windows 安全提示阻挡，发布前仍需补做真实 Player 的 NewGame → Save → FrontEnd → Continue → Safe Quit。PlayMode 已有同类流程证据，不能据此声称 Player 点击冒烟已完成。
- 历史测试隔离故障曾改写本机真实自动档；隔离机制已修复并通过真实文件哈希保护验证。原玩家数据恢复仍依赖外部备份，本次未验证其恢复状态。

机器可读证据：[历史覆盖清单](../../testing/archive/alpha-0.2.7/Alpha027AcceptanceCoverage.json)与[历史构建记录](../../testing/archive/alpha-0.2.7/Alpha027ReleaseAcceptance.json)，2026-09-07 测试审计已从 Assets 原样归档。本次已核对实际版本、构建场景、EventSystem 归属及物品拖放代码入口。

## 当前能力边界

- 当前只有单一 Main 玩法地图，战斗仍使用自动攻击；`Look` / `Attack` 尚未接入手动战斗操作。
- 持续伤害、异常状态、召唤物、反伤、大型天赋盘和复杂触发链尚未实现。
- 背包尚无旋转、堆叠或重量；装备尚无双持、武器组切换、耐久、套装和唯一效果。
- 交易尚无回购或多商人独立库存；打造尚无独立通货、材料、配方、锁词条或批量操作。
- 存档只有 `auto`；手动槽位、Profile 选择、云同步、多地图、远端 Catalog 尚未实现。
- Text Scale、High Contrast、Screen Shake、Display Mode 仍为数据预留；首批视觉和内容密度仍属功能原型。

这些是现状边界，是否纳入 alpha 0.3 取决于版本目标，不自动视为本版任务。

## 启动顺序与成功标准

1. 明确版本目标：描述要改善的玩家流程，列出本版范围、非目标和可观察验收条件。
2. 按目标核对相关模块与代码，形成总计划，再从 `alpha 0.3.0` 开始拆分可独立验收的阶段。
3. 每阶段先确定数据与稳定 ID、存档 / 内容版本影响、输入方式、本地化文本、资源所有权和异常 / 取消路径，再实现功能。
4. 开工时记录实际工作区与测试基线；按改动运行领域测试、相关 PlayMode、策略 / 配置验证，封板时再做综合回归和 Player 冒烟。
5. 完成阶段后更新模块文档并归档执行计划；版本号与 Schema / Content Version 分别管理。

准备完成的标准：基线与未闭合事项有来源，现有能力与待开发范围可区分；进入功能实现的条件：alpha 0.3 目标及首阶段验收标准已明确。

## 实施注意事项

- 沿用 QFramework Command / Query / Event 边界，以及 Application / Profile / Session / Scene 所有权；异步必须可取消，旧 Session continuation 不得触碰新 generation。
- 存档、本地化、输入、时间、随机、日志、音频、场景流和 Addressables 使用既有唯一入口；新增 UI 同时覆盖键鼠与手柄。
- PlayMode 必须使用既有隔离存储环境，不读写真实玩家 Settings / Saves。
- 版本升级前曾需处理 `Alpha027AcceptanceContractTests` 的固定版本断言及 `Alpha027ReleaseAcceptanceTests` 的历史记录比较；2026-09-07 前置测试审计已移除这两个一次性核验，保留历史 JSON，后续版本另行记录新证据。
- 本次起始工作区已有 `GameCjkFont.asset`、`GamePanelSettings.asset` 修改及未跟踪的 `Assets/AddressableAssetsData/Windows.meta`。这些是已有本地变更，后续提交前需检查来源与用途。

2026-09-07：用户明确 UI 迭代四项目标及独立窗口可同时显示的交互；待办已由正式总计划承接，本记录保留为启动时的历史基线。
