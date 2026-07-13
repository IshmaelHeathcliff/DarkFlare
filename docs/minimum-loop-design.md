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
  Actors/
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

## 当前代码落地

已完成第一版战斗原型，并按 QFramework 分层落地（不再是 Controller 之间直接互相调用）：

- 数据配置：`CharacterDefinition`（玩家/通用角色属性，含 Prefab 引用）、`ProjectileSkillDefinition`（投射物技能，含 Prefab 引用）、`MonsterDefinition`（怪物属性、Prefab 引用、碰撞伤害）、`MonsterSpawnDefinition`（刷怪间隔、存活上限、怪物池权重）。怪物、玩家、投射物的 Prefab 引用统一使用 `AssetReferenceGameObject`，走 Addressables 加载，不再用直接 `GameObject` 引用或运行时现造对象。
- `CombatModel`：按阵营维护存活 `CombatActor` 列表。
- `CombatSystem`：负责注册/注销 Actor、`ApplyDamage`（伤害结算 + 生死判定）、`Revive`，并在状态变化时发送 `ActorDamagedEvent`/`ActorDiedEvent`/`ActorRevivedEvent`。
- `SpawnSystem`：负责 `PreloadAsync`（预热玩家、技能、怪物池对应的 Addressable Prefab）和 `SpawnPlayer`/`SpawnMonster`/`SpawnProjectile`（从已预热的 Prefab 实例化）。
- `PrefabAssetLoader`（`IUtility`，原名 `CombatAssetLoader`，改名后不再局限于战斗场景，供任意功能预热/缓存 Addressable Prefab）：Addressables 加载、缓存与释放。
- Command：`RegisterActorCommand`/`UnregisterActorCommand`/`ApplyDamageCommand`/`ReviveActorCommand`/`SpawnPlayerCommand`/`SpawnMonsterCommand`/`FireProjectileCommand`/`PickupLootCommand`/`EquipItemCommand`。
- Query：`GetClosestActorQuery`（按阵营、位置、范围查找最近 Actor，取代原来挂在 `CombatActor` 上的静态方法）。
- Controller：`CombatActor`（生命/阵营/属性本地状态，变更入口只允许 `CombatSystem` 调用，自身不再触发全局事件；新增 `SetModifiers` 供装备生效使用）、`PlayerController`（读取 Input System 输入移动，通过 Command 发起攻击和复活）、`MonsterController`（追踪玩家、通过 Command 造成碰撞伤害，监听 `ActorDiedEvent` 处理死亡表现，对外暴露 `Definition` 供 `LootSystem` 反查掉落表）、`ProjectileController`（命中后通过 `ApplyDamageCommand` 结算）、`MonsterSpawner`（定时通过 `SpawnMonsterCommand` 生成怪物）、`LootPickupController`（掉落物在世界中的表现，按稀有度着色，玩家触碰后通过 `PickupLootCommand` 尝试入包；背包满则拾取物留在地上不销毁）、`CombatPrototypeBootstrap`（场景内常驻组件，预热资源后生成玩家、激活刷怪器、设置相机跟随目标；不再运行时现造 GameObject）。
- 掉落：`LootTableDefinition`（掉落池 + 词条池，`PickItem` 加权选取、`GenerateLoot` 调用已有的 `ItemGenerator` 生成 `ItemInstance`）、`LootSystem`（监听 `ActorDiedEvent`，命中怪物阵营时按 `MonsterDefinition.LootTable` 生成掉落物并在死亡位置生成 `LootPickup` Prefab；`CollectLoot` 把物品放入 `InventoryModel`，背包满则返回 false）。物品随机生成的纯逻辑（`ItemInstance`/`ItemGenerationOptions`/`ItemGenerator`）此前已经写好但从未被调用，掉落这一步是它们第一次被接入实际流程。
- 装备：`EquipmentModel`（按 `CombatActor` 存放已装备武器，为后续给怪物/NPC 装备预留）、`CombatSystem.EquipWeapon`（写入 `EquipmentModel`、从 `InventoryModel` 移出该物品、并调用 `CombatActor.SetModifiers(item.CollectModifiers())`，让 `DamageCalculator` 里的 Increase/More/Conversion 等阶段真正吃到装备词条）。v1 只做武器单槽位，卸装备和多槽位留给后续。修正了两处会让装备验证不出效果的历史数据问题：`Assets/Data/Preset/Stats/05伤害.asset` 的 `_id` 从 `Damage` 改成 `damage`（与 `StatIds.Damage` 大小写对齐），`Assets/Data/Preset/Afflixes/000基础伤害增加.asset` 的运算方式从 `Flat` 改成 `Increase`（`Flat` 目前只有类型专属伤害属性会被 `DamageCalculator.GetFlatDamage` 读取，通用 `damage` 属性走不到）。
- 背包：`InventoryGrid`（纯逻辑二维格子，`TryAdd` 按 `ItemBaseDefinition.GridSize` 行优先找空矩形占用、`Remove` 释放格子，放不下返回 false）、`InventoryModel`（持有单个玩家背包 grid，默认 10x6，暴露 `TryAddItem`/`RemoveItem`/`Grid`）。拾取即入包，背包满则拾取物留在地上——体现"有限空间导致取舍"。当前只做格子占用，堆叠（无可堆叠物品）和重量限制（格子已是空间约束）暂缓。从背包穿戴武器时物品会移出背包（`EquipWeapon` 内处理）；这一步不再自动穿戴，从背包选物穿戴的交互等背包 UI。
- Prefab：`Assets/Prefabs/Combat/Player.prefab`、`Monster_Basic.prefab`、`Projectile_Default.prefab`、`Assets/Prefabs/Loot/LootPickup.prefab`，均已标记为 Addressable。占位视觉使用共享的方块贴图 `Assets/Art/Textures/Prototype/PrototypeSquare.png`，按 `SpriteRenderer.Color` 区分（玩家/怪物/投射物用固定颜色，掉落物按稀有度着色）。
- 数据资源：`Assets/Data/Preset/Actors/玩家.asset`、`Skills/基础投射物技能.asset`、`Monsters/基础怪物.asset`、`Monsters/基础刷怪表.asset`、`Loot/基础怪物掉落表.asset`（引用已有的 `Items/001大剑.asset` 和 `Afflixes/000基础伤害增加.asset`），已在 `Main.unity` 场景中挂到 `CombatPrototypeBootstrap`/`MonsterSpawner`。
- 测试：`Assets/Scripts/Test/Editor/MonsterSpawnDefinitionTests.cs`、`LootTableDefinitionTests.cs`、`DamageCalculatorTests.cs`、`InventoryGridTests.cs` 分别覆盖 `MonsterSpawnDefinition.PickMonster`、`LootTableDefinition.PickItem` 的权重选取逻辑、"带 Increase 词条的伤害结算确实比没有词条时更高"，以及背包格子占用（放入/满拒绝/移除后重放/不重叠）。

## 暂缓内容

- 大型天赋盘
- 复杂异常状态
- 召唤物
- 正式美术资源（当前 Prefab 用占位方块贴图代替最终美术）
- 交易和打造闭环（掉落生成、装备词条生效、背包格子占用已接入；从背包选物穿戴/整理需要背包 UI，堆叠与重量限制暂缓）
- 多场景撤离
- 联机同步
- 完整经济模拟
- 自动寻路经营玩法

这些内容都依赖伤害、词条、物品实例和经济价值底座，应该在最小循环稳定后再扩展。
