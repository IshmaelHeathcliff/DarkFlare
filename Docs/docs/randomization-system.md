# 随机化与掉落规则

## 当前范围

阶段 3 为首版战斗建立统一、可复现的随机种子链，覆盖怪物生成位置、怪物实例生命、玩家攻击、怪物攻击和掉落。阶段 4 已在同一规则上接入三种怪物、三张掉落表和完整首批装备 / 词条池。

当前正式数值为：

| 内容 | 配置 |
| --- | --- |
| 基础怪物生命倍率 | `0.85–1.15` |
| 玩家空手技能伤害 | `10–14` 物理伤害 |
| 荒原游魂接触伤害 / 掉落 | `6–10` / `35%` |
| 裂爪猎犬接触伤害 / 掉落 | `4–7` / `30%` |
| 铁壳尸傀接触伤害 / 掉落 | `10–14` / `45%` |
| 大剑基础伤害 | `20–40` 物理伤害 |

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

## 怪物实例生命

`MonsterDefinition` 仍保存共享基础配置，不在运行时改写资产。生成流程为：

```text
MonsterSpawner
├── SpawnPosition 子种子 -> 生成位置
└── MonsterInstance 子种子
    └── SpawnMonsterCommand / SpawnSystem
        ├── 选择怪物定义
        └── MonsterDefinition.CreateInstanceData(seed)
            └── MonsterInstanceData
                ├── Seed
                ├── MaxHealth
                └── Stats
```

`CreateInstanceData` 根据生命倍率生成实例最大生命，并同步覆盖实例 `StatBlock` 中的 `max_health`。`MonsterController.Configure` 只消费实例数据，不修改 `MonsterDefinition`。

## 攻击随机

- `FireProjectileCommand` 从 `PlayerAttack` 通道取得一次种子，再由 `AttackSnapshotFactory` 生成发射时快照。
- `MonsterController` 每次有效接触攻击从 `MonsterAttack` 通道取得一次种子。
- `DamageRollDefinition` 只使用调用方传入的 `System.Random`，不直接调用 `UnityEngine.Random`。
- 同一攻击的基础伤害只在攻击发起时掷一次；投射物命中、换装或后续表现不会重新掷点。

正式技能和怪物配置不再依赖固定 `12` / `8` 伤害。代码仍保留这两个值作为空配置保护，避免错误配置直接阻断最小循环。

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
- 技能、怪物和武器伤害池为空。
- 伤害为负数或范围上下限倒置。
- 掉落概率超出 `0–1`。
- 掉落池为空、条目为空或不存在正权重物品。

对应配置资产在 `OnValidate` 中输出带资产上下文的 Warning，正式内容进入运行前应保持校验结果为空。

## 当前边界

- 当前有荒原游魂、裂爪猎犬和铁壳尸傀三种怪物；三者共享生命倍率规则，但使用独立基础生命、伤害、掉落表和 Prefab。
- 掉落仍是每只怪物最多一件，不含保底、多次抽取和独立稀有度抽取。
- 暴击、命中、闪避和异常状态随机不属于本阶段。
- 三种怪物使用独立视觉与 Animator，但继续共享阶段 3 建立的随机种子链和生命倍率规则。

## 验证记录

- Unity EditMode 全量 82/82 通过；生命范围、伤害范围、固定种子重放、通道隔离、掉落边界、`35%` 统计容差和正式资产配置均有覆盖。
- Unity PlayMode 共 11 项：9 项通过、2 项为项目既有 Ignore、0 失败。
- 固定种子 `24681357` 连续运行两次 `Main`，前三只怪物生命、玩家 / 怪物攻击和掉落结果完全一致；关闭固定种子后两次启动根种子不同。
- Runtime、Editor、EditMode 与 PlayMode 程序集编译 0 警告、0 错误。
- 阶段 4 将固定种子验证扩展到 Main 前 12 个实例；两次运行的怪物类型、实例生命、玩家 / 怪物伤害和掉落序列一致，并覆盖三种怪物。
- 阶段 6 全量 PlayMode 继续 0 失败；固定种子重放、默认随机种子变化、怪物生成与掉落路径均通过整合回归。

装备伤害快照见 [装备系统](./equipment-system.md)，完整池配置见 [首批内容池](./content-system.md)，伤害计算与词条语义见 [伤害系统与词条系统设计](./damage-affix-system.md)。
