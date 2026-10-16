# 0.7.1-alpha.5 玩家棋盘验收

2026-10-16：**阶段五与 0.7.1 小版本收尾完成**。实现基于 main / `9fd34f8` 加本轮未提交改动，见[源码身份](source-identity.json)。Unity 6000.6.4f1、运行版本 0.7.1-alpha.5；core v13、Save / Settings Schema 12 / 1、地图 V4 保持。[实施计划](../../plans/phase-5-execution.md)、[总计划](../../plans/alpha-0.7.1-skill-board-plan.md)和[完整清单](../checklist.md)均已归档，当前行为见[玩家模块](../../../../../skill-board/player-board.md)。

## 交付

- 棋盘与背包并排，退出恢复有效窗口组合；中心瓶＋三属性＋三空格、六根跳转、无边界视口、平移 / 缩放、六邻居游标与双向旋转可用。
- 复用唯一物品拖放 owner 和正式 Query / Command；预览使用候选最终态、完整形状和结构化失败原因。多候选在放下后显式勾选 / 确认，手柄可进入连接列表。嵌入、点亮、逐件取出、单件拆卸和退款均走权威事务。
- 专栏图标 / 能量 / 容量 / 满瓶说明、部分瓶中心确认、角色余量 / 下一成本、属性分配和六槽分配可操作；实例移槽不复制，范围步数与实际效果共用 BFS。
- 面板、Settings / Modal、设备拔出、挂起和 Session 解绑沿共享生命周期清理预览、捕获、刷新与事件；没有新增资源 owner、Action Asset 或场景 YAML 改动。

## 当次验证

| 范围 | 结果 | 证据 |
| --- | --- | --- |
| 项目 EditMode | 970/970，0 失败 / 跳过 | [完整结果](editmode-full.json) |
| 项目 PlayMode | 106/106，0 失败 / 跳过 | [完整结果](playmode-full.json) |
| 棋盘定向及三语言采集 | 3/3；增强后的渠道 / 输入 / 保存闭环再次 1/1 | [UI 采集](board-ui-qa.json)、[闭环](board-loop-final.json) |
| 最终本地化策略 / 资源回归 | 20/20 | [定向结果](localization-final.json) |
| 最终整页视觉采集 | 1/1，含页签 / 操作条 | [最终采集](ui-capture-final.json) |
| 中键按住关闭 | 1/1，捕获正确释放 | [结果](pan-close.json) |
| 正式内容与 Editor 收尾 | 目录 / 配置 0 问题，停止状态、Reload Scene Only | [实际检查](content-and-editor-state.json) |

闭环夹具固定正式商人货池及购买金币，通过 BuyItemCommand 获得四类道具；在野外使用正式怪物生成和 CombatSystem 死亡结算收集部分瓶，没有调用调试道具发放。实际键鼠 / 模拟手柄输入覆盖瓶子确认、属性分配、柴薪和普通天赋分别点亮、旋转、游标、鼠标拖放、余烬嵌入和原子移槽。退出后按键施法，经正式 SaveAuto / Return / Continue 恢复，验证实例、槽位、冷却和成长；恢复后手动逐件取出、清槽、单件拆卸和实花点退款。

既有套件覆盖安装连接、环 / 跨根、三尺寸、三档瓶、效果隔离、重绑定、旧工作台、历史迁移与连续 Session。新增投影用例保护格子往返、六方向和溢出；原效果用例同时断言 UI 所读路径步数。三项 UI 回归保留在现有 Phase1Ux 套件，没有新建按阶段重复套件。临时采集代码与 helper 已清理，源片段保存为[文本](ui-capture.cs.txt)，Unity MCP 创建 / 导入脚本见[存证](ui-mcp-setup.cs.txt)，对应输入为[UXML](ui-layout.uxml.txt)、[USS](ui-styles.uss.txt)、[文本表](ui-localization.tsv)，复现时复制回脚本中的 Temp 原路径。

## 视觉与修正

[中文](board-zh-Hans.png) · [英文](board-en.png) · [Pseudo](board-qps-ploc.png) · [实际安装棋盘](board-populated.png)。均为 1920×1080；已目视复核窗口不重叠、确认区可达、长文本换行和详情滚动。伪本地化包含三个已分配技能槽；采集时暂时停止该面板时钟以避免动态文本被恢复，正式代码没有该入口。截图在运行版本字段推进前采集，随后只作发布元数据与文档收尾。

接入中修复了背包焦点没有同步棋盘选择、多连接时携带状态阻止聚焦列表、动态本地化绑定覆盖运行文本、详情沿用旧滚动位置等问题。[闭环初轮](board-loop-first.json)记录测试误用了城镇无怪物的生成入口；改为野外正式生成。[导航初轮](board-navigation-first.json)记录同帧按放 D-pad 未产生 UI 导航；改为跨帧输入，保留行为断言，最终通过。[首轮全量](playmode-full-first.json)为 104/106，新增页签受共用最大宽高限制导致 Pseudo 裁切；页签改为自然宽度。[定向复核](ux-first-fix.json)及[操作条复核](ux-bar-first.json)又暴露棋盘操作条高度限制，恢复自然高度后再验收。没有删除失败用例或增加 Ignore。

文档链接[核对](documentation-check.json)通过：新增 / 受影响文档的本地链接均无失效目标；源码 / 配置身份覆盖 144 个文件。完成的总计划、合同和清单已归档，当前模块入口保留在 `Docs/docs/skill-board/`。

## 未执行项

本次未构建 Player，未执行真实硬件、跨进程或当前版本性能矩阵；模拟输入和 Editor 结果不代表这些项目。没有扩展武器独立近战表现、洗点、自动换瓶 / 拆卸或最终数值平衡。0.7 里程碑总计划继续活动，下一小版本范围尚未定义。
