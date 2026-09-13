# 物品、伤害与掉落配置参考

本文件封板物品基底、伤害范围、掉落表及其嵌套条目。正式示例位于 `Assets/Data/Preset/Items` 和 `Assets/Data/Preset/Loot`。物品实例拥有稀有度、词缀与随机结果，基底 ScriptableObject 只保存不可变模板。

## ItemBaseDefinition

创建入口：`DarkFlare/Data/Items/Item Base Definition`。用途：声明物品身份、占格、装备兼容、基础价值、武器基础伤害与隐式修改器。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 正式资产必填；唯一小写 `snake_case`，发布后稳定 | 实例 ID、价格、Addressable 映射、随机与存档消费；改名须迁移 |
| `_displayName` | `string` / 空 | 正式资产必填；中文名称 | 物品信息浮窗、交易与调试消费 |
| `_localizedName` | `LocalizedContentReference` / 空引用 | 正式资产必填；固定使用 `items/item.<id>.name` | 玩家可见名称由 `LocalizationService` 按当前语言解析；`_displayName` 仅保留作者识别与日志兼容 |
| `_itemType` | `ItemType` / `Weapon` | 必填；必须与槽位、伤害和标签派生一致 | 背包/装备、词缀 SpawnQuery 和 UI 消费；改变类型属于实例迁移 |
| `_stackable` | `bool` / `false` | 独立堆叠开关；装备始终不可堆叠 | 背包自动合并与拖动合并消费 |
| `_maxStackSize` | `int` / `999` | 至少 1；不堆叠物品有效上限为 1 | 拾取、数量校验和存档恢复消费；金币正式上限为 100，锻造矿石为 50 |
| `_consumable` | `bool` / `false` | 独立消耗开关，不由物品类型或堆叠性推导 | 金币支付与材料消耗校验；预留消耗品尚无使用效果 |
| `_icon` | `AssetReferenceSprite` / `null` | 正式资产必填；Addressable GUID 有效且各正式物品独立 | `SpriteAssetLoader` 预热/缓存/释放；禁止 Resources 与直接路径加载 |
| `_allowedEquipmentSlots` | `EquipmentSlotMask` / `None` | 可装备物品必须非空；Weapon 只能 Weapon，护甲/饰品遵循对应槽位 | 拖拽装备与装备模型校验消费 |
| `_defaultRarity` | `ItemRarity` / `Normal` | 必填枚举；必须与初始显式词缀状态相容 | `CreateInstance` 未另传稀有度时使用；正式掉落通常由条目覆盖 |
| `_baseValue` | `int` / `0` | 不得小于 `0` | `ItemValueCalculator` 结合稀有度/词缀及商人倍率计算价格 |
| `_gridSize` | `Vector2Int` / `(1, 1)` | 两轴至少 `1`，不得超过背包网格 | 玩家/商人背包放置、拖拽和 UI 占格消费；变更需迁移已有摆放 |
| `_weight` | `float` / `0` | 不得小于 `0` | 当前作为物品元数据与详情展示；不参与掉落权重 |
| `_tags` | `List<TagDefinition>` / 空 | 仅允许非派生 ItemSpawn 标签；正式物品当前为空 | `CombatTagResolver` 从类型/槽位派生 weapon/armor/ring 等事实 |
| `_baseDamages` | `List<DamageRollDefinition>` / 空 | Weapon 必须非空；非武器必须为空 | `AttackSnapshotFactory` 仅从装备武器建立基础伤害；无固定伤害回退 |
| `_implicitModifiers` | `List<StatModifierDefinition>` / 空 | 可空；每项必须有合法当前消费者 | `CreateInstance` 使用传入种子生成固定实例隐式值；装备结算和详情消费 |

显式前后缀容量不属于基底字段，由 `ItemRarityRules` 按实例稀有度决定。普通物品无显式词缀；魔法、稀有、传奇分别受对应最小总数与两侧容量约束。

## DamageRollDefinition

用途：嵌套于武器、技能和怪物接触攻击，表示一个伤害类型的闭区间随机量。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_damageType` | `DamageType` / `Physical` | 必填；Physical/Fire/Cold/Lightning/Chaos 之一 | 伤害包、护甲/抗性与类型标签派生消费 |
| `_amountRange` | `Vector2` / `(0, 0)` | 两端非负且下限不大于上限；正式攻击应能产生正伤害 | `CreatePacket(random)` 消费调用方随机流；非法范围不应静默交换 |
| `_tags` | `List<TagDefinition>` / 空 | 只保存不能由 DamageType 与血统派生的自定义 Damage 标签 | `CombatTagResolver` 自动派生 damage/type；正式基础伤害当前为空 |

同一攻击的伤害种子由攻击根种子派生。空伤害列表返回无伤害，而不是制造隐藏固定值。

## LootTableDefinition

创建入口：`DarkFlare/Data/Loot/Loot Table Definition`。用途：控制一次掉落尝试、加权条目与物品显式词缀候选池。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 必填；小写 `snake_case`；正式值与对应怪物稳定 ID 一致 | 完整内容 ID 使用 `loot:<local_id>`；存档与迁移不依赖资产路径或中文名 |
| `_dropChance` | `float` / `1` | `0–1` | `GenerateLoot` 首先消费调用方随机流判定是否掉落；`0/1` 不产生边界歧义 |
| `_entries` | `List<LootTableEntry>` / 空 | 正式表必须非空，至少一个正权重有效物品；不得含无意义重复项 | `PickEntry` 按整数相对权重选择；列表顺序会影响同一随机流映射 |
| `_affixPool` | `List<AffixDefinition>` / 空 | 生成非普通物品时必须能完整满足条目指定数量；无空/重复引用 | `ItemGenerator` 进行合法候选过滤和确定性词缀生成 |

## LootTableEntry

用途：嵌套于掉落表；声明候选物品、相对权重、稀有度与精确生成数量。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_item` | `ItemBaseDefinition` / `null` | 正权重条目必填 | 父掉落表持有引用；所选基底创建新 `ItemInstance` |
| `_quantityRange` | `Vector2Int` / `(1, 1)` | 两端至少 1，最大值不超过物品堆叠上限 | 同掉落随机流抽取闭区间数量；装备保持 1 |
| `_weight` | `int` / `100` | 不得小于 `0`；大于 `0` 才参与抽取 | `PickEntry` 的相对整数权重，不是百分比 |
| `_rarity` | `ItemRarity` / `Normal` | 必填；必须与指定前后缀数量满足 `ItemRarityRules` | 生成实例稀有度；不会自动提升或降级 |
| `_prefixCount` | `int` / `0` | 非负、不超过当前稀有度前缀容量；与后缀合计满足最小/最大数 | `ItemGenerator` 精确请求前缀数；候选不足时整次生成失败 |
| `_suffixCount` | `int` / `0` | 非负、不超过当前稀有度后缀容量；与前缀合计合法 | `ItemGenerator` 精确请求后缀数；不做 Clamp |

### 校验、随机与迁移

- 掉落概率、条目选择和物品生成根种子依次消费调用方 `System.Random`；词缀定义和值再从种子确定性派生。
- Inspector 与配置中心拒绝负权重、非法稀有度数量、候选池不足、非武器基础伤害和 Weapon 空伤害。
- 单图图标应原位保留 GUID；更换 GUID 时必须同步 `AssetReferenceSprite` 与 Addressables，并验证预热失败日志。
- 修改 `_gridSize`、`_itemType` 或稳定 ID 时需要显式迁移现有实例/存档，不能依赖 CLR 默认值。
