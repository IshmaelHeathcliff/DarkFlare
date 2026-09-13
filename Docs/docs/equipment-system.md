# 装备系统

状态阶段 3 提供 `ItemBaseDefinition.ProvidedStatus` 可选持续来源，穿脱、槽位移动与恢复共同提交装备和状态投影；失败恢复背包原格位。来源配置与生命周期见[异常与来源](./status-ailments.md)。

alpha 0.4 起，装备是[统一物品系统](./item-system.md)中的一种分类，仍保留武器 / 护甲 / 饰品子类型和十槽合同；装备不堆叠，金币与材料不参与穿戴或装备属性结算。

## 模块职责

装备系统负责十槽穿戴状态、背包与装备之间的原子交换、装备效果聚合，以及武器攻击来源的发射时快照。槽位身份保持稳定，与视觉排列解耦：

- `Weapon = 0`：主武器；`Armor = 1`：身体。
- `RingLeft = 2`、`RingRight = 3`：左、右戒指。
- `Head = 4`、`Hands = 5`、`Legs = 6`：头部、手部、腿部。
- `OffHand = 7`、`Necklace = 8`、`Belt = 9`：副手、项链、腰带。

Mask 使用对应编号的位。`Defenses` 包含身体、头部、手部、腿部、副手；`Accessories` 包含双戒指、项链、腰带。护甲与饰品分别只能使用对应集合的非空子集，未知位和跨类组合拒绝。

`ItemType` 只表示物品类别，实际可装备位置由 `ItemBaseDefinition.AllowedEquipmentSlots` 决定。运行时统一通过 `CanEquipTo(slot)` 检查兼容性，不能再只根据 Weapon / Armor / Accessory 推断目标槽。

## 数据与运行时结构

| 类型 | 职责 |
| --- | --- |
| `EquipmentSlot` / `EquipmentSlotMask` | 定义稳定槽位和可序列化兼容掩码 |
| `EquipmentLoadout` | 保存单个 Actor 的完整槽位状态，并阻止同一实例重复占用多个槽 |
| `EquipmentModel` | 保存 `CombatActor -> EquipmentLoadout` 映射并提供按槽查询 |
| `EquipmentSystem` | 执行装备、替换、卸下事务并重建角色装备效果 |
| `EquipItemCommand` / `UnequipItemCommand` | 保留自动首空位回包的按钮兼容入口 |
| `EquipItemFromGridCommand` / `UnequipItemToGridCommand` | 拖拽使用的精确来源 / 目标位置事务 |
| `MoveEquippedItemCommand` | 双戒指槽位移动或原子交换 |
| `GrantStartingWeaponCommand` | 幂等授予普通 1 级初始武器并原子装备 |
| `EquipmentChangedEvent` | 在完整提交后发布 Actor、槽位、旧物品和新物品 |

旧的双参数 `EquipItemCommand(actor, item)` 仅作为武器槽兼容入口保留；新 UI 和后续业务应显式传入目标槽。

## 原子事务

首次装备、同槽替换和卸下都由 `EquipmentSystem` 统一处理：

1. 验证 Actor、物品、目标槽、背包归属和重复实例。
2. 通过 `InventoryGrid.CanAdd / CanExchange` 预检完整空间。
3. 使用 `InventoryModel` 无事件入口提交格子状态，再提交 Loadout。
4. 从全部槽位重新收集装备效果并刷新 Actor 属性。
5. 所有状态完成后发送背包事件，最后发送 `EquipmentChangedEvent`。

任一步失败时，背包、装备槽、角色属性和成功事件数量都保持不变。替换时，新物品原占用格会参与旧物品回包空间计算；卸下时背包空间不足会直接拒绝。

## 装备效果

`EquipmentEffectResolver` 每次从完整 Loadout 重建效果，避免逐件增减造成旧词条残留。

- `LocalItem` 只从 Weapon 槽物品收集，在发射时加入攻击快照；当前与 `GlobalActor` / `Skill` 一起进入统一伤害管线，并没有独立的武器本地预结算阶段。
- `GlobalActor`、`Skill`、`TargetTaken` 等已支持 Scope 进入角色修改器集合。
- 护甲、抗性等非伤害属性由 `CombatStatResolver` 聚合到 Actor 有效属性；直接修改器处理完成后，再统一派生力量对应的最大生命、敏捷对应的命中 / 闪避和智力对应的最大法力。
- 最大生命统一使用稳定 ID `max_health`。`CombatActor.MaxHealth`、当前生命和 HUD 均读取聚合后的有效属性；穿脱装备时保持当前生命比例，避免通过反复换装恢复生命。
- 基础伤害、类型伤害、转换、额外获得、Increase 和 More 仍由伤害管线按标签处理，不能在静态属性层重复应用。
- 非武器上的 `LocalItem` 会被忽略并给出配置 Warning。

`EquipmentConfigurationValidator` 同时校验物品基底和词条配置，包括空稳定 ID、类别与槽位不匹配、未知 `StatId`，以及 LocalItem 出现在非武器配置等问题。正式内容进入装备池前应先通过该校验。

## 武器伤害与攻击快照

`ProjectileSkillDefinition.DamageSource` 决定投射物使用技能基础伤害还是已装备武器。当前基础投射物配置为武器来源：

- 有有效武器时，使用 Weapon 槽的基础伤害和武器 LocalItem 词条；大剑当前基础范围为 20–40 物理伤害。
- 战斧使用 26–34 物理伤害；两把武器都通过对应子类与 `physical` 标签筛选词条。
- 无有效武器时，武器来源技能构建失败，不生成投射物、不发送攻击事件、不消耗玩家攻击种子；真正的 `Skill` 来源仍可使用自己的配置伤害。
- `CombatPrototypeBootstrap` 在启用刷怪器前幂等授予并装备普通 1 级大剑；失败时保持刷怪器关闭，避免进入无法反击的软锁。
- `AttackSnapshotFactory` 从一次攻击根种子派生基础伤害、命中和暴击子流，并冻结来源物品 ID、已掷出的伤害包、标签、攻击者属性和修改器。
- `ProjectileController` 命中时只补充当前防御者快照；飞行途中换装、卸装或修改攻击者词条不会改变已发出的投射物。

即时伤害也在 Command 建立时转换为攻击快照，避免执行链中继续读取可变攻击者状态。

## 背包 UI

独立背包窗口展示十个真实槽位，并提供候选详情、按住比较、装备 / 替换和卸下操作。装备区按人物部位排列，槽位尺寸不小于对应物品占格：头手腿 2×2，护甲 / 武器 / 副手 2×3，项链 1×2，腰带 2×1，戒指 1×1；键鼠与手柄按实际布局寻找空间邻居。默认左 Shift / 手柄左扳机对比兼容槽位的已装备物品，双戒指同时呈现左右物品信息，空槽与自身不参与比较。详见[物品工作台](./item-ui-workbench.md)。

- 唯一兼容槽通过 `EquipmentSlots.GetUniqueTarget` 自动确定，项链、腰带等新部位沿用相同规则。
- 多目标配置不会隐式覆盖戒指；必须明确选择左戒指或右戒指。
- 比较只展示能可靠解释的基础伤害范围与直接数值差异；条件词条、转换和额外获得保留原文，不生成虚假战力分。
- 成功或失败后会刷新快照并恢复合理选择和焦点，键鼠与手柄共享同一目标槽状态。
- 背包装备可拖到兼容槽位；拖动、右键、动作菜单及装备按钮统一使用来源格交换，替换时旧装备必须精确回填来源区域，受阻不自动另找位置。装备可拖回指定背包格，左右戒指可移动或交换；任一步失败均整笔回滚。

HUD 不再展示武器或装备摘要，完整槽位信息集中在背包。背包内可展开的“当前属性”区域直接读取
`CombatActor.Stats` 的最终有效值，换装后随 `EquipmentChangedEvent` 刷新护甲、闪避、移动速度、
暴击率与四类抗性。

## 验证记录

- 当前 EditMode 覆盖完整十槽、旧四槽兼容、重复实例、替换 / 卸下回滚、初始武器幂等事务、事件最终态、效果聚合、最大生命与 HUD 同步、属性配置校验和快照深复制。
- 当前全量仅运行项目两个测试程序集；alpha 0.3.4 的真实计数、双输入、三语言和 Windows Player 证据见[综合验收](./assets/acceptance/alpha-0.3.4-equipment/README.md)。下列早期阶段记录保留当时的内容范围。
- 新增端到端用例覆盖键盘装备武器 / 护甲、最大生命装备后的 HUD 数值、手柄显式选择左右戒指、卸下、1920×1080 布局边界，以及在途投射物换装仍使用旧快照。
- `DarkFlare.Runtime`、`DarkFlare.Editor`、EditMode 与 PlayMode 测试程序集编译通过。
- 阶段 4 使用正式资产完成大剑、皮甲、铁指环和翡翠戒指的购买、武器 / 护甲 / 双戒指装备、卸下、武器打造与出售路径。
- 阶段 6 继续使用正式七件装备完成交易、打造、四槽装备与卸下回归；全量 PlayMode 0 失败，1920×1080 背包中的槽位、详情和比较区域均在面板边界内。
- alpha 0.1.0 新增精确装备 / 卸下、戒指交换与失败回滚的纯逻辑覆盖，并通过真实菜单三档分辨率回归。

## 当前边界

- 正式内容包含二十件独立基底：八个非戒指部位各两件，双戒指共享四件戒指；详细数值和词条兼容见[首批内容池](./content-system.md)。
- 不支持耐久、套装、唯一装备效果或完整纸娃娃；完整登记槽位进入 `auto` 存档，当前人物轮廓只提供空间参照。
- 副手只贡献防御 / 辅助属性，不提供第二份武器攻击来源；大剑和战斧仍可搭配副手，本轮不引入双手占槽或格挡公式。
- `core` v1 旧档通过分离 DTO 升级到 v2，旧四槽和已掷数值保持不变，新增槽为空。未知目录、未来版本或伪装成 v1 的新增槽数据拒绝恢复。
- 当前不支持双持、武器组切换、耐久或武器专精；命中、闪避和暴击已经进入统一攻击快照与结果模型。
- Actor 注销会清理运行时 Loadout；当前流程不把被清理的装备自动送回背包，注销只用于场景或架构生命周期结束。

伤害公式和词条 Scope 见 [伤害系统与词条系统设计](./damage-affix-system.md)，配置字段见[角色、物品与攻击配置参考](./config-reference/combat-content.md)，菜单交互见 [输入与运行时 UI](./input-ui-system.md)。

实际调用顺序、公式与代码依据见[装备、词条与伤害作用流程](./architecture/equipment-affix-damage/README.md)。

## 与状态来源共存

换装、RestoreLoadout 和装备注销仅更新 CombatActor 的 `equipment` 来源；怪物词条及状态使用独立来源。状态消费、到期或死亡清理不会清除装备。来源汇总按稳定键排序，从基础属性计算一次最终资源比例；完整合同见[状态战斗接入](./status-combat.md)。整个 Actor 注销仍执行既有 Loadout 清理，不能与单独释放状态混淆。
