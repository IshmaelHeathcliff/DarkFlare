# alpha 0.1.5 全属性与怪物词条执行计划

> 状态：已完成（2026-08-12）
> 建立日期：2026-08-12
> 所属版本：`alpha 0.1`
> 前置阶段：`alpha 0.1.1–alpha 0.1.4` 已完成
> 后续依赖：`alpha 0.1.6` 随机打造重构

## 阶段目标

本阶段完成两个可独立验收的纵向切片：

1. 让当前 23 个公开属性都有真实运行时消费者，以及至少一条合法、可生成、可显示的物品词条路径。
2. 建立独立于物品词条的怪物词条模型，并贯通确定性生成、实例属性、接触攻击、世界辨识和调试输出。

本阶段完成后，装备词条池应能支撑下一阶段的随机打造；怪物词条应能真实改变伤害、防御、生命与移动，而不是只修改名称或面板数字。

## 当前基线

- `StatIds.All` 当前登记 23 项属性；阶段 A 已接入力量、敏捷和智力的集中派生规则。
- 当前 12 个正式物品词条直接覆盖最大生命、通用伤害、物理伤害、护甲和四类抗性；火焰、冰霜只通过额外获得伤害出现，其他属性尚无完整生成覆盖。
- `CombatStatResolver` 负责聚合非伤害 `GlobalActor` 修改器；六项伤害属性由 `DamageCalculator` 在攻击快照中消费。
- `MonsterDefinition.CreateInstanceData` 只随机生命倍率；`MonsterInstanceData` 只保存种子、最大生命和属性。
- 怪物接触攻击通过 `AttackSnapshotFactory.CreateImmediate` 冻结 `CombatActor.Stats` 与 `CombatActor.Modifiers`，因此怪物攻击型词条可以复用正式伤害管线。
- 当前怪物世界表现只有受伤后显示的生命条，没有词条名称、标记或详情入口。
- `GameplayRandomChannel.MonsterInstance` 已为每个怪物提供独立根种子，但实例内生命、词条数量、词条选择和词条数值尚未拆分子流。

## 冻结决策

### 主属性派生规则

力量、敏捷和智力先使用集中、可测试的初版派生规则，不把公式散落在 Controller 或 UI：

| 主属性 | 每 1 点派生效果 | 初版边界 |
| --- | --- | --- |
| 力量 `strength` | `+2` 最大生命 | 不直接增加伤害或护甲 |
| 敏捷 `dexterity` | `+1` 命中、`+1` 闪避 | 不直接增加移动速度或暴击 |
| 智力 `intelligence` | `+2` 最大法力 | 不直接增加元素伤害或恢复 |

处理顺序固定为：角色基础值 → 装备 `GlobalActor` 直接修改器 → 主属性派生 → 运行时最终属性。每次重建都从基础值开始，禁止把派生结果再次作为输入，避免重复叠加。

主属性常量集中在纯逻辑解析器中，并为每条公式提供边界测试。后续平衡可以修改常量，但不得改变“先聚合主属性、再派生最终属性”的顺序。

### 物品词条覆盖合同

覆盖不是简单检查 `StatId` 出现次数。每个属性必须同时满足：

- 至少一个正式 `AffixDefinition` 通过修改器明确引用该属性。
- 词条的 Operation、Scope、数值范围和物品生成查询能够被当前运行时消费。
- 至少兼容一个正式物品基底，并能由当前掉落或后续打造词条池生成。
- 物品详情和当前属性面板能显示变化；固定种子下结果可复现。

本阶段保留现有 12 个词条，新增 13 个词条，并调整 3 个既有词条，最终正式物品词条数为 25。

| 属性 | 覆盖词条 | 类型 / 适用物品 | 修改器合同 |
| --- | --- | --- | --- |
| 最大生命 | `healthy` | 前缀；护甲、戒指 | 既有 Flat `15–30` |
| 最大法力 | `arcane_reserve` | 前缀；护甲、戒指 | Flat `15–30` |
| 生命恢复 | `regenerating` | 前缀；护甲、戒指 | Flat `0.5–1.5` / 秒 |
| 法力恢复 | `meditative` | 前缀；护甲、戒指 | Flat `1–3` / 秒 |
| 力量 | `mighty` | 前缀；武器、护甲、戒指 | Flat `5–10`，经主属性规则派生生命 |
| 敏捷 | `deft` | 前缀；武器、护甲、戒指 | Flat `5–10`，经主属性规则派生命中 / 闪避 |
| 智力 | `learned` | 前缀；武器、护甲、戒指 | Flat `5–10`，经主属性规则派生法力 |
| 通用伤害 | `of_power` | 后缀；武器、戒指 | 既有 Increase `8–15%`；中文名由“力量之”改为“威能之”以消除歧义 |
| 物理伤害 | `sharp` / `tempered` | 前缀；武器 | 既有 LocalItem Flat `3–6` / Increase `15–25%` |
| 火焰伤害 | `flame_touched` | 前缀；武器、戒指 | 保留物理额外获得火焰 `8–12%`，新增火焰伤害 Increase `8–15%` |
| 冰霜伤害 | `frost_touched` | 前缀；武器、戒指 | 保留物理额外获得冰霜 `8–12%`，新增冰霜伤害 Increase `8–15%` |
| 闪电伤害 | `storm_touched` | 前缀；武器、戒指 | 物理额外获得闪电 `8–12%`，闪电伤害 Increase `8–15%` |
| 混沌伤害 | `chaos_touched` | 前缀；武器、戒指 | 物理额外获得混沌 `8–12%`，混沌伤害 Increase `8–15%` |
| 暴击率 | `deadly` | 后缀；武器、戒指 | Flat `2–5` 个百分点 |
| 暴击伤害 | `of_ruin` | 后缀；武器、戒指 | Flat `10–20` 个百分点 |
| 命中 | `accurate` | 后缀；武器、戒指 | Flat `10–25` |
| 护甲 | `reinforced` | 前缀；护甲 | 既有 Flat `15–30` |
| 闪避 | `elusive` | 前缀；护甲、戒指 | Flat `8–20` |
| 火焰抗性 | `of_fire_guard` | 后缀；护甲、戒指 | 既有 Flat `10–20` |
| 冰霜抗性 | `of_cold_guard` | 后缀；护甲、戒指 | 既有 Flat `10–20` |
| 闪电抗性 | `of_lightning_guard` | 后缀；护甲、戒指 | 既有 Flat `10–20` |
| 混沌抗性 | `of_chaos_guard` | 后缀；护甲、戒指 | 既有 Flat `10–20` |
| 移动速度 | `of_swiftness` | 后缀；护甲、戒指 | Increase `5–10%` |

`flame_touched`、`frost_touched`、`storm_touched` 和 `chaos_touched` 共用元素 / 类型额外伤害互斥组。每个词条继续使用非空 `GroupId`，同组词条不能共存。

新增词条必须进入三张正式掉落表与打造配置的词条池。验证器不只检查总数，还要按 `StatIds.All` 生成覆盖报告，并拒绝“存在配置但没有兼容物品”或“Operation / Scope 不可消费”的伪覆盖。

### 怪物词条领域模型

怪物词条不复用 `AffixDefinition`、前缀 / 后缀容量或物品标签查询。新增独立配置和实例模型：

```text
MonsterAffixDefinition
├── Id / DisplayName / GroupId
├── Weight
├── DisplayColor
└── StatModifierDefinitions

MonsterAffixInstance
├── Definition
└── rolled ModifierInstances

MonsterInstanceData
├── RootSeed / instance sub-seeds
├── BaseMaxHealth / BaseStats
├── Affixes / Modifiers
└── EffectiveStats / EffectiveMaxHealth
```

约束如下：

- 怪物词条修改器只允许当前管线真实支持的 `GlobalActor` 修改器。
- 防御与移动使用 Flat、Increase 或 More；元素附加伤害使用 `GainAsExtra`，从物理接触伤害获得对应元素伤害。
- 禁止 `LocalItem`、物品生成查询、前后缀容量以及 Chance / Trigger / Limit 占位操作进入正式怪物池。
- 同一 `GroupId` 在一个怪物实例中最多出现一次，按权重无放回选择。
- `MonsterDefinition` 显式配置词条池和数量范围；三种正式怪物初版统一为 `0–2` 条。
- 生命倍率、词条数量、词条选择和每条词条数值使用实例根种子派生的独立子种子。修改一个词条内部的随机次数不能改变实例生命或其他词条数值。
- 继续使用现有 `MonsterInstance` Gameplay Random 通道，不新增会扰动刷怪、攻击或掉落序列的顶层通道。

### 首批怪物词条池

首批建立 10 个 `MonsterAffixDefinition`：

| 类别 | 数量 | 修改器合同 | 互斥组 |
| --- | ---: | --- | --- |
| 火焰 / 冰霜 / 闪电附加伤害 | 3 | 物理伤害额外获得对应元素 `15–25%` | `monster_elemental_damage` |
| 火焰 / 冰霜 / 闪电抗性 | 3 | 对应抗性 Flat `20–35` | `monster_elemental_guard` |
| 强健 | 1 | 最大生命 More `25–40%` | `monster_health` |
| 装甲 | 1 | 护甲 Flat `30–60` | `monster_armor` |
| 灵巧 | 1 | 闪避 Flat `20–40` | `monster_evasion` |
| 迅捷 | 1 | 移动速度 Increase `10–20%` | `monster_speed` |

初版不加入混沌附加伤害、混沌抗性、暴击、命中、恢复、特殊 AI、击中触发或死亡触发。独立模型允许后续扩展，但本阶段不提前实现行为词条。

### 运行时接入

1. `MonsterDefinition.CreateInstanceData` 从实例根种子派生生命、数量、选择和数值子种子。
2. 先生成随机生命基础属性，再由 `MonsterAffixGenerator` 生成不可变词条实例和修改器。
3. `MonsterController.Configure` 在对外发布资源变化前，把基础属性与怪物词条修改器统一配置到 `CombatActor`。
4. `CombatStatResolver` 产生最终生命、护甲、抗性、闪避和移动速度；`AttackSnapshotFactory.CreateImmediate` 自动冻结附加元素伤害修改器。
5. `MonsterInstanceData.EffectiveStats`、`CombatActor.Stats` 与调试输出必须一致；共享 `MonsterDefinition` 不得被运行时改写。
6. `SpawnSystem` 日志记录定义 ID、实例根种子、词条 ID 与掷值摘要、最终生命及关键属性，便于固定种子复现。

### 世界辨识

- 新增独立 `MonsterAffixVisual`，不把词条文字塞进 `MonsterController` 或生命条计算逻辑。
- 只有带词条怪物显示紧凑的世界空间词条标记；零词条怪物不显示空标签。
- 标记展示最多两条短名称，并使用配置颜色区分元素、生命、防御和速度类别。
- 标记与怪物生命条相邻但生命周期独立：词条身份默认可见，生命条仍只在受伤后显示。
- Sprite 翻转不得镜像文字；死亡、禁用和销毁时隐藏标记。
- 本阶段不新增怪物悬停详情窗、图鉴、头顶图标美术或 HUD 怪物面板。

## 执行阶段

### 阶段 A：冻结属性消费者（已完成，2026-08-12）

- 新增纯逻辑主属性派生解析器并接入 `CombatStatResolver`。
- 冻结上述三条派生公式及处理顺序。
- 补充资源比例保持、重复重建不叠加和伤害属性不进入静态聚合的测试。
- 回归物品工作台按 `StatIds.All` 展示全部 23 项属性的现有合同。

验收：`Alpha015PrimaryAttributeTests` 共 6 项通过。力量、敏捷、智力换装后分别改变真实生命、命中 / 闪避、法力；直接词条先聚合再派生，反复刷新结果不增长，生命 / 法力比例保持，伤害修改器仍留在攻击管线，当前属性快照与 `StatIds.All` 顺序一致。

### 阶段 B：补齐 23 属性物品词条（已完成，2026-08-12）

- 创建 13 个新词条资产，调整 3 个既有词条。
- 更新三张掉落表、打造配置与内容池校验。
- 建立由 `StatIds.All` 驱动的覆盖矩阵验证，不在测试中另写一份失同步的属性列表。
- 用固定种子验证每个词条的实例数值、详情文本和实际消费者。

验收：`Alpha015AffixCoverageTests` 与内容校验通过。23/23 属性具备正式词条路径和真实消费者；25 个正式词条均兼容至少一个正式物品，并已同步进入三张掉落表与打造池。

### 阶段 C：怪物词条纯领域与随机合同（已完成，2026-08-12）

- 新增怪物词条配置、实例、生成器和实例随机子流。
- 扩展 `MonsterDefinition` 与 `MonsterInstanceData`，保持共享资产只读。
- 增加组互斥、权重、数量范围、非法修改器和固定种子测试。
- 验证调整词条池或词条内部随机调用不会改变生命子流。

验收：`Alpha015MonsterAffixTests` 6/6 通过。固定定义与种子得到相同数量、ID、掷值和有效属性；子流互不扰动，不存在重复组或非法 Scope。

### 阶段 D：战斗、移动与世界表现接入（已完成，2026-08-12）

- 在 `MonsterController.Configure` 中原子应用实例修改器。
- 验证元素附加伤害、抗性、生命、护甲、闪避和移动速度分别进入既有消费者。
- 新增 `MonsterAffixVisual`，通过 Unity MCP / Editor API 精准绑定三个怪物 Prefab，不直接编辑已打开场景或 Prefab YAML。
- 扩充生成日志和调试格式。

验收：`Alpha015MonsterAffixPlayModeTests` 2/2 通过。六类怪物词条均进入既有战斗、资源或移动消费者；三个正式怪物 Prefab 已绑定世界标记，零词条隐藏、死亡隐藏、复活恢复，文字不受根节点朝向翻转影响。

### 阶段 E：正式内容、文档与综合回归（已完成，2026-08-12）

- 创建 10 个怪物词条资产，三种怪物显式配置完整池与 `0–2` 数量范围。
- 补全 `AffixDefinition` 与新 `MonsterAffixDefinition` 配置参考。
- 更新属性、词条、内容池、随机化、玩法循环与配置索引文档。
- 运行精准测试、全量 EditMode / PlayMode、内容校验和 Main 固定种子重放。
- 完成后总结模块文档，将本计划移入 `Docs/docs/plan/archive/`，并更新总计划状态。

验收：10 个正式怪物词条和三份完整怪物池通过 13/13 内容专项测试；全量 EditMode 176/176 通过，PlayMode 20 项中 18 项通过、2 项为 Input System 上游既有忽略、0 失败。Main 固定种子 `24681357` 双次重放一致，四个相关程序集构建 0 错误，Unity Console 无新增错误。

## 预计改动面

### 运行时代码

- `StatIds` / `CombatStatResolver` 与新增主属性派生解析器。
- `MonsterDefinition`、`MonsterInstanceData` 与新增怪物词条配置 / 实例 / 生成器。
- 实例随机子流与可复用的确定性种子派生工具；现有攻击随机输出必须保持不变。
- `MonsterController`、`SpawnSystem`、`MonsterAffixVisual`。
- 不修改 `DamageCalculator` 的公式顺序；怪物词条通过现有 Stats / Modifiers 入口接入。

### Editor 与配置

- `ContentConfigurationValidator`、`EquipmentConfigurationValidator`、`RandomizationConfigurationValidator`。
- `Assets/Data/Preset/Affixes` 新增 13 项，正式物品词条总数变为 25。
- 新增 `Assets/Data/Preset/MonsterAffixes` 及 10 项正式怪物词条。
- 三张 `MonsterDefinition`、三张掉落表和基础打造配置。
- 三个怪物 Prefab 只通过 Unity MCP / Editor API 增加表现绑定；执行前必须先处理或保留工作区中已有的 Prefab 修改，禁止覆盖用户变动。

### 测试

- 已新增 `Alpha015PrimaryAttributeTests`：派生公式、处理顺序、重复重建、资源比例保持和伤害属性边界。
- 阶段 B 新增 `Alpha015AffixCoverageTests`：23 属性覆盖、合法候选、固定种子和真实消费者。
- 已新增 `Alpha015MonsterAffixTests`：数量、权重、组互斥、子流隔离、实例属性与攻击修改器。
- 已新增 `Alpha015MonsterAffixPlayModeTests`：正式资产生成、接触伤害、防御、移动和世界标记。
- 回归 `DamageCalculatorTests`、`EquipmentPhase2Tests`、`Phase4ContentTests`、`RandomizationPhase3Tests`、`Alpha012–014` 专项测试。

## 验收矩阵

| 层级 | 必须证明的结果 |
| --- | --- |
| 纯逻辑 | 三项主属性派生准确且不重复；25 个物品词条和 10 个怪物词条固定种子可复现 |
| 配置 | 23/23 属性覆盖；所有词条有合法 ID、组、权重、范围、Scope、兼容物品或怪物池 |
| 战斗 | 元素附加、通用 / 类型伤害、暴击、命中、闪避、护甲和抗性进入正式伤害结果 |
| 资源 | 最大生命 / 法力与两类恢复真实生效，换装时继续保持当前资源比例 |
| 移动 | 玩家装备移动速度词条和怪物迅捷词条都改变对应 Controller 的真实速度 |
| 随机 | 固定根种子重放得到相同怪物定义、生命、词条、掷值、攻击和掉落；不同顶层通道互不扰动 |
| 表现 | 有词条怪物在未受伤时可辨认；零词条不显示空标记；文字不镜像、不遮挡生命条 |
| UI | 物品详情显示完整词条；当前属性按 `StatIds.All` 显示 23 项且换装后刷新 |
| 工程 | Runtime、Editor、EditMode、PlayMode 程序集通过，Main Console 无新增错误或警告 |
| 文档 | 属性、物品词条、怪物词条、随机化、内容池和配置字段均与实现一致 |

## 提交门禁

- 测试期间可以临时调整 Enter Play Mode Options，但测试完成、失败或中止后必须恢复 `m_EnterPlayModeOptions: 0`。
- 每次提交前检查 `ProjectSettings/EditorSettings.asset` 的实际值与 Git diff，禁止提交测试配置或无关 Unity 自动序列化差异。
- 不提交测试截图、临时 ScriptableObject、一次性调试菜单或未使用的怪物词条资产。
- 不读取或修改 `Docs/design/`；该目录现有变动属于用户内容。
- 执行前先检查三个怪物 Prefab 的现有未提交变动，所有绑定必须精准合并。

## 非目标

- 不实现定向打造、锁词条、排除词条或提高特定词条权重；这些属于 `alpha 0.1.6`，且正式打造仍不得直接指定词条。
- 不新增装备槽、怪物稀有度体系、Boss、远程怪物 AI、异常状态、持续伤害或行为触发词条。
- 不扩展 Chance / Trigger / Limit 占位操作。
- 不进行最终数值平衡；本阶段数值只用于验证机制、辨识度和可复现性。
- 不重构现有伤害计算顺序，不把怪物词条写成 `MonsterController` 中的专用数值分支。

## 完成定义

- 23 个公开属性都有真实消费者和至少一条正式物品词条路径，自动覆盖结果为 `23/23`。
- 正式物品词条池为 25 项，掉落与打造池同步，固定种子生成与 UI 显示一致。
- 三种正式怪物均能按实例根种子生成 `0–2` 条独立怪物词条。
- 元素伤害、元素抗性、生命、护甲、闪避和移动速度词条都真实改变运行时行为。
- 玩家无需打开调试面板即可识别带词条怪物，日志可解释词条来源、掷值和最终属性。
- 全量验证通过，文档同步完成，本计划归档，`alpha 0.1` 总计划进入 `alpha 0.1.6`。

## 完成记录

- 阶段 A：主属性派生专项 6/6 通过。
- 阶段 B：25 个物品词条覆盖 23/23 公开属性；三张掉落表与打造池同步完成。
- 阶段 C：怪物词条纯领域与确定性随机专项 6/6 通过。
- 阶段 D：怪物词条运行时与世界表现 PlayMode 2/2 通过。
- 阶段 E：正式内容专项 13/13、全量 EditMode 176/176 通过；全量 PlayMode 18 通过、2 个 Input System 上游既有用例跳过、0 失败。
- Main 固定种子 `24681357` 连续两次生成的前 12 个怪物定义、实例生命、词条、玩家 / 怪物攻击和掉落序列一致。
- Runtime、Editor、EditMode、PlayMode 四个程序集顺序构建均为 0 错误；仅保留项目依赖已有的 `System.Threading.Tasks.Extensions` 版本冲突警告。
- `ProjectSettings/EditorSettings.asset` 保持 `m_EnterPlayModeOptions: 0`，未产生 ProjectSettings 变更。
