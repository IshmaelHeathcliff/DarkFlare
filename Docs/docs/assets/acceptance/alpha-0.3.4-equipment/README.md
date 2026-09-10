# alpha 0.3.4 十槽装备验收

执行日期：2026-09-10。运行采集版本为 `0.3.3-alpha`；本阶段尚未封板，不能将此目录视为已发布记录。

## 已完成内容

- 十槽保留旧 0–3 身份并追加六槽，普通单目标自动选择，双戒指明确目标；副手复用防御属性，不增加攻击来源。
- 正式目录升级为 core v2，104 个配置含二十件装备。新增十三件独立基底、六个空槽剪影，共十九张单 Sprite；[内容清单](./content-manifest.json)、[生成与导入合同](./asset-contract.md)、[逐图审计](./art-audit.json)。
- 商店为十列固定视口纵向滚动；同窗焦点可滚动至屏外商品，跨窗候选限于可见区域。设置 / 返回确认继续接管物品输入。
- 旧档在分离 DTO 上完成 core v1 → v2 兼容；旧装备 ID / 已掷值 / 库存保留，新槽为空。真实[四槽夹具来源](./old-fixture-provenance.md)与结构迁移夹具长期保留。

## 当前验证

| 验证 | 结果与证据 |
| --- | --- |
| EditMode 项目程序集 | [434/434 通过](./editmode-results.xml)，0 失败 / 跳过 |
| PlayMode 项目程序集 | [63/63 通过](./playmode-results.xml)，0 失败 / 跳过 |
| Editor 完整流程 | [通过](./editor/result.json)，正常采集期间 Error 0、Warning 0 |
| Windows Player 行为 | [通过](./player/behavior-result.json)，正常采集期间 Error 0、Warning 0；使用独立 UserData 根 |
| Addressables 内容构建 | 成功，内容构建 Error 为空 |
| Windows 验收构建 | [成功](./player/acceptance-build.txt)，0 Error、6 Warning；临时验收定义不进入正式交付 |
| 无临时入口的干净候选构建 | [成功](./player/clean-build.txt)，0 Error、1 Pipeline 配置 Warning；版本仍为封板前 `0.3.3-alpha` |

Editor 与 Player 均通过 Input System 注入鼠标 / 手柄状态驱动真实 UI，非人工实体手柄测试。包含右键购买、九个新装备拖入（主武器使用新游戏初始装备）、手柄上下空间邻居、跨窗拿起 / 取消、拖动买入 / 卖出 / 打造 / 丢弃、六词条详情、设置 / 返回确认、十槽保存后连续三次前台往返及真实旧档继续。其余正式基底经同一 Command 路径购买、打造与出售，长期内容测试覆盖逐件合法穿脱。

## Editor 视觉证据

1920×1080；伪本地化使用一次文本扩展和项目一致的 ASCII 填充字符，不新增玩家语言选项。

| 场景 | 中文 | 英文 | 伪本地化 |
| --- | --- | --- | --- |
| 商店 / 背包 / 十槽 | [图](./editor/zh-Hans-shop-ten-slots.png) | [图](./editor/en-shop-ten-slots.png) | [图](./editor/qps-ploc-shop-ten-slots.png) |
| 打造 / 六词条详情 | [图](./editor/zh-Hans-craft-six-affixes.png) | [图](./editor/en-craft-six-affixes.png) | [图](./editor/qps-ploc-craft-six-affixes.png) |

[初始完整商品与空槽](./editor/empty-slots.png)、[返回确认](./editor/return-modal.png)。三语言组合截图在购买 / 出售流程之后采集，商店已空；初始图保留完整二十件库存。详情使用独立滚动区域，十槽、三窗和 HUD 在安全区内。

## 未封板事项与日志区分

后台 Windows Player 返回的截图全黑，已丢弃，不能作为视觉通过证据。待允许显示游戏窗口后，使用已构建的隔离验收程序重新采集和核对。无临时入口的最终全量回归和干净候选构建已通过；仍需版本更新及计划归档。

构建的六条警告为 Pipeline 未配置 RuntimePipelineConfig，以及原有 ContentCatalogDefinition、ComponentLifecycle、SceneSessionBinding、AudioService 的弃用 API。初次构建另有四条来自临时采集器的弃用提示，后续构建已清除，详见[初始警告](./player/initial-build-warnings.txt)。[完整后台 Player 日志](./player/background-player.log)在退出后另有一条 `GarbageCollector disposing of ComputeBuffer`；该消息未落入运行中采集窗口，项目业务源码未创建 ComputeBuffer，来源尚未定位，不能将它记为正常测试 Log 或声称整个进程零 Warning。

临时内容登记、Editor 截图测试与 Player 入口源码已删除，数据路径工厂已恢复。全量测试曾因临时入口触发四项基础设施策略检查；删除后 EditMode 全部通过。旧环尺寸 / 四槽数量假设已调整，资产既有可见尺寸合同未放宽；不保留版本专用测试套件。

字体资产恢复为执行前备份，SHA-256 为 `EAE7E5E78B5D26845433656E2842` 开头，保留用户原有修改；测试设置恢复为 EnterPlayModeOptions None / disabled，Editor 留在 Bootstrap 非 Play 状态。构建自动写入的 PlayerSettings 缓存已恢复。隔离验收程序在本地 `Builds/Alpha034Acceptance/DarkFlare.exe`，干净候选在 `Builds/Alpha034Candidate/DarkFlare.exe`，均未提交到版本库。
