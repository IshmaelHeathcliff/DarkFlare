# Alpha 0.3 前置测试审计

> 日期：2026-09-07
> 状态：已完成；Alpha 0.3 测试维护前置已满足

## 范围与基线

项目测试源码共 97 个文件：EditMode 68 个（66 个含测试、2 个辅助），PlayMode 29 个（24 个含测试、5 个辅助）。逐文件审查测试意图、断言类型、夹具依赖及重复覆盖；不因类名带 Alpha / Phase 就删除有效测试。

- 清理前项目 EditMode：442/442 通过，MCP job `762b1e6512974773a25e6d5e9f3f00e3`。与历史封板的 443 项不同，本次以实际程序集结果为准。
- 清理前项目 PlayMode：完成 56 项，报告 1 项失败，MCP job `e91e3f41173b47ffac05a1a0bd0fcdf0`。固定种子回放需要 12 个怪物样本，限时只取得 7 个；本次清理前已出现，保留该用例并单独复核。
- PlayMode 起初因 Editor 未聚焦停滞，使用 Unity MCP 聚焦 Game View 后完成。未通过修改测试数量、Ignore 或降低采样断言处理失败。
- 第三方程序集由 Unity / Input System / 其他包维护，不修改供应商与包缓存测试。根目录生成的包 `.csproj` 不代表项目自有测试；Test Runner 的临时场景由 Runner 清理。

## 已确认可精简项与覆盖对照

以下清理不修改任何 Runtime / Editor 玩法实现，不修改公式、价格、槽位或内容配置。

| 项目 | 理由 | 保留的保障 |
| --- | --- | --- |
| `Alpha027AcceptanceContractTests` | 只锁定 0.2.7 版本、历史清单与源码方法名称；不能检测当前功能行为 | 对应领域及 PlayMode 流程保留；历史 Coverage JSON 归档 |
| `Alpha027ReleaseAcceptanceTests` | 只读取旧构建成功 JSON，并要求当时的 Windows 阻挡状态永远成立；没有执行当前构建 | 历史 Release JSON 归档；后续版本构建用真实当次结果验收 |
| `ItemWorkbenchStructureTests` | 十项均读取 UXML / USS / C# 字符串，锁定共享工作台、互斥页签、特定宽高 / 字号及实现调用 | `Phase0ExperiencePlayModeTests` 保留真实点击 / 拖动 / 装备；`Phase1UxPlayModeTests` 保留右键出售、打造槽、详情防闪现与布局；`EquipmentPhase2PlayModeTests` 保留装备焦点 / 布局；`GameInputTests` 保留拿起 / 导航 / Cancel 输入 |
| `Phase5VisualIntegrationTests.ItemWorkbench_UsesThinScalableFramesWithoutLegacyNineSlice` | 重复旧工作台细边框 / 无背景源码约束，与已确定 UI 迭代冲突 | 保留正式 Sprite 导入 / 引用、图标加载生命周期及真实 UI 布局回归 |
| `GameplayUiFoundationTests.AttributesMoveFromHudToInventoryContext` | 只验证旧版本把属性从 HUD 搬到背包的 UXML 字符串，不保护属性结果 | 保留 HudSnapshot、装备属性刷新、三语言 UI 边界；同时去掉视觉集成中重复的无背景字符串断言 |
| `ConfigurationDocumentationCoverageTests.ConfigurationDocumentationManifest_Exists` | 下一项已调用 ScanOfficial；实现明确把缺清单报告为失败，单独 Exists 无新增故障覆盖 | `OfficialConfigurationDocumentation_CoversDiscoveredTypesAndFields` 及扫描器缺字段 / 孤儿 / 重复映射负例全部保留 |
| 两项 `OfficialContent_Passes…Validation`（AffixCoverage / MonsterAffixContent） | 都无差别调用同一个 ContentConfigurationValidator.Scan 并 Assert.IsEmpty，与 Phase4 入口完全重复 | `Phase4ContentTests.OfficialContent_HasExpectedCountsIdsAndNoValidationIssues` 原样保留；两个词条测试文件中的真实生成 / 消费 / 确定性断言全部保留 |
| 三处旧分辨率循环 | 当前 visual-style 明确仅保证 1920×1080，720p / 1440p 属历史 | 保留所有测试方法、中文 / 英文 / Pseudo Locale、输入流程、文本测量与边界断言，仅缩减分辨率参数 |

宽泛的第二批精简未采用：不删除输入 / 场景流值类型合同，不把内容 / 资产计数批量改为非空，不删除已有视觉迁移验证，不因待迭代就提前解除当前装备和内容规则。进一步调整应随消费者变化评估独有保障。

一次批量补丁被自动审批拒绝，执行器已部分写入前五文件；随后已用本次读取的原文精确恢复，确认测试目录回到清理前状态，再按上述明确覆盖对照收窄操作。用户随后确认已修改权限并要求继续；最终范围按覆盖价值判断，未重新采用宽泛删除。

## 保留原则

领域原子性、真实历史存档迁移、失败补偿、输入与重绑定、跨 Session 隔离、玩家数据根隔离、资源句柄释放、纯算法边界均保留。现有 PlayMode 集成场景包含不同故障路径，不为减少文件数强行合成巨型用例；本次不重命名版本前缀，避免无价值引用迁移。

长期规则见[测试维护规范](./test-maintenance.md)，全部 97 个原始测试 / 辅助文件的决定见[逐文件审查清单](./test-suite-inventory.md)。

## 清理后验证

- 测试源码由 97 个文件精简为 94 个文件；减少 18 项 EditMode 测试，56 项 PlayMode 行为全部保留。
- EditMode：424/424 通过，0 失败 / Ignore，MCP job `489b75acfb584f7582157b61e0357ab6`，约 22.7 秒。减少 18 项，未以运行时长波动宣称性能提升。
- 固定种子回放单独复核：1/1 通过，MCP job `ba27dc00742f46d9961bb6a9923797cf`，约 35.1 秒；12 个样本及两轮序列比较原样保留。基线失败未通过删测或降低断言掩盖，后台帧推进环境是后续排查重点。
- 完整 PlayMode：56/56 通过，0 失败 / 跳过，约 64.4 秒。Unity 原生 TestRunnerApi run `7a757df7-ece5-478b-a529-7faddd82b2fe`，原始结果见 [PlayMode XML](./archive/2026-09-07/playmode-results.xml)；包含上述固定种子回放，未修改其采样和比较断言。
- MCP 包的 `TestRunnerService` 会临时启用 DisableDomainReload；本次完整 PlayMode 改由 Unity 原生 TestRunnerApi 执行，保持项目规定的 EnterPlayModeOptions 为 0。临时 Editor 回调、程序集、生成的 csproj 与相关 `.meta` 已清理；两个 EditorSettings 选项均恢复为 0，项目设置无遗留差异。
- 删除源码后出现过旧编译清单的 CS2001；通过完整 AssetDatabase 刷新重新生成清单，当前编译错误为 0，无 Runtime 改动。
- Console 仍保留故障注入用例的预期错误日志，以及 Input System 的 `Already added touchscreen` 断言 / 异常记录；后者在清理前运行中也出现，本轮未修改包或运行时处理。测试全量通过不等于 Console 无错误；本轮只确认编译错误为 0，并单列该既有输入设备清理问题。

本次只完成测试维护前置，Alpha 0.3 的窗口、交互与装备功能仍按原计划实施。[执行计划已归档](../plan/archive/test-suite-maintenance-plan.md)。
