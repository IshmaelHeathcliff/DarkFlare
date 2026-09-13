# 属性与词缀配置参考

本文件封板属性元数据、物品词缀与嵌套修改器的序列化合同。正式示例分别位于 `Assets/Data/Preset/Stats` 与 `Assets/Data/Preset/Affixes`。创建入口为 `DarkFlare/Data/Stats/Stat Definition` 和 `DarkFlare/Data/Affixes/Affix Definition`。

## StatDefinition

用途：为稳定属性 ID 提供显示、分类与作者范围。运行时数值由 `StatBlock` 与 `CombatStatResolver` 持有；配置中的范围不会自动裁剪运行时结果。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 正式资产必填；小写 `snake_case`；必须与 `StatIds.All` 一一对应，发布后稳定 | `StatBlock`、修改器、UI 与存档共同消费；重命名必须全链路迁移 |
| `_displayName` | `string` / 空 | 正式资产必填；中文名称 | 属性面板、物品说明和 Inspector 消费；不参与数值计算 |
| `_localizedName` | `LocalizedContentReference` / 空引用 | 正式资产必填；固定使用 `stats/<id>` | 属性面板和物品详情按当前语言解析；稳定属性 ID 仍是数值与存档真值 |
| `_category` | `StatCategory` / `Survival` | 必填枚举；仅决定作者与 UI 分组 | 配置中心和属性展示消费；改变分类不改变公式 |
| `_defaultValue` | `float` / `0` | 必须位于作者声明范围内 | 作为元数据和未显式初始化场景的约定；角色与怪物仍应显式写入基础值 |
| `_minValue` | `float` / `0` | 不得大于 `_maxValue` | Inspector/验证器消费；不会自动 Clamp `StatBlock` |
| `_maxValue` | `float` / `999999` | 不得小于 `_minValue` | Inspector/验证器消费；公式硬上限应在对应结算系统声明 |
| `_isPercent` | `bool` / `false` | 显示语义必须与稳定 ID 一致 | UI 格式化消费；不把数值自动除以 100 |
| `_description` | `string` / 空 | 建议填写完整结算语义 | 配置说明与调试消费；不进入公式 |

## AffixDefinition

用途：声明可生成在物品实例上的前缀或后缀。`AffixGenerationUtility` 先按等级、SpawnQuery、组与容量筛选，再按权重随机；调用方提供确定性种子。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 正式资产必填；唯一小写 `snake_case`；发布后稳定 | 词缀实例、随机子种子与 UI 消费；重命名会改变确定性派生结果，必须迁移 |
| `_displayName` | `string` / 空 | 正式资产必填；可直接展示的中文短名称 | 物品信息浮窗与调试消费 |
| `_localizedName` | `LocalizedContentReference` / 空引用 | 正式资产必填；固定使用 `affixes/item_affix.<id>.name` | 物品详情在最终显示边界解析；快照只携带表名和键 |
| `_affixType` | `AffixType` / `Prefix` | 必填；只能为 Prefix 或 Suffix | 容量、精准打造范围与 UI 分组消费；改变类型属于实例数据迁移 |
| `_groupId` | `string` / 空 | 正式资产必填；小写 `snake_case` | 同一物品同组最多一项；随机生成与打造候选过滤消费 |
| `_minItemLevel` | `int` / `1` | 至少 `1` | 候选生成按物品等级过滤；不会自动提高传入等级 |
| `_weight` | `int` / `100` | 必须大于 `0` | 合法候选内加权随机；相对值而非百分比 |
| `_spawnQuery` | `TagQueryDefinition` / 空条件实例 | 正式资产必填且含条件；固定 `SourceItem`，至少一个 `RequiredAny` ItemSpawn 标签 | `AffixDefinition.CanApplyTo` 与生成器消费；取代旧允许/阻止标签列表 |
| `_modifierTags` | `List<TagDefinition>` / 空 | 可空；仅允许 Modifier Domain；当前正式资产保持为空 | 预留给词缀分类；当前没有运行时查询消费者，不得假定其会改变效果 |
| `_allowedItemTags` | `List<TagDefinition>` / 空 | 迁移兼容字段；正式资产必须为空 | 仅旧数据兼容；不得与 `_spawnQuery` 双写，后续删除前由验证器阻止新内容使用 |
| `_blockedItemTags` | `List<TagDefinition>` / 空 | 迁移兼容字段；正式资产必须为空 | 仅旧数据兼容；迁移时等价转入结构化查询 |
| `_modifiers` | `List<StatModifierDefinition>` / 空 | 正式资产至少一项；不得有空项或不可消费组合 | 实例生成逐项掷值；装备结算和物品说明消费 |

## StatModifierDefinition

用途：嵌套描述一次属性修改或伤害类型变换。`CreateInstance` 使用调用方的 `System.Random` 在 `_valueRange` 内取值；配置对象不持有随机状态。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_stat` | `StatDefinition` / `null` | Flat、Increase、More、Override 等属性操作必填；伤害转换类可按操作合同为空 | `CombatStatResolver`、本地武器伤害和 UI 消费；引用资产由父词缀持有 |
| `_operation` | `ModifierOperation` / `Flat` | 必填；只允许当前运行时已实现的组合 | 属性结算、伤害转换与验证器消费；未实现枚举值属于配置错误 |
| `_scope` | `ModifierScope` / `GlobalActor` | 必填；必须与属性及父配置领域匹配 | `LocalItem` 只影响来源物品，`GlobalActor` 影响角色，`Skill` 影响技能快照 |
| `_valueRange` | `Vector2` / `(0, 0)` | 两端非负且下限不大于上限 | 词缀实例创建随机取值；相同根种子和稳定顺序必须可复现 |
| `_fromDamageType` | `DamageType` / `Physical` | Conversion/GainAsExtra 必填；与目标类型不得相同 | 伤害血统与类型变换消费；普通属性操作忽略 |
| `_toDamageType` | `DamageType` / `Fire` | Conversion/GainAsExtra 必填；目标需为允许类型 | 伤害包构建消费；普通属性操作忽略 |
| `_condition` | `TagQueryDefinition` / 空条件实例 | 可空条件；有条件时 Scope 不得为 None 且 Domain 必须匹配 | 攻击/伤害结算匹配；取代旧必要与阻止标签列表 |
| `_requiredTags` | `List<TagDefinition>` / 空 | 迁移兼容字段；正式资产必须为空 | 仅保留旧平面条件读取，不得与 `_condition` 双写 |
| `_blockedTags` | `List<TagDefinition>` / 空 | 迁移兼容字段；正式资产必须为空 | 仅保留旧平面条件读取；新内容禁止使用 |

当前物品修改器可消费合同：伤害属性的 Flat/Increase/More 可用于 LocalItem、GlobalActor 或 Skill；非伤害属性只接受 GlobalActor 的 Flat/Increase/More/Override。怪物词条另受更严格的 [角色与怪物配置](./actors-and-monsters.md#monsteraffixdefinition) 约束。

### 校验、随机与迁移

- 配置中心检查 StatIds 登记的全部正式属性、完整词缀 ID、稳定组、权重、查询、合法消费者和每个正式属性的词缀覆盖。
- 词缀候选选择、数值生成和打造分别由调用方提供根种子；共享 ScriptableObject 从不在运行时改写。
- “没有任何兼容装备”通常表示 SpawnQuery Domain/Scope 错误或候选物品缺少派生类别。
- “修改器没有当前运行时消费者”必须修改建模或实现消费者，不能仅关闭验证。
- 兼容字段正式资产必须为空；移除兼容层前先通过内容扫描确认零使用。

## 异常抗性属性

状态阶段 3 新增 weakness_resistance、stun_resistance、bleeding_resistance、burning_resistance、chill_resistance、shock_resistance、poison_resistance。基础缺省 0，有效值在施加时裁剪到 0–100%；属性异常缩幅、伤害异常缩伤、眩晕缩时。对应后缀配置为固定 20 点抗性，已加入打造与掉落池。参数和来源快照详见[异常与来源](../status-ailments.md)。
