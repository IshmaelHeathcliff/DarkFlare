# 统一物品使用核心

2026-09-15：阶段 1 已完成，EditMode 562/562、PlayMode 73/73 通过。范围为使用能力、四类效果与复合事务；阶段 2 已接入正式药水、商人库存和背包 Use 动作，见[消耗品与交互](./consumables.md)。综合验收后运行版本为 `0.4.2-alpha`，正式目录 core v5、Schema 3。

## 入口与所有权

`ItemBaseDefinition.UseDefinition` 可选引用 `ItemUseDefinition`。可堆叠、可消耗、可主动使用分别表达；金币和材料不会仅因 IsConsumable 自动获得使用动作。字段参考见[使用配置](./config-reference/item-use.md)。

Controller 先发送 `CreateItemUseRequestCommand(actor, item)` 获取一次操作请求，使用 `GetItemUseSnapshotQuery(request)` 预览，再发送 `UseItemCommand(request)`。System 提供 CreateRequest / Prepare / Commit，Prepare 的回执可 Dispose 放弃。结构化 ItemUseResult 返回 Code、Resources、Statuses、ConsumeCount 与可选 StatusCode，供 UI 和调试工具直接读取，不额外维护计算公式。

请求仅接受当前 Session 的本地玩家自身、实际背包实例与有效状态身份。所有权与既有背包查询一致，以 CombatModel 中首个玩家为背包持有人；不支持对第二玩家、怪物或装备槽物品使用。商人物品和地面物品不属于背包，不能使用。已移除实例不回退到另一同基底堆叠。

使用请求记录物品 Revision、状态目标代际与操作号。System 按 Session 单调发号，成功后记录最大已完成号；小于等于该号的请求均拒绝，即先执行较新号会淘汰尚未执行的旧号。无需无限增长的去重表，也不把操作号存入存档。UI 应在操作开始时创建新请求，不缓存跨 Session 的请求。

## 候选效果与提交

恢复生命、恢复法力、施加临时状态和驱散异常按配置顺序模拟。恢复复用 CombatActor 的资源裁剪规则；状态使用 StatusStore.Prepare 及现有战斗投影。状态改变资源上限时，候选资源按既有比例规则转换，再处理后续恢复。预览只创建候选数据，不写背包、资源、状态、随机通道或事件。

物品状态只允许 Timed；来源属性与身份在使用准备时固定，具体状态规则、异常抗性及周期快照沿用状态系统。来源键为物品实例与本次操作号，末件消失或剩余物品被出售不撤销已施加效果。驱散要求 Ailment 类别并可选定义 / 标签，遵守保护层。完全抵抗和无匹配驱散视为单项无变化；全部无变化不消费，规则冲突和非法配置则整次失败。

提交复核：Session、行动许可、状态版本 / 时间 / 来源、角色属性与资源、背包对象、物品位置 / Revision / 数量 / 品质 / 耐久、能力引用及消费资格。旧准备结果拒绝时保留外部合法变化，不能用旧快照覆盖新数据。

`StatusPreparation.CommitExternalProjection` 是内部协调接点：即使没有状态层变化，资源 / 背包复合投影也走相同目标锁与版本检查。最终投影统一应用状态、资源和选中实例扣除，所有写入完成后才发布通知。准备与提交之间可以放弃，实际提交区间同步执行且不跨 await；暂存状态尚未 Publish 时由 StatusCommit.Dispose 精确回退。物品与资源在最终 Apply 才写入，因此发布前拒绝无需反向治疗或重新施加状态。

首次资源、背包、状态或 ItemUsedEvent 通知均能读取完整最终结果。通知期间重入使用返回 Busy；状态写请求沿原队列处理。各通知组通过 finally 保证后续组仍有机会发布；单组内部沿用 QFramework 异常语义，订阅者异常可能向调用者传播，但已公开结果不会补偿回退，原请求不能重复消费。调用方遇到异常应刷新查询，不把异常等同于“尚未扣除”。

## 生命周期与存档

存活且 CanUseItem 未被禁止才能使用；Cast / Move / Attack 封锁不自动禁止净化。背包或暂停菜单造成的游戏时间暂停允许即时使用，限时状态等待恢复运行后推进。Shell 接管、输入挂起、场景切换、Session 未运行 / 退出及状态快照锁期间拒绝使用。

实例剩余数量使用现有 ItemInstanceDto，已提交状态使用现有状态 DTO。读取不会再执行即时恢复或再次扣除。旧物品的使用能力默认为空；不增加存档字段，不改变已发布内容或怪物掉落权重。真实场景保存恢复回归使用临时能力并在 finally 恢复原配置，不生成正式药水资产。

## 验证与后续

`ItemUseTests` 长期保护精确消费、组合顺序、无变化、拒绝 / 放弃、外部版本变化、状态抵抗、行动许可、所有权、重入与通知异常。`SaveCaptureRestorePlayModeTests` 复用真实 Session 保存恢复链路，覆盖资源不重复恢复、数量保留、物品来源状态及旧请求拒绝。

当次结果与覆盖说明见[阶段 1 验收](./assets/acceptance/alpha-0.4-item-use-core/README.md)。正式药水配置、图标、本地化、商人获得途径及键鼠 / 手柄背包交互已接入；整体 Player 验收已完成，见[综合验收与交付](./assets/acceptance/alpha-0.4-item-use-acceptance/README.md)。
