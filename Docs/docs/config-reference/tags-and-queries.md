# 标签与查询配置参考

本文件是标签定义与结构化标签查询的字段合同。正式示例位于 `Assets/Data/Preset/Tags`；创建入口为 `DarkFlare/Data/Tags/Tag Definition`。标签只表达分类事实，不消耗随机数，也不代替 `ItemType`、`ActorTeam`、技能类型或 `DamageType` 已能表达的事实。

## TagDefinition

用途：声明跨资产引用的稳定标签。`CombatTagResolver` 负责强类型事实派生，查询与验证器按 Domain 和 Usage 限制消费者。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 正式资产必填；全局唯一的小写 ASCII `snake_case`；发布后视为稳定 ID | 由标签资产拥有，运行时比较与跨资产引用使用；重命名必须迁移所有引用，不能只改显示名 |
| `_displayName` | `string` / 空 | 正式资产必填；中文短名称 | Inspector、调试与配置中心显示；不参与匹配 |
| `_domain` | `CombatTagDomain` / `ItemSpawn` | 必填；必须与生产者和引用位置一致 | `ContentConfigurationValidator` 校验；旧资产未显式保存时按 `ItemSpawn` 解释，迁移后必须明确复核 |
| `_usage` | `CombatTagUsage` / `Active` | `Active` 必须有正式查询消费者；预留但无消费者使用 `Reserved` | 配置中心拒绝预留标签被当作正式规则消费；改变状态不改变匹配算法 |
| `_description` | `string` / 空 | 正式资产必填；说明语义、生产者及允许消费者 | 仅供作者与审查；不进入运行时结果 |

当前 Active 合同：`weapon`、`armor`、`ring` 属于 ItemSpawn，`physical` 属于 Damage。`damage`、元素类型、`sword`、`axe`、`projectile`、`melee`、`monster` 当前为 Reserved。物品类别、怪物阵营、投射物类型和伤害类型均由运行时派生，不应重复手填。

## TagQueryDefinition

用途：嵌套于 `AffixDefinition._spawnQuery` 与 `StatModifierDefinition._condition`。查询本身不拥有标签，不消费随机数；调用方提供各 Scope 的标签快照。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_scopeMask` | `CombatTagScope` / `None` | 查询含任何条件时不得为 `None`；只勾选真实读取的数据域 | `TagQueryDefinition.Matches` 与配置验证器消费；空查询可保持 `None` |
| `_requiredAll` | `List<TagDefinition>` / 空 | 可空；非空时全部标签都必须命中；不得含空引用、重复项或跨 Domain 引用 | 由父配置拥有列表；顺序不影响结果；旧平面条件迁移后不得双写 |
| `_requiredAny` | `List<TagDefinition>` / 空 | 可空；非空时至少一项命中；SpawnQuery 正式合同要求至少一个 SourceItem 条件 | 词条候选过滤消费；同一标签不得同时出现在必要与阻止集合 |
| `_blockedAny` | `List<TagDefinition>` / 空 | 可空；任一项命中即拒绝；不得含空引用或重复项 | 生成与战斗条件共同消费；不用于表达反向的强类型字段 |

`AffixDefinition._spawnQuery` 固定以 `SourceItem` 查询 ItemSpawn；战斗修改器条件可组合 Actor、Skill、SourceItem、Attack 与 Damage，但每个标签 Domain 必须与 Scope 相容。`Modifier` Domain 仅用于词缀自身分类，不进入战斗查询。

### 校验、错误与迁移

- “查询有条件但 Scope=None”：选择真正拥有标签的 Scope，不得通过扩大 Scope 掩盖建模错误。
- “Domain 与引用位置不一致”：移动或移除引用；不要复制同义标签到另一个 Domain。
- 新字段或新嵌套查询类型必须先进入 `coverage-manifest.json`，并在对应 H2 下增加独立字段行。
- `alpha 0.1.1` 以前的平面标签列表仅为兼容读取；正式配置必须使用结构化查询并清空旧字段。

