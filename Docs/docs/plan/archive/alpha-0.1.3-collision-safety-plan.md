# alpha 0.1.3 碰撞与怪群安全执行计划

> 状态：已完成并归档
> 建立日期：2026-08-11
> 最近更新：2026-08-11
> 所属版本：`alpha 0.1`
> 前置阶段：`alpha 0.1.2` 伤害判定与武器伤害源已完成
> 相关模块：[首版玩法循环](../../gameplay-loop.md)、[伤害系统与词条系统](../../damage-affix-system.md)、[战斗内容配置参考](../../config-reference/combat-content.md)

## 目标

本阶段只解决怪群实体碰撞把玩家挤住、卡住的问题，不增加受击无敌帧或全局伤害限频：

1. 玩家可以穿过怪物群，不再被玩家—怪物或怪物—怪物的实体接触阻断。
2. 玩家和怪物仍受世界边界与后续世界阻挡约束。
3. 怪物不依赖物理挤压分散，而使用有上限、可复现的软分离转向。
4. 现有接触伤害继续由每只怪物独立的伤害半径、攻击间隔、命中、闪避和伤害结算控制。
5. 投射物、掉落拾取、商人和打造交互 Trigger 不因 Layer 迁移回归。

## 当前事实

- `Player.prefab` 与三个怪物 Prefab 的根对象均在 `Default` 层，使用 Dynamic `Rigidbody2D`、非 Trigger `CircleCollider2D` 和 `0.4` 本地半径。
- `ProjectSettings/Physics2DSettings.asset` 当前是全层互撞；`TagManager.asset` 没有项目自定义 Layer。
- `WorldBounds` 是 `Main.unity` 中闭合的非 Trigger `EdgeCollider2D`，范围为 `-16..16`，当前也在 `Default` 层。
- 两层地表 Tilemap 没有 Collider；当前正式世界阻挡只有 `WorldBounds`。
- 玩家和怪物都在 `FixedUpdate` 中直接设置 `Rigidbody2D.linearVelocity`。怪物始终朝玩家中心移动，没有停止距离或邻居分离。
- 怪物接触伤害不依赖 `OnCollision` 或 `OnTrigger`：`MonsterController` 按中心距离与独立攻击间隔调用 `ApplyDamageCommand`。
- 三种怪物的接触半径均为 `0.75`；荒原游魂、裂爪猎犬、铁壳尸傀的攻击间隔分别为 `0.75 / 0.55 / 1.0` 秒。进入范围时立即尝试一次，Miss / Evade 仍消耗该怪物自身冷却。
- 每只怪物独立计时，多只怪物允许在同一物理帧分别结算；本阶段不合并这些攻击，也不增加玩家全局受伤冷却。
- 正式刷怪表最多同时存在 `12` 只怪物，因此邻居软分离的最坏比较规模为每物理帧 `144` 对。
- 投射物、掉落拾取和世界交互均使用 Trigger 回调，并通过 `CombatActor` 或交互目标类型过滤；层迁移必须保留它们与玩家 / 怪物的触发关系。

## 范围

### 纳入本阶段

- Physics2D Layer 与碰撞矩阵合同。
- 玩家、三种怪物 Prefab 和 `WorldBounds` 的 Layer 迁移。
- 怪物接近停止、同阵营软分离和完全重叠时的稳定退让。
- 怪物移动参数配置、验证器、自动化合同和调试信息。
- EditMode / PlayMode 回归与相关模块文档同步。

### 不纳入本阶段

- 玩家或怪物受击无敌帧、全局伤害冷却、同帧伤害合并或其他免疫窗口。
- `CombatSystem`、`DamageResult`、命中 / 闪避 / 暴击、接触伤害间隔与伤害数值重构。
- 击退、硬直、格挡、翻滚、主动闪避、眩晕和异常状态。
- NavMesh、寻路、障碍绕行、流场或 DOTS 群集模拟。
- 精英怪特殊碰撞体、怪物词条和新的地图障碍。
- 美术、受击特效、镜头震动、音效、法力、技能消耗与恢复。

## 冻结合同

### Layer 与碰撞矩阵

新增三个按名称解析的 Layer，不在代码中硬编码数值编号：

| Layer | 对象 | 职责 |
| --- | --- | --- |
| `PlayerActor` | 玩家 Prefab 整个层级 | 玩家移动实体和接收 Trigger |
| `MonsterActor` | 三个怪物 Prefab 整个层级 | 怪物移动实体和投射物目标 |
| `WorldObstacle` | `WorldBounds` 及后续正式世界阻挡 | 阻止角色离开可玩区域 |

必须冻结以下关系：

| Layer 对 | 是否产生 2D 接触 | 原因 |
| --- | --- | --- |
| `PlayerActor` ↔ `MonsterActor` | 否 | 玩家必须能穿出怪群；接触伤害按距离判定 |
| `MonsterActor` ↔ `MonsterActor` | 否 | 禁止物理挤压、连锁推力和求解器抖动 |
| `PlayerActor` ↔ `PlayerActor` | 否 | 当前单玩家，不建立无意义实体接触 |
| `PlayerActor` ↔ `WorldObstacle` | 是 | 玩家不能穿越世界边界 |
| `MonsterActor` ↔ `WorldObstacle` | 是 | 怪物不能离开可玩区域 |
| `PlayerActor` ↔ `Default` | 是 | 保留掉落和世界交互 Trigger |
| `MonsterActor` ↔ `Default` | 是 | 保留默认层投射物 Trigger |

迁移只修改上述必要关系，不重置整个 Layer Collision Matrix。禁止在每个实例上调用 `Physics2D.IgnoreCollision` 建立难以审计的运行时例外。

### 怪物接近与软分离

- 接触伤害继续由中心距离和 `ContactDamageInterval` 决定，不新增接触 Trigger 或碰撞回调伤害入口。
- `MonsterDefinition` 增加并文档化三个移动参数：
  - 接近停止距离：`0.6`，必须大于等于 `0` 且不大于接触伤害半径。
  - 软分离半径：`0.8`，与当前两个 `0.4` 本地碰撞半径之和一致。
  - 软分离权重：`0.65`，合法范围 `0..2`。
- 目标距离小于等于停止距离时不再继续穿过玩家中心，只保留软分离速度。停止距离仍位于 `0.75` 接触伤害半径内，因此不会取消持续接触攻击。
- 邻居只读取存活的 `MonsterActor`；分离向量随距离衰减，不改变角色配置的最终移动速度上限。
- 完全重叠且差向量为零时，使用 `MonsterInstanceData.Seed` 派生稳定退让方向，禁止使用每帧随机数。
- 通过 QFramework Query 获取同阵营只读列表；Controller 不直接修改 `CombatModel`。
- 当前上限为 12 只怪物，允许简单的有界 `O(n²)` 邻居比较；热路径禁止 LINQ、`Physics2D.Overlap*All` 和逐邻居临时集合。
- 空间移动调整不得重置、同步或合并 `_lastContactDamageTime`。每只怪物仍按自身配置独立攻击。

## 预计代码与资产变更

### Runtime

- `MonsterDefinition`：新增停止距离、分离半径和分离权重。
- `MonsterController`：接近停止、稳定软分离和速度上限。
- 新增同阵营只读 Query；保持 Controller 只通过 Query 读取 `CombatModel`。
- 新增纯逻辑转向计算器，便于无 Unity 物理场景的确定性测试。
- `RandomizationConfigurationValidator` / `ContentConfigurationValidator`：验证新增配置范围和相互约束。
- 不修改 `CombatSystem`、`CombatActor`、`HitResolutionCalculator` 或 `DamageResult`。

### ProjectSettings、Prefab 与场景

- `TagManager.asset`：注册 `PlayerActor`、`MonsterActor`、`WorldObstacle`。
- `Physics2DSettings.asset`：应用冻结的碰撞矩阵。
- `Player.prefab`：整个层级迁移到 `PlayerActor`。
- 三个怪物 Prefab：整个层级迁移到 `MonsterActor`。
- `Main.unity/WorldBounds`：迁移到 `WorldObstacle`。
- 三个怪物定义：写入统一的 `0.6 / 0.8 / 0.65` 初始移动参数。

ProjectSettings、Prefab、ScriptableObject 和已打开场景的迁移必须使用 Unity Editor API / Unity MCP。若使用一次性迁移工具，必须满足精准目标、二次执行零变化、验证通过后删除；禁止直接编辑 `Main.unity` YAML 或手工点击形成不可复现步骤。

### 文档

- 更新[首版玩法循环](../../gameplay-loop.md)的角色移动、碰撞和接触伤害流程。
- 更新[战斗内容配置参考](../../config-reference/combat-content.md)的怪物移动参数。
- 明确接触伤害仍由每只怪物独立间隔结算，没有玩家全局受伤冷却。
- 完成后把本计划归档并将总计划切换到 `alpha 0.1.4`。

## 实施顺序

### 阶段 0：迁移前特征冻结

1. 补充 Prefab Layer、Collider、Rigidbody 和 Physics2D Matrix 的当前特征测试。
2. 冻结接触伤害不依赖 Collision / Trigger 回调、每只怪物独立计时的事实。
3. 冻结投射物、掉落、世界交互与 `WorldBounds` 的现有触发 / 阻挡路径。
4. 保持 `alpha 0.1.2` 全量回归通过。

验收：在任何 Layer 迁移前，测试能准确描述当前全为 `Default`、全层互撞和接触攻击独立计时的事实。

### 阶段 1：碰撞层合同与精准迁移

1. 建立 Layer 名称常量与 Editor 合同验证器。
2. 通过 Unity API 新增三个 Layer 并只修改冻结的碰撞对。
3. 精准迁移四个 Actor Prefab 和 `WorldBounds`。
4. 验证玩家 / 怪物继续与 Default Trigger 和世界边界交互。

验收：角色之间无物理解算，角色与 `WorldBounds` 仍发生实体阻挡，投射物、拾取和交互 Trigger 不回归。

### 阶段 2：怪物停止距离与软分离

1. 增加怪物移动配置与验证器。
2. 增加同阵营只读 Query 和纯逻辑转向计算。
3. 接入 `MonsterController.FixedUpdate`，使用实例种子处理完全重叠。
4. 精准迁移三种正式怪物配置。

验收：12 只怪物不会通过物理挤压玩家，也不会长期完全叠为一个中心点；任何怪物速度不超过配置移动速度，接触攻击频率保持原配置。

### 阶段 3：文档与综合回归

1. 更新配置参考与模块文档。
2. 运行 Layer / Prefab / Scene 合同、全量 EditMode、PlayMode 和程序集构建。
3. 在 Main 中完成怪群脱困、边界阻挡、接触攻击频率和 Trigger 实机验证。
4. 归档计划并把当前阶段切换到 `alpha 0.1.4`。

## 测试矩阵

### 纯逻辑与 EditMode

- 12 个邻居位置下软分离结果可复现，速度上限不被突破；完全重叠使用实例种子得到稳定方向。
- 目标进入 `0.6` 停止距离后追逐分量为零，但仍处于 `0.75` 接触伤害半径内。
- 接近、分离与完全重叠三种情况不修改怪物攻击冷却状态。
- 三个 Layer 存在，必要碰撞对与冻结矩阵一致，其余关系未被迁移器意外覆盖。
- 四个 Actor Prefab 及其层级、`WorldBounds` Layer、Collider 类型和 Trigger 状态正确。
- 三种怪物新增配置字段通过内容扫描，非法范围或停止距离大于伤害半径会被拒绝。
- 现有命中、闪避、伤害结果和事件特征测试保持不变。

### PlayMode

- 12 只怪物围住玩家时，玩家沿四个主方向的位移至少达到无怪基线的 `90%`，没有实体碰撞卡死。
- 怪物和玩家持续朝边界移动后，Collider Bounds 仍留在 `WorldBounds` 内。
- 多只怪物处于接触范围时各自按 `ContactDamageInterval` 独立尝试攻击，不出现统一限频、冷却互相重置或漏攻击。
- 荒原游魂、裂爪猎犬、铁壳尸傀在持续接触下分别保持约 `0.75 / 0.55 / 1.0` 秒的尝试间隔，允许 `FixedUpdate` 量化误差。
- 玩家仍能拾取掉落、进入商人 / 打造交互；投射物仍能命中怪物并在命中后销毁。
- Main 中怪物生成、相机边界、Tilemap、装备、交易、打造、掉落和 Addressables 释放保持通过。

### 工程回归

- Runtime、Editor、EditMode、PlayMode 程序集零错误、零警告。
- 全量 EditMode 与 PlayMode 零失败；`alpha 0.1.2` 的 141 项 EditMode 和 11 项 PlayMode 基线不得回退。
- Unity Console 没有新增错误；既有负向测试日志必须仍由测试显式声明。
- ProjectSettings 与资产迁移二次执行零变化，Git 空白和文档链接检查通过。

## 风险与控制

- **Layer 迁移破坏 Trigger**：显式保留 Actor ↔ Default，增加投射物、拾取和交互 PlayMode 回归。
- **怪物穿过玩家后反复折返**：进入停止距离后关闭追逐分量，只保留软分离。
- **怪物完全重叠无法分离**：使用实例种子派生稳定方向，不使用帧随机。
- **软分离改变移动速度**：组合方向后统一归一化并乘配置速度，不叠加额外速度。
- **空间逻辑意外改变伤害频率**：保留独立 `_lastContactDamageTime`，增加持续接触间隔回归测试。
- **直接编辑项目设置或场景**：实施时统一走 Unity API / MCP，并以二次迁移幂等验证约束工具。
- **跨入后续范围**：不实现无敌帧、击退、格挡、法力、恢复或怪物词条。

## 完成记录

- 运行时新增按名称解析的 Layer 常量、同阵营只读 Query 和纯逻辑 `MonsterSteeringCalculator`；`MonsterController` 接入停止距离与有上限软分离，没有修改伤害系统或独立接触冷却。
- `MonsterDefinition` 新增并在三种正式资产中显式保存 `0.6 / 0.8 / 0.65`；随机化配置验证器检查停止距离、分离半径、分离权重及接触参数。
- 永久编辑器校验覆盖三个 Layer、七组碰撞关系、四个 Actor Prefab 整个层级，以及 `Main.unity/WorldBounds` 的 Layer 和非 Trigger `EdgeCollider2D`。
- 一次性 Unity Editor 迁移首次产生 17 项受控变更，二次执行为 0；10 个 ProjectSettings、Prefab、怪物资产与场景目标复跑前后 SHA-256 完全一致，随后删除迁移入口。
- EditMode 定向 `6/6` 通过；PlayMode 定向 `2/2` 通过，覆盖 12 只怪物、四个主方向 90% 位移、世界阻挡及 Player / Monster 对 Default Trigger 的触发。
- 最终全量 EditMode `148/148` 通过；PlayMode `17` 项中 `15` 项通过、`2` 项为 Input System 上游既有忽略、零失败。
- Core、Runtime、Editor、EditMode、PlayMode 五个项目程序集均构建成功且零错误；新增测试程序集零警告。依赖链仍报告本阶段前已存在的 Unity 包源码与 UI 过时 API / 未使用字段警告，本阶段没有新增编译警告。
- 模块文档、内容配置参考、内容池与 alpha 总计划已同步，当前开发入口切换到 `alpha 0.1.4` 规划。

## 完成定义

- 玩家—怪物和怪物—怪物没有实体阻挡，12 只怪物包围时玩家仍能稳定脱离。
- 玩家和怪物不能穿越 `WorldBounds`，现有投射物、拾取与交互 Trigger 均正常。
- 怪物使用停止距离和稳定软分离，不依赖物理挤压处理群聚。
- 三种怪物持续接触攻击继续由各自 `0.75 / 0.55 / 1.0` 秒间隔独立结算，没有新增玩家全局免疫或限频。
- 配置、ProjectSettings、Prefab 与场景通过自动合同验证，迁移可重复且无漂移。
- 全量测试与程序集构建零失败，模块文档同步，计划归档，总计划进入 `alpha 0.1.4`。
