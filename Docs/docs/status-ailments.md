# 异常、抗性与状态来源

2026-09-13：阶段 3 已完成；EditMode 全量 527/527、随后本地化专项 20/20、PlayMode 68/68 通过，见[验收记录](./assets/acceptance/alpha-0.4-status-ailments/README.md)。

本模块在[状态核心](./status-system.md)与[角色战斗](./status-combat.md)上提供七异常、对应抗性及来源接点。自动 Session 时间、状态保存恢复与图标仍属于后续阶段；当前通过 `AdvanceStatusesCommand` 或 `StatusSystem.Advance` 显式推进。正式技能默认不附加异常，可主动选择样例验证。

## 正式内容和示例参数

状态资产位于 `Assets/Data/Preset/Statuses/`，名称和说明位于 `statuses` 中英本地化表。以下参数用于首期验证，可通过配置调整。

| ID / 名称 | 效果 | 模型 / 上限 | 时长 |
| --- | --- | --- | --- |
| weakness / 虚弱 | 造成伤害 Less 20% | Strongest / 8 | 5 秒 |
| stun / 眩晕 | 禁止移动、攻击、施法；可使用物品 | Uniform + Shared / 1 | 2 秒 |
| bleeding / 流血 | 每层每秒 4 点物理伤害 | Independent / 8 | 5 秒 |
| burning / 燃烧 | 每秒 6 点火焰伤害 | Strongest / 8 | 5 秒 |
| chill / 冰缓 | 移动速度 Less 20% | Strongest / 8 | 5 秒 |
| shock / 感电 | TargetTaken More 20% | Strongest / 8 | 5 秒 |
| poison / 中毒 | 每层每秒 3 点混沌伤害 | Independent / 8 | 5 秒 |

除眩晕外均独立计时；满层使用核心默认策略。七异常均可驱散、可消费；两个资格独立配置。装备样例 `equipment_guard` 为 SourceOwned 光环，提供 10 点护甲，Strongest、最多 16 个候选。所有状态暂不配置正式图标，图标和浮窗在阶段 4 统一完成。

## 抗性

每种异常对应 `<id>_resistance` 属性。基础缺省为 0，经普通属性聚合，可通过对应防御后缀获得 20 点抗性；词缀已进入正式打造和掉落池。HUD 与属性详情包含这七项属性，以百分比显示。运行时有效范围为 0–100%，与元素 / 混沌类型抗性的 -100–75% 不同。

`AilmentResistanceResolver` 在共同准备路径计算 `k = 1 - clamp(R, 0, 100) / 100`。虚弱、冰缓和感电只缩放修改器幅度；周期异常先冻结来源伤害，再缩放每跳数值；眩晕只缩短施加时长。完全抵抗返回 `Resisted`，不会创建零值层、刷新已有层或消费同批标记。非有限值拒绝为非法请求。

`AilmentResistanceSnapshot` 保存原始 / 有效抗性、抵抗前效果与原时长，逐层效果保留抵抗后参数。后续换装不重新缩放已有层；新施加按当前抗性处理。Strongest 比较抵抗后的幅度或每秒伤害，目标类型防御的实时变化不重排候选。各跳仍经过当时的类型抗性和承伤修正，物理周期继续跳过护甲。

准备批次按开始时已提交的角色属性读取抗性，不因同批先加入增益而改变后续请求结果。目标属性 / 资源在发布前变化会使事务失效。抵抗属于失败，组合请求整笔回退；命中已提交的伤害不会因附加异常被抵抗而撤销。

## 来源接点

- `StatusApplication.Capture` 冻结规则、来源参数和伤害归属，转换为通用 StatusMutation；Skill、Equipment、Talent、Consumable 和 Mechanism 共用同一状态入口。
- `ProjectileSkillDefinition.OnHitStatus` 为显式可选配置。攻击快照在发出时冻结状态，独立子种子不改变命中与暴击随机流。命中存活目标后施加，未命中、致死及周期伤害不附加。来源离场后快照仍有效。`status_burning_example` 为正式投射物样例，没有改动玩家默认技能。
- `UseStatusSkillCommand` 检查施法许可与目标，可直接施加或按异常分类驱散；驱散无匹配返回 NoChange。该最小技能接点不引入新的资源收费规则。
- `ItemBaseDefinition.ProvidedStatus` 为可选装备持续来源。身份使用装备实例加状态 ID，不随槽位改变。重复协调幂等；穿脱、交换、槽位移动和恢复共同准备装备修改器与状态投影，失败恢复背包及槽位；资源上限一次重建。来源解绑只移除自身维持状态。
- `ConsumeStatusForEffectCommand` 将足额消费标记与获得同目标临时效果组合成一次批次，附带版本校验，返回确切消费记录。失败不消耗层，普通到期不执行消费逻辑。此接点不包含跨角色伤害事务。
- 天赋 / 消耗物品可直接使用 StatusSource 类型和上述请求合同。限时效果不依赖来源物品仍在背包；完整天赋树和物品使用事务尚未实现。

装备维持样例通过物品配置的“装备维持状态”引用 `equipment_guard` 启用；现有正式物品默认留空。测试通过独立配置实例验证真实装备入口，不改写共享资产。

## 内容与调试

core 内容版本升级为 4；v3 → v4 为追加内容迁移，不改写旧实例、词缀掷值或资源；缺失新抗性按 0 解析。保留 v1 → v2 → v3 的旧迁移链。Schema 仍为 2，当前没有状态 DTO，不能将本次内容迁移解释为状态已支持存档。

Unity 菜单 `DarkFlare/调试/状态系统` 打开 Odin 工具。进入 Session 后选择角色与定义，可施加、查看逐层抗性 / 数值 / 时间快照、消费、驱散或显式推进时间；显示操作结果和积压标志。目标以当前 Session 身份校验，退出后按钮禁用；工具不创建自动时间任务。实际伤害样例会扣除目标生命，请使用测试 Session。

长期验证扩展 StatusCombatIntegrationTests、StatusCombatPlayModeTests、SaveRestorePreparerTests 与现有内容测试。旧资源 / 词条测试移除固定历史数量断言，保留完整属性覆盖；历史标签迁移矩阵限定其历史词条集合，继续精确保护旧候选兼容性。新增状态本地化表纳入语言职责合同。
