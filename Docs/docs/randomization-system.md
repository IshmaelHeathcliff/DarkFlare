# 随机化与掉落规则

## 当前范围

统一随机种子链覆盖怪物生成位置、怪物实例生命与词条、玩家攻击、怪物攻击和掉落。`alpha 0.1.5` 在既有 `MonsterInstance` 顶层通道内增加实例子流，没有新增或推进其他顶层随机序列。

当前正式数值为：

| 内容 | 配置 |
| --- | --- |
| 基础怪物生命倍率 | `0.85–1.15` |
| 玩家初始大剑伤害 | `20–40` 物理伤害 |
| 荒原游魂接触伤害 / 掉落 | `6–10` / `35%` |
| 裂爪猎犬接触伤害 / 掉落 | `4–7` / `30%` |
| 铁壳尸傀接触伤害 / 掉落 | `10–14` / `45%` |
| 战斧基础伤害 | `26–34` 物理伤害 |

## 根种子与独立通道

`GameplayRandomSystem` 由 `GameArchitecture` 注册。`CombatPrototypeBootstrap` 在预热资源前调用：

```csharp
GameplayRandomSystem.Configure(bool useFixedSeed, int fixedSeed)
```

- 默认关闭固定种子，每次启动生成新的根种子。
- 开启后完整复用 Inspector 中的固定种子，默认调试值为 `12345`。
- 根种子会写入启动日志，便于复现问题。
- `NextSeed(GameplayRandomChannel channel)` 为每个通道维护独立序列。

当前通道：

| 通道 | 用途 |
| --- | --- |
| `SpawnPosition` | 怪物生成方向、边界内重试和回退位置 |
| `MonsterInstance` | 刷怪池选择与怪物实例数据 |
| `PlayerAttack` | 玩家投射物发射时伤害快照 |
| `MonsterAttack` | 怪物接触攻击伤害快照 |
| `Loot` | 是否掉落、条目权重和物品生成种子 |

某个通道新增随机调用不会推进其他通道，因此增加刷怪位置重试不会改变后续攻击或掉落结果。

## 怪物实例生命与词条

`MonsterDefinition` 仍保存共享基础配置，不在运行时改写资产。生成流程为：

```text
MonsterSpawner
├── SpawnPosition 子种子 -> 生成位置
└── MonsterInstance 子种子
    └── SpawnMonsterCommand / SpawnSystem
        ├── 选择怪物定义
        └── MonsterDefinition.CreateInstanceData(seed)
            ├── HealthSeed -> 生命倍率
            ├── AffixCountSeed -> 词条数量 `0–2`
            ├── AffixSelectionSeed -> 按权重、同组无放回选择
            ├── 每个词条 ID 派生独立 ValueSeed -> 修改器掷值
            └── MonsterInstanceData
                ├── BaseMaxHealth / BaseStats
                ├── Affixes / Modifiers
                └── EffectiveStats / MaxHealth
```

`MonsterInstanceRandomSeeds` 使用稳定混合和稳定字符串哈希派生子种子。词条内部增加随机调用不会改变生命、选择或其他词条的值；同一根种子会重放相同生命、词条 ID、掷值和最终属性。`MonsterController.Configure` 只消费实例数据，不修改共享 `MonsterDefinition`。

`MonsterAffixGenerator` 只接受正权重定义，按权重选择后移除同 `GroupId` 的全部候选。正式三种怪物使用完整 10 词条池与 `0–2` 数量范围；配置验证会拒绝负数量、倒置范围、空池、重复引用或上限超过可用互斥组数量。

## 攻击随机

- `FireProjectileCommand` 先验证伤害来源，只有可以发射时才从 `PlayerAttack` 通道取得一次根种子。
- `MonsterController` 只有存在合法接触伤害配置时才从 `MonsterAttack` 通道取得一次根种子。
- `DamageRollDefinition` 只使用调用方传入的 `System.Random`，不直接调用 `UnityEngine.Random`。
- 同一攻击的基础伤害只在攻击发起时掷一次；投射物命中、换装或后续表现不会重新掷点。

每个攻击根种子通过稳定混合函数派生互不干扰的 `BaseDamageSeed`、`HitRoll` 和 `CriticalRoll`。增加基础伤害包或改变其随机调用次数不会改变同一次攻击的命中与暴击判定。

正式技能和怪物配置不再依赖固定 `12` / `8` 伤害，也没有代码级空配置保护。武器来源无效时不消耗攻击根种子；正式内容由配置验证和开局初始武器保障闭环。

## 掉落判定

`LootTableDefinition.DropChance` 只决定“是否掉落”，原有条目权重只决定“成功后掉什么”：

```text
怪物死亡
-> Loot 通道子种子
-> DropChance 判定
   ├── 失败：返回 null，不创建世界物体
   └── 成功：条目权重选择 -> 物品种子 -> ItemGenerator
```

掉落实例 ID、物品种子和调试日志均由掉落子种子稳定生成。合法无掉落不会修改背包，也不会发送额外成功事件。

## 配置校验

`RandomizationConfigurationValidator` 与现有装备校验共同检查：

- 生命倍率非正数或上下限倒置。
- Skill 来源技能、怪物和武器伤害池为空，或武器来源技能错误保留技能基础伤害。
- 伤害为负数或范围上下限倒置。
- 掉落概率超出 `0–1`。
- 掉落池为空、条目为空或不存在正权重物品。

对应配置资产在 `OnValidate` 中输出带资产上下文的 Warning，正式内容进入运行前应保持校验结果为空。

## 当前边界

- 当前有荒原游魂、裂爪猎犬和铁壳尸傀三种怪物；三者共享生命倍率规则，但使用独立基础生命、伤害、掉落表和 Prefab。
- 掉落仍是每只怪物最多一件，不含保底、多次抽取和独立稀有度抽取。
- 命中、闪避和暴击已经使用攻击内独立子流；异常状态随机尚未接入。
- 三种怪物使用独立视觉与 Animator，但继续共享阶段 3 建立的随机种子链和生命倍率规则。

## 验证记录

- Unity EditMode 全量 82/82 通过；生命范围、伤害范围、固定种子重放、通道隔离、掉落边界、`35%` 统计容差和正式资产配置均有覆盖。
- Unity PlayMode 共 11 项：9 项通过、2 项为项目既有 Ignore、0 失败。
- 固定种子 `24681357` 连续运行两次 `Main`，前三只怪物生命、玩家 / 怪物攻击和掉落结果完全一致；关闭固定种子后两次启动根种子不同。
- Runtime、Editor、EditMode 与 PlayMode 程序集编译 0 警告、0 错误。
- 阶段 4 将固定种子验证扩展到 Main 前 12 个实例；两次运行的怪物类型、实例生命、玩家 / 怪物伤害和掉落序列一致，并覆盖三种怪物。
- 阶段 6 全量 PlayMode 继续 0 失败；固定种子重放、默认随机种子变化、怪物生成与掉落路径均通过整合回归。
- alpha 0.1.5 怪物词条随机合同专项 6/6、Main 随机化 PlayMode 2/2 通过。固定根种子 `24681357` 连续两次得到相同的前 12 个怪物定义、生命、词条 ID / 掷值、玩家 / 怪物攻击和掉落序列；默认随机启动仍会更换根种子。

装备伤害快照见 [装备系统](./equipment-system.md)，完整池配置见 [首批内容池](./content-system.md)，伤害计算与词条语义见 [伤害系统与词条系统设计](./damage-affix-system.md)，配置字段见[角色、物品与攻击配置参考](./config-reference/combat-content.md)。
