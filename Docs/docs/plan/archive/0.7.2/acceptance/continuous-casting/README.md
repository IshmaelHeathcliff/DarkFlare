# 0.7.2-alpha.1 存档路径与持续施法验收

2026-10-26：**阶段一完成，当前 `0.7.2-alpha.1` / core v14**。实施基线为 `main` / `da2474c` 加本轮改动，Unity `6000.6.7f1`，见[源码身份](source-identity.json)。[实施计划](../../plans/phase-1-execution.md)与[存档前置计划](../../plans/save-path-isolation-plan.md)已归档；[优化总计划](../../plans/alpha-0.7.2-optimization-plan.md)继续活动。

## 实际交付

- 普通 Editor Play 使用固定正式设置 / 存档目录。自动化测试通过 Test Framework 的程序集启动钩子声明身份，在 Bootstrap 启动前安装独占临时目录；测试结束、错误和退出测试 Play 均清理本轮安装。无测试身份时拒绝显式重装。
- 普通技能与六火焰槽支持短按和保持连放。每次释放读取最新瞄准，冷却与法力不足时等待；松键、来源改变、窗口、暂停、挂起、死亡和 Session 失效终止旧输入。复活后释放再按能重新施法。
- 正式大剑一次三枚，中心 / 左 10° / 右 10°，每枚最多结算两个不同合法敌人。每次施法只取一个根种子，各枚派生独立冻结快照；同枚去重先于伤害通知，防止事件重入。
- 整组准备成功且再次复核来源、地图、动作和资源后，才扣一次蓝、开始一次冷却并通知一次。部分创建或准备失败时立即停用并回收整组。
- core v14 接续已知历史内容迁移；Save / Settings Schema 12 / 1、地图 V4、城镇 r1 / 野外 r9 保持。旧实例身份、成长、物品、冷却和随机状态继续恢复。
- 联合回归中修复 `RuntimePanelView` 的同根节点重新挂回通知，并解绑旧根监听，避免界面可见但 Shell 仍处于解除绑定状态。

## 验证

| 验证 | 最终结果 | 材料 |
| --- | --- | --- |
| 项目 EditMode 全量 | 982/982，0 失败 / 跳过 | [完整结果](editmode-full.json) |
| 项目 PlayMode 全量 | 112/112，0 失败 / 跳过 | [完整结果](playmode-full.json) |
| 连放场景定向 | 5/5，包含死亡后自动复活再按键 | [结果](playmode-held-skills.json) |
| 界面重载与 Shell 联合顺序 | 11/11，保留三语言边界与真实交互断言 | [修复后](panel-sequence-after-fix.json)、[修复前 9/11](panel-sequence-before-fix.json) |
| 地图准备 / 保存失败回滚 | 1/1，并纳入最终全量 | [定向结果](map-save-wait-final.json) |
| 正式历史档只读预检 | 原 `0.5.4-alpha` 解码并恢复准备成功，目标 core v14 | [结果](original-save-compatibility.json) |
| 两轮普通 Play | 继续按钮均通过预检；角色 / 武器 / 物品 / 金币一致；显式保存代际 2→4，均返回前台 | [第一轮](ordinary-continue-first-final.json)、[第二轮](ordinary-continue-second-final.json) |
| 配置与 Editor 收尾 | core v14 有效；35 类 / 274 字段文档覆盖通过；Reload Scene Only；已停止、仅 Bootstrap、无测试标记 | [实际检查](editor-final.json) |
| 普通 Play 控制台 | 两轮普通启动、保存、退出后 0 Error / 0 Warning | [结果](ordinary-play-console.json) |
| 原文件保护 | 1 个正式档与 2 个既有临时档 SHA-256 全部一致 | [逐文件结果](original-files-restored.json) |

完整回归通过后仅推进 `bundleVersion` 并整理材料；core v14 与当前生产实现均参与上述全量，最后两轮普通 Play 在 `0.7.2-alpha.1` 下执行。原有正式档恢复后仍为开始前字节内容，下一次普通 Play 会按当前兼容链读取。汇总见[test-summary.json](test-summary.json)。

普通 Play 使用 `Application.persistentDataPath/DarkFlare/Saves` 和固定 Settings 根；真实停止后再启动，未重新注入路径工厂。采集逻辑保留为[普通 Play 片段](ordinary-play.cs.txt)与[Editor 收尾片段](editor-final-check.cs.txt)，临时执行脚本从工作区验证目录清理，不进入运行程序集。

实际项目设置文件已写入 `0.7.2-alpha.1`；Unity 原生保存带来的空字段行尾空格保留，忽略行尾空白后仅版本号发生变化。保存时保护了其他资产的待保存标记，原字体文件哈希不变。文档 395 个链接无缺失，源码 / 配置 35 个文件哈希均一致，七个临时执行脚本已清理；见[收尾记录](cleanup-and-links.json)。

长期测试补入现有输入、技能、保存预检、资源、生命周期和 UX 套件，没有新建按版本命名的永久测试套件。保留玩家数据隔离、历史夹具和在途攻击冻结断言；将旧夹具中的未登记 / 未激活目标改为真实有效目标，UI 等待实际就绪状态后保留原行为断言。

## 排查记录

- 首轮 EditMode 979/981：新增三个配置字段尚缺文档覆盖行。补齐字段说明后，正式配置扫描为 274/274、0 问题，最终全量 982/982 确认。
- 首轮 PlayMode 107/111：旧命中夹具未登记或禁用目标；另有两处 Shell 操作失败。补齐真实有效目标和有期限的 UI 就绪等待，未放宽命中规则或 UI 断言；Shell 的后续定位如下。
- 修复命中夹具后全量 110/112，两个 Shell 用例仍失败。联合顺序 9/11 复现时，实际观察到 `shellBound=false`、`detached=true`、根节点已挂回 Panel。原因是原同根节点恢复没有触发新的 Renderer 版本回调，只有脱离时的解绑。新增根节点 Attach 通知，补齐旧根监听清理，并在原重载用例中验证恢复原资产、释放临时资产之后仍可打开 / 关闭设置。实际修复前后的结果与最终全量分别保存，不把增加等待视为已修复此问题。
- Shell 修复后的[完整回归](playmode-before-save-wait-fix.json)为 111/112；唯一失败是 `ManualReset_PreparationAndSaveFailuresKeepOldMap` 在故障注入前等待保存的 120 帧上限不足，断言位置为原 233 行。改为最多 30 秒等待真实空闲，保留原空闲、事务失败、地图 / 名额与持久状态回滚断言；本次没有修改地图或保存实现。该修改仅涉及 PlayMode 夹具，之后重跑完整 PlayMode。
  - 中间一次补丁误命中同文件另一条相同语句，导致重跑仍为 111/112；已还原误改处并按完整方法上下文修正确切位置，随后先单独验证此用例，再运行最终全量。
- 连放夹具改用输入系统事件处理后的结果断言；同一键盘状态字节的双键保持用完整状态事件提交，缺蓝场景显式抑制自然回蓝。
- 复查发现死亡使旧任务版本失效但未清运行标志，可能阻止复活后的新施法。已修复并增加长期回归。修正前的中间全量主动取消，不计为通过。
- 关闭 Domain Reload、先 Play 后直接跑 EditMode 时，包内 `InputTestFixture.Setup` 重置输入会触发 UI 输入提供器的 `onActionsChange`，随后 `ProjectWideActionsAsset.Verifier` 收到空 asset，34 项准备失败（948/982）。[原始结果](editmode-after-play-failed.json)保留复现栈。使用项目现有“手动刷新脚本域”菜单后，完整 EditMode 982/982；本轮未修改 Input System 包，最终 EditMode 结果以刷新后的环境为准，未将此测试准备问题登记为已修复。

## 原有存档保护与边界

开始前复制正式旧档与已有临时档，并记录三个原文件的 SHA-256；备份保留在项目 `Library/AgentBackups/alpha-0.7.2-1/`，不将玩家存档内容纳入版本库。正式旧档为 `0.5.4-alpha` / 原 Schema 5 / core v6，当前反序列化及 core v14 只读预检通过。

普通 Play 验收会正常写正式目录，因此结束后将生成的 `4.save` / `5.save` 另存到 `Library/AgentBackups/alpha-0.7.2-1/final-ordinary-play-generated/`，恢复原 `1.save`，三个原正式 / 临时文件哈希均一致。没有认领或迁移已有临时档，也没有用测试角色替换玩家进度。

本阶段未改 HUD / 天赋布局、能量瓶入口、地图尺寸或刷怪名额；后续仍按活动总计划实施。未构建 Player，未执行真实手柄、跨进程或 300 怪物性能验收。Unity Stop 仍不承诺等待异步保存，应先完成游戏内保存或返回主菜单。
