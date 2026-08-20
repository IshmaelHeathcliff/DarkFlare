# 角色与怪物配置参考

本文件封板角色基础值、怪物原型、怪物词条与生成池。正式示例位于 `Assets/Data/Preset/Actors`、`Monsters` 和 `MonsterAffixes`。所有实例随机结果由调用方根种子派生，共享配置资产不得在运行时改写。

## CharacterDefinition

创建入口：`DarkFlare/Data/Actors/Character Definition`。用途：提供玩家或可复用角色的基础属性和 Addressable Prefab。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 正式资产必填；唯一小写 `snake_case`，发布后稳定 | `CombatActor.ActorId`、调试与潜在存档消费；改名需迁移引用 |
| `_displayName` | `string` / 空 | 正式资产必填 | Inspector/UI 显示，不参与计算 |
| `_localizedName` | `LocalizedContentReference` / 空引用 | 正式资产必填；固定使用 `monsters/actor.<id>.name` | 玩家可见角色名按当前语言解析；旧显示名仅供作者与日志识别 |
| `_prefab` | `AssetReferenceGameObject` / `null` | 可实例化的正式角色必填且 GUID 有效 | `SpawnSystem` 异步加载并释放；禁止改用 Resources |
| `_maxHealth` | `float` / `100` | 至少 `1` | `CreateStats` 写入最大生命，`CombatActor` 初始化资源 |
| `_mana` | `float` / `0` | 不得小于 `0` | 写入最大法力；旧资产默认零，正式资产应显式保存 |
| `_healthRegeneration` | `float` / `0` | 不得小于 `0`；单位为每秒固定值 | `ResourceRegenerationSystem` 消费 |
| `_manaRegeneration` | `float` / `0` | 不得小于 `0`；单位为额外每秒固定值 | `ResourceRegenerationSystem` 在 `5%` 最大法力/秒的基础恢复上叠加 |
| `_moveSpeed` | `float` / `5` | 不得小于 `0` | 玩家/角色移动控制器消费 |
| `_armor` | `float` / `0` | 不得小于 `0` | 物理伤害减免公式消费 |
| `_accuracy` | `float` / `100` | 必须大于 `0` | 命中率计算消费 |
| `_evasion` | `float` / `20` | 不得小于 `0` | 闪避计算消费 |
| `_criticalChance` | `float` / `5` | `0–100`，单位百分比 | 暴击掷骰消费 |
| `_criticalDamage` | `float` / `50` | 不得小于 `0`；表示额外百分比 | 暴击倍率消费；`50` 表示总倍率 `150%` |
| `_fireResistance` | `float` / `0` | 可为负；结算时裁剪到 `-100–75` | 只降低 Fire 伤害 |
| `_coldResistance` | `float` / `0` | 可为负；结算时裁剪到 `-100–75` | 只降低 Cold 伤害 |
| `_lightningResistance` | `float` / `0` | 可为负；结算时裁剪到 `-100–75` | 只降低 Lightning 伤害 |
| `_chaosResistance` | `float` / `0` | 可为负；结算时裁剪到 `-100–75` | 只降低 Chaos 伤害 |

## MonsterDefinition

创建入口：`DarkFlare/Data/Monsters/Monster Definition`。用途：声明怪物原型、接触攻击、随机生命和随机词条。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 正式资产必填；唯一小写 `snake_case`，发布后稳定 | 生成、随机子种子、调试与实例标识消费 |
| `_displayName` | `string` / 空 | 正式资产必填 | 世界 UI、调试与配置中心消费 |
| `_localizedName` | `LocalizedContentReference` / 空引用 | 正式资产必填；固定使用 `monsters/monster.<id>.name` | 世界 UI 在显示边界解析；实例与随机仍使用稳定 `_id` |
| `_character` | `CharacterDefinition` / `null` | 可空；非空时下面的基础角色数值均由引用资产提供 | `CreateStats` 优先消费引用；切换所有权必须迁移旧内联值，不能混合生效 |
| `_prefab` | `AssetReferenceGameObject` / `null` | 正式怪物必填且 GUID 有效、彼此独立 | `SpawnSystem` 加载；Prefab 必须满足角色层级与碰撞合同 |
| `_maxHealth` | `float` / `24` | 无 `_character` 时至少 `1` | 内联基础生命；有引用时隐藏且不生效 |
| `_mana` | `float` / `0` | 无 `_character` 时不得小于 `0` | 内联最大法力；旧资产缺字段按零解释 |
| `_healthRegeneration` | `float` / `0` | 无 `_character` 时不得小于 `0` | 内联每秒生命恢复 |
| `_manaRegeneration` | `float` / `0` | 无 `_character` 时不得小于 `0` | 内联额外每秒固定法力恢复；基础恢复为最大法力的 `5%/秒` |
| `_moveSpeed` | `float` / `2.6` | 无 `_character` 时不得小于 `0` | `MonsterController` 追逐与软分离最终速度上限 |
| `_accuracy` | `float` / `90` | 无 `_character` 时必须大于 `0` | 接触攻击命中计算 |
| `_evasion` | `float` / `15` | 无 `_character` 时不得小于 `0` | 受击闪避计算 |
| `_criticalChance` | `float` / `5` | 无 `_character` 时 `0–100` | 接触攻击暴击掷骰 |
| `_criticalDamage` | `float` / `50` | 无 `_character` 时不得小于 `0` | 暴击倍率计算 |
| `_armor` | `float` / `0` | 无 `_character` 时不得小于 `0` | 物理减伤计算 |
| `_fireResistance` | `float` / `0` | 可为负；结算裁剪到 `-100–75` | Fire 抗性 |
| `_coldResistance` | `float` / `0` | 可为负；结算裁剪到 `-100–75` | Cold 抗性 |
| `_lightningResistance` | `float` / `0` | 可为负；结算裁剪到 `-100–75` | Lightning 抗性 |
| `_chaosResistance` | `float` / `0` | 可为负；结算裁剪到 `-100–75` | Chaos 抗性 |
| `_healthMultiplierRange` | `Vector2` / `(1, 1)` | 两端必须大于 `0` 且下限不大于上限 | `CreateInstanceData(seed)` 为每个实例确定性掷出生命倍率 |
| `_contactDamageInterval` | `float` / `0.75` | 至少 `0.05` 秒 | 每只 `MonsterController` 独立控制攻击尝试频率；没有玩家无敌帧或全局冷却 |
| `_contactDamageRadius` | `float` / `0.75` | 至少 `0.05` | 按中心距离判定接触攻击，不依赖碰撞回调 |
| `_contactStopDistance` | `float` / `0.6` | `0..ContactDamageRadius` | 进入距离后关闭追逐分量，避免持续挤压玩家 |
| `_separationRadius` | `float` / `0.8` | 必须大于 `0` | 只采样存活同阵营怪物，生成软分离方向 |
| `_separationWeight` | `float` / `0.65` | `0–2` | 与追逐方向组合；最终仍受移动速度限制 |
| `_tags` | `List<TagDefinition>` / 空 | 只允许 Actor Domain 的非派生自定义标签；正式怪物当前为空 | `CombatTagResolver` 会自动派生 `monster`，不得重复填写 |
| `_contactDamages` | `List<DamageRollDefinition>` / 空 | 正式怪物必须非空且每项合法 | 接触攻击按攻击种子掷伤害；空列表没有代码级固定伤害回退 |
| `_lootTable` | `LootTableDefinition` / `null` | 正式怪物必填 | 死亡掉落消费，掉落随机流由调用方提供 |
| `_affixPool` | `List<MonsterAffixDefinition>` / 空 | 正式怪物必须有足够合法候选；无空引用、重复引用 | 怪物实例生成按权重无放回选择 |
| `_minimumAffixCount` | `int` / `0` | 非负且不大于最大值 | 实例生成先在闭区间内掷数量；移除词条不涉及此字段 |
| `_maximumAffixCount` | `int` / `0` | 不小于最小值，且不超过可用互斥组数 | 正式初版上限为 `2`；非法池不会静默 Clamp |

## MonsterAffixDefinition

创建入口：`DarkFlare/Data/Monsters/Monster Affix Definition`。正式资产位于 `Assets/Data/Preset/MonsterAffixes`。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 必填；唯一小写 `snake_case`，发布后稳定 | 词条实例、随机子种子、世界标签与调试消费 |
| `_displayName` | `string` / 空 | 必填；简短且可在世界标签显示 | `MonsterAffixVisual` 与调试消费 |
| `_localizedName` | `LocalizedContentReference` / 空引用 | 正式资产必填；固定使用 `affixes/monster_affix.<id>.name` | `MonsterAffixVisual` 监听语言切换并重新解析 |
| `_groupId` | `string` / 空 | 必填；小写 `snake_case` | 单个怪物实例同组最多一项，生成器互斥过滤消费 |
| `_weight` | `int` / `100` | 必须大于 `0` | 合法候选内加权随机；为相对权重 |
| `_displayColor` | `Color` / 白色 | Alpha 必须大于 `0`，保证可辨认 | 世界词条标签消费；不影响战斗数值 |
| `_modifiers` | `List<StatModifierDefinition>` / 空 | 至少一项；只允许怪物管线可消费组合 | `MonsterAffixGenerator` 掷值，`EffectiveStats` 与元素附伤消费 |

怪物词条只允许 `GlobalActor`。防御、生命和移动使用 Flat/Increase/More；元素附伤使用 `GainAsExtra`，来源固定 Physical，目标限 Fire/Cold/Lightning。禁止物品 SpawnQuery、前后缀容量、LocalItem 与未实现操作。

## MonsterSpawnDefinition

创建入口：`DarkFlare/Data/Monsters/Monster Spawn Definition`。用途：定义场景生成节奏与加权怪物池。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 必填；小写 `snake_case`；正式值为 `main` | 完整内容 ID 为 `spawn:main`；存档与迁移只保存该稳定 ID，不保存资产路径或显示名 |
| `_spawnInterval` | `float` / `1.5` | 至少 `0.1` 秒 | `MonsterSpawner` 的生成节奏；不是每个怪物的攻击间隔 |
| `_maxAliveCount` | `int` / `12` | 至少 `1` | 生成器统计当前存活数并限流 |
| `_spawnRadius` | `float` / `8` | 至少 `0.1` 世界单位 | 生成位置计算消费；仍须落入 WorldBounds |
| `_rules` | `List<MonsterSpawnRule>` / 空 | 正式配置必须至少有一个正权重有效引用；不得重复无意义规则 | `PickMonster(random)` 加权选择；调用方提供随机流 |

## MonsterSpawnRule

用途：嵌套于怪物生成配置；不是独立资产。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_monster` | `MonsterDefinition` / `null` | 正权重规则必填 | 由父生成资产持有；`SpawnSystem` 实例化所选怪物 |
| `_weight` | `int` / `100` | 不得小于 `0`；只有大于 `0` 才参与 | `PickMonster` 使用整数相对权重；顺序改变可改变同一随机流对应结果 |

### 校验、随机与迁移

- 配置中心检查正式 ID、Prefab、属性范围、接触伤害、掉落表、怪物词条池、生成规则和碰撞/世界边界合同。
- 怪物实例根种子派生生命、词条数量、选择和逐词条数值；完全重叠时的稳定分离方向不消费帧随机数。
- `_character` 从空改为引用或反向迁移时，必须确认唯一基础属性所有者；隐藏字段不是并行叠加来源。
- 新增怪物数值字段时同步更新 `CreateStats`、实例快照、验证器、本文独立字段行与覆盖测试。
