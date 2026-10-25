# Editor 手动脚本域刷新计划

2026-10-17，已完成。源码基线 `d74aeb1` 加本次菜单改动，Unity `6000.6.6f1`；运行版本保持 `0.7.1-alpha.5`。本项为 Editor 工具补充，不新增发布阶段。实际结果见[验收记录](../acceptance/editor-manual-domain-reload.md)。

## 实施与成功标准

1. 在 `DarkFlare/工具/手动刷新脚本域` 添加 Editor 菜单，使用 `EditorUtility.RequestScriptReload()` 重载程序集并重置静态状态。
2. Play Mode 与其切换过程、编译、资源导入期间禁用；执行方法再次校验状态，避免程序调用绕过限制。
3. 保持 Reload Scene Only 项目设置，使用时无需临时启用自动 Domain Reload；不增加运行时入口或更新数据版本。
4. 编译并验证菜单入口、允许状态和实际域重载；该薄封装采用当次验证，不新建永久测试套件。记录无法执行的项目。
5. 更新[Domain Reload 说明](../../../../infrastructure/domain-reload-compatibility.md)，将本计划及验收结果归档到 `archive/0.7.1/`。
