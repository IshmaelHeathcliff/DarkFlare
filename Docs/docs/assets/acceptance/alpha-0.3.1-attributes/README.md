# alpha 0.3.1 属性详情验收

- 日期：2026-09-08；实现基线 `fa4c85e`，完成版本 `0.3.1-alpha`。
- Unity `6000.4.3f1`；项目自有程序集，测试使用既有隔离存储与测试角色。
- 范围：独立属性窗口、来源解释、动态状态、双输入与三语言布局；完整 HUD / 皮肤 / 十槽装备属于后续阶段。
- 模块说明：[独立属性详情](../../../attribute-details.md)；[归档计划](../../../plan/archive/alpha-0.3.1-realtime-attributes-plan.md)。

## 最终回归

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| EditMode 项目全量 | 432 / 432；0 失败、0 跳过 | [NUnit XML](./editmode-results.xml)，11:51:52–11:52:05 UTC |
| PlayMode 项目全量 | 62 / 62；0 失败、0 跳过 | [NUnit XML](./playmode-results.xml)，11:53:21–11:54:34 UTC |
| 当次三语言视觉采集 | 1 / 1；33 张 1920×1080 截图 | [采集结果](./visual-capture-results.xml)，11:47:11–11:47:17 UTC；临时测试已删除 |

最终编译成功；Editor 非 Play / 非编译，活动场景 `Assets/Scenes/Bootstrap.unity`。`EnterPlayModeOptions=0`、选项关闭；测试造成的动态字体缓存与 EditorSettings 改动已还原。版本只更新 bundleVersion，内容版本与存档 Schema 未改。

MCP 在部分测试的 Domain Reload 后丢失 job 回调，最终 EditMode 请求也遇到连接中断，但 Unity 实际完成运行；以上以带完成时间、逐项结果的 NUnit XML 为准，未将请求失败解释成测试通过。Console MCP 对普通结构化 Info 日志存在类型误分类，不能凭其 `Exception` 标签判断失败；最终无编译错误，项目测试均通过。

## 行为与数值

- 正式聚合 trace 覆盖 Flat / Override 次序、Increase 合计、逐项 More 和主属性派生。解释结果与正式结果一致，重复重建不累积。
- 属性 Query 连续执行不改变生命 / 法力、装备或任何具名随机通道的状态。基础属性返回副本，来源保存槽位、物品 ID 和词条身份。
- 暴击原值 157.5% 显示有效 100%；抗性原值 90% / -120% 分别有效 75% / -100%。恢复能力在暂停时保留，额外暴伤与最终倍率分开。
- 已覆盖无来源、法力不足、冷却、死亡 / 复活、满资源、禁用、注销和空角色状态。当前技能适配器只读现有冷却，不另建推进时钟。
- 独立打开 / 关闭属性、关闭背包保留属性、设置取消返回、换装来源刷新、折叠与滚动、手柄左右跨窗 / 上下导航、返回关闭最后窗口通过。既有完整物品操作与 Session / Shell 回归继续通过。
- 属性不注册物品来源和放置目标；三窗物品预览保持唯一高亮，标题 / 关闭可达，切回属性恢复滚动内容。

## 三语言窗口组合

| 组合 | 中文 | 英文 | 伪本地化 |
| --- | --- | --- | --- |
| 属性单窗 | [图](./attributes-zh-Hans.png) | [图](./attributes-en.png) | [图](./attributes-qps-ploc.png) |
| 背包 + 属性 | [图](./inventory-attributes-zh-Hans.png) | [图](./inventory-attributes-en.png) | [图](./inventory-attributes-qps-ploc.png) |
| 商店 + 属性 | [图](./shop-attributes-zh-Hans.png) | [图](./shop-attributes-en.png) | [图](./shop-attributes-qps-ploc.png) |
| 打造 + 属性 | [图](./crafting-attributes-zh-Hans.png) | [图](./crafting-attributes-en.png) | [图](./crafting-attributes-qps-ploc.png) |
| 背包 + 商店 + 属性 | [图](./inventory-shop-attributes-zh-Hans.png) | [图](./inventory-shop-attributes-en.png) | [图](./inventory-shop-attributes-qps-ploc.png) |
| 背包 + 打造 + 属性 | [图](./inventory-crafting-attributes-zh-Hans.png) | [图](./inventory-crafting-attributes-en.png) | [图](./inventory-crafting-attributes-qps-ploc.png) |

属性宽 440 px，背包 560 px，商店 / 打造 760 px，间隔 16 px；窗口对齐，背包网格维持原尺寸。单窗居中，背包标题栏图标代替占用额外内容行的按钮，三主属性摘要与穿脱行完整可见。几何采集见[布局观测](./layout-observations.txt)；通过行为与文字测量断言验证，观测文件本身不是运行测试。

## 六词条与展开阅读

使用正式大剑与词条池，固定生成种子 `424242`、等级 90、稀有 3 前缀 / 3 后缀并实际装备。词条包括力量、敏捷、暴击率、暴击伤害、命中和混沌伤害 / 额外伤害，用于核验直接聚合与攻击上下文的区别。伪本地化在英文基础上扩展字符与长度；不修改正式本地化配置或玩家物品。

| 展开区域 | 中文 | 英文 | 伪本地化 |
| --- | --- | --- | --- |
| 资源 | [图](./details-resources-zh-Hans.png) | [图](./details-resources-en.png) | [图](./details-resources-qps-ploc.png) |
| 攻击 | [图](./details-attack-zh-Hans.png) | [图](./details-attack-en.png) | [图](./details-attack-qps-ploc.png) |
| 防御 | [图](./details-defense-zh-Hans.png) | [图](./details-defense-en.png) | [图](./details-defense-qps-ploc.png) |
| 修改器来源 | [图](./details-modifiers-zh-Hans.png) | [图](./details-modifiers-en.png) | [图](./details-modifiers-qps-ploc.png) |
| 三窗六词条浮窗 | [图](./six-affix-preview-zh-Hans.png) | [图](./six-affix-preview-en.png) | [图](./six-affix-preview-qps-ploc.png) |

全部分组可展开、底部可滚动，当前视口之外的行由 ScrollView 正常裁切；长文本在行内换行。三窗物品浮窗临时替换属性阅读区，包含六词条的伪本地化正文仍在屏幕安全区，属性标题和关闭按钮没有被覆盖。

## 测试维护与收尾

长期新增 2 项 EditMode 与 1 项 PlayMode，均扩展已有套件；原背包完整属性列表断言改为摘要 / 独立详情行为，移除旧列表样式。没有版本专用测试套件，没有读取本目录历史成功记录的运行测试。

实施中修复了未纳入生命周期作用域的刷新任务、背包摘要在伪本地化下挤压，以及菜单打开时 HUD 入口覆盖标题的问题。临时截图脚本还曾在帧末测量 UI 引发重入，改为常规帧测量后重新采集通过；失败截图已由最终结果覆盖。临时脚本、`.meta` 和诊断工具已删除。

完整 Addressables / Windows Player 发布构建按总计划在 0.3.4 综合封板，本阶段完成 Editor 内功能与全量项目回归。
