# 最小循环设计

## 目标

先在一个场景中建立可反复验证的最小循环：

```text
进入场景 -> 刷怪 -> 战斗 -> 掉落 -> 拾取 -> 装备/整理 -> 交易/打造 -> 再次战斗
```

这个循环服务于类暗黑、流放之路的构筑驱动战斗，同时保留类塔科夫的背包空间、资源取舍和跑商经营压力。

## 首版成功标准

- 玩家能在 `Assets/Scenes/Main.unity` 中进入战斗区域。
- 场景能持续或分波次生成怪物。
- 玩家和怪物都走同一套伤害结算管线。
- 怪物死亡后按掉落表生成装备、材料、货币。
- 装备能进入背包，并能穿戴到装备栏。
- 装备上的词条会影响角色属性和伤害结算。
- 商人能买入、卖出基础物品。
- 打造能对装备执行至少一种确定性操作，例如添加随机词条、重铸词条、提升数值。
- 玩家完成一次整理、交易或打造后，能重新进入战斗并感到构筑变化。

## 核心系统边界

### 战斗

负责刷怪、技能命中、伤害计算、死亡和掉落触发。

首版只做命中伤害，不急于实现复杂持续伤害、异常状态、召唤物和反伤。

### 掉落

负责根据怪物等级、区域等级、掉落表和物品权重生成物品。

首版掉落类型：

- 装备
- 打造材料
- 交易货币

### 背包

负责物品格子、堆叠、重量或容量限制。

首版建议先做格子占用和堆叠，不急于做复杂容器嵌套。类塔科夫体验可以从“有限空间导致取舍”开始。

### 装备

负责装备栏、基础属性、局部词条、全局词条和耐久等扩展字段。

装备只提供词条与基础数据，不直接写战斗逻辑。

### 交易

负责商人库存、买入价、卖出价和物品价值计算。

首版交易价值可以由物品基底、稀有度、词条等级、词条数量和材料类型共同计算。

### 打造

负责修改装备词条。

首版打造操作：

- 添加一个随机词条
- 移除并重随一个随机词条
- 重随全部随机词条
- 提升一个词条数值

## QFramework 分层建议

### Model

- `CharacterModel`：玩家和怪物的基础属性、当前生命、等级等运行时数据。
- `InventoryModel`：背包格子、物品实例、货币和材料。
- `EquipmentModel`：装备槽位和已穿戴物品。
- `LootModel`：掉落表、场景掉落物和拾取状态。
- `EconomyModel`：商人库存、买卖倍率、价格缓存。
- `CraftingModel`：打造配方、打造材料和可用操作。

### System

- `SpawnSystem`：按场景规则生成怪物。
- `CombatSystem`：组织攻击、命中、伤害、死亡。
- `StatSystem`：聚合角色、装备、技能、天赋、临时效果提供的属性。
- `DamageSystem`：执行纯伤害结算。
- `LootSystem`：根据掉落表生成掉落物。
- `ItemGenerationSystem`：生成装备实例、随机词条和词条数值。
- `TradingSystem`：处理买卖和价格计算。
- `CraftingSystem`：处理装备打造和词条变更。

### Controller

- `SceneLoopController`：驱动场景最小循环。
- `PlayerCombatController`：把输入转换为攻击或技能 Command。
- `MonsterController`：怪物行为和受击表现。
- `LootController`：拾取交互。
- `InventoryController`：背包、装备、交易和打造 UI 交互。

Controller 只注册事件和发送 Command，不直接修改 Model。

### Command

- `AttackCommand`
- `ApplyDamageCommand`
- `KillActorCommand`
- `GenerateLootCommand`
- `PickItemCommand`
- `EquipItemCommand`
- `SellItemCommand`
- `BuyItemCommand`
- `CraftItemCommand`

### Query

- `GetActorStatsQuery`
- `GetDamagePreviewQuery`
- `GetInventoryQuery`
- `GetItemPriceQuery`
- `GetCraftingOptionsQuery`

## 数据配置

首版建议放在 `Assets/Data/Preset` 下：

```text
Assets/Data/Preset/
  Affixes/
  Items/
  LootTables/
  Monsters/
  Skills/
  Stats/
  Tags/
  Traders/
  Crafting/
```

每类配置优先使用 `SerializedScriptableObject` 或 `ScriptableObject`，字段用中文标注，编辑器显示优先使用 Odin。

## 首版实现顺序

1. 先实现 `Stats`、`Tags`、`Affixes`、`Items` 的配置结构。
2. 实现属性聚合和伤害结算的纯 C# 管线。
3. 做一个固定技能和一个固定怪物，验证伤害、死亡和掉落。
4. 接入装备生成、穿戴和词条生效。
5. 接入背包格子和拾取。
6. 接入商人买卖。
7. 接入基础打造。
8. 用 `Main.unity` 串成一轮完整循环。

## 暂缓内容

- 大型天赋盘
- 复杂异常状态
- 召唤物
- 多场景撤离
- 联机同步
- 完整经济模拟
- 自动寻路经营玩法

这些内容都依赖伤害、词条、物品实例和经济价值底座，应该在最小循环稳定后再扩展。

