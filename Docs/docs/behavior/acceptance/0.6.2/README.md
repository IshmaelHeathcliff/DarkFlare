# Alpha 0.6.2 守卫 Boss 验收记录

2026-09-23：守卫 Boss 的地图点位、行为、战斗、表现、内容和持久化已实现，并生成 `0.6.2-alpha` Windows 构建。完整自动回归与两种构建均通过。用户决定跳过本阶段 Windows 构建验收：独立 Player 操作、跨进程继续、Player 生命周期和 300 / 600 性能矩阵**未执行，按用户豁免，不记为通过**。0.6.2 按调整后的门槛结束，[阶段计划](../../../plan/archive/alpha-0.6.2-guardian-boss-plan.md)已归档。

## 已验证

- [完整 EditMode](editmode-final.json)：812/812，0 失败。覆盖 Boss 技能门禁、动作状态、地图 V3、core v8、Schema 7、旧图修订与旧档迁移、内容引用和编辑器 Patch / Undo / Redo。全量运行时还修复了 Odin 字段回调导致无关资源变化重建地表预览的问题。
- [完整 PlayMode](playmode-final.json)：97 项中 95 通过、0 失败；2 项 Unity Input System 包内 Windows 鼠标移动测试因包自身标记不稳定而跳过。正式场景 Boss 测试覆盖挥击预警与单次伤害随机消费、冲锋锁向后躲避、低血量强化一次、击杀后切图不复活。现有 20 轮切图测试扩充为同时核对 Boss 稳定身份、行为 / 导航注册数、冻结状态、掉落、随机流和原生导航容量。
- [开发构建](development-build.json)与[非 Development 构建](production-build.json)均成功、0 错误。后者位于 `Builds/Alpha062/DarkFlare.exe`，使用 Bootstrap / Main、产品名 `DarkFlare`、版本 `0.6.2-alpha`，Runtime Pipeline 关闭；2 项警告分别为 Pipeline 关闭提示和已有 TextMeshPro Shader pragma 弃用。[构建身份](artifacts.json)记录 EXE、Core / Runtime 程序集及 Addressables 目录哈希，并确认正式 Player 未包含 Pipeline Runtime 程序集。

## 用户豁免的验收项

[启动阻止记录](player-launch-blocked.json)：开发构建 `Builds/Alpha062Audit/DarkFlare.exe` 启动时，Windows Code Integrity 事件 3077 拒绝未满足 Enterprise signing level 的程序。系统拒绝发生在进程创建之前，与游戏加载、Boss 脚本或存档无关；本机未取得独立 Player 运行结果。因此不能将 PlayMode 测试推断为真实 Player 的键鼠 / 手柄、跨进程继续、正式启动或预警可见性验收。

同一原因使 300 常规 / 600 压力单位、每档诊断开关各三轮的 Windows Player 性能矩阵无法采集。0.6.1 对 500 / 1000 普通怪物的历史性能数据仍有效，但没有同场 Boss，不能代替 0.6.2 门槛。20 轮切图目前为 PlayMode 证据，也未替代 Player 生命周期实测。

上述项目是 0.6.2 的明确豁免项，而非失败或通过记录。后续若重新要求独立 Player 验收，应在可运行的环境中单独执行：Boss 全流程与键鼠 / 手柄、关键动作阶段的跨进程继续、20 轮切图资源收敛、300 / 600 性能矩阵和非 Development 构建启动。该豁免不自动改变 0.6.5 综合交付的性能目标。
