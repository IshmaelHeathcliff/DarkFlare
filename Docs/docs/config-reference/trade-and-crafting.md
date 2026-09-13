# 交易与打造配置参考

本文件封板商人库存、交易倍率与基础打造成本。正式示例位于 `Assets/Data/Preset/Traders` 和 `Assets/Data/Preset/Crafting`。交易与打造都由 System 操作库存和金币；配置资产不直接修改 Model。

## TraderDefinition

创建入口：`DarkFlare/Data/Trading/Trader Definition`。用途：声明商人身份、买卖倍率和固定库存模板。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 正式资产必填；唯一小写 `snake_case`，发布后稳定 | 库存实例 ID 前缀、调试与潜在存档消费；改名会改变新实例标识 |
| `_displayName` | `string` / 空 | 正式资产必填；中文名称 | 商店 UI、Inspector 与调试消费 |
| `_localizedName` | `LocalizedContentReference` / 空引用 | 正式资产必填；固定使用 `monsters/trader.<id>.name` | 商店与交互 UI 按当前语言解析；库存实例 ID 仍由 `_id` 派生 |
| `_buyMultiplier` | `float` / `1.5` | 不得小于 `0` | `EconomyModel` 与 `ItemValueCalculator.GetBuyPrice` 消费；表示玩家购买溢价 |
| `_sellMultiplier` | `float` / `0.4` | 不得小于 `0` | `EconomyModel` 与 `ItemValueCalculator.GetSellPrice` 消费；表示玩家出售回收比例 |
| `_stock` | `List<TraderStockEntry>` / 空 | 正式商人必须有可售条目；无空引用、非法数量或无意义重复项 | `CreateStock(random)` 逐条创建实例并填入商人网格背包 |

`SetupMerchant` 清空旧库存、设置倍率并使用新的 `System.Random` 创建库存。当前库存每次启动可变化；若未来要求存档复现，必须把根种子纳入存档，而不是写回 TraderDefinition。

## TraderStockEntry

用途：嵌套于商人配置，声明一个物品模板的稀有度、等级与生成数量。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_item` | `ItemBaseDefinition` / `null` | 正数量条目必填 | 父商人资产持有；每份库存调用 `CreateInstance` 创建独立实例 |
| `_rarity` | `ItemRarity` / `Normal` | 必填；初始状态必须能由当前基底合法创建 | 决定库存实例稀有度和价值；当前条目不额外生成显式前后缀 |
| `_itemLevel` | `int` / `1` | 至少 `1` | 传入物品实例，影响等级门槛与详情 |
| `_count` | `int` / `1` | 至少 `1` | `CreateStock` 精确创建份数；每份拥有独立 GUID 实例 ID 与随机 seed |

## CraftingDefinition

创建入口：`DarkFlare/Data/Crafting/Crafting Definition`。正式资产为 `Assets/Data/Preset/Crafting/基础打造配置.asset`。用途：声明共用词缀池、六类操作基础价与前/后缀精准范围溢价。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 必填；小写 `snake_case`；正式值为 `default` | 完整内容 ID 为 `crafting:default`；存档与迁移不保存资产路径或显示名 |
| `_material` | `ItemBaseDefinition` / `null` | 正式配置必须引用可消耗的材料 | 打造资格、扣费与 UI 需求显示消费 |
| `_materialCost` | `int` / `1` | 至少 1；精准范围乘以精准倍率 | 成功打造一次性扣除对应数量，失败保持资源不变 |
| `_affixPool` | `List<AffixDefinition>` / 空 | 正式配置必须覆盖完整合法物品词缀；无空/重复引用；每件装备至少有足够前后缀组 | `CraftingOperations` 过滤候选；配置只提供池，不指定最终词缀 |
| `_normalToMagicCost` | `int` / `15` | 不得小于 `0` | 普通升魔法基础价；成功提交后由 `InventoryModel` 扣除 |
| `_magicToRareCost` | `int` / `60` | 不得小于 `0` | 魔法升稀有基础价 |
| `_rareToUniqueCost` | `int` / `160` | 不得小于 `0` | 稀有升传奇基础价；传奇继续升级返回不可用 |
| `_resetToNormalCost` | `int` / `20` | 不得小于 `0` | 清除全部显式词缀并还原普通的固定价 |
| `_rerollAffixesCost` | `int` / `40` | 不得小于 `0` | 重随词缀定义和值的 Any 基础价 |
| `_addAffixCost` | `int` / `60` | 不得小于 `0` | 在容量内随机增加一条的 Any 基础价 |
| `_removeAffixCost` | `int` / `60` | 不得小于 `0` | 随机移除一条的 Any 基础价；可低于稀有度最小总数且不降级 |
| `_rerollAffixValuesCost` | `int` / `80` | 不得小于 `0` | 随机选择一条可变词缀并重随其数值的 Any 基础价 |
| `_precisionMultiplier` | `float` / `3` | 至少 `1`；正式合同固定 `3` | Reroll/Add/Remove/RerollValues 的 Prefix 或 Suffix 范围使用 `CeilToInt(基础价 × 倍率)` |

配置只保存操作族基础价，不重复序列化 UI 展示的 14 个操作变体。`UpgradeRarity`、`ResetToNormal` 固定 Any；其余四类提供 Any/Prefix/Suffix。打造不能指定具体词缀，所有选择均由 Crafting 随机通道根种子确定。

稀有度规则：普通无显式词缀；魔法前后缀容量各 `1` 且总数至少 `1`；稀有容量各 `3` 且总数至少 `2`；传奇容量各 `3` 且总数至少 `4`。重随全部必须生成合法完整结果；增加不得超容量；移除可无视最小总数但保留当前稀有度和容量。

### 校验、交易原子性与迁移

- 购买先验证库存、金币和玩家背包空间，再扣金币并移除商人库存；出售先从玩家背包移除，再增加金币。
- 打造先无副作用评估，成功构造新状态后原子提交，再扣金币；扣款失败会回滚词缀状态。
- 配置中心检查倍率、库存引用/等级/数量、打造非负价格、精准倍率和每件装备的合法词缀候选覆盖。
- 调价只影响新查询结果，不迁移现有金币；改变库存数量或基底 ID 时，存档系统上线后必须提供显式版本迁移。
