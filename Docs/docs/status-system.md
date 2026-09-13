# 状态系统

2026-09-13：阶段 1–3 已完成，EditMode 全量 527/527、随后本地化专项 20/20、PlayMode 68/68 通过，见[阶段 3 验收记录](./assets/acceptance/alpha-0.4-status-ailments/README.md)。运行版本 0.4.0-alpha、core v4、Schema 2；后续安排见[总计划](./plan/alpha-0.4-status-system-plan.md)。

## 当前职责

`StatusDefinition` 提供中文 Odin 配置和稳定 `status:` 内容身份；`StatusRules`、`StatusEffectSnapshot` 冻结规则和效果。`StatusStore` 是不依赖场景的纯状态容器，管理目标、来源、层数、择强、时间、消费、驱散及事务。配置字段参考见[状态配置](./config-reference/statuses.md)。

`StatusModel` 在 Session 中持有容器，`StatusSystem` 管理角色绑定、效果参与者及 QFramework 通知。[角色战斗接入](./status-combat.md)已实现属性来源共存、周期扣血和行动门禁。七异常与抗性已接入，详见[异常与来源](./status-ailments.md)；自动时间任务、存档及状态 UI 尚未接入；当前不会在游戏中自动出现状态或药水效果。

代码位于 `Assets/Scripts/Runtime/Data/Statuses/` 和 `Assets/Scripts/Runtime/Gameplay/Statuses/`。所有状态共用核心，未为各异常创建独立 MonoBehaviour 或计时任务。

## 身份与快照

宿主通过 `RegisterTarget(PlayerId / MonsterInstanceId)` 获取 StatusTargetId，其包含 Session 代际、稳定角色身份及本次注册代次。重复登记当前目标返回同一键；注销后重绑生成新键，旧请求和旧准备结果失效。Session 结束释放全部状态、通知订阅和排队操作。

来源使用 StatusSource，记录技能、天赋、装备、消耗品或其他机制的稳定键及可选来源角色身份。限时 / 无限状态的来源仅用于解释；SourceOwned 状态才随 ReleaseSource 移除。来源不持有 Unity 对象。跨代际来源请求被拒绝；来源离场后，本代际已有快照仍可用于归属。

`Capture` 返回不可变目标和逐层快照，包含规则、效果、来源、实例 ID、持有与生效信息、剩余秒数、下次周期时间和版本。`GetStacks(filter, activeOnly)` 与 `HasStatus(id)` 支持标记检测；筛选支持定义、分类及全部指定标签。无限与来源维持层用明确的 Lifetime 和无限剩余时间表达，展示方不能当作普通秒数格式化。

## 叠层与来源

| 模型 | 行为 |
| --- | --- |
| Uniform | 所有层采用统一单层参数；施加增加到上限，覆盖现存层参数并刷新现存到期点，保留各层来源与周期相位 |
| Strongest | 各候选独立保存数值 / 时间，只激活最强者；同强度保留较早实例；强层移除后弱层按原剩余时间接替 |
| Independent | 各层独立生效；默认满层替换最早到期层，到期相同按施加时间、实例 ID 选择 |

Shared 只允许 Uniform，描述共同到期点；周期相位仍按实例保存。Uniform 的 PerLayer 默认也刷新所有现存层；关闭 RefreshExistingLayers 后，未满层新增保留旧层时间，实现统一数值、逐层到期减层。满层时仍按配置的满层策略刷新或拒绝。

满层可覆盖为 Reject；Uniform / Independent 还支持 RefreshTime，保留数值、来源、身份及周期相位。独立刷新可明确指定实例，旧实例引用拒绝。独立与择强请求可带 count，内部按请求顺序逐层处理，整笔准备失败不会留下部分层。

SourceOwned 以目标、定义、来源键去重，重复 Apply 无变化；改变来源参数使用显式 UpdateSource。新维持来源必须实际获得层身份：Uniform 已满且新来源尚无层时返回 Capacity，不能仅刷新其他来源的参数；需要候选竞争的光环使用 Strongest。ReleaseSource 仅释放对应维持层，保留其他来源及已提交的限时药剂状态。正在持有的同定义组拒绝不一致的规则，编辑配置不会改变已有快照。

统一叠层 More 线性累计与独立层分别乘算由角色效果参与者实现；纯核心提供层数和逐层参数，不直接依赖 CombatActor。

## 操作与事务

Controller 使用 `ChangeStatusesCommand` 提交 StatusMutation 列表，使用 `GetStatusSnapshotQuery` 查询。System 提供 ApplyStatus、TryConsumeStatusStacks、DispelStatuses、ReleaseStatusSource；底层都调用相同规则。宿主通过 Store 完成身份登记、时间和组合事务编排。

- Consume 必须指定定义，默认最早到期优先且要求足额；可选 Active 或指定实例集合。数量不足、重复 / 失效实例、不可消费和旧版本均整次失败。
- Dispel 按筛选条件与上限移除允许驱散的层；未匹配返回 NoChange，不误删保护层。
- 操作记录区分新增、刷新、到期、消费、驱散、来源释放、替换及目标注销。Changes 是发生记录，当前完整生效状态以 Result.Snapshot 为准。
- 相同数值、相同到期点的覆盖施加返回 NoChange；有效延长时间、增加层数或改变效果会产生变更。

复合事务先 `Prepare(target, mutations, expectedVersion)`，在候选状态中顺序模拟，无真实写入、ID 消费或通知。`Commit` 复核目标、状态版本及逻辑时间；准备结果只能使用一次。不同目标在同刻的普通变更不使彼此的准备失效。

提交回执实现 IDisposable，宿主必须选择 Publish 或 Rollback；未处理而 Dispose 时自动回退。未发布期间锁定目标写入和全局时间，公开查询与角色属性保留原状态，候选最终状态通过回执 Result 读取。Rollback 精确还原原层、时间、周期和 ID 进度。受管理角色在 Publish 时再次验证属性和资源，同步公开投影后通知；若角色已变化则返回 false、Result 为 StaleVersion，回退候选状态并保留外部变化。完整合同见[一致提交](./status-combat.md#一致提交)。此接点供后续物品事务使用，不用“反向扣血”或重新 Apply 模拟恢复。

事件回调产生的新写请求排队，StatusOperation.IsCompleted 在执行完后变为 true，最终结果独立复核版本。回调不能将“已排队”视作已消费成功。每轮默认排空最多 256 个排队操作，宿主可检查 PendingOperations 并续调 DrainOperations；时间推进也在事件边界处理队列。

## 时间合同

宿主显式 `Advance(elapsed, eventBudget)`；核心使用游戏时间，无后台任务，也不从现实时间补算离线伤害。默认每次处理最多 256 个周期或到期事件；预算耗尽用 `Advance(0, budget)` 续处理，HasPending 即使 Time 等于 PendingUntil 也可能为 true，因为同刻到期仍未处理。积压期间新的非零增量和普通写入返回 Busy。

事件按发生时刻、周期优先、目标注册次序、实例次序排列。同刻先结算当时有效层应有的周期，后移除到期层。第一跳在完整间隔以后，尾段不补跳；刷新不改变相位，替换从新实例开始。受压制层仍推进相位但不发布周期，恢复后不补发受压制期间的跳数。

受管理角色每跳先执行内部战斗提交和死亡清理，再发送 StatusTickEvent；处理回调队列后寻找下一事件。纯容器仍只发布周期记录。StatusDamageStage 区分 Base 与 SourceResolved：战斗参与者在施加准备时将 Base 结算并冻结为 SourceResolved，每跳只读取实时目标防御。

周期从施加时间与整数周期序号推导，末跳与到期用有限浮点容差对齐。每目标最多 4096 层，每笔事务最多 4096 个请求及 4096 单位层处理预算；超限整笔失败。游戏时钟和配置时长上限为 1,000,000,000 秒，非零间隔至少 0.000001 秒，以保持双精度时钟可推进；NaN、Infinity、负时间和溢出被拒绝。无限状态使用 Lifetime，不通过无限数值输入表达。

## 验证与后续

`StatusSystemTests` 长期保护层数、择强接替、周期、容量、只读快照、事务失败回退、来源隔离、重入及 QFramework Session 释放。配置文档覆盖和内容身份复用已有测试套件。

阶段 2 已接入[角色战斗模块](./status-combat.md)，新增集成与 PlayMode 生命周期回归。阶段 3 已接入[七异常、抗性及来源样例](./status-ailments.md)；自动时间、存档与图标按阶段 4 继续。消耗品在完整状态系统验收后实施。
