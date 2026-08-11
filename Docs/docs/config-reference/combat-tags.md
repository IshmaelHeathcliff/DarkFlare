# 标签配置参考

## 创建入口与示例

创建菜单：

```text
DarkFlare/Data/Tags/Tag Definition
```

正式资产位于 `Assets/Data/Preset/Tags`。`physical` 是当前 Damage 域正式查询示例，`weapon`、`armor`、`ring` 是 ItemSpawn 域正式查询示例。

## `TagDefinition`

| 字段 | 类型 / 默认值 | 规则 |
| --- | --- | --- |
| `_id` | `string` / 空 | 必填；小写 ASCII `snake_case`；全局唯一；运行时比较和存档使用稳定值 |
| `_displayName` | `string` / 空 | 必填；面向内容作者和调试界面的中文名 |
| `_domain` | `CombatTagDomain` / `ItemSpawn` | 必填；必须与所有引用位置一致 |
| `_usage` | `CombatTagUsage` / `Active` | `Active` 必须有正式消费者；无消费者但保留的定义使用 `Reserved` |
| `_description` | 多行 `string` / 空 | 说明语义、生产者和允许的消费者；正式标签建议填写 |

当前目录合同：

| 标签 | Domain | 使用状态 | 生产或消费规则 |
| --- | --- | --- | --- |
| `weapon`、`armor`、`ring` | `ItemSpawn` | `Active` | 由物品类型 / 装备槽派生，供词条生成查询消费 |
| `sword`、`axe` | `ItemSpawn` | `Reserved` | 当前无强类型子类字段和正式消费者 |
| `physical` | `Damage` | `Active` | 由伤害类型 / 缩放血统派生，当前伤害词条条件消费 |
| `damage`、`fire`、`cold`、`lightning`、`chaos` | `Damage` | `Reserved` | 运行时可派生；当前尚无对应配置查询消费者 |
| `projectile`、`melee` | `Skill` | `Reserved` | `projectile` 由投射物技能类型派生；当前无正式查询消费者 |
| `monster` | `Actor` | `Reserved` | 由 `ActorTeam.Monster` 派生；当前无正式查询消费者 |

## `TagQueryDefinition`

这是 `AffixDefinition._spawnQuery` 和 `StatModifierDefinition._condition` 使用的嵌套结构。

| 字段 | 类型 / 默认值 | 规则 |
| --- | --- | --- |
| `_scopeMask` | `CombatTagScope` / `None` | 查询有任何条件时必填且不得为 `None` |
| `_requiredAll` | `List<TagDefinition>` / 空 | 每个标签都必须在声明作用域内命中 |
| `_requiredAny` | `List<TagDefinition>` / 空 | 非空时至少一个标签命中 |
| `_blockedAny` | `List<TagDefinition>` / 空 | 任意标签命中即拒绝 |

同一标签不得同时出现在必要条件和阻止条件中。引用不得为空，不得跨 Domain 使用：

- 词条 `_spawnQuery` 固定查询 `SourceItem`，只能引用 `ItemSpawn`。
- 战斗修改器 `_condition` 可查询角色、技能、来源物品、本次攻击或伤害，但每个引用的 Domain 必须与 Scope 相容。
- `Modifier` Domain 只允许出现在 `AffixDefinition._modifierTags`，不进入战斗查询。

## 关联配置字段

### `AffixDefinition`

- `_spawnQuery`：决定词条能否进入物品候选池。当前正式资产使用 `SourceItem + RequiredAny` 保持原候选矩阵。
- `_modifierTags`：词缀自身分类，供未来定向打造和权重规则；当前没有消费者，保持为空。
- 旧 `_allowedItemTags`、`_blockedItemTags` 只用于迁移兼容，正式资产迁移后必须为空。

### `StatModifierDefinition`

- `_condition`：修改器的战斗标签条件。当前四个伤害修改器使用 `Damage + RequiredAll(physical)`。
- 旧 `_requiredTags`、`_blockedTags` 只用于迁移兼容，正式资产迁移后必须为空。

### 物品、技能、怪物与伤害

- `ItemBaseDefinition._tags` 只保存无法从类型 / 槽位推导的 ItemSpawn 标签；当前正式物品为空。
- `ProjectileSkillDefinition._tags` 只保存无法从技能类型推导的 Skill 标签；当前正式技能为空。
- `MonsterDefinition._tags` 只保存无法从阵营推导的 Actor 标签；当前正式怪物为空。
- `DamageRollDefinition._tags` 只保存无法从伤害类型推导的自定义 Damage 标签；当前正式伤害为空。

## 运行时读取者

- `CombatTagResolver` 集中执行强类型派生。
- `AffixDefinition.CanApplyTo` 使用物品生成查询。
- `AttackSnapshotFactory` 构造结构化来源上下文。
- `CombatSystem` 在命中时补入目标角色。
- `DamageCalculator` 按伤害包生成 Damage 作用域并匹配修改器。

标签本身不消耗随机数。词条数值仍由 `StatModifierDefinition.CreateInstance` 使用调用方提供的 `System.Random` 生成。

## 校验与常见错误

- “Domain 与引用位置不一致”：移动到正确 Domain 或移除错误引用，禁止通过扩大 Scope 绕过。
- “查询有条件但 Scope=None”：为查询选择唯一必要作用域或明确的组合。
- “重复事实”：移除物品类别、阵营、技能类型或伤害类型的手填标签，让解析器派生。
- “Reserved 标签被正式配置消费”：确认已经有真实运行时消费者后再改为 `Active`。
- “新旧字段混用”：运行一次迁移工具并检查旧列表为空；不要只迁移部分条件。

## 迁移兼容

`alpha 0.1.1` 迁移保持所有资产 GUID 和当前物品—词条候选集合不变。迁移执行时只核对已知标签、物品、词条、技能和怪物配置目录，没有扫描字体、贴图或其他无关大文件；二次预检为零变更后，一次性 Editor 菜单已移除。

旧运行时构造函数暂时保留严格的旧平面匹配语义。新资产必须使用结构化查询，不能依赖兼容层获得自动派生标签。
