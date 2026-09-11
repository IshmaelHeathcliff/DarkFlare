# alpha 0.3.4 十槽装备验收

执行日期：2026-09-10–11；2026-09-11 完成验收，正式构建版本 `0.3.4-alpha`。Editor / Player 行为与截图采集在更新版本号前完成，采集版本 `0.3.3-alpha`；随后清理临时入口、重跑全量回归并构建正式版本。

## 已完成内容

- 十槽保留旧 0–3 身份并追加六槽，普通单目标自动选择，双戒指明确目标；副手复用防御属性，不增加攻击来源。
- 正式目录升级为 core v2，104 个配置含二十件装备。新增十三件独立基底、六个空槽剪影，共十九张单 Sprite；[内容清单](./content-manifest.json)、[生成与导入合同](./asset-contract.md)、[逐图审计](./art-audit.json)。
- 商店为十列固定视口纵向滚动；同窗焦点可滚动至屏外商品，跨窗候选限于可见区域。设置 / 返回确认继续接管物品输入。
- 旧档在分离 DTO 上完成 core v1 → v2 兼容；旧装备 ID / 已掷值 / 库存保留，新槽为空。真实[四槽夹具来源](./old-fixture-provenance.md)与结构迁移夹具长期保留。

## 最终验证（2026-09-11）

| 验证 | 结果与证据 |
| --- | --- |
| EditMode 项目程序集 | [434/434 通过](./editmode-results.xml)，0 失败 / 跳过 |
| PlayMode 项目程序集 | [63/63 通过](./playmode-results.xml)，0 失败 / 跳过 |
| Editor 完整流程 | [通过](./editor/result.json)，正常采集期间 Error 0、Warning 0 |
| Windows Player 行为 | [通过](./player/behavior-result.json)，正常采集期间 Error 0、Warning 0；使用独立 UserData 根 |
| Addressables 内容构建 | 成功，内容构建 Error 为空 |
| Windows 验收构建 | [成功](./player/acceptance-build.txt)，0 Error、6 Warning；临时验收定义不进入正式交付 |
| 无临时入口的干净候选构建 | [成功](./player/clean-build.txt)，0 Error、1 Pipeline 配置 Warning；版本仍为封板前 `0.3.3-alpha` |
| 最终 Windows 正式构建 | [成功](./player/release-build.txt)，`0.3.4-alpha`，0 Error、2 Warning；仅 Bootstrap / Main，无临时编译定义 |

Editor 与 Player 均通过 Input System 注入鼠标 / 手柄状态驱动真实 UI，非人工实体手柄测试。包含右键购买、九个新装备拖入（主武器使用新游戏初始装备）、手柄上下空间邻居、跨窗拿起 / 取消、拖动买入 / 卖出 / 打造 / 丢弃、六词条详情、设置 / 返回确认、十槽保存后连续三次前台往返及真实旧档继续。其余正式基底经同一 Command 路径购买、打造与出售，长期内容测试覆盖逐件合法穿脱。

## Editor 视觉证据

1920×1080；伪本地化使用一次文本扩展和项目一致的 ASCII 填充字符，不新增玩家语言选项。

| 场景 | 中文 | 英文 | 伪本地化 |
| --- | --- | --- | --- |
| 商店 / 背包 / 十槽 | [图](./editor/zh-Hans-shop-ten-slots.png) | [图](./editor/en-shop-ten-slots.png) | [图](./editor/qps-ploc-shop-ten-slots.png) |
| 打造 / 六词条详情 | [图](./editor/zh-Hans-craft-six-affixes.png) | [图](./editor/en-craft-six-affixes.png) | [图](./editor/qps-ploc-craft-six-affixes.png) |

[初始完整商品与空槽](./editor/empty-slots.png)、[返回确认](./editor/return-modal.png)。三语言组合截图在购买 / 出售流程之后采集，商店已空；初始图保留完整二十件库存。详情使用独立滚动区域，十槽、三窗和 HUD 在安全区内。

## 可见 Player 视觉证据

2026-09-11 已补齐可见 Windows Player 验收：[结果](./player/visible-result.json)、[完整日志](./player/visible-player.log)。同一隔离程序以可见窗口启动后，所有 PNG 均正常，确认先前黑图属于后台窗口采集。八张图已逐张复核，十槽、双窗 / 三窗、最高六词条详情和返回确认均正常；运行中 Error 0、Warning 0。

| Player 场景 | 中文 | 英文 | 伪本地化 |
| --- | --- | --- | --- |
| 商店 / 十槽 | [图](./player/zh-Hans-shop-ten-slots.png) | [图](./player/en-shop-ten-slots.png) | [图](./player/qps-ploc-shop-ten-slots.png) |
| 打造 / 六词条 | [图](./player/zh-Hans-craft-six-affixes.png) | [图](./player/en-craft-six-affixes.png) | [图](./player/qps-ploc-craft-six-affixes.png) |

另见[初始完整商品与空槽](./player/empty-slots.png)、[返回确认](./player/return-modal.png)。

后台窗口采集的黑图已丢弃；以上可见窗口截图为最终视觉证据。版本更新及计划归档已完成。

## 日志区分与已知问题

最终构建保留两条警告：现有中文字体资产的 NativeFormatImporter 结果不一致提示（显式重新导入后仍出现），以及 Pipeline 未配置 RuntimePipelineConfig。构建成功且三语言画面已核对；本轮保留用户原有字体数据，没有通过重建字体资产消除此提示。详见[构建警告](./player/release-build-warnings.txt)。早期验收构建中的弃用 API 与临时采集器提示保留在原始记录中，不代表最终构建新增错误。

Player 退出时的 `GarbageCollector disposing of ComputeBuffer` 已通过独立空场景对照定位到 2D Animation 14.0.3 的静态回退缓冲区：不加载项目启动器、装备或 UI 也会复现，仅释放该缓冲区后提示消失。它不是正常测试 Log；本轮没有修改包或加入反射兼容代码。详见[定位报告及 A/B 证据](./player/gpu-exit-diagnosis.md)。运行中 Error / Warning 为零，不表示整个进程退出日志零 Warning。

临时内容登记、Editor 截图测试与 Player 入口源码已删除，数据路径工厂已恢复。全量测试曾因临时入口触发四项基础设施策略检查；删除后 EditMode 全部通过。旧环尺寸 / 四槽数量假设已调整，资产既有可见尺寸合同未放宽；不保留版本专用测试套件。

字体资产恢复为本次继续验收前的逐文件备份，保留用户原有修改；测试设置恢复为 EnterPlayModeOptions None / disabled，Editor 留在 Bootstrap 非 Play 状态。构建自动写入的 PlayerSettings 缓存已恢复，仅保留版本号更新。正式程序位于本地 `Builds/Alpha034/DarkFlare.exe`，构建产物未提交到版本库。

[alpha 0.3 总计划](../../../plan/archive/alpha-0.3-plan.md)与[0.3.4 阶段计划](../../../plan/archive/alpha-0.3.4-equipment-completion-plan.md)已完成归档，模块文档已同步。
