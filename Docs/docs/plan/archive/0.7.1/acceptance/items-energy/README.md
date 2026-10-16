# 0.7.1-alpha.2 道具、安装事务与能量瓶验收

2026-10-07：**阶段二通过并归档**。当前运行版本 `0.7.1-alpha.2`，Unity `6000.6.4f1`，core v11，Save / Settings Schema 10 / 1，地图 V4。源码身份为 `main` / `9b82259` 加本轮未提交改动，详见[源码与配置 SHA-256](source-identity.json)。[实施计划](../../plans/phase-2-execution.md)已归档，中心充能、点亮、六键手动技能和正式玩家棋盘由[活动总计划](../../plans/alpha-0.7.1-skill-board-plan.md)继续推进。

## 实际交付

- 八项正式道具：大 / 中 / 小柴薪、投射物火焰、增伤余烬和容量 1000 / 2000 / 4000 的三档能量瓶。前三类占背包 1×1，瓶占 2×1，均为独立不可堆叠实例；实际布局、词条结果、技能引用、范围及瓶内能量只初始化一次。
- 不可变棋盘与预检 / 提交：根安装、旋转、裂缝连接、火焰 / 余烬嵌入、逐件取回与专栏换瓶共用权威事务。预检完整最终背包、占位、拓扑和身份；过期预览、空间不足、断根或非法插槽失败不部分修改。裂缝连接与余烬互斥，中柴薪中心留白，拆卸前先取出嵌入物。
- 仅装备瓶承接当前玩家的有效怪物死亡收益；四个收益因素分别计算，超量舍弃、满瓶手动更换，重复死亡与旧 Session 不补领。玩家 Profile 身份与战斗来源身份分别校验。
- 统一物品表只保存每件实例一次，棋盘 / 专栏保存所有权引用；Restore 验证布局、坐标、角色、连接、连根、能量与重复所有者。Schema 9→10 初始化空棋盘并保留原技能 / 冷却；切图、交易和恢复不重生成实例。
- 掉落 / 商店、Localization、Addressables 与八张独立图标已接入。Odin / UI Toolkit 开发窗口复用正式 Query / Command，展示坐标、身份、配对、Revision、能量和失败原因。

当前模块：[道具与安装](../../../../../skill-board/items-installation.md)、[能量瓶](../../../../../skill-board/energy-bottles.md)、[图标合同](../../../../../skill-board/item-icons.md)、[配置参考](../../../../../config-reference/skill-board.md)。普通怪物基础能量 10、两项加成各 5、守卫基础 100 / Unique，以及节点效果、掉落 / 货池权重均为首轮可调整值；瓶子基价为 100 / 200 / 400，交易仍使用既有倍率。

## 自动验证

| 范围 | 当次结果 | 原始记录 |
| --- | --- | --- |
| 项目完整 EditMode | **931/931**，0 失败 / 跳过，184.52 秒 | [最终结果](editmode-final.json) |
| 项目完整 PlayMode | **102/102**，0 失败 / 跳过，278.54 秒 | [最终结果](playmode-final.json) |
| 安装事务模块定向 | 14/14 | [结果](editmode-skillboard.json) |
| 能量规则模块定向 | 7/7 | [结果](editmode-energy.json) |
| 物品实例 / DTO 定向 | 8/8 | [结果](editmode-items.json) |
| 真实击杀、保存 / Restore 与城镇 / 野外切图 | 1/1 | [结果](playmode-save-roundtrip.json) |
| 正式内容配置扫描 | 无问题 | [结果](content-validation.json) |

PlayMode 最终使用 `filter_type=assembly`、`filter=DarkFlare.Tests.PlayMode`，限定项目自有程序集。首轮宽范围 106 项含 4 项 Input System 包测试，其中 2 项因包内既有 Issue 1252825 跳过；这些包项目不计入最终项目 102 项，也未记作项目通过。

长期保护覆盖：固定实例生成与交易、背包 / 棋盘失败回退、过期预览和重复提交、嵌入互斥、末端取回、成环 / 跨根仍连根、完整保存所有权与损坏数据拒绝、四因素收益、归属 / 去重 / 容量截断 / 手动换瓶和 Session 隔离。既有 PlayMode 保存测试新增真实 CombatSystem 持续伤害死亡收能及切图 / Restore。没有增加按版本命名的永久套件。

## 历史存档与首轮修正

`save-v9-migrated-player.json` 来自项目长期保存的真实发布 Schema 5 夹具 `save-v5-navigation.json`，经未修改的 5→6→7→8→9 链导出，并按既有 canonical JSON / SHA-256 算法生成校验和。**它是历史玩家数据迁移后的 Schema 9 夹具，不是原生 Schema 9 玩家采集**。现有测试保留历史字段、技能 / 冷却、资源、随机流与完整数据比较；Schema 9→10 只追加空棋盘 / 新道具扩展默认值，再恢复和重新保存验证。来源见测试夹具 manifest；[导出辅助文本](ExportSkillBoardMigrationFixture.cs.txt)作为证据保留，临时执行脚本已清理。外部真实玩家存档没有写入或改动。

首轮记录保留：[EditMode 927 通过 / 4 失败](editmode-first-full.json)、[PlayMode 100 通过 / 4 失败 / 2 包内跳过](playmode-first-full.json)、[能量测试夹具定位](energy-fixture-failure.json)。已修正新道具池筛选、历史比较中新增默认字段的验证、Editor 怪物夹具登记和测试库存预算；随机获得测试改用正式掉落配置的逐项候选工厂，避免概率性遗漏。

PlayMode 首轮导航检查遇到非零 NativeBytes，推断与此前手动运行 Session 的资源清理时序有关；停止后归零，干净状态定向 2/2 和最终全量均通过，未修改导航运行代码或手动清零计数。Boss 检查隔离普通怪物的攻击干扰；地图 20 次重置改为 30 秒实时时限等待实际保存完成，保留原资源 / 所有权断言。[三项定向复测](playmode-regression-retests.json)与最终全量共同记录这些修正；最终结束时导航内存为 0、Bootstrap 场景未脏，见[Editor 状态](editor-final-state.json)。

## 图标和开发入口证据

八张独立 128×128 RGBA Sprite 由 Unity MCP Editor 辅助逐个生成，先检查样图再展开；按 Single / FullRect、居中枢轴、Point、无压缩 / Mipmap 和既有物品 PPU / Addressables 合同导入。每张已原生像素检查，自动审计 **8 通过、0 警告 / 错误**，见[资产清单](icon-manifest.json)、[审计](icon-audit.json)和[辅助文本](SkillBoardContentSetup.cs.txt)。这组用于阶段二识别，不作为最终精细美术交付。

- [开发窗口截图](debug-window.png)与[操作记录](debug-window.json)：通过正式事务安装柴薪、嵌入火焰、装备瓶；显示身份 / 能量，验证未先取出火焰时拆卸被拒绝。
- [实际 1920×1080 背包截图](inventory-icons.png)与[运行记录](runtime-icons.json)：八张图标经 Addressables 加载，实际 UITK 背包展示和瓶详情 `333 / 1000`。能量 333 仅为截图夹具注入；真实击杀收益由前述 PlayMode 测试另行证明。截图在隔离的 PlayModeTests 数据根完成，未触及玩家正式数据。

## 收尾与未执行项

新测试按安装 / 能量模块保留，复用并扩展现有物品、迁移、保存和地图套件，审查后保留独有行为覆盖。一次性资源生成、迁移导出、截图和测试取消辅助均从 Temp 清理，只保留必要文本与结果证据；没有将历史成功记录变成反复运行的测试。动态字体运行缓存与输入包装类生成器注释已恢复本轮开始时的跟踪状态。

中心只确定短暂选择引用合同，实际中心选择、整瓶消费、跨瓶余额、递增兑换和点亮在阶段三；六槽手动施法及效果在阶段四。当前单技能自动循环仍保持原行为。开发窗口不能替代正式玩家 UI 和手柄可达性验收。

本轮未构建 Player，未执行真实手柄 / 设备、跨进程恢复、Pseudo Locale 完整视觉矩阵或性能矩阵；不以 Editor 结果替代这些项目。完整五阶段小版本仍未完成。

最终[文档与源码检查](document-and-source-check.json)核对 26 份新增 / 受影响 Markdown 的本地链接、受影响 C# 的 UTF-8 / LF / 末尾换行及 143 个源码 / 资产哈希；本地链接和 C# 文本检查无问题，git diff --check 通过。

[返回版本归档](../../README.md)
