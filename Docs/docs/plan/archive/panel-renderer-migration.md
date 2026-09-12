# PanelRenderer 迁移计划

## 目标

将 Bootstrap 与 Main 的 UIDocument 迁移为 Unity 6.6 原生 PanelRenderer，复用 UXML、共享 PanelSettings 和 200 / 100 排序，保持输入、暂停和 Session 隔离。

## 实施

1. 使用 RuntimePanelView 接收版本化 UI 重载回调，统一先解绑再绑定，处理同一根节点下子树重建。
2. 迁移玩法 Controller、应用 Shell 和场景组件；场景通过 Unity MCP 保存。
3. 更新现有测试访问方式，补齐视觉树重载、禁用再启用、输入可达和订阅唯一性回归。
4. 运行项目 EditMode / PlayMode，检查真实渲染、场景切换、控制台并更新模块文档，归档计划。

## 成功标准

- 正式场景和运行时代码不再依赖 UIDocument。
- 前台 / Settings / HUD / 背包 / 商店 / 打造的交互和层次保持正确。
- 重载后使用新视觉元素；关闭与场景卸载没有残留输入、暂停或事件订阅。
- 测试通过；保留 Reload Scene Only 项目基线与用户已有修改。

## 完成记录

2026-09-12：运行时代码、Bootstrap / Main 场景和现有测试已完成迁移；EditMode 434/434、PlayMode 67/67、重载专项 2/2 通过。模块说明已同步输入 UI、Shell、HUD、工作台、项目结构和概览；验收见 [记录](../../assets/acceptance/panel-renderer-migration/README.md)。
