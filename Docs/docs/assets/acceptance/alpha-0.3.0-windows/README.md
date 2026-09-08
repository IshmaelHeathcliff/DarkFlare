# 独立窗口切片验收（2026-09-08）

EditMode **429/429**（04:01:00 UTC 完成），PlayMode **59/59**（2026-09-08 04:14:08Z 完成），无失败或跳过。最后一轮 PlayMode 包含长标题与关闭按钮边界回归。使用隔离数据，保留正常 Domain / Scene Reload；不写入玩家正式存储。

实际 Game View 1920×1080，六种组合 × 中文 / 英文 / Pseudo Locale。临时预览生成一件 90 级六显式词缀物品（正式词缀池，3 前缀 + 3 后缀），核对可见文本、完整词缀详情、安全区与窗口排列；临时物品不进入正式资产或存档。[布局检查结果](./layout-check.txt)。

| 组合 | 中文 | 英文 | Pseudo Locale |
| --- | --- | --- | --- |
| 背包 | [截图](./inventory-zh-Hans.png) | [截图](./inventory-en.png) | [截图](./inventory-qps-ploc.png) |
| 商店 | [截图](./shop-zh-Hans.png) | [截图](./shop-en.png) | [截图](./shop-qps-ploc.png) |
| 打造 | [截图](./crafting-zh-Hans.png) | [截图](./crafting-en.png) | [截图](./crafting-qps-ploc.png) |
| 背包 + 商店 | [截图](./inventory-shop-zh-Hans.png) | [截图](./inventory-shop-en.png) | [截图](./inventory-shop-qps-ploc.png) |
| 背包 + 打造 | [截图](./inventory-crafting-zh-Hans.png) | [截图](./inventory-crafting-en.png) | [截图](./inventory-crafting-qps-ploc.png) |
| 背包 + 展开属性 | [截图](./attributes-zh-Hans.png) | [截图](./attributes-en.png) | [截图](./attributes-qps-ploc.png) |

[十槽空间线框](./ten-slot-space-reference.png)仅验证现有装备区域可容纳两块大槽和八块小槽，属于运行时临时线框，**不是实际可装备控件或最终视觉设计**。真实十槽、每槽两件配置及旧档兼容仍在 0.3.4；本次没有变更装备枚举和存档格式。

长期测试复用现有窗口、Pointer、Shell、资源生命周期及数据隔离用例，新增空间邻居与输入所有权行为覆盖。删除被替代的互斥页签 / 固定布局断言；截图采集、临时 Runner 与迁移脚本已清理，不为历史通过记录新增运行测试。

本记录完成独立窗口切片。指定格购买、拖动出售 / 世界丢弃、打造拖回新格、完整快捷动作矩阵及代表视觉组件继续在 0.3.0 实施。当前验收范围为 1920×1080，尚未承诺其他画布下的新窗口适配；正式 HUD 与完整皮肤另行实施。
