# 0.7.1-alpha.3 中心充能、属性与点亮验收

2026-10-15：**阶段三通过并归档**。当前版本 0.7.1-alpha.3，Unity 6000.6.4f1，core v12、Save / Settings Schema 11 / 1、地图 V4。源码为 main / 1d7fd34 加本轮未提交改动，见[源码与配置身份](source-identity.json)。[实施计划](../../plans/phase-3-execution.md)归档于同一版本系列；[活动总计划](../../../../alpha-0.7.1-skill-board-plan.md)继续推进阶段四、五。

## 交付结果

- 中心短暂选择背包 / 专栏瓶，原容器保留所有权；预览 / 取消不消费，确认一次完成整瓶移除、能量池与两类点数。未满瓶可用，空瓶拒绝并保留，消费专栏瓶后不自动换瓶。
- 第 n 点消耗 100＋25×(n−1)，每点同时获得 1 天赋点和 1 属性点，余量跨瓶累计。累计成本使用受检整数和有界搜索；花点和退款不降低累计兑换序号。
- 属性点实际分配到力量 / 敏捷 / 智力，通过独立 player_growth 来源重建；派生与装备 / 状态共用既有管线，保持生命 / 法力比例，恢复与复活不重复加值。
- 柴薪和普通天赋分别点亮、各花 1 点；大根起步，中小须经已点亮柴薪连根，普通天赋沿实际显式边逐点点亮。裂缝免费随柴薪开放，中心火焰资格随大柴薪开放。
- 逐件取回清除当前件激活，返还该件与普通天赋的实花天赋点；保留属性分配和累计兑换数。背包满、失去几何 / 激活路径、旧预览均整次失败，不自动连带退款。
- Profile.Growth 参与统一快照 / Detached / Restore，Schema 10→11 添加空成长；旧棋盘、瓶内能量、技能 / 冷却、资源和 RNG 保留。core 11→12 引用成长配置，该配置沿已有非独立配置发现规则接入配置中心与字段文档。

当前模块：[成长](../../../../../skill-board/growth.md)、[安装](../../../../../skill-board/items-installation.md)、[能量瓶](../../../../../skill-board/energy-bottles.md)、[配置](../../../../../config-reference/skill-board.md#playergrowthconfiguration)。

## 当次验证

| 范围 | 实际结果 | 原始记录 |
| --- | --- | --- |
| 项目完整 EditMode | **949/949**，0 失败 / 跳过，195.92 秒 | [结果](editmode-final.json) |
| 项目完整 PlayMode | **102/102**，0 失败 / 跳过，289.76 秒 | [结果](playmode-final.json) |
| 成长模块定向 | 17/17 | [结果](editmode-growth.json) |
| 棋盘事务定向 | 14/14 | [结果](editmode-transactions.json) |
| Restore 预检定向 | 11/11 | [结果](editmode-save-preparer.json) |
| 序列化 / 历史迁移定向 | 14/14 | [结果](editmode-serializer.json) |
| 配置文档 / 内容身份 | 3/3、16/16 | [字段覆盖](editmode-configuration-docs.json)、[身份](editmode-content-identity.json) |
| 真实死亡、成长、保存 / Continue、城镇 / 野外切图 | 1/1 | [结果](playmode-save-roundtrip.json) |
| 正式配置扫描 | 无问题 | [结果](content-validation.json) |

完整回归均以 assembly 过滤 DarkFlare.Tests.EditMode / DarkFlare.Tests.PlayMode。阶段二历史 EditMode 931 项还包含一项 Addressables.DocExampleCode.TestStub.RequiredTest；本次项目基线对应 930 项，增加 17 项成长与两项既有保存 / 迁移套件行为覆盖至 949，没有删除项目测试来降低数量。

长期测试保护递增阈值、分瓶等价、余额 / 点数守恒、空瓶、极大整数边界、唯一所有权、资源比例、重复重建、真实死亡积蓄、路径与跨根 / 环、退款、背包满、过期预览、通知抛错后的已提交状态及旧 Session 隔离。背包 Revision 特别覆盖“移走再放回原位”仍不可复用旧选择。

## 修正与历史输入

首轮 [EditMode 948 通过 / 1 失败](editmode-first-full.json)发现成长配置创建菜单错归入要求独立 ContentId 的 Data 分类；移入已有非独立配置分类后，[第二轮 947 通过 / 2 失败](editmode-second-full.json)指出配置发现入口尚未补齐。最终按 FirewoodGenerationDefinition 等既有模式接入 ConfigurationTypeDiscovery，保留内容身份和字段覆盖检查，未添加 Ignore 或放宽断言。

首轮 [PlayMode 保存定向失败](playmode-save-roundtrip-first.json)来自既有测试仍使用加点前的生命预期；新增两点力量提高了生命上限。已在加点处加入明确的资源比例预期和断言，继续保留“恢复不重复治疗 / 回蓝”等全部后续检查，定向与最终项目全量均通过。

历史输入继续使用真实发布 Schema 5 玩家档经未修改的 5→9 链导出的 save-v9-migrated-player.json；新增测试在内存中再经原 9→10 构造源合同，然后验证 10→11 只增加空成长。该输入的来源和迁移派生性质明确保留，未称作原生 Schema 10 玩家采集。另有当前真实 Session 安装 / 积蓄 / 充能 / 分配 / 点亮后保存与跨图恢复，验证实际变体、所有权、点数和激活无重放。

## 开发入口证据与清理

[中心预览截图](center-preview.png)展示输入 1000、预期同时获得 6 点、剩余 25、下一成本 250，同时瓶仍在背包。实际按钮执行取消后瓶仍保留，再确认整瓶消费；[成长结果截图](growth-result.png)显示力量 2、剩余属性点 4 / 天赋点 4、柴薪及一个普通天赋已点亮，实际最大生命 104。两张均为实际 1280×920 Odin / UI Toolkit 窗口，已查看确认文字与状态。

[操作记录](debug-window.json)由隔离 Editor Preview Scene / Session 的真实按钮、Query、Command 生成。1000 能量为截图夹具注入；真实击杀收集由 PlayMode 另行验证。该过程不访问正式玩家存档，关闭窗口后清除选择与预览，销毁临时对象并停止 Session / Input。[最终 Editor 状态](editor-final-state.json)确认 Bootstrap 场景未脏、无当前架构、导航内存为 0。

一次性内容设置和截图辅助从 Temp 清理，必要的复现文本归档；动态字体运行缓存和输入生成器注释恢复至本轮开始的跟踪状态。保存测试和新增成长套件均保留独有行为覆盖；未保留读取历史成功记录或采集截图的一次性运行测试。

本轮未构建 Player，未执行真实手柄、跨进程、完整中英 / Pseudo Locale 视觉或性能矩阵。正式玩家棋盘、天赋 / 余烬战斗效果与六键手动技能仍属阶段四、五；当前单技能自动循环保持原行为。

最终[文档与源码检查](document-and-source-check.json)检查 20 份新增 / 受影响 Markdown 的本地链接、受影响 C# 的 UTF-8 / LF / 末尾换行及 82 个源码 / 资产哈希，均无问题。新 Unity YAML 行尾空格已清理，git diff --check 通过；格式清理不改变已验收的字段值或代码行为。

[返回版本归档](../../README.md)
