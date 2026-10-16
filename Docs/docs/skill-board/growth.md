# 中心充能、属性分配与点亮

中心与成长通过 `PlayerGrowthModel / PlayerGrowthSystem` 接入当前 Session；瓶子归属仍由[安装事务](items-installation.md)管理。当前[手动技能](manual-skills.md)消费柴薪激活资格；全局 / 局部天赋和余烬按[效果投影](effects.md)进入实际战斗。

## 兑换与点数

`PlayerGrowthConfiguration` 由正式玩家 CharacterDefinition 引用。首轮配置为第 n 点消耗 `100 + 25 × (n - 1)` 能量，柴薪 / 普通天赋各花 1 天赋点，每个属性分配点增加 1 对应主属性。规则合同及实际参数随角色成长保存，配置不兼容变化在预检拒绝。

未满瓶也可充入全部实际能量并消费整瓶；空瓶拒绝并保留。余额跨瓶累计；每兑换一点，同时增加 1 天赋点和 1 属性点。1000 能量可兑换 6 点，保留 25，下一点成本 250；拆成多瓶充入结果一致。花点、退款、换装和恢复均不改变累计兑换数。

角色持有非负整数能量池、累计点数、两类可用点数、力量 / 敏捷 / 智力已分配点、激活身份及实花点数。点数守恒为：

- 可用属性点＋三属性已分配点＝累计获得点数。
- 可用天赋点＋有效激活记录实花点数＝累计获得点数。

计算使用受检整数累计成本及有界搜索；角色池须小于下一阈值。累计点数技术上限为 Single 可精确表示的 16,777,215，属性分配还受配置倍率的同一精度约束；越界整次拒绝，不裁掉能量或重复兑换。

## 中心选择与提交

`SelectEnergyBottleQuery` 返回短暂 `EnergyBottleSelection`；原瓶仍在背包或专栏，选择不新增持久化所有者。`PreviewEnergyChargeQuery` 生成正式 SkillBoardPreview，`CommitSkillBoardCommand` 一次更新瓶归属、成长池和点数。专栏瓶消费后栏位清空，背包瓶消费释放 2×1 位置，不自动换瓶。

选择绑定 System / 玩家、原容器与背包位置。背包栅格 Revision 防止移走又放回原位后复用旧选择；换装、出售或 Session 结束也使选择失效。瓶继续积蓄不移动所有权，但实例 Revision 使旧充能预览失效。预览还捕获棋盘、成长、配置与完整物品修订；取消 / 关闭只清除临时引用。

提交先复核全部候选，再整体替换状态，最后通知 Inventory / SkillBoard / PlayerGrowthChanged。通知异常不会撤销已提交状态，预览已消费后不能重试以复制点数。SaveCoordinator 监听成长变化标脏。

## 属性分配

`PreviewPlayerGrowthQuery` 对 strength / dexterity / intelligence 生成分配候选，`CommitPlayerGrowthCommand` 扣点并应用已准备的 Actor 属性投影。`player_growth` 来源使用 GlobalActor / Flat 修改器和独立 Growth 来源标记；每次从分配记录重建并替换来源，不修改共享配置或已派生基础值。

沿用 PrimaryAttributeResolver：每力量派生 2 最大生命，敏捷派生命中 / 闪避各 1，智力派生 2 最大法力。有效上限变化保持生命 / 法力比例；装备、状态、复活与重复配置保留同一成长来源。属性详情展示本地化“永久属性分配”，查询不发点或重建第二套公式。

## 激活与取回

柴薪本身的激活记录用物品 ID＋空 NodeId；普通天赋用物品 ID＋实际局部节点 ID。中柴薪的物理中心仍留白，空 NodeId 仅表示柴薪激活记录。

- 大柴薪从初始空洞点亮；中小必须通过已点亮柴薪连到至少一个根。几何安装允许预先布置未点亮柴薪。
- 柴薪与普通天赋分别花点。裂缝不单独花点，所属柴薪点亮后开放；大柴薪中心火焰资格随该柴薪点亮开放。
- 普通天赋沿实际保存的显式边逐点点亮。大中心为起点，中小以连接到有效激活网络的裂缝为起点；未接入裂缝不能凭空作为起点。连线可跨空格，不能用六格相邻代替连线。
- 图遍历处理多入口、环与跨根；移动 / 重连后所有既有激活仍须合法，不自动花点、退款或丢记录。

拆卸仍先手动逐件取出火焰 / 余烬，再单件断连取回。成功取回只清除当前柴薪及其普通天赋记录，退还实花天赋点；累计兑换数、属性分配保持。剩余几何须连根，其他已点亮柴薪和天赋仍须有激活路径，否则整次拒绝，不连带退款。背包满时也不能先退点。

## 保存、恢复与验证入口

阶段三 core v12 / Save Schema 11 将 Growth 放在 Profile，瓶子和柴薪仍只在统一 Items 表保存一次。临时选择不入档。10→11 初始化空成长、已确认的首轮成本与当前玩家身份；保留旧棋盘、能量瓶、技能 / 冷却、资源和随机状态，不赠送点数或重放充能。

Validator / Mapper / Restore Preparer 共同验证非负与守恒、成本合同、所有权、节点角色、重复记录和激活网络。恢复先建立成长来源、装备 / 状态属性，再恢复绝对资源；不会再次消费、兑换或退款。

运行有效 Session 后打开 `DarkFlare/Debug/柴薪安装与能量瓶`：输入瓶实例 ID，选择 → 预览 → 确认；查看余量、累计点数和下一成本；输入属性 ID 与数量进行分配，使用柴薪 ID / 节点 ID 分别点亮。取消 / 关闭清除临时选择和预览。此开发入口复用正式 Query / Command；正式充能、加点与手柄操作见[玩家棋盘](player-board.md)。

长期覆盖为 PlayerGrowthTests，以及扩展的 SkillBoardTransactionsTests、SaveSerializerTests、SaveRestorePreparerTests 与 SaveCaptureRestorePlayModeTests。实施和当次验收入口见[阶段三计划](../plan/archive/0.7.1/plans/phase-3-execution.md)。
