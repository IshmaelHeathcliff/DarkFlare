# Alpha 0.3.0 收尾验收

2026-09-08，运行版本 `0.3.0-alpha`。本阶段完成独立窗口、统一物品交互、代表视觉组件与整体空间线框；实时属性解释、正式 HUD、全套皮肤和十槽装备继续在 0.3.1–0.3.4 实施。

## 当前结果

- 完整 EditMode **430/430**，PlayMode **61/61**，无失败或跳过。[本次测试结果](./test-results.json)。PlayMode 保留正常 Domain / Scene Reload，数据路径隔离。
- 一个正式窗口底板接入三种窗口；[单图审计](./panel-audit.json)零错误 / 警告，画布 1254×1254、覆盖率 1、中心 (627,627)。[生产合同](./asset-contract.md)、[manifest](./panel-manifest.json)、[生成记录](./generation-prompt.md)。
- [最终 Editor 核对](./editor-validation.json)：Bootstrap、非 Play、编译成功，输入预览选项已恢复；GameRoot 正确依赖新单图底板，缺失依赖为零。
- 1920×1080 × 中文 / 英文 / Pseudo Locale，六种窗口组合共 18 项[布局检查](./layout-check.txt)通过；使用正式词缀池生成的 90 级、3 前缀 + 3 后缀临时物品，不写入正式内容或玩家存档。
- 修复展开属性时穿脱行被压缩、长属性名称排版和内容区被压为空的问题；当前摘要改为整行名称和右侧数值。关闭按钮复用现有图标与本地化 tooltip，保持键鼠 / 手柄的点击和焦点路径。
- 实测窗口宽 560 / 760，高 788–798；装备区仅收紧留白，152 px 大槽及 48 px 背包格不缩放。背景九宫格、关闭按钮和展开属性可共存，无额外命中节点。

## 运行截图

| 组合 | 中文 | 英文 | Pseudo Locale |
| --- | --- | --- | --- |
| 背包 | [截图](./inventory-zh-Hans.png) | [截图](./inventory-en.png) | [截图](./inventory-qps-ploc.png) |
| 商店 | [截图](./shop-zh-Hans.png) | [截图](./shop-en.png) | [截图](./shop-qps-ploc.png) |
| 打造 | [截图](./crafting-zh-Hans.png) | [截图](./crafting-en.png) | [截图](./crafting-qps-ploc.png) |
| 背包 + 商店 | [截图](./inventory-shop-zh-Hans.png) | [截图](./inventory-shop-en.png) | [截图](./inventory-shop-qps-ploc.png) |
| 背包 + 打造 | [截图](./inventory-crafting-zh-Hans.png) | [截图](./inventory-crafting-en.png) | [截图](./inventory-crafting-qps-ploc.png) |
| 背包 + 展开属性 | [截图](./attributes-zh-Hans.png) | [截图](./attributes-en.png) | [截图](./attributes-qps-ploc.png) |

## 阶段合同核对

| 合同 | 证据 |
| --- | --- |
| 单窗可用、双窗独立开关、不可用页签隐藏 | 既有窗口与本次完整 UI PlayMode 回归；[独立窗口验收](../alpha-0.3.0-windows/README.md) |
| 设置 / 返回确认交接和输入所有权 | `Alpha025InputOwnershipPlayModeTests`、Shell 矩阵与生命周期回归 |
| 拖动、右键、手柄穿脱 / 买卖 / 打造 / 丢弃 | `Phase1UxPlayModeTests`、`EquipmentPhase2PlayModeTests`；[统一操作证据](../alpha-0.3.0-actions/README.md) |
| 事务失败回退、指定格、唯一归属、保存继续 | 完整领域、物品操作和 `SaveCaptureRestorePlayModeTests` 回归 |
| 四向跨窗口导航、唯一高亮、取消与设备丢失 | `SpatialNavigationTests`、输入所有权及真实输入 PlayMode；十槽复验保留给 0.3.4 |
| 本地化排版与属性可见性 | 本次 18 张截图；既有 UI 回归补充展开属性、至少一整行可见、滚动首尾和按钮不重叠 |
| 资源与三轮 Session | 完整资源测试和 `ThreeSessionRestarts_DoNotDuplicateHostPlayerOrArchitecture` 等生命周期用例 |

## 后续线框与边界

[横向资源容器](./wireframe-rectangles.png)、[圆形资源容器](./wireframe-orbs.png)均通过临时运行时 UI 验证背包 + 独立属性窗口与底部空间。横向方案优先用于后续切角容器，文本余量更宽；圆形方案仅作比较。图片中的属性分组、资源数字与技能文字是明确标注的空间示例，没有实现新公式、冷却查询或 HUD 状态消费。

[十槽空间参考](../alpha-0.3.0-windows/ten-slot-space-reference.png)仍是线框。当前 162 px 装备画布保留 152 px 高的大槽和两排 60 px 小槽所需高度；真实十槽排布、交互与内容由 0.3.4 验证，不能以线框代替。

## 测试维护与清理

没有新增测试套件或增加测试数量。`Phase5VisualIntegrationTests` 扩展正式 Sprite 家族合同；`Phase1UxPlayModeTests` 扩展持续有价值的展开 / 滚动 / 本地化布局回归，并明确只检查可见层级与滚动可视区。没有删除失败用例或放宽文字适配断言。

首次失败记录：穿戴按钮底边 775，而属性区起点上限 760，确认真实遮挡；修复伸缩、标题占宽和属性阅读宽度后回归通过。临时预览程序集、隔离预览数据、截图代码和虚拟环境在验证后清理；动态字体缓存与测试改变的 EditorSettings 不作为功能改动。正式场景没有新增线框对象。

本次不执行最终 Addressables / Windows Player 发布构建；它们按总计划在 0.3.4 综合封板时执行。本记录不声明整个 alpha 0.3 已完成。
