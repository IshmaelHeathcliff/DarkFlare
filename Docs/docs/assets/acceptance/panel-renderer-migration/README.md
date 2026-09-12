# PanelRenderer 迁移验收

日期：2026-09-12；Unity 6000.6.0f1；画布 1920×1080。

- Bootstrap / ApplicationShell 与 Main / UIRoot 已使用原生 PanelRenderer，排序分别 200 / 100，共用 GamePanelSettings。实际运行检查旧 UIDocument 数量为 0。
- EditMode：434 / 434，通过结果见 `editmode.json`。
- PlayMode：67 / 67，通过结果见 `playmode.json`；包含键鼠、手柄、装备对比、交易打造、Shell 层级与跨 Session 生命周期。
- 新增的两个长期行为用例位于现有 Phase1Ux 与 Alpha025InputOwnership 套件：覆盖重载释放旧元素、禁用 Renderer / 宿主后恢复、模态确认仅执行一次、焦点与输入所有权恢复。补齐测试失败路径资源还原后专项 2 / 2 再次通过。
- 前台、背包和设置覆盖层实机图见 `frontend.png`、`inventory.png`、`settings.png`，已逐张检查。
- 手工检查使用隔离的 PlayModeTests 数据目录；没有改写正式玩家存档。最后保留 Bootstrap 非运行状态和 Reload Scene Only 设置。
- 清理了本次 Unity 自动写入的 Prefab / 字体缓存及 Main 中无关 SpriteRenderer 序列化升级；未修改 UXML、USS 或美术资源。未执行 Player 构建。

迁移说明见 [输入与运行时 UI](../../../input-ui-system.md#面板生命周期)。
