# 消耗品阶段 2 验收

2026-09-15。范围：四种正式药水、坚韧状态、商人获得路径、统一 Use 路由、详情与反馈、键鼠 / 手柄操作、历史内容兼容及资源绑定。运行版本保持 `0.4.1-alpha`，core v5、Schema 3。Windows Player 综合构建和跨进程发布验收由阶段 3 完成，本记录不将旧构建视为包含当前改动。

## 行为验证

- `Phase1UxPlayModeTests.Consumables_BuyMergeUseAndRestoreFocusThroughKeyboardAndGamepad`：购买两份药水合堆；商品不可直接使用；满生命不扣除；商店默认出售且菜单提供使用；键盘和手柄连用只消耗所选实例一件；耗尽关闭菜单并恢复焦点；鼠标右键在眩晕时完成回血和净化；药剂耗尽后坚韧保留并显示；打造拒绝消耗品，Shell 接管拒绝使用。
- `SaveCaptureRestorePlayModeTests`：正式法力药水使用后余量与资源经序列化、新 Session 恢复；不重复回蓝；旧请求拒绝；原有材料临时能力、来源状态、装备、地面掉落和随机续行覆盖保留。
- `SaveRestorePreparerTests`：core v3 / v4 的追加内容升级保持源文档、完整 Payload、物品数值与商人库存不变；既有 v1 / v2 历史迁移保留。
- 正式内容、完整本地化引用、配置文档覆盖和项目架构策略均通过统一 EditMode 校验。掉落资产和运行版本未修改。

实际全量结果见 [tests.json](tests.json)。保留原有使用核心 22 项行为回归；扩展现有交互、保存恢复及迁移测试，没有新增版本专用测试套件。一次性截图采集方法已移出 Assets。

## 视觉和输入证据

Unity Editor PlayMode 实际渲染，使用 Input System 测试设备注入键盘、鼠标和手柄事件；不是实体手柄人工验收。伪本地化通过项目 PseudoLocalizationUtility 扩展现有文本，不作为正式语言发布。矩阵覆盖中文 / 英文 / Pseudo × 1920×1080 / 1280×720，并检查浮窗确实可见、文本尺寸与菜单边界。未使用隐藏浮窗的早期截图作为最终证据。

| 场景 | 中文 | 英文 | Pseudo |
| --- | --- | --- | --- |
| 1920 详情 | [详情](zh-Hans-1920-details.png) | [详情](en-1920-details.png) | [详情](qps-ploc-1920-details.png) |
| 1920 菜单 | [菜单](zh-Hans-1920-menu.png) | [菜单](en-1920-menu.png) | [菜单](qps-ploc-1920-menu.png) |
| 1920 HUD | [HUD](zh-Hans-1920-hud.png) | [HUD](en-1920-hud.png) | [HUD](qps-ploc-1920-hud.png) |
| 1280 详情 | [详情](zh-Hans-1280-details.png) | [详情](en-1280-details.png) | [详情](qps-ploc-1280-details.png) |
| 1280 菜单 | [菜单](zh-Hans-1280-menu.png) | [菜单](en-1280-menu.png) | [菜单](qps-ploc-1280-menu.png) |
| 1280 HUD | [HUD](zh-Hans-1280-hud.png) | [HUD](en-1280-hud.png) | [HUD](qps-ploc-1280-hud.png) |

[地面掉落](zh-Hans-world.png)验证四件丢弃物均生成，截图可见范围内显示图标、名称与数量。[状态窗口](zh-Hans-status.png)显示坚韧、层数、剩余时间、属性效果和消耗品来源；玩法日志仍保留内部来源身份。

## 图标与清理

四药水图标和状态图标逐张生成、审计并使用 Unity 工具导入和绑定；详见[合同](asset-contract.md)、[提示与来源](prompts.md)、[药水审计](sprite-audit.json)和[状态审计](status-audit.json)。满幅状态背景接触画布边缘的警告符合既有家族规格。

保持 Scene Reload Only 基线；清理测试造成的字体和 Prefab 自动序列化噪声。文档、配置说明和完成计划一并更新，本阶段未执行 Git 提交。
