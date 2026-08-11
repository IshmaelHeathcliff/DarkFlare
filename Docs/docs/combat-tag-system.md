# 战斗标签系统

## 模块职责

战斗标签只描述可查询语义，不直接执行伤害、词条生成或打造行为。运行时把物品生成、词缀自身、角色、技能、本次攻击与伤害分为独立域，避免不同对象上的同名标签被无条件合并。

Unity GameObject Tag 不属于本模块。

## 运行时结构

- `TagDefinition`：稳定标签 ID、中文名、Domain、使用状态和说明。
- `TagSet`：不可变、去重、使用稳定 ID 比较的运行时集合。
- `TagQueryDefinition`：可序列化查询，包含作用域、全部条件、任一条件和阻止条件。
- `TagQuery`：纯运行时匹配器，不读取 Unity 资产状态。
- `CombatTagContext`：分别保存来源角色、目标角色、技能、来源物品、本次攻击和当前伤害包标签。
- `DamagePacket`：分别保存最终伤害类型 `CurrentType`、缩放血统 `ScalingTypes` 和不可推导的 `CustomTags`。

攻击快照冻结来源侧上下文；命中时由 `CombatSystem` 加入目标角色；`DamageCalculator` 为每个伤害包单独派生伤害标签并执行查询。

## Domain 与查询作用域

| Domain | 配置对象 | 可查询位置 |
| --- | --- | --- |
| `ItemSpawn` | 物品基底特征 | `SourceItem`；用于词条候选池 |
| `Modifier` | 词缀自身分类 | 仅供未来打造和权重规则，不进入战斗上下文 |
| `Actor` | 角色特征 | `SourceActor`、`TargetActor` |
| `Skill` | 技能分类 | `Skill` |
| `Attack` | 本次攻击动态语义 | `Attack` |
| `Damage` | 伤害类型与血统 | `Damage` |

查询只读取 `ScopeMask` 声明的集合。`RequiredAll` 必须全部命中，非空 `RequiredAny` 至少命中一个，`BlockedAny` 任意命中都会阻止查询。

## 自动派生规则

- `weapon`、`armor`、`ring` 由 `ItemType` 和装备槽派生。
- `monster` 由 `ActorTeam.Monster` 派生。
- `projectile` 由 `ProjectileSkillDefinition` 类型派生到技能域。
- `damage` 与五种伤害类型由 `DamagePacket.CurrentType`、`ScalingTypes` 派生。
- 物理转火焰后的包保留 `physical` 缩放血统，同时获得最终 `fire` 语义；防御仍只按最终伤害类型结算。

资产不得再次手填可由强类型字段推导的标签。`sword`、`axe`、`melee` 等当前没有正式查询消费者的定义保留为预留，不放入生产资产引用。

## 兼容层

旧 `TagSet` 构造函数和只读属性暂时保留。旧 `ModifierInstance(requiredTags, blockedTags)` 只读取迁移前的平面攻击上下文与 `DamagePacket.CustomTags`，不会突然读取目标角色或自动派生的伤害血统。

新配置必须使用结构化查询。兼容层仅用于旧调用方过渡，不得作为新功能入口；预计在后续版本确认所有调用方迁移后移除。

## 调试

`TagQuery.TryMatch` 在失败时返回确定性说明，包含：

- 查询作用域；
- `RequiredAll`、`RequiredAny`、`BlockedAny` 的完整稳定 ID；
- 缺失或命中的具体条件；
- 查询实际读取到的各作用域标签。

`TagSet.ToDebugString`、`CombatTagContext.ToDebugString` 和 `TagQuery.ToDebugString` 使用稳定排序，便于测试和日志比较。伤害热路径不会自动输出日志，调用方只在调试或校验失败时请求说明。

## 验证与测试

配置中心的内容校验检查：

- 标签 ID、Domain、使用状态和已知目录完整性；
- 标签引用位置与 Domain 是否一致；
- 查询有条件但作用域为空、跨域引用、空引用和互相矛盾的条件；
- 可推导标签是否仍被手填；
- 词条生成查询与战斗条件是否仍使用旧字段。

EditMode 回归覆盖当前物品—词条候选矩阵、作用域隔离、旧接口等价、转换与额外伤害血统、伤害数值基线以及攻击快照不可变性。

## 相关文档

- [标签配置参考](./config-reference/combat-tags.md)
- [伤害系统与词条系统](./damage-affix-system.md)
- [归档：战斗标签系统改进计划](./plan/archive/combat-tag-system-improvement-plan.md)
