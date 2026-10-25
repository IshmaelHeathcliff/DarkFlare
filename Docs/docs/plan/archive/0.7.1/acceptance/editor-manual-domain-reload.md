# Editor 手动脚本域刷新验收

2026-10-17，`main` / `d74aeb1` 加本次工具改动；Unity `6000.6.6f1`、运行版本 `0.7.1-alpha.5`。菜单为 `DarkFlare/工具/手动刷新脚本域`，实现见 `Assets/Scripts/Editor/ScriptDomainReloadMenu.cs`。

| 检查 | 实际结果 |
| --- | --- |
| 编译与菜单 | Unity 完成编译，无编译错误；实际展开 DarkFlare / 工具，菜单项可见并能点击 |
| 编辑模式执行 | 原生界面点击后观察到程序集初始化；随后通过正式菜单路径再次调用，探针记录 `beforeAssemblyReload` 从 0 次变为 1 次 |
| 运行时限制 | Bootstrap 前台 Play Mode 中校验返回 false；直接调用执行方法也未触发重载，计数保持 0 |
| 恢复与设置 | 停止运行后校验恢复 true；刷新后 Editor 就绪、不在 Play / 编译中，`enterPlayModeOptionsEnabled = true`、选项数值 1 保持 |

原始命令结果与源码指纹见[验证数据](editor-manual-domain-reload.json)。一次性探针位于 `Temp/`，仅用于观察正式菜单与真实重载事件；已清除探针、SessionState 标记，未增加永久测试套件。

编译 / 导入与 Play Mode 切换中的禁用条件已随代码编译，但未人为延长这些瞬态过程做独立 UI 采集。未运行全量 EditMode / PlayMode 或构建 Player；本次未修改运行时逻辑、场景、数据格式和自动 Domain Reload 设置。历史套件数字不作为本次测试结果。

入口：[实施计划](../plans/editor-manual-domain-reload-plan.md) · [当前使用说明](../../../../infrastructure/domain-reload-compatibility.md#手动刷新脚本域)。
