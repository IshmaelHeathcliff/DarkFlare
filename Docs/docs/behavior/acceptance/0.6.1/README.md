# Alpha 0.6.1 验收

2026-09-23，**M1–M6 完成**，交付 `0.6.1-alpha`、core v7、Schema 6、地图 V2；[阶段计划](../../../plan/archive/alpha-0.6.1-monster-behavior-plan.md)。普通怪物性能验收目标按用户最新要求为常规 300、压力 600 单位，本次 Player 实测规模为更高的 500 / 1000 单位。

## 已验证的行为

三种正式怪物使用共享的普通行为配置，完成范围外休眠、遮挡闲逛、可见追逐、前摇 / 单次命中 / 后摇。配置字段、模块边界和接口见[普通怪物模块](../../monsters.md)。HFSM 与 BT 继续独立位于 Core，本阶段的地图、怪物与 Animator 接入位于 Runtime。

- [最终完整 EditMode](editmode-final.json)：808/808；其中新增 / 扩展回归包括独立行为规则、配置发现与文档、Schema 5 历史输入、core 5 / 6 追加迁移不修改源文档、按实例订阅表现事件的隔离与释放，以及托管 / Burst 导航查询差分。
- [最终完整 PlayMode](playmode-final.json)：92/92；包括真实遮挡、控制中断、导航接管、独立调试输入、切图四个提交边界回退，以及原地图 20 轮往返。
- 随后的动画语义收尾：[EditMode 14/14](presentation-editmode.json)、[PlayMode 7/7](presentation-playmode.json)。禁止移动不吞掉攻击前摇表现，恢复后摇立即同步表现；导航等待不显示行走。

首轮场景测试纠正了固定等待代替导航重建完成的夹具假设；全量回归发现 View 键与现有暂停冲突，正式调试入口改为 F8 / R3。旧状态测试改为等待有效导航恢复，并明确感知朝向，保留控制及重新绑定断言。

清理验收脚本后，既有地图对象预览测试暴露固定 240 次 Editor 迭代不足以等待异步预览的问题：[首轮](editmode-clean-first.json)为 806/807。改为最多 5 秒的真实时间等待，保留位移 / Undo / Redo / 地表不重建断言；[专项 8/8](map-preview-recheck.json)及随后完整 807/807 通过。导航补充回归后最终完整 808/808 通过。临时工具触发的基础设施策略检查没有加入豁免，工具移除后恢复通过。

## Windows 流程与输入

[开发构建](development-build.json)使用正式 Bootstrap / Main，产品名 `DarkFlareMonsterAudit`；读写独立 LocalLow 目录，未使用正式产品存档。[Player 流程](workflow.json)与[新进程继续](continue.json)来自实际 Windows Player。

保存的动作 ID 37、前摇剩余 0.19 秒、冷却 0.41 秒和行为随机序号 19，在 20 次往返及退出重启后保持。继续后只消费一次攻击随机数并结算命中。

[20 轮原始记录](travel.json)每次回图均为 1 个行为 / 导航实例、3 个 Session 注册对象、6 个资源 owner、46 个 lease，原生导航字节固定 4,304,896 B，未完成加载为 0。旧行为 Handle 在源图释放后失效。大规模保存性能仍以 Alpha 0.5 的专项记录为历史参考，本次不把单怪物往返解释为 1000 单位保存耗时。

[输入记录](panel.json)使用 Input System 虚拟 Keyboard / Gamepad：F8 / R3 打开，Esc / B 关闭，关闭恢复 Gameplay 输入并释放选择。它是注入输入验证，不代表实体手柄人工体验。

面板截图取自实际 Player 的 PanelSettings RenderTexture，黑色区域不代表场景背景：[中文](player-zh-Hans.png)、[英文](player-en.png)、[伪本地化](player-qps-ploc.png)。本次验证行为面板的文本、按钮与只读边界；伪本地化截图底部既有 HUD 的缺失翻译提示不属于行为面板，也不表示正式中英语言缺失。

## 普通 Player 与正式构建

[非开发 Player 启动检查](release-check.json)确认新游戏启动成功、`Debug.isDebugBuild = false`、行为诊断对象不激活。该次检查使用隔离产品名，见[构建记录](release-check-build.json)。之后移除临时采集与启动检查脚本，恢复产品名 DarkFlare，导航优化后在最终源码上完成 808 / 92 项完整回归。

[正式 Windows 构建](production-build.json)输出 `Builds/Alpha061/DarkFlare.exe`，使用 Bootstrap / Main、版本 `0.6.1-alpha`，无 Development 选项，Pipeline Runtime 关闭。构建成功、零错误；两项提示分别为 Pipeline 已关闭及既有 TMP Shader 的调试 pragma 弃用。正式 Runtime 程序集不含临时验收类型，文件 SHA-256 见[构建身份](artifacts.json)。

## 性能与归档

完整性能矩阵 12/12 轮通过：实测 500 / 1000 各三轮诊断开启、关闭，活动数符合实测规模。五类场景三轮中最差总帧 P95 分别为 **9.96 / 19.15 ms**，导航 P95 为 **1.31 / 2.52 ms**，满足原规模对应的 16.7 / 33.3 ms 总帧及 2 / 3 ms 导航预算，也覆盖调整后的 300 / 600 单位目标。方法、分项成本、分配边界和修正前结果见[性能报告](performance.md)。

临时采集 / 截图 / 启动检查脚本与生成配置已清理，保留长期行为、迁移、事务、输入和实例隔离回归。项目版本已更新，模块与配置文档同步，阶段计划归档；下一阶段为 0.6.2 守卫 Boss。有限总量刷怪和手动重置尚属后续阶段，当前离场保留状态的规则不变。
