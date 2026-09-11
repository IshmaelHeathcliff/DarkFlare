# Domain Reload 兼容检查

2026-09-11–12，Unity `6000.6.0f1`，游戏版本 `0.3.4-alpha`。范围为 Editor Play Mode 生命周期；升级 API 与字体修复见[Unity 6.6 升级记录](./unity-6.6-upgrade.md)。

## 检查方式

读取生命周期、静态状态与订阅代码后，通过 Unity MCP 切换实际 Enter Play Mode 选项，使用隔离的测试数据根执行游戏流程。没有以手工调用静态重置替代进入 Play Mode；临时探针仅观察正式重置与关闭结果，并在清理阶段恢复默认设置。

| 模式 | 新游戏 → 暂停 → 返回前台 → 停止 | 游戏内暂停后直接停止 |
| --- | --- | --- |
| 默认 Domain / Scene Reload | 连续 2 次通过 | 未单独扩展 |
| 关闭 Domain Reload | 连续 3 次通过 | 连续 2 次通过 |
| 同时关闭 Domain / Scene Reload | 连续 3 次通过 | 连续 2 次通过 |

共 12 次完成的循环，逐次退出观察见[矩阵证据](../assets/acceptance/unity-6.6-domain-reload/matrix.json)。临时采集器等待 Editor 真正退出后才采纳结果，排除 stop 请求已返回但仍处于 Play 的中间观察。

每次游戏启动检查唯一宿主、唯一 EventSystem、InputReady 与暂停订阅各一次、隔离存档根、Session generation 递增；旧输入资产已销毁、旧租约失效。失焦会正常触发平台暂停，因此在检查菜单暂停前显式模拟焦点恢复。正常返回前台后，保留同一 Application 输入资产并清空 Session；退出后输入、事件、任务和 Addressables 租约均释放。每个完成循环的 Console Warning / Error 均为 0。

## 修复的问题

- 测试隔离入口 `Alpha027PlayModeDataEnvironment.Install` 在关闭 Domain Reload 后重复执行，却保留上一轮安装句柄，第二次进入会抛出“应用数据路径工厂已安装”。入口现在先释放旧安装并安全清理旧测试目录，再创建新目录；安装工厂仍保留重复占用保护，不允许回退到真实玩家数据。
- 启动中停止 Play Mode 时，本地化返回 `Cancelled` 被宿主先判定为失败，产生错误日志并进入 FailBoot。音频加载存在同样的结果判定顺序。两个 await 后均先检查所属取消 Token，再判断真实失败；未取消的资源 / 本地化失败仍走原错误路径。

长期回归分别加入现有 `ApplicationLifecyclePlayModeTests` 与 `ApplicationHostSceneTransitionPlayModeTests`：重复运行初始化后目录与工厂切换、取消结果不得改写宿主状态。未新增版本专用测试套件。

完整回归另暴露了既有随机化采样测试对 Editor 焦点的依赖：一次采集 6/12 个样本超时，单独重跑通过；再次完整运行采集 11/12 个样本时观察到 Editor 失焦、`timeScale = 0`、一个暂停租约。该用例现在在采样期间显式恢复 `FocusLost`，继续通过正式生成器验证固定种子序列；样本数、超时及序列断言不变，其他暂停原因不清除。平台失焦暂停行为仍由独立输入生命周期测试覆盖。

## 测试执行说明

连续切换测试筛选后，MCP / Test Runner 曾返回成功但实际执行 0 项；这些空运行不计为验收。恢复默认 Play Mode 设置并刷新程序集后，重新发现了完整 65 项 PlayMode 测试。此处记录观察与恢复方式，尚未定位到具体第三方包内部根因。

2026-09-12 最终结果：

- EditMode：434/434 通过，0 跳过，20.33 秒。
- 两项新增生命周期回归定向 2/2 通过，后续完整套件中也均通过。
- 最后一次完整 PlayMode：64/65 通过，随机化采样通过；既有 `MainScene_PointerDragMovesInventoryItemAndEquipsIt` 在“装备槽拖回背包”断言出现一次失败。该用例此前完整运行通过，随后定向复测 1/1 通过（1.92 秒）。根因尚未确认，保留为 UI 回归稳定性待查项，不把分次通过合并声称全量 65/65。
- 原始结果保留于 `Temp/domain-edit-results.xml`、`Temp/domain-play-after-focus-fix.xml`、`Temp/domain-pointer-results.xml`；实际 Domain Reload 循环证据独立归档。临时探针代码与 `.meta` 已删除。
- 独立正常启动 Bootstrap 达到 `Ready / FrontEnd`，启动和停止后 Console Warning / Error 均为 0，编译无错误。字体动态数据与 EditorSettings 已按执行前备份恢复，Editor 留在 Bootstrap 非 Play 状态。

## 应急隔离边界

在 Main 中直接停止 Play Mode 时，Unity 可能停止 PlayerLoop，无法等待 Session 异步回滚完全结束。项目既有合同会把 Session 标为 `Abandoned`，取消任务并释放应用资源，暂时保留架构；当次停止后可能仍观察到一个尚未完成的取消 continuation。它不等同于正常 Session 清理成功，也不能据此在同一次运行里启动下一代 Session。

下一次进入 Play Mode，`SubsystemRegistration` 会应急释放旧架构；旧租约失效，generation 保持单调，新宿主重新初始化。两种关闭 Domain Reload 的模式均验证了该路径。此次没有绕过隔离约束或强行让 Abandoned 变回可运行状态，详见[生命周期合同](./alpha-0.2-runtime-contract.md)。

## 静态状态审查

| 状态 | 结论 |
| --- | --- |
| ApplicationHost / InputReady / 日志安装 | 由 SubsystemRegistration 重置；正常销毁注销 wantsToQuit 和异常监控 |
| GameArchitectureProvider / QFramework 架构 | 正式 lease 管理 Deinit；旧 generation 不复用，应急遗留在下一次静态重置释放 |
| GameTimeService.Shared | 新宿主恢复暂停与时间比例；Scene Flow Dispose 注销 PauseChanged，旧 pause ID 不复用 |
| Input、音频、场景与异常事件 | 归属 Application owner，循环中未出现重复宿主、重复绑定或资源租约增长 |
| 内容类型元数据、Glyph 与只读规则缓存 | 缓存不持有场景对象，不需要每次 Play 重建；程序集变化仍会触发脚本域重载 |
| 文件临时序号、安装代际 | 保持递增，避免旧句柄或文件名与新一轮冲突 |
| QFramework 全局事件 API | 当前业务未使用 TypeEventSystem.Global / EasyEvents 全局入口；结论不为将来未登记的静态订阅提供保证 |

Unity 官方说明关闭 Domain Reload 后必须显式处理静态字段及事件，见[域重新加载](https://docs.unity3d.com/cn/6000.0/Manual/domain-reloading.html)。

## 适用范围

检查收尾时曾恢复 `EnterPlayModeOptions.None`。2026-09-12 用户确认改用 **Reload Scene Only** 并提交：`m_EnterPlayModeOptionsEnabled = 1`、`m_EnterPlayModeOptions = 1`（`DisableDomainReload`），保留 Scene Reload。此配置对应上表已验证的“关闭 Domain Reload”模式，现为项目提交基线；后续测试应恢复该基线。

支持上述已验证的 Bootstrap / Main 流程，不表示所有任意场景、热重编译、第三方 Editor 窗口或 Windows Player 已全面验收。Player 的历史构建警告与进程退出提示另见原升级记录，不以 Editor 验证替代 Player 验收。
