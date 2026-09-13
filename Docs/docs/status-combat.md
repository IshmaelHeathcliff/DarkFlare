# 状态与角色战斗接入

本模块承接[状态核心](./status-system.md)，实现阶段 2 的角色效果、周期伤害与行动限制。正式七异常及抗性见[异常与来源](./status-ailments.md)；自动时间任务、存档、状态图标仍按[总计划](./plan/alpha-0.4-status-system-plan.md)推进；当前通过 `StatusSystem.Advance` 显式推进时间，不会自动生成异常或药水玩法。

## 身份和生命周期

`StatusSystem.Bind` 使用 `PlayerId` 或 `MonsterInstanceId` 建立 CombatActor 与 StatusTargetId 双向映射。生产 Controller 配置完成后发送 `BindActorStatusesCommand`；Instantiate 的早期 OnEnable 只登记战斗角色，没有配置身份时不创建临时状态身份。已有配置的角色重新启用，由 CombatSystem 登记时重新绑定；同一对象配置为不同实例时释放旧身份，身份冲突在清理前拒绝。

玩家使用 `player:` 身份，怪物使用实例 ID；相同怪物基底不会共享状态。`CombatIdentity` 在攻击快照中保留此稳定来源，即使来源后来死亡或离场也不会改变已发出攻击的归属。纯规则容器仍可独立使用，无 Controller 的集成测试通过显式身份绑定状态。

战斗致死与注销调用 Unbind：使映射失效，移除目标状态及状态派生效果，释放该来源维持的 SourceOwned 贡献。已冻结限时伤害保留来源并继续结算。清理能在时间预算积压期间执行，并取消该目标尚未发布的准备提交。对象销毁后的 Session 清理不再访问 Unity 对象。

状态清理只移除状态来源，保留装备及怪物词条。整个战斗角色注销时，EquipmentSystem 仍按既有合同移除 Loadout 和装备来源；复活不会触发此卸装流程。复活重新绑定新代次，旧控制、限时伤害及旧准备请求不会恢复。持续提供方可在配置 / 复活后的绑定接点重新施加。

## 来源集合和属性

CombatActor 按来源键管理修改器：`equipment`、`monster`、`status` 分别由对应模块维护；旧 `SetModifiers` 只替换 `legacy` 来源。来源键按 Ordinal 排序，来源内部保留顺序；状态定义也按稳定 ID 排序。准备接点支持一批来源替换，统一从 CaptureBaseStats 的基础属性重建一次。

| 状态模型 | 属性投影 |
| --- | --- |
| Uniform | Flat / Increase / More / Conversion / GainAsExtra 的单层值乘生效层数后合并一项；三层 More 10% 为 1.3 |
| Independent | 每层分别进入修改器列表；三层 More 10% 为 1.331 |
| Strongest | 同一个最强候选的完整效果生效，弱层接替保留原参数 |
| Override / 行动限制 | Override 不乘层数，限制按位合并 |

标签从所有持有层合并到静态角色标签，数值与行动限制只取有效层。非伤害 GlobalActor 进入 CombatStatResolver 和主属性派生；伤害、Skill、TargetTaken 留在伤害管线。普通属性条件只读取当前角色的 SourceActor 标签，状态配置中带条件的其他域组合会明确拒绝；旧兼容修改器保留原标签匹配模式。

每笔变更从原生命、法力比例缩放到最终上限。例如 50/100 → 100/200 → 50/100。仅刷新时间、推进周期相位而有效投影不变时不重建属性，也不增加 StatsRevision。

ModifierOrigin 区分装备、怪物词条、状态和兼容来源；状态带定义 ID、层 ID 和合并层数。Increase 与 More 的正式计算步骤保留具体来源，属性详情使用角色标签判断条件，并提供中英状态来源文本。正式异常名称见[异常模块](./status-ailments.md)，状态图标在阶段 4 补齐。

## 一致提交

StatusStore 为受管理目标安装内部 IStatusParticipant，适用于公开 Store 和 Command / System 的变更入口；角色投影不依赖 StatusChangedEvent 的订阅顺序。

1. Prepare 模拟状态候选，同时准备修改器、标签、行动限制、最终属性和资源比例。非有限值、数值溢出或缺失伤害上下文整笔拒绝。
2. Commit 校验目标、状态版本、时间及角色属性 / 资源；未发布期间锁定状态目标与时钟，状态查询和角色属性均保持原值。
3. Publish 再次验证角色未发生变化，且 SourceOwned 的提供方仍有效，同步公开状态与角色投影，再发布资源和状态通知。若期间发生扣血、耗蓝、换装或失效，返回 false，回执 Result 为 StaleVersion，候选状态回退，保留外部真实变化。
4. Rollback / Dispose 不产生角色效果或通知；不通过反向施加或重新缩放恢复数据。宿主必须检查最终 Publish 和 Result，不能把 Commit 的准备成功当作效果已经发布。

到期、消费、驱散及来源释放使用同一投影。到期候选计算失败时恢复该事件前状态与时间并返回错误；此前已处理事件不撤销。未来物品事务可使用此接点编排，但本阶段没有实现背包、物品数量和多角色的完整事务。

## 周期伤害

DamageForm 将 Hit 与 Periodic 分开；周期的 HitOutcome 为 NotApplicable，IsHit / IsCritical 为 false，正伤害仍使 DidDealDamage 为 true。无效或零值周期不会显示未命中、闪避、暴击或受击动画；正常伤害数字、血条及统一死亡流程继续使用。

- Base 效果在施加准备时执行一次来源转换、额外伤害和增伤，冻结为 SourceResolved；需要有效来源角色。
- SourceResolved 不再重复来源计算，每跳只读取当前目标标签、TargetTaken 和类型抗性。物理周期跳过护甲；元素与混沌保留类型抗性。异常抗性在施加时冻结，见异常模块。
- DamageSourceSnapshot 保存稳定身份、施加时阵营、技能 / 物品来源及标签。没有来源角色的机制必须显式提供上下文，缺失则拒绝。
- 周期不使用命中、闪避、暴击随机流，不自动追加异常。核心按有效层逐条产生周期，每条只结算这一层，不能再次乘组层数。
- 内部战斗接点先提交伤害和必要的死亡清理，再发布 StatusTickEvent；处理队列后才查找下一事件。大步推进中首个致死后不再对该目标结算，死亡事件只发一次。

CombatSystem 的命中与周期共用资源、伤害和死亡提交。ActorDiedEvent 包含 Form 与稳定 Source，保留既有 Actor 消费者；当前归属用于事件、日志和测试，没有新增经验或任务奖励系统。

## 行动许可

`GetActorActionsQuery` 返回 CanMove / CanAttack / CanCast / CanUseItem 和阻止来源。无效、未配置、死亡、禁用及已解绑角色不开始新行为；既有无 Controller 的独立 CombatActor 保留配置后的兼容调用入口，其状态仍需显式身份绑定。

玩家与怪物在 FixedUpdate 检查移动许可并清零受限速度。FireProjectileCommand 在随机数、生成和耗蓝前检查 CanCast，装备武器来源额外检查 CanAttack；失败返回 ActionBlocked。自动施放沿原延迟调度继续，没有补发队列。

ContactAttackCommand 统一检查目标、攻击许可、距离和冷却，然后取种子、记录冷却、通知攻击并发起伤害。Controller 不再自行消费随机数或先更新冷却。旧投射物和冻结周期不重新检查来源的行动或生命状态。CanUseItem 独立，Move / Attack / Cast 控制不会自动阻止后续净化物品。

## 回归入口

`StatusCombatIntegrationTests` 保护来源共存、叠层数值、资源比例、延迟发布与回退、溢出拒绝、标签和来源解释、快照与实时防御、来源清理、重复致死、行动门禁及旧投射物。`StatusCombatPlayModeTests` 验证真实配置身份、移动停止、接触攻击拒绝、禁用重绑和死亡复活。原有状态核心及装备、伤害、资源、动画测试继续作为共同回归。
