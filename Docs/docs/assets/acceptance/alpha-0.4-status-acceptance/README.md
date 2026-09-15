# 状态系统综合验收

2026-09-14，Unity 6000.6.0f1。状态阶段 1–4 及显示分档基线为 `a418188`；本轮修正两处已弃用的 UI 导航 API，复用原有长期测试，不新增版本专属套件。

## 自动化回归

EditMode 540/540、PlayMode 73/73，均无失败或跳过，见[结果摘要](./tests.json)。测试运行时已清理临时 Player 入口并恢复数据路径工厂。保留历史存档夹具和现有数据隔离保护，见[覆盖核对](./coverage.md)。

状态 UI 使用 `FocusController.IgnoreEvent` 与 `StopPropagation`，保持详情滚动和自定义方向导航的焦点语义，不再调用已弃用的 `PreventDefault`。

## 独立 Player

验收程序通过 Bootstrap / Main 和正式 Addressables 内容启动；临时编译定义仅为验收提供数据路径及场景操作入口。存档与设置写入程序旁的 `UserData/Saves`、`UserData/Settings`，不读写真实玩家存档。默认 Unity Player 日志仍使用 Unity 的默认日志位置。

状态来自统一 System 入口，正式异常配置和怪物掉落权重未修改。通过游戏内 Input System 注入设备状态验证输入，不宣称进行了实体手柄人工测试。截图来自可见独立 Player 的渲染结果，不以 Editor 截图替代。

已验证标记施加 / 消耗、眩晕限制与到期、暂停冻结、周期伤害、驱散、周期致死及复活清理。跨进程退出再启动后，层数、剩余时间和下一跳时间完全保留，见[重启结果](./player-restart.json)、[保存前结果](./player-before-restart.json)及[相位快照](./expected-phases.json)。相位快照为验收数据而非正式存档 DTO，无限下次时间以字符串记录。

最终在 `0.4.1-alpha` 重跑上述行为，并完成[前台往返](./player-cycle.json)与[输入和布局检查](./player-input.json)：继续游戏保持状态，新 Session 无旧层，七异常共 11 层在隐藏切换后不变。鼠标切换 HUD 可隐藏状态、键盘暂停 / 恢复、手柄确认切换均通过 Input System 设备事件执行。

| 排版 | HUD | 状态与长详情 |
| --- | --- | --- |
| 中文 1920×1080 | [截图](./player/zh-Hans-hud.png) | [截图](./player/zh-Hans-details.png) |
| 英文 1920×1080 | [截图](./player/en-hud.png) | [截图](./player/en-details.png) |
| Pseudo 1920×1080 | [截图](./player/pseudo-hud.png) | [截图](./player/pseudo-details.png) |
| 中文 1280×720 | [截图](./player/small-hud.png) | [截图](./player/small-details.png) |

Pseudo 为临时 ASCII 扩张压力文本，不新增正式语言，层数和倒计时保持原数值；动态刷新文本可能恢复原译文。截图验证右侧边界及详情滚动容器，完整焦点和到期移除规则由长期 UI 回归覆盖。

## 最终构建与启动

运行版本 `0.4.1-alpha`，core v4、Schema 3 保持不变。[验收构建](./acceptance-build.txt)与[干净 / 正式构建结果](./builds.json)分别留档。最终输出为项目下 `Builds/Alpha041/DarkFlare.exe`，普通 Windows x64 Mono 构建，Bootstrap / Main，包含正式 Addressables；不包含临时验收入口或额外验收编译定义。

移除入口并恢复正式数据路径工厂后，以独立产品名 `DarkFlare-Status-Smoke` 生成同代码的干净启动构建，避免读取真实用户设置 / 存档。该构建进入主菜单并显示 `0.4.1-alpha`，见[启动截图](./player/clean-startup.jpg)和[启动日志](./clean-startup.txt)。随后恢复产品名 `DarkFlare` 并生成正式交付构建；正式产品名的输出未再次启动，以免访问真实用户目录。

构建警告为可选 Pipeline 运行配置缺省（Player 不启用开发控制通道）及 TextMeshPro shader 的调试指令弃用；无构建错误。启动日志有环境音频设备不可用、D3D12 调试队列不可用提示，主菜单实际渲染正常。本轮没有音频验收结论。系统网络提示仍显示在启动截图中，未授予网络权限；状态交互证据来自隔离 Player 的 Input System 注入与渲染采集，不将系统提示遮挡下的原生鼠标点击计为通过。

## 验收过程中修正的夹具问题

最初周期样例使用玩家阵营，被友方伤害规则拦截；随后使用活怪物引用又可能被正常战斗击杀，最终使用稳定的脱离场景伤害来源。快速前台往返曾与“继续游戏”预检并发，磁盘文件独立反序列化和恢复预检均有效；验收入口改为等待预检结束，与按钮可用条件一致。场景切换会正常释放暂停租约，夹具曾保留旧引用，现已在每次切换后重取租约并通过连续验证。临时鼠标输入类型与 UI Toolkit 同名、诊断字段使用弃用 API 的问题也已清理。这些临时失败不计入长期套件通过结果。

初次 Addressables 构建遇到可选报告提示，关闭后继续。开发构建曾触发系统网络访问提示，验收没有授予网络权限；后续改用普通构建。早期构建报告还包含工具请求超时日志，最终构建结果单独记录。
