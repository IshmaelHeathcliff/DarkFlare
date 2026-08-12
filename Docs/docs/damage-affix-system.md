# 伤害系统与词条系统设计

## 设计目标

这套系统要支撑装备效果、技能效果、天赋效果、怪物能力、打造结果和伤害防御机制。核心目标不是堆公式，而是建立一个能长期扩展的词条表达模型。

参考方向：

- 暗黑：清晰的装备词条、稀有度、局部属性和全局属性。
- 流放之路：基于标签的技能与词条匹配、伤害类型转换、增加与更多的分层乘区、复杂防御层。
- 类塔科夫资源管理：装备价值、材料价值、容量占用和交易价值影响玩家取舍。

## 核心原则

- 词条只描述“改变什么”，不直接写战斗流程。
- 伤害结算由统一管线执行，玩家、怪物、技能、装备都走同一套规则。
- 标签是扩展核心，技能、伤害、物品、角色状态都应有标签。
- 绝大多数效果用数据配置表达，只有少数特殊行为需要代码扩展。
- 首版先保证可测、可解释、可组合，再扩展复杂机制。

## 基础概念

### 标签

标签用于匹配词条生成条件和战斗修改器，但不同语义域不会再进入同一个任意并集。`TagDefinition` 保存稳定 ID、中文名、Domain、使用状态与说明；`CombatTagContext` 分离来源角色、目标角色、技能、来源物品、本次攻击和当前伤害包。

物品类别、角色阵营、技能类型和伤害类型优先由强类型字段派生。当前正式查询消费 `weapon`、`armor`、`ring` 和 `physical`；其余既有定义在出现真实消费者前标记为预留。完整合同见[战斗标签系统](./combat-tag-system.md)。

### 属性

属性是可被词条修改和查询的数值。

首版属性示例：

- 生存：最大生命、护盾、护甲、火焰抗性、冰冷抗性、闪电抗性、混沌抗性
- 攻击：物理伤害、火焰伤害、攻击速度、施法速度、暴击率、暴击伤害
- 通用：移动速度、掉落数量、物品稀有度、交易价值
- 资源：背包容量、负重上限、打造成功率、商人折扣

建议用 `StatDefinition` 配置资产定义属性 ID、中文名、类别、默认值、最小值、最大值、是否百分比。

配置资产和运行时代码必须共用同一个稳定 ID。最大生命的标准 ID 为 `max_health`；使用显示名或旧 ID 会被配置校验报告为未知属性，不能进入有效属性聚合。

完整属性清单、运行时消费者和配置校验规则见[属性定义与调用关系](./stat-system.md)。

### 词条

词条由一个或多个修改器组成。

示例：

```text
前缀：燃烧的
+12% 火焰伤害
+5% 点燃几率
```

```text
后缀：商旅之
+8% 物品卖出价值
+3 背包容量
```

词条配置应包含：

- 稳定 ID
- 中文名
- 前缀或后缀
- 词条组
- 可出现物品标签
- 禁止出现物品标签
- 最低物品等级
- 权重
- 等级段
- 修改器列表

词条组用于避免互斥词条同时出现，例如同一件装备不能同时拥有两个同组的基础伤害前缀。

## 修改器模型

一个修改器描述一次属性变化。

建议字段：

- `StatId`：目标属性
- `Operation`：计算方式
- `Value`：数值或数值区间
- `Scope`：局部、全局、技能、区域、临时
- `RequiredTags`：必须匹配的标签
- `BlockedTags`：禁止匹配的标签
- `Condition`：额外条件
- `Priority`：特殊排序需要时使用

### Operation 类型

- `Flat`：固定加减，例如 `+20 最大生命`
- `Increase`：增加或降低，同类相加，例如 `+15% 火焰伤害`
- `More`：更多或更少，单独相乘，例如 `20% 更多投射物伤害`
- `Override`：覆盖值，例如 `暴击率固定为 5%`
- `Conversion`：伤害转换，例如 `40% 物理伤害转化为火焰伤害`
- `GainAsExtra`：额外获得，例如 `获得物理伤害 10% 的混沌伤害`
- `Chance`：概率，例如 `10% 几率造成双倍伤害`
- `Trigger`：触发效果，例如 `击杀时回复生命`
- `Limit`：上限或下限变化，例如 `火焰抗性上限 +2%`

首版必须实现 `Flat`、`Increase`、`More`、`Conversion`、`GainAsExtra`。`Trigger` 可以只保留数据结构，等最小循环稳定后再实现。

### Scope 类型

- `LocalItem`：只影响物品自身，例如武器本地物理伤害。
- `GlobalActor`：影响角色整体，例如全局火焰伤害。
- `Skill`：只影响指定技能或技能标签。
- `TargetTaken`：让目标承受更多或更少伤害。
- `Area`：区域规则，例如地图词条。
- `Temporary`：Buff、药剂、临时状态。

局部词条和全局词条必须分开。武器本地伤害先改变武器基础伤害，再进入角色全局伤害计算。

## 伤害上下文

一次伤害计算使用 `DamageContext`。

建议字段：

- 攻击者 ID
- 防御者 ID
- 技能 ID
- 武器或来源物品 ID
- 命中 ID
- 随机种子
- 伤害包列表
- 结构化 `CombatTagContext`
- 攻击者快照属性
- 防御者快照属性
- 是否暴击
- 是否命中

上下文必须是快照。一次攻击开始后，不应因为装备或 Buff 在中途变化而改变已发出的伤害。

## 伤害管线

首版推荐按固定顺序执行：

1. 分别冻结来源角色、技能、来源物品和本次攻击标签。
2. 生成基础伤害包，例如武器物理伤害或技能基础火焰伤害。
3. 应用局部物品词条，得到最终武器基础伤害。
4. 应用技能倍率和附加基础伤害。
5. 执行伤害转换，例如物理转火焰。
6. 执行额外获得伤害，例如物理获得额外混沌。
7. 汇总 `Increase` 与 `Reduced`，按匹配标签加总。
8. 逐个应用 `More` 与 `Less` 乘区。
9. 计算命中、闪避、格挡和暴击。
10. 命中时补入目标角色，按包派生伤害标签，再应用目标承受伤害变化、抗性、穿透、护甲和其他减伤。
11. 分配到护盾、生命或其他资源。
12. 发送命中、受伤、击杀、掉落等事件。

首版可以先简化第 9 步，只实现命中必定成功、暴击可选。防御先实现护甲和抗性即可。

## 伤害类型

首版伤害类型：

- `Physical`
- `Fire`
- `Cold`
- `Lightning`
- `Chaos`

持续伤害不要在首版强行混入命中流程。后续可以扩展 `DamageForm`：

- `Hit`
- `DamageOverTime`

## 防御层

防御按从外到内的顺序设计：

1. 避免命中：闪避、格挡、躲避。
2. 命中后减伤：护甲、抗性、承受伤害降低。
3. 资源承伤：护盾、生命、特殊护盾。
4. 命中后响应：反击、吸血、击回、受击触发。

首版建议实现：

- 抗性：按伤害类型降低元素或混沌伤害。
- 护甲：降低物理命中伤害。
- 生命：最终承伤资源。

## 属性聚合

属性聚合不应每帧全量重算。

建议分层：

- 静态层：角色基础、装备、天赋。
- 半动态层：技能选择、区域规则、药剂。
- 动态层：本次伤害上下文、目标状态、临时触发条件。

装备变化、天赋变化、Buff 变化时标记缓存失效。单次伤害只对动态条件做筛选。

## 物品与词条生成

装备实例由基底和词条组成。

建议结构：

- 物品实例 ID
- 物品基底 ID
- 稀有度
- 物品等级
- 随机种子
- 品质
- 耐久
- 前缀列表
- 后缀列表
- 隐式词条列表
- 打造锁定状态
- 格子尺寸
- 重量

生成流程：

1. 根据掉落表选择物品类型。
2. 根据区域等级和怪物等级确定物品等级。
3. 根据稀有度确定词条数量上限。
4. 按物品标签筛选可用词条。
5. 按权重随机词条。
6. 按词条等级段随机数值。
7. 写入物品实例。

## 打造模型

打造是对物品实例执行受限变更。

当前正式打造操作：

- `UpgradeRarity`：普通、魔法、稀有依次提升一级，并补足目标稀有度最低词条数。
- `ResetToNormal`：清除全部显式词条并还原为普通。
- `RerollAffixes`：重建任意、前缀或后缀范围内的全部词条。
- `AddAffix`：在任意、前缀或后缀范围内随机增加一条合法词条。
- `RemoveAffix`：在指定范围随机移除一条词条，可低于稀有度正常数量下限。
- `RerollAffixValues`：在指定范围随机选择一条可变词条，仅重随它的数值。

玩家只能选择物品、操作和 `Any / Prefix / Suffix` 范围，不能指定具体词条。容量与正常生成数量由 `ItemRarityRules` 统一决定；成本由 `CraftingDefinition` 配置，前缀 / 后缀精准范围固定为任意范围的三倍。完整事务、价格和失败规则见[打造系统](./crafting-system.md)。

## 示例词条

### 武器局部词条

```text
锋利的
Scope: LocalItem
RequiredTags: Sword, Weapon
Modifiers:
  PhysicalDamage Flat +6 到 +12
  PhysicalDamage Increase +25%
```

### 全局伤害词条

```text
燃烬之
Scope: GlobalActor
RequiredTags: Fire
Modifiers:
  Damage Increase +18%
```

该词条只影响带 `Fire` 标签的伤害。

### 技能限定词条

```text
穿刺者的
Scope: Skill
RequiredTags: Projectile
Modifiers:
  Damage More +20%
```

该词条只影响投射物技能，属于独立乘区。

### 转换词条

```text
熔铸的
Scope: GlobalActor
RequiredTags: Physical
Modifiers:
  Damage Conversion Physical -> Fire 40%
```

转换后，后续火焰伤害增加可以影响被转换的部分。

### 跑商词条

```text
商旅之
Scope: GlobalActor
Modifiers:
  SellValue Increase +8%
  CarryCapacity Flat +3
```

这类词条不直接增加战斗强度，但会影响带出资源和交易收益。

## 配置资产建议

```text
Assets/Data/Preset/
  Tags/
    TagDefinition.asset
  Stats/
    StatDefinition.asset
  Affixes/
    AffixDefinition.asset
  MonsterAffixes/
    MonsterAffixDefinition.asset
  Items/
    ItemBaseDefinition.asset
  Skills/
    SkillDefinition.asset
  Monsters/
    MonsterDefinition.asset
  LootTables/
    LootTableDefinition.asset
  Crafting/
    CraftingRecipeDefinition.asset
```

配置类字段应标注中文，使用 Odin 提供分组、表格、预览和校验按钮。

## 与 QFramework 的关系

- `EquipmentSystem` 负责四槽事务与装备效果重建。
- `CombatSystem` 负责组织伤害、生死和 Actor 生命周期，调用纯计算的 `DamageCalculator`。
- `ItemGenerator` 负责装备实例和词条随机，由掉落、交易和测试等入口调用。
- `CraftingSystem` 负责修改物品实例。
- `TradingSystem` 负责价格与交易。

事件由 System、Model 或 Command 在状态变化后发送，Controller 只注册事件并刷新表现。

## 首版实现范围

必须实现：

- 标签定义
- 属性定义
- 修改器定义
- 词条定义
- 物品基底定义
- 物品实例
- 属性聚合
- 命中伤害计算
- 抗性和护甲
- 随机装备生成
- 穿戴后词条生效

## 当前代码落地

已完成第一版代码底座：

- `TagDefinition`：带 Domain 与使用状态的标签配置资产。
- `StatDefinition`：属性配置资产。
- `AffixDefinition`：词条配置资产，包含修改器、作用域、权重和物品标签筛选。
- `ItemBaseDefinition`：物品基底配置资产，包含标签、基础伤害、隐式修改器和格子信息。
- `ItemInstance`：运行时物品实例，支持隐式、前缀、后缀和修改器收集。
- `ItemGenerator`：基于物品基底、词条池、权重和随机种子生成物品实例。
- `MonsterAffixDefinition`、`MonsterAffixInstance`、`MonsterAffixGenerator`：独立于物品前后缀的怪物词条定义、掷值与同组无放回选择。
- `TagSet`、`TagQueryDefinition`、`TagQuery`、`CombatTagContext`：不可变标签集合、结构化查询和作用域上下文。
- `ModifierInstance`、`StatBlock`、`StatAggregator`：运行时词条和属性聚合结构。
- `AttackRandomRolls`、`HitResolutionCalculator`：从攻击根种子派生具名子流，并纯逻辑计算命中、闪避和暴击。
- `DamageContext`、`DamagePacket`、`DamageResult`、`DamageCalculator`：纯 C# 命中伤害计算管线；伤害包区分最终类型、缩放血统和自定义标签，结果按类型解释承伤、防御和最终值。
- `EquipmentEffectResolver`、`CombatStatResolver`：从四槽分流 LocalItem 与角色效果，并聚合护甲、抗性等有效属性。
- `CombatActor`：从有效属性读取 `max_health` 与 `mana`，保存当前生命 / 法力；穿脱装备时按资源上限变化保持当前比例，并由资源和装备事件触发 HUD 刷新。
- `AttackSnapshot`、`AttackSnapshotFactory`：在攻击发起时冻结来源角色、技能、来源物品、本次攻击、随机伤害包、攻击者属性和修改器。
- `GameplayRandomSystem`：提供根种子与独立随机通道，隔离生成位置、怪物实例、玩家攻击、怪物攻击和掉落序列。
- `ContentConfigurationValidator`：校验首批标签、词条、装备、怪物与各内容池，并检查 Addressable Prefab。
- `MonsterAffixVisual`：最多显示两条配置颜色的世界名称；零词条、死亡或禁用时隐藏，不依赖受伤后生命条。

已实现的伤害计算内容：

- 基础伤害包
- 伤害转换，单个来源伤害转换总量封顶 100%
- 额外获得伤害
- `Increase` 加算
- `More` 独立乘算
- 暴击伤害倍率
- 目标承伤倍率
- 元素和混沌抗性
- 物理护甲减伤
- `Missed / Evaded / Hit / NoDamage` 结果与一次攻击级暴击判定
- 武器 / 技能基础伤害严格所有权；空手武器技能不再回退到技能或代码常量
- 开局初始大剑在刷怪器启用前原子授予并装备
- 发射时攻击快照；投射物命中时只读取当前防御者快照
- `LocalItem` 仅作用于武器本地伤害，伤害修改器不会在属性层重复计算
- 转换和额外获得伤害保留来源类型血统并补充最终类型语义；物理转火焰可同时匹配 Damage 作用域的 `physical` 与 `fire`，防御只读取最终类型。
- 旧平面 `TagSet` 查询保留隔离的兼容通道，不会读取目标标签或自动派生的伤害血统。

当前正式物品池为 25 个词条，覆盖 `StatIds.All` 的 23 项公开属性；修改器必须同时具备合法物品候选和当前运行时消费者。怪物池另有 10 个独立词条，只允许 `GlobalActor` 的 Flat / Increase / More，或从物理伤害获得元素伤害的 `GainAsExtra`。完整 ID、范围、权重和内容池见[首批内容池](./content-system.md)。

`ActorDamagedEvent` 只表示实际正数生命损失；未命中、闪避和无伤害通过统一结算结果事件驱动文字反馈，不触发 Hit 动画、闪白或 `-0`。

暂缓实现：

- 持续伤害
- 异常状态
- 召唤物
- 反伤
- 大型天赋盘
- 复杂触发链
- Buff、区域与多段技能各自独立的快照时机

## 风险与约束

- 不要为每个词条写一个 C# 类，否则后期无法维护。
- 不要让装备直接调用战斗逻辑，装备只能提供数据和修改器。
- 不要把所有标签写死在枚举里，设计期会频繁增删标签。
- 不要让伤害计算读取实时对象状态，必须使用快照。
- 不要过早做复杂异常状态，先让命中伤害、装备词条和打造闭环稳定。
