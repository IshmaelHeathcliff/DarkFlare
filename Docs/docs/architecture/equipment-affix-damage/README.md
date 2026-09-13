# 装备、词条与伤害作用流程

基于 2026-09-13 当前工作区实现分析。此图展示投射物命中路径，装备重建是准备阶段，不表示每次攻击都重新穿戴。

- [交互流程图](./equipment-affix-damage.html)
- [图源](./equipment-affix-damage.workflow.json)
- [交付回执](./equipment-affix-damage.delivery.json)
- [浏览器回执](./equipment-affix-damage.visual-check.json)
- [明暗主题截图](./equipment-affix-damage.visual-check.html)

## 四个生效时机

| 时机 | 实际行为 | 核心代码（相对项目根目录） |
| --- | --- | --- |
| 生成 / 打造物品 | 物品保存隐式、前后缀及已掷出的 ModifierInstance。生成按物品标签、等级、稀有度容量、重复定义和互斥组筛选，按权重选择；不足以完整生成则失败。词条不是每次攻击重新抽取。 | `Assets/Scripts/Runtime/Gameplay/Items/ItemGenerator.cs`、`AffixGenerationUtility.cs` |
| 穿戴 / 卸下 | 校验槽位、实例与背包空间，更新 Loadout，再由 StatusSystem 协同重建装备修改器及 ProvidedStatus；失败回滚，成功后发送背包与装备事件。 | `Assets/Scripts/Runtime/Gameplay/Combat/EquipmentSystem.cs`、`Gameplay/Statuses/StatusCombatBinding.cs` |
| 发射攻击 | 选取基础伤害来源，按攻击随机子流掷出基础包，复制攻击者属性、修改器和标签。武器来源额外收集 Weapon 槽 LocalItem。投射物飞行途中换装不会改变已冻结数据。 | `Assets/Scripts/Runtime/Gameplay/Combat/AttackSnapshotFactory.cs`、`AttackSnapshot.cs` |
| 命中目标 | 读取当前目标属性、修改器和标签，先做命中 / 暴击判定，再算伤害与承伤；目标在飞行期间改变防御会影响本次结果。 | `Assets/Scripts/Runtime/Gameplay/Combat/CombatSystem.cs`、`HitResolution.cs`、`DamageCalculator.cs` |

## 词条分流

`EquipmentEffectResolver` 遍历完整十槽；隐式、前缀和后缀均转为带物品 / 槽位来源的修改器。

- 非伤害 `GlobalActor`：由 `CombatStatResolver` 筛选，`StatAggregator` 处理 Flat / Override，再加总 Increase、逐个 More，最后派生力量→生命、敏捷→命中/闪避、智力→法力。每次从基础属性重建，资源上限变化保持当前比例。
- 伤害 `GlobalActor` / `Skill`：保留在角色修改器中，攻击时冻结、伤害时按包消费，避免静态属性和伤害管线重复计算。
- `LocalItem`：不进入角色修改器聚合；只从主武器加入攻击快照。非武器上的该 Scope 被忽略并记录 Warning。**当前它与全局伤害词条共用计算阶段，没有单独的本地乘区。**
- `TargetTaken`：保存在拥有者的角色修改器中，当拥有者成为防御者时使用。攻击者身上的 TargetTaken 不会因此让对方承受更多伤害。
- `Temporary` / `Area`：不因存在枚举就自动生效；不要视作装备收集器已支持的作用域。

## 命中后的计算顺序

1. **Conversion**：对原始包执行一轮转换。总转换超过 100% 时按比例归一化；不会继续递归转换新包。
2. **GainAsExtra**：基于转换后的包追加额外伤害，新增包不参与本轮再次获得。
3. **Flat → Increase → More**：每个包使用结构化标签上下文筛选，StatId 还必须匹配通用伤害或当前类型伤害。
4. **暴击倍率**：一次攻击共用一次暴击结论，倍率为 `1 + critical_damage / 100`，该函数本身不裁剪负值。
5. **TargetTaken**：目标匹配的 Increase 与 More 当前均独立相乘，不采用来源端的 Increase 加算规则。
6. **类型防御**：按最终类型合并；物理命中使用护甲，火/冰/电/混沌使用对应抗性。
7. **提交**：扣血、资源通知、DamageResolvedEvent；有正数伤害才发送 ActorDamagedEvent，新死亡才发送 ActorDiedEvent，掉落订阅死亡事件。有效目标的未命中 / 闪避仍发布结算反馈，但无受伤事件。

伤害包的类型血统与最终类型是两个概念：物理转火可以同时匹配 Damage 标签域的 physical / fire，但防御只看最终火焰类型。`physical_damage` 这样的 StatId 仍按最终类型匹配，不能与标签血统混为一谈。

命中率是 `clamp(accuracy / (accuracy + evasion), 0.05, 0.95)`，命中不大于零时取 5%；命中与暴击使用独立随机子流，只有命中可暴击。

每包来源伤害（不含转换与额外获得的展开过程）：

```text
来源伤害 = (当前包数值 + 当前类型基础属性 + 匹配的 Flat)
         × (1 + 匹配 Increase 总和 / 100)
         × Π(1 + 每条 More / 100)
         × 暴击倍率（非暴击为 1）

目标承伤 = 来源伤害 × Π(1 + 每条匹配 TargetTaken Increase / More 的值 / 100)
物理减伤比例 = 护甲 / (护甲 + 10 × 合并后的物理目标承伤)
元素 / 混沌最终伤害 = 合并后的目标承伤 × (1 - clamp(抗性, -100, 75) / 100)
```

护甲或物理伤害不大于零时护甲减伤为零，最终类型伤害裁剪为非负。全局 Flat 当前在转换与额外获得之后逐包追加，不提前参与这两步，也不自动生成一个尚不存在的类型包。

## 两个可核对的例子

示例仅解释代码，不代表正式装备配置。

- 单包物理基础伤害 100，武器 LocalItem Increase 20%、全局 Increase 30%、More 20%、暴击额外 50%：`100 × 1.5 × 1.2 × 1.5 = 270`。目标无承伤词条、护甲 2700，则减伤 50%，最终 135。当前不是先乘局部 1.2 再乘全局 1.3。
- 物理基础伤害 100，40% 转火，然后获得物理的 10% 为混沌：转换后是物理 60、火焰 40，额外混沌为 6。没有其他修正时，火抗 25% 会使火焰部分变为 30；混沌不是按转换前的 100 得到 10。

## 边界与验证

无有效武器的武器来源技能拒绝发射；Skill 来源仍读取自身基础包。副手不提供第二份武器攻击来源。命中状态在伤害提交后、目标仍存活时施加。周期伤害已有独立入口，不判命中和暴击，物理周期跳过护甲，各跳读取当前目标承伤与抗性；自动计时和状态存档不是本图已实现能力。

本次修正模块文档中的独立 LocalItem 预结算、旧推荐伤害顺序及“状态周期完全未实现”等过时表述，没有修改运行时代码或公式。

验收：`diagram_type: workflow`；Showcase 9/9，0 错误、0 警告；四种桌面尺寸浏览器证据通过。视觉复核记录与精确规格 / HTML SHA-256 见回执和交付说明；本次为代码分析与文档生成，未运行 Unity 测试。

- `browser_evidence: passed`：1440×900、1600×1000、1920×1080、2048×1320 均无页面溢出。
- `visual_review: passed`：复核最终 2048×1320 浅色、1440×900 深色截图，节点、连线、图例与卡片完整；小屏可放大查看细节。
- `correction_rounds: 1`：将查看器的默认图例名称改为运行时计算、实例与快照、领域事件及反馈表现，并重新交付与验收。
- 规格 SHA-256：`0ac0c2c4d74bc5fe2b1719e7ad4172adcd586b7192273a2a124a125605aedc98`。
- HTML SHA-256：`8f3b0676316dd40701d94b05b97cd038b7a474494fa722d5d9be49d53c71b425`。
