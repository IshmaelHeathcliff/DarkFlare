# alpha 0.2 综合验收记录

> 状态：`alpha 0.2.0–0.2.7` 已完成
> 验收日期：2026-08-26
> 版本：`0.2.7-alpha`
> 基线提交：`be85dd4`
> Unity：`6000.4.3f1`
> 长期契约：[alpha 0.2 长期运行时契约](./alpha-0.2-runtime-contract.md)

## 结论

`alpha 0.2` 的应用生命周期、稳定身份、迁移、存档、设置、本地化、场景流、UI Shell、输入、音频、可访问性、平台生命周期、日志、错误处理和 Addressables 资源治理已经形成同一条可运行闭环。封板补齐了自动档删除、完整默认设置恢复、PlayMode 用户数据隔离、迁移夹具、综合故障证据、发布构建与长期文档入口。

机器可读证据位于：

- [历史覆盖清单](../testing/archive/alpha-0.2.7/Alpha027AcceptanceCoverage.json)
- [历史构建记录](../testing/archive/alpha-0.2.7/Alpha027ReleaseAcceptance.json)
- `Assets/Scripts/Tests/Fixtures/Migration/manifest.json`

2026-09-07 测试审计将前两项原样移出运行测试目录；其中版本、计数、源码路径和方法名均描述当时证据，不作为后续版本的测试约束。当前套件维护结论见[测试审计](../testing/test-suite-audit.md)。

## 功能闭环

- FrontEnd 可新建、继续、二次确认删除 `auto` 自动档；删除只在 FrontEnd、无 Session 且 Save Coordinator 空闲时执行。
- Settings Page 可二次确认恢复 `UserSettingsSnapshot.Default`；Locale、Binding Override / Glyph、Audio 与 Reduce Motion 使用同一提交快照即时收敛。
- 综合 PlayMode 在隔离根内完成：冷启动 → 英文 → 重绑定 → NewGame → 保存 → FrontEnd → Continue → 再保存 → 删除自动档 → 恢复默认 → 重启 Host。
- 删除后 Continue 立即禁用且重启后保持；恢复设置不创建存档，删除存档不修改设置。
- Save V0 / Settings V0 代表样本使用严格 UTF-8，带来源版本与预期迁移摘要，不含绝对路径或真实用户数据。

## 自动回归

| 层级 | 结果 |
| --- | --- |
| Unity 编译 | 0 Error / 0 Warning |
| EditMode | 443/443 通过 |
| 项目 PlayMode | 56/56 通过 |
| 完整 PlayMode | 60 项：58 通过、0 失败、2 Ignore |
| 已知 Ignore | Input System issue 1252825 的两项 Windows 鼠标集成测试；与封板前登记一致 |
| 策略扫描 | 18/18 通过，零未登记违规；例外清单仍为 8 项且均含原因与移除条件 |

## 故障矩阵

| 故障 | 自动证据 | 结果 |
| --- | --- | --- |
| 当前 / 双代存档损坏 | `LocalSaveStorageTests.LoadLatest_CorruptCurrentFallsBackButAllCorruptFailsSafely` | 当前损坏回退 Backup；全部损坏安全失败 |
| 校验篡改 / 未来 Schema | `SaveSerializerTests.Deserialize_RejectsTamperingFutureSchemaDuplicatePropertiesAndDeepJson` | 拒绝且不提交状态 |
| 迁移步骤失败 | `MigrationPipelineTests.StepFailure_ReturnsStructuredReportAndLeavesInputUnchanged` | 结构化报告，输入不原地修改 |
| ContentId / 派生状态不一致 | `SaveRestorePreparerTests.Prepare_RejectsCatalogMismatchAndDerivedMonsterStateMismatchBeforeCommit` | Main 加载前拒绝，不创建 Session |
| 磁盘提交失败 / 取消 | `LocalSaveStorageTests.Commit_MoveFailureOrCancellationPreservesPreviousGenerationAndCleansTemporaryFile` | 上一有效代际保留，临时文件清理 |
| 删除部分 IO 失败 | `LocalSaveStorageTests.DeleteSlot_IoFailureReportsPartialStateWithoutTouchingOtherSlots` | 返回部分状态，不越权其他槽位 |
| 场景替代 / 取消 | `Alpha024SceneFlowFoundationTests.SceneFlow_LatestRequestSupersedesActiveAndCompletesExactlyOnce`、`SceneFlow_CancelledLoadCompensatesWithoutStartingSession` | latest-wins，失败补偿且不启动 Session |
| 资源失败 / 取消 / owner 提前关闭 | `Alpha026DiagnosticsTests.ResourceService_FailureCancellationAndEarlyOwnerCloseReleaseEverything` | 句柄、in-flight、owner 与 lease 回到基线 |
| 首个致命根因 / 重复异常 | `Alpha026DiagnosticsTests.FailureCoordinator_PreservesFirstFailureAndCountsDuplicates` | first-failure-wins，重复计数不覆盖根因 |
| 重叠挂起 / 退出 | `AccessibilityAndPlatformLifecycleTests.PlatformLifecycle_CoalescesOverlapAndRestoresOnlyAfterAllReasons` | 重叠 reason 合并，全部释放后才恢复 |
| Abandoned 迟到 continuation | `AbandonedGenerationIsolationTests.EmergencyStop_LateContinuationsCannotLeaveAbandonedOrTouchNewGeneration` | 不改写终态、不触碰新 generation |

## UI 与输入矩阵

- `Alpha027ShellUiMatrixPlayModeTests` 覆盖 `zh-Hans`、`en`、`qps-ploc` × 1280×720、1920×1080、2560×1440 的 FrontEnd、Settings 与错误 Modal，验证关键边界和可见文本测量。
- `Phase1UxPlayModeTests` 与 `EquipmentPhase2PlayModeTests` 覆盖玩法 HUD / 面板、三档分辨率、键鼠与手柄焦点、选择及导航。
- `Alpha025InputOwnershipPlayModeTests` 覆盖 FrontEnd / Paused 两处 Settings 入口、唯一 Runtime Action Asset、UI Context 与 pause lease 所有权。

## 构建与 Player

- Addressables 配置验证为 0 issue，已执行 clean content build；四个 `DarkFlare-*` 分组的 Windows catalog、settings 与 bundle 生成成功。
- Windows `StandaloneWindows64` Development Player 构建成功，输出在仓库外；0 Error、12 条 Unity 包 / 构建管线警告，Editor Console 为 0 Error / 0 Warning。正式 Managed 程序集只包含 `DarkFlare.Core.dll` 与 `DarkFlare.Runtime.dll`，未发现 Test、Editor 或 NUnit 程序集；反射确认 `InstallForTests` 与测试所有者实现未进入 Player DLL。
- 隔离产品名 Player 已从 Bootstrap 冷启动并显示 `alpha 0.2.7` FrontEnd。Windows 防火墙安全提示阻止了自动化真实 Player 点击路径；同一 NewGame / Save / Continue / Delete / Reset / Restart 流程由真实 `ApplicationHost` PlayMode 测试完成。

## 数据安全与验收事件

- 最终隔离实现不只依赖运行时初始化回调：每次 PlayMode 测试运行都在 OneTimeSetUp 重建测试 Host 与隔离根；`ApplicationBootstrap` 的 Subsystem 静态重置保留仍由测试夹具持有的路径覆盖。
- 修复后项目 PlayMode 56 项全量运行前后，真实 Settings / Saves 四个文件的相对路径、长度与 SHA-256 完全一致；`PlayModeTests` 运行目录子项为 0。
- 验收过程中曾发现旧隔离实现会在连续 Test Runner 运行及显式 Bootstrap 静态重置后回退到真实路径；该验证运行已把本机真实自动档代际从先前记录推进到测试数据。问题已修复并加入回归，但应用的两代保留策略未留下原始代际副本。本机在仓库外保留了发现问题时的数据副本；需要从外部备份恢复原玩家数据。

## 当前边界

- 只有 `auto` 自动档，没有手动槽位、Profile 选择、重命名、云同步或跨设备迁移。
- 只有 Bootstrap 与单一 Main，不包含多地图、Addressables Scene、远端 Catalog 或 CDN。
- Text Scale、High Contrast、Screen Shake 与 Display Mode 仍是数据预留，尚未作为玩家可操作设置开放。
- Windows 防火墙提示属于本机 Player Connection 环境限制；发布前仍应在人工允许或关闭该提示后补做一次真实 Player 完整点击冒烟。
