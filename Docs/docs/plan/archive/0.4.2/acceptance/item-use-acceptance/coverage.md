# 消耗品综合验收覆盖

2026-09-15。以下将长期回归与本轮独立 Player 证据分别列出；没有新增版本专用测试套件。

| 合同 | 长期保护 | 本轮 Player |
| --- | --- | --- |
| 使用能力、所有权、精确消费 | ItemUseTests：能力独立、非堆叠消耗、其他角色拒绝、移动和旧版本拒绝 | 商人物品拒绝使用、购买合堆、每次扣一件、末件移除 |
| 资源与复合事务 | ItemUseTests：按序模拟、百分比、裁剪、晚失败回退、无变化、抗性、通知重入和异常 | 回血 40、回蓝 60；满资源不扣；净化回血和驱散分别有效时仍成功 |
| 状态生命周期 | StatusSystemTests、StatusCombatIntegrationTests、ItemUseTests | 坚韧按比例调整生命、刷新、暂停冻结、30 秒自然到期；耗尽来源仍保留状态 |
| 驱散与门禁 | ItemUseTests：受保护异常、有益状态保留，UseItem 与 Cast 独立 | 七种正式异常同时驱散、坚韧保留；眩晕时鼠标净化；Shell 接管拒绝 |
| 输入与上下文 | Phase1UxPlayModeTests.Consumables_BuyMergeUseAndRestoreFocusThroughKeyboardAndGamepad | 键盘、手柄和鼠标 Input System 注入；商店默认出售、背包默认使用、打造允许菜单使用、耗尽后焦点有效 |
| 持久化 | SaveCaptureRestorePlayModeTests、SaveRestorePreparerTests | 使用后保存并退出进程，恢复资源、余量、身份、位置、商人库存和状态时间；core v4 真实历史文件继续并重存 |
| Session 与资源 | ItemUseTests、Alpha024SceneFlowPlayModeTests 及现有资源 / 场景生命周期回归 | 返回前台再继续、新游戏无旧药水或状态，旧请求拒绝；资源租约释放由全量回归验证 |
| 装备、交易、打造、掉落 | EquipmentPhase2Tests、GameplayUiFoundationTests、Phase4ContentPlayModeTests、LootTableDefinitionTests | 新商品精确扣款；药水不能投入打造；正式掉落资产未变 |
| 资源与排版 | 正式配置、本地化、资源策略测试；阶段 2 三语言文本尺寸验证 | 四种图标、状态图标、地面物品、HUD、详情和菜单实际渲染；三语言 × 两档分辨率 |

临时 Player 驱动仅通过现有 System / Command 操作，受控前置数据用于制造缺血、缺蓝和异常。实际输入是事件注入，未进行实体手柄、音频或网络权限验收。完整物品与状态规则沿用模块测试，不把本轮截图当成长久运行测试。
