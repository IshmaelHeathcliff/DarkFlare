# 角色、物品与攻击配置参考

> 本文保留为 `alpha 0.1.2–0.1.6` 阶段迁移摘要。`alpha 0.1.7` 起，逐字段封板合同统一从[配置参考索引](./README.md)进入，并由 `coverage-manifest.json` 自动校验；新增或修改字段不要继续维护本文的合并字段表。

## 覆盖类型

本文覆盖 `alpha 0.1.2–0.1.6` 直接改变的八类配置：

- `StatDefinition`
- `CharacterDefinition`
- `MonsterDefinition`
- `MonsterAffixDefinition`
- `ItemBaseDefinition`
- `ProjectileSkillDefinition`
- `LootTableDefinition`
- `CraftingDefinition`

正式示例分别位于 `Assets/Data/Preset/Stats`、`Actors`、`Monsters`、`MonsterAffixes`、`Items` 和 `Skills`。嵌套伤害范围统一使用 `DamageRollDefinition`，物品和怪物词条共用 `StatModifierDefinition` 的计算语义，但不共用生成领域模型。

## 通用战斗属性

玩家与怪物最终都把基础值写入 `StatBlock`，运行时公式不直接读取配置资产。

| 字段 | 类型 | 合同 |
| --- | --- | --- |
| `_accuracy` | `float` | 必须大于 0；参与命中率 |
| `_evasion` | `float` | 不得小于 0；区分闪避失败 |
| `_criticalChance` | `float` | `0–100`，单位为百分比 |
| `_criticalDamage` | `float` | 不得小于 0；表示额外百分比，`50` 即总倍率 `150%` |
| `_armor` | `float` | 不得小于 0；只降低物理命中伤害 |
| 四类 `_...Resistance` | `float` | 结算时裁剪到 `-100–75`，只作用于对应类型 |
| `_mana` | `float` | 不得小于 0；写入稳定 ID `mana`，表示最大法力 |
| `_healthRegeneration` | `float` | 不得小于 0；每秒固定生命恢复 |
| `_manaRegeneration` | `float` | 不得小于 0；基础 `5%` 最大法力/秒之外的固定加成 |

正式基础值：

| 角色 | 命中 | 闪避 | 暴击率 | 暴击伤害 | 护甲 | 四抗 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 玩家 | 100 | 20 | 5 | 50 | 0 | 0 |
| 荒原游魂 | 90 | 15 | 5 | 50 | 0 | 0 |
| 裂爪猎犬 | 110 | 30 | 8 | 50 | 0 | 0 |
| 铁壳尸傀 | 80 | 5 | 3 | 50 | 40 | 0 |

资源基础值用于验证链路，不代表最终平衡：玩家为 `200` 最大法力、每秒 `1` 生命恢复、额外固定法力恢复 `0`，因此实际基础法力恢复为 `10/秒`；三种正式怪物三项均显式保存为 `0`。

## StatDefinition

创建菜单：`DarkFlare/Data/Stats/Stat Definition`。

| 字段 | 类型 | 合同 |
| --- | --- | --- |
| `_id` | `string` | 必填、小写 `snake_case`，且必须存在于 `StatIds.All` |
| `_displayName` / `_description` | `string` | 中文显示元数据；正式属性显示名必填 |
| `_category` | `StatCategory` | 只用于配置与显示分组 |
| `_defaultValue` / `_minValue` / `_maxValue` | `float` | 范围有序；不会自动注入或裁剪运行时 `StatBlock` |
| `_isPercent` | `bool` | 只决定配置说明和 UI 格式，不改变修改器算法 |

`Assets/Data/Preset/Stats` 必须与 `StatIds.All` 一一对应。当前共 23 项；`mana` 的显示名为“最大法力”，新增 `health_regeneration` 与 `mana_regeneration`。稳定 ID、显示语义和完整消费者见[属性定义与调用关系](../stat-system.md)。

## CharacterDefinition

创建菜单：`DarkFlare/Data/Actors/Character Definition`。

| 字段 | 类型 | 必填 / 范围 | 运行时读取者 |
| --- | --- | --- | --- |
| `_id` | `string` | 必填，稳定 `snake_case` | `CombatActor.ActorId` |
| `_displayName` | `string` | 正式内容必填 | Inspector / UI |
| `_prefab` | `AssetReferenceGameObject` | 正式玩家必填 | `SpawnSystem` |
| `_maxHealth` | `float` | 至少 1 | `CreateStats` / `CombatActor` |
| `_mana` | `float` | 不得小于 0 | `CreateStats` / `CombatActor.MaxMana` |
| `_healthRegeneration` / `_manaRegeneration` | `float` | 不得小于 0；前者为每秒固定生命恢复，后者为额外每秒固定法力恢复 | `ResourceRegenerationSystem`；法力另有最大法力 `5%/秒` 的基础恢复 |
| `_moveSpeed` | `float` | 不得小于 0 | `PlayerController` |
| 战斗属性字段 | `float` | 见“通用战斗属性” | `CreateStats` |

字段新增前的旧资产不能依赖 CLR 默认值；正式资产必须由迁移或 Inspector 显式保存。`ContentConfigurationValidator` 会扫描 `Assets/Data/Preset/Actors`。

## MonsterDefinition

创建菜单：`DarkFlare/Data/Monsters/Monster Definition`。

| 字段 | 类型 | 合同 |
| --- | --- | --- |
| `_id` / `_displayName` | `string` | 稳定 ID 与正式中文名必填 |
| `_character` | `CharacterDefinition` | 可选；非空时角色基础值由它提供 |
| `_prefab` | `AssetReferenceGameObject` | 正式怪物必填且 GUID 独立 |
| `_maxHealth` / `_moveSpeed` | `float` | 未引用角色定义时生效 |
| `_mana` / 两类 `_...Regeneration` | `float` | 未引用角色定义时生效；均不得小于 0 |
| 战斗属性字段 | `float` | 未引用角色定义时生效；见通用合同 |
| `_healthMultiplierRange` | `Vector2` | 两端大于 0，且下限不大于上限 |
| `_contactDamageInterval` / `_contactDamageRadius` | `float` | 必须为正数；分别控制每只怪物独立的攻击尝试间隔与中心距离 |
| `_contactStopDistance` | `float` | `0..ContactDamageRadius`；进入该距离后关闭追逐分量 |
| `_separationRadius` | `float` | 必须大于 0；只读取存活的同阵营怪物 |
| `_separationWeight` | `float` | `0..2`；与追逐方向组合后仍受移动速度上限约束 |
| `_tags` | `List<TagDefinition>` | 只保存不能由 Monster 阵营推导的 Actor 标签 |
| `_contactDamages` | `List<DamageRollDefinition>` | 必须非空；没有代码级固定伤害回退 |
| `_lootTable` | `LootTableDefinition` | 正式怪物必填 |
| `_affixPool` | `List<MonsterAffixDefinition>` | 正式三种怪物都引用完整 10 词条池；不得为空引用或重复引用 |
| `_minimumAffixCount` / `_maximumAffixCount` | `int` | 非负且有序；正式配置固定为 `0 / 2`，上限不得超过可用互斥组数量 |

`CreateInstanceData(seed)` 从实例根种子派生生命、词条数量、选择和逐词条数值子种子，生成 `BaseStats`、不可变词条 / 修改器和 `EffectiveStats`；共享资产不在运行时改写。根种子同时为完全重叠时的软分离提供稳定方向，不消费帧随机数。正式三种怪物的停止距离、软分离半径和权重统一为 `0.6 / 0.8 / 0.65`。

接触攻击不依赖物理碰撞回调，继续由每只 `MonsterController` 按中心距离独立计时。荒原游魂、裂爪猎犬、铁壳尸傀的尝试间隔分别为 `0.75 / 0.55 / 1.0` 秒；未命中或闪避也只消费当前怪物自己的冷却。本阶段没有玩家受击无敌帧、全局伤害冷却或同帧伤害合并。

## MonsterAffixDefinition

创建菜单：`DarkFlare/Data/Monsters/Monster Affix Definition`。正式资产位于 `Assets/Data/Preset/MonsterAffixes`。

| 字段 | 类型 | 合同 |
| --- | --- | --- |
| `_id` / `_displayName` | `string` | 稳定小写 `snake_case` ID 与世界显示短名称，均必填 |
| `_groupId` | `string` | 必填；同组在单个实例中最多生成一次 |
| `_weight` | `int` | 必须大于 0；用于池内加权选择 |
| `_displayColor` | `Color` | Alpha 必须大于 0；用于世界词条标签 |
| `_modifiers` | `List<StatModifierDefinition>` | 至少一项；只允许当前管线可消费的怪物修改器 |

怪物词条修改器只允许 `ModifierScope.GlobalActor`。防御、生命和移动只使用 Flat / Increase / More；元素攻击只使用 `GainAsExtra`，来源固定为 Physical，目标限 Fire / Cold / Lightning。禁止 LocalItem、物品 SpawnQuery、前后缀容量、Override、Conversion、Chance、Trigger 和 Limit。

初版 10 项正式内容为三种元素附伤、三种元素抗性、最大生命、护甲、闪避和移动速度。`MonsterAffixGenerator` 按权重无放回选择，并在选择一项后移除同组所有候选；每项数值由根种子和稳定词条 ID 单独派生。

## ItemBaseDefinition

创建菜单：`DarkFlare/Data/Items/Item Base Definition`。

除既有 ID、名称、图标、槽位、价格、格子与隐式修改器外，基础伤害遵循严格所有权：

- 显式词缀容量不再是基底字段，统一由 `ItemRarityRules` 根据实例稀有度决定。

- `ItemType.Weapon` 的 `_baseDamages` 必须非空。
- Armor、Accessory、Material、Currency 的 `_baseDamages` 必须为空。
- 武器必须且只能允许 Weapon 槽；其他类别继续遵循各自槽位合同。
- Armor 的非空槽位掩码只能来自身体、头部、手部、腿部、副手；Accessory 只能来自双戒指、项链、腰带。未知位或跨类别掩码拒绝。旧 0–3 槽身份保留，新槽追加 4–9；副手不产生武器基础伤害。
- `LocalItem` 修改器只能配置于武器，并只参与该武器的本地伤害。

`CreateInstance` 的随机种子只负责隐式修改器；一次攻击的武器伤害由 `AttackRandomRolls.BaseDamageSeed` 单独掷出。

## ProjectileSkillDefinition

创建菜单：`DarkFlare/Data/Skills/Projectile Skill Definition`。

| 字段 | 类型 | 合同 |
| --- | --- | --- |
| `_id` / `_displayName` | `string` | 稳定 ID 与中文名 |
| `_projectilePrefab` | `AssetReferenceGameObject` | 正式技能必填 |
| `_cooldown` / `_targetRange` | `float` | 必须为正数 |
| `_projectileSpeed` / `_projectileRadius` / `_projectileLifetime` | `float` | 必须为正数 |
| `_tags` | `List<TagDefinition>` | 不重复填写自动派生的 `projectile` |
| `_damageSource` | `ProjectileDamageSource` | `Skill` 或 `EquippedWeapon` |
| `_baseDamages` | `List<DamageRollDefinition>` | `Skill` 必须非空；`EquippedWeapon` 必须为空 |
| `_manaCost` | `float` | 不得小于 0；正式基础投射物为 `8` |

`EquippedWeapon` 找不到有效 Weapon 槽来源时，攻击构建失败，不生成投射物、不发送攻击事件，也不消耗 `PlayerAttack` 根种子。法力不足同样在取随机种子前拒绝释放；只有投射物成功生成后才提交一次耗蓝和攻击事件。`Skill` 来源只读取自己的配置伤害；两种来源都没有固定 `12` 点保护。

## LootTableDefinition

创建菜单：`DarkFlare/Data/Loot/Loot Table Definition`。正式资产位于 `Assets/Data/Preset/Loot`。

| 字段 | 类型 | 默认 / 合同 |
| --- | --- | --- |
| `_dropChance` | `float` | 默认 `1`，合法范围 `0–1` |
| `_entries` | `List<LootTableEntry>` | 必须非空，正式三张表覆盖全部二十件装备 |
| `_affixPool` | `List<AffixDefinition>` | 正式表引用完整 25 物品词条，不得缺失 |

`LootTableEntry` 字段：

| 字段 | 类型 | 合同 |
| --- | --- | --- |
| `_item` | `ItemBaseDefinition` | 正权重条目必填 |
| `_weight` | `int` | 不得为负；大于零才参与抽取 |
| `_rarity` | `ItemRarity` | 与词缀数量共同满足 `ItemRarityRules` |
| `_prefixCount` / `_suffixCount` | `int` | 非负、各侧不超过容量，且总数位于正常生成范围 |

调用方提供 `System.Random`。掉落概率、条目选择和物品生成种子依次消费该随机流；`ItemGenerator` 再由种子确定性派生定义与数值。非法数量不会被 Clamp：Inspector 和内容扫描报告错误，运行时返回空掉落并记录上下文。

## CraftingDefinition

创建菜单：`DarkFlare/Data/Crafting/Crafting Definition`。正式资产为 `Assets/Data/Preset/Crafting/基础打造配置.asset`。

| 字段 | 类型 | 正式值 / 合同 |
| --- | --- | --- |
| `_affixPool` | `List<AffixDefinition>` | 完整 25 词条；无空引用、重复引用；每件装备至少兼容 3 前缀组和 3 后缀组 |
| `_normalToMagicCost` | `int` | `15`，不得为负 |
| `_magicToRareCost` | `int` | `60`，不得为负 |
| `_rareToUniqueCost` | `int` | `160`，不得为负 |
| `_resetToNormalCost` | `int` | `20`，不得为负 |
| `_rerollAffixesCost` | `int` | `40`，不得为负 |
| `_addAffixCost` | `int` | `60`，不得为负 |
| `_removeAffixCost` | `int` | `60`，不得为负 |
| `_rerollAffixValuesCost` | `int` | `80`，不得为负 |
| `_precisionMultiplier` | `float` | 固定 `3`；前缀 / 后缀精准范围统一乘此倍率 |

配置只保存操作族基础价，不重复序列化 14 个变体。`GetCost` 按当前稀有度选择升阶成本，并对四类带范围操作使用 `CeilToInt(baseCost × precisionMultiplier)`。随机状态不存于配置对象，由 `GameplayRandomSystem` 的 Crafting 通道提供。

## DamageRollDefinition

| 字段 | 类型 | 合同 |
| --- | --- | --- |
| `_damageType` | `DamageType` | Physical / Fire / Cold / Lightning / Chaos |
| `_amountRange` | `Vector2` | 两端非负，下限不大于上限 |
| `_tags` | `List<TagDefinition>` | 只保存不能由伤害类型和血统派生的自定义 Damage 标签 |

调用方必须传入确定性 `System.Random`。配置对象不得读取全局随机状态，也不得在空列表时制造隐藏伤害。

## 校验与迁移

- Inspector 使用 `EquipmentConfigurationValidator` 与 `RandomizationConfigurationValidator` 输出上下文 Warning。
- 配置中心内容校验会检查 23 份正式属性、25 个物品词条、10 个怪物词条、角色、技能、装备和怪物资产，并拒绝不可消费修改器、非法词条池、负法力、负恢复或负技能耗蓝；`GameplayPhysicsConfigurationValidator` 还会检查正式 Layer、碰撞矩阵、Actor Prefab 完整层级和 `WorldBounds`。
- 新增字段或改变伤害来源时，应通过 Editor API / Unity MCP 精准迁移并保留 GUID。
- 正式提交前至少运行 `Alpha012DamageResolutionTests`、`Alpha013CollisionSafetyTests`、`Alpha014ResourceSystemTests`、`Alpha015MonsterAffixTests`、`Alpha015MonsterAffixContentTests`、`RandomizationPhase3Tests`、`Phase4ContentTests` 和 Main PlayMode 集成测试。
