# 统一物品操作与事务验收（2026-09-08）

本切片属于 alpha 0.3.0，接通统一拖放、右键和手柄动作菜单；不包含代表视觉组件、属性解释、最终 HUD 或十槽装备。模块现状见[物品 UI 工作台](../../../item-ui-workbench.md)。

## 自动化验证

- 项目 EditMode：430/430 通过，无失败或跳过，2026-09-08 07:16:52Z 完成。
- 项目 PlayMode：61/61 通过，无失败或跳过，2026-09-08 07:15:31Z 完成。
- 保持正常 Domain / Scene Reload，`EnterPlayModeOptions = 0`；输入与场景测试使用隔离数据根。最终隔离预览及退出的 Editor 日志未新增异常、脚本编译错误或丢失脚本记录。

| 长期回归 | 本次保护的行为 |
| --- | --- |
| `GameplayUiFoundationTests` | 指定格购买受阻时不换格、不扣费；成交事件中库存、背包和金币已全部提交；回调重入不能重复购买 |
| `Phase1UxPlayModeTests` | 鼠标跨来源买卖、打造放入 / 指定格取回、装备与丢弃；右键上下文动作；手柄戒指选槽、动作菜单、拿起与跨窗放置；设备断开取消；菜单外右键不穿透 |
| `EquipmentPhase2PlayModeTests` | 键盘 / 手柄动作菜单换装、明确戒指槽、卸下；来源格回填受阻时保留物品；属性、HUD 与已发出投射物快照继续正确 |
| `SaveCaptureRestorePlayModeTests` | 丢弃资源不可用时保留原格；世界实例、种子、真实词缀及掷值保存恢复；不原地自动拾回；拾取事件发布前清除世界归属，重入不能重复拾取 |
| 原有输入、Shell、生命周期及空间导航回归 | 设置 / 返回确认交接、暂停与恢复、唯一高亮、Session 清理、四向邻居和按格放置 |

新增 1 项 EditMode、2 项 PlayMode，其他覆盖合并到现有模块用例。删除被共享会话替代的背包拖放实现及其无用字段，不增加按版本重复建套件的测试。临时测试运行器、截图入口和迁移脚本在验证后清理，正式资产不包含预览物品。

## 视觉与布局证据

实际 Game View 为 1920×1080。临时预览使用正式基础装备与词缀池生成 90 级、3 前缀 + 3 后缀物品；未修改正式装备配置。中文、英文和 Pseudo Locale 分别检查完整详情、动作菜单、有效 / 无效购买落点，以及单窗和双窗操作栏。

检查包含可见文本测量、操作控件与容器边界、买卖按钮与拖放区不重叠、打造范围与说明不重叠，以及操作栏与可见窗口宽度同步。长文本让按钮自然换行，来源列表保持可滚动；不缩小物品真实占格。[测量记录](./layout-check.txt)。

| 状态 | 中文 | 英文 | Pseudo Locale |
| --- | --- | --- | --- |
| 背包六词缀详情 | [截图](./inventory-details-zh-Hans.png) | [截图](./inventory-details-en.png) | [截图](./inventory-details-qps-ploc.png) |
| 戒指动作菜单 | [截图](./ring-actions-zh-Hans.png) | [截图](./ring-actions-en.png) | [截图](./ring-actions-qps-ploc.png) |
| 指定格购买：有效 | [截图](./purchase-valid-zh-Hans.png) | [截图](./purchase-valid-en.png) | [截图](./purchase-valid-qps-ploc.png) |
| 指定格购买：无效 | [截图](./purchase-invalid-zh-Hans.png) | [截图](./purchase-invalid-en.png) | [截图](./purchase-invalid-qps-ploc.png) |
| 单窗商店动作菜单 | [截图](./shop-actions-zh-Hans.png) | [截图](./shop-actions-en.png) | [截图](./shop-actions-qps-ploc.png) |
| 背包与打造六词缀详情 | [截图](./crafting-details-zh-Hans.png) | [截图](./crafting-details-en.png) | [截图](./crafting-details-qps-ploc.png) |

截图中的拖放预览由临时工具选取真实来源与目标，不提交交易；真实提交由上面的 Pointer / Input System 回归验证。前一切片的[独立窗口组合与十槽空间线框](../alpha-0.3.0-windows/README.md)仍是独立历史证据，十槽正式实现尚未完成。
