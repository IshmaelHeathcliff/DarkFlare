# 0.7.1-alpha.4 技能重构验收

2026-10-15：**阶段四完成**。计划先提交为 `6ed7091`，再执行本阶段；实现身份为 main / 该提交加未提交改动，见[源码身份](source-identity.json)。当前 Unity 6000.6.4f1、0.7.1-alpha.4、core v13、Save / Settings Schema 12 / 1、地图 V4。[实施计划](../../plans/phase-4-execution.md)已归档；[手动技能](../../../../../skill-board/manual-skills.md)和[效果模块](../../../../../skill-board/effects.md)描述当前行为，正式玩家棋盘仍属阶段五。

## 交付

- 移除默认自动施法。武器提供独立普通技能，六槽只分配已安装点亮的火焰；同一实例原子移槽，空槽不回退。鼠标左键 / 手柄西键普攻，1–6 / 东、南、双肩与双扳机施放火焰，七个入口可重绑定。
- 指针 / 右摇杆瞄准，无方向沿用上次方向；来源、输入世代、冷却、法力和 Session 在提交时复核。UI 点击、挂起和旧按住状态不补发；成功生成才扣蓝 / 开始冷却。
- 普通技能通道冷却跨换装保留；火焰冷却随实例流转。局部天赋 / 余烬按已点亮网络最短步数，仅作用于对应火焰；全局来源只重建一次，攻击快照保留火焰与武器两个身份。
- Schema 11→12 显式迁移旧等待时间，六槽为空，不赠物品 / 点数；未知历史技能无映射时拒绝。当前 Schema 缺字段、重复 / 非激活火焰和非法冷却均不能污染当前 Session。
- 最小六槽 HUD、普通技能绑定提示、属性局部来源解释及正式 Command / Query 开发分配入口可用。大剑 / 战斧分别配置普通技能，当前复用投射物表现和首轮暂定参数。

## 测试结果

| 验证 | 最终结果 | 原始材料 |
| --- | --- | --- |
| 项目 EditMode | 969/969，0 失败 / 跳过 | [完整复跑](editmode-full-rerun.json) |
| 项目 PlayMode | 103/103，0 失败 / 跳过 | [完整结果](playmode-full.json) |
| 技能与效果定向 | 12/12 | [结果](editmode-skills-final.json) |
| 保存序列化与迁移定向 | 17/17 | [结果](editmode-serializer-final.json) |
| HUD 中 / 英 / Pseudo | 1/1 临时采集，三张 1920×1080 目视复核通过 | [采集结果](hud-qa-with-bindings.json)、[文本测量](hud-qa.csv) |
| 正式内容 / Editor 收尾 | 目录及配置 0 问题，Reload Scene Only，非 PlayMode | [实际检查](content-and-editor-state.json) |

永久输入套件 23 项、成长 17 项、保存预检 11 项、保存往返 4 项均在最终整项目结果中通过，见[汇总](test-summary.json)。复用现有伤害、标签、状态、安装 / 成长、数据隔离与 Session 回归；没有新建按版本重复扫描的永久套件。

技能用例验证换装 / 卸装来源、实例单槽、取回清槽、冷却保留、失败不扣蓝、局部暴击冻结、长连线只计一步与范围边界；余烬验证未点亮阻断、旋转、无路径和同定义火焰隔离。已有几何 / 成长 / 安装测试继续保护环 / 跨根与断根拒绝；PlayMode 实际按键覆盖零输入零施法、冷却连按、模拟手柄、空槽及 HUD 点击防穿透。保存往返同时保留非空成长、普通技能与火焰剩余冷却。

## 失败记录与修正

- 早期保存测试发现默认六槽在 Newtonsoft 填充时被追加成十二槽；改为集合 Replace，保留历史完整 Payload 比较。[原序列化](editmode-serializer.json)、[原预检](editmode-preparer.json)及 fixed 文件保存当时结果。
- [首轮全量](editmode-full-first.json)为 963/965：旧测试仍断言 Attack 不可重绑；新增正式技能零耗蓝与既有正式配置约束冲突。改为符合新合同的断言和 1 / 2 耗蓝配置，未放宽验证器。
- [969 项首轮](editmode-full.json)为 968/969，地图导入 File.Replace 报 IOException；未改地图实现。[模块单独复跑](editmode-map-rerun.json) 6/6、随后完整复跑 969/969 均通过。错误原因未进一步证明，保留原栈供后续偶发问题对照。
- 新增旧设置兼容用例先因同步读取未完成 UniTask，再因 EditMode UnityTest 不支持 InputTestFixture.Set 失败；改用 await 和显式事件队列，最终输入 23 项全部通过。中间文件 `editmode-input-final.json`、`editmode-input-repaired.json` 保留失败含义，以整项目复跑为最终结论。
- [首次](hud-qa-first.json)与[第二次](hud-qa-second.json)采集暴露固定高度裁切；改为自然高度后，又通过截图发现默认 ProgressBar 内部高度和鼠标空 Glyph。限制内部冷却条、采用可读鼠标文本后复采通过。`hud-qa-final.json` 是最后一次鼠标提示修正前的中间结果；最终以 `hud-qa-with-bindings.json` 和下列 PNG 为准。
- PlayMode 调用返回遇到 300 秒传输超时；Unity test_status 确认测试已完整完成（296.56 秒、103/103），本目录保存该实际最终结果，未按超时猜测通过。

## 界面证据

[中文](hud-zh-Hans.png) · [英文](hud-en.png) · [Pseudo Locale](hud-qps-ploc.png)。采集涵盖三个已分配槽、三个空槽、实际耗蓝、暂停状态、保留冷却与普通技能按键；文字测量及目视检查均包含换行和卡片边界。截图在版本字段封板前采集，之后只推进 bundleVersion 和收尾文档，相关 HUD 行为未变。

[采集片段](hud-capture.cs.txt)需结合原 Phase1UxPlayModeTests 夹具复现；[内容与 UI 调整脚本](content-setup.cs.txt)通过 Unity MCP 执行。临时采集方法和本次 Temp helper 已从工作代码清除，仅以文本存证。永久行为用例保留。

## 实施边界与未执行项

棋盘配置当前仅支持原生修改器并禁止 ProvidedStatus，因此由棋盘 / 成长权威记录做所有权与激活预检、幂等重建；没有新增无消费者的 Talent 状态来源提供方。完整纯投影保持简单，未增加增量缓存。这两项实际落地已同步修正归档计划。

正式棋盘、玩家充能 / 点亮 / 六槽分配交互及完整键鼠 / 手柄闭环由阶段五交付。当前分配使用开发窗口；模拟手柄通过不代表真实设备测试。Player 构建、真实硬件、跨进程及当前版本性能矩阵未执行。测试使用隔离玩家数据；字体动态图集、Prefab 序列化噪声和一次性采集入口已清理。
