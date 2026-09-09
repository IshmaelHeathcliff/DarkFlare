# alpha 0.3.2 HUD 验收

2026-09-09；实现基线 `b36c90e`；Unity 6000.4.3f1。范围为正式底部资源仪表、现有技能读数、入口 / Glyph、降低动态与生命周期。完整 UI 皮肤和十槽装备仍分别属于 0.3.3、0.3.4。模块见[战斗 HUD](../../../hud-system.md)。

## 验证记录

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| 项目 EditMode | 432/432，0 失败 / 跳过 | [NUnit XML](./editmode-results.xml) |
| 项目 PlayMode | 63/63，0 失败 / 跳过 | [NUnit XML](./playmode-results.xml) |
| 状态与窗口截图 | 1/1，三语言 48 张；其中三张六词条截图由下项更新 | [采集 XML](./visual-results.xml) |
| 本地化六词条压力布局 | 1/1，三语言各一张 | [采集 XML](./dense-results.xml) |
| 资源自动审计 | 0 错误；1 项合同允许的全出血边缘提示 | [审计](./panel-audit.json)、[manifest](./panel-manifest.json) |

截图使用隔离 Session / 存储。状态采集固定资源与技能读取器并停止玩法时间，以比较 0%、25%、50%、100% 和剩余冷却；这类截图验证表现，真实恢复、暂停和施法事务由长期行为测试覆盖。六词条使用既有压力快照、正式本地化名称与修改器格式，不写入正式物品配置。伪本地化扩展可见语言文本；真实键位文本不经过伪翻译，仍检查其可见边界。

## 运行表现

| 场景 | 中文 | 英文 | 伪本地化 |
| --- | --- | --- | --- |
| Gameplay | [图](./gameplay-zh-Hans.png) | [图](./gameplay-en.png) | [图](./gameplay-qps-ploc.png) |
| 背包 | [图](./inventory-zh-Hans.png) | [图](./inventory-en.png) | [图](./inventory-qps-ploc.png) |
| 背包 + 商店 + 属性 | [图](./shop-attributes-zh-Hans.png) | [图](./shop-attributes-en.png) | [图](./shop-attributes-qps-ploc.png) |
| 背包 + 打造 + 属性 | [图](./crafting-attributes-zh-Hans.png) | [图](./crafting-attributes-en.png) | [图](./crafting-attributes-qps-ploc.png) |
| 暂停 | [图](./pause-zh-Hans.png) | [图](./pause-en.png) | [图](./pause-qps-ploc.png) |
| 六词条压力预览 | [图](./six-affixes-zh-Hans.png) | [图](./six-affixes-en.png) | [图](./six-affixes-qps-ploc.png) |
| 25% 低生命 | [图](./state-low-zh-Hans.png) | [图](./state-low-en.png) | [图](./state-low-qps-ploc.png) |
| 50% 与半冷却 | [图](./state-half-cooldown-zh-Hans.png) | [图](./state-half-cooldown-en.png) | [图](./state-half-cooldown-qps-ploc.png) |
| 零资源 / 死亡 | [图](./state-empty-dead-zh-Hans.png) | [图](./state-empty-dead-en.png) | [图](./state-empty-dead-qps-ploc.png) |
| 降低动态 | [图](./state-reduced-motion-zh-Hans.png) | [图](./state-reduced-motion-en.png) | [图](./state-reduced-motion-qps-ploc.png) |
| 无有效来源 | [图](./state-no-source-zh-Hans.png) | [图](./state-no-source-en.png) | [图](./state-no-source-qps-ploc.png) |
| 法力不足 | [图](./state-no-mana-zh-Hans.png) | [图](./state-no-mana-en.png) | [图](./state-no-mana-qps-ploc.png) |
| 恢复后就绪 | [图](./state-recovered-zh-Hans.png) | [图](./state-recovered-en.png) | [图](./state-recovered-qps-ploc.png) |
| 360 px 仪表扩展 | [图](./state-wide-frame-zh-Hans.png) | [图](./state-wide-frame-en.png) | [图](./state-wide-frame-qps-ploc.png) |

画布为 1920×1080；HUD 位于 y=932–1068，物品窗口与操作条在其上方。资源框 300×136，中区 840×136，间隔 12。主数字和填充即时更新，冷却条不盖入口；窗口打开时入口退出点击与导航。正式底板原生规格、角部保护和重新生成原因见[合同](./asset-contract.md)与[生成记录](./generation-record.md)。

## 修复与测试维护

- 真实鼠标回归发现 Gameplay 未启用 UI 指针，导致可见 HUD 按钮无法点击；现仅额外启用 Point / Click，Navigate / Submit 保持关闭，挂起仍禁用全部动作。
- 手柄返回后恢复 HUD，设备变化刷新 Start Glyph；低生命轮廓在降低动态、暂停与解绑时停止，恢复 Gameplay 后按当前状态重新表现。
- 自动设备识别忽略 Context 切换时未移动的指针位置回放，防止手柄返回后提示误切键盘；真实鼠标移动仍切换为键鼠提示。
- 法力不足提示从当前状态计算，资源恢复后无需等待下一次成功攻击；暂停 / 死亡时法力说明显示真实状态。
- 沿用现有 Query、输入、资源和 UX 套件，净新增一个长期 PlayMode 行为测试。UI 底板在原家族合同中登记，未新建版本测试；六词条夹具补入本地化服务，避免只测回退键名。
- 临时截图脚本与 `.meta` 已删除。带版本目录仅保存证据，不保留反复核验这些历史结果的测试。

部分 MCP job 在 Domain Reload 后丢失结束回调；以 Unity 生成且包含结束时间与逐项结果的 NUnit XML 为准。孤立 job 仅在 Editor 已停止测试时清理，不将工具请求成功或空测试集当作测试通过。

最终版本 `0.3.2-alpha`，内容 / 存档 Schema 不变。编译成功，HUD 底板和共享 Glyph 样式在 GameRoot 依赖链中，Sprite 加载有效。Editor 停在 Bootstrap、非 Play；EnterPlayModeOptions 恢复关闭 / None。动态字体恢复到执行前备份，保留用户原有字体修改；未处理用户的设计文档改动。未执行发布构建，阶段规划仍在 0.3.4 做最终 Player 验收。
