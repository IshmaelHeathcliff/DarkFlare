# 装备系统

## 模块职责

装备系统负责四槽穿戴状态、背包与装备之间的原子交换、装备效果聚合，以及武器攻击来源的发射时快照。当前槽位固定为：

- `Weapon`：武器。
- `Armor`：护甲。
- `RingLeft`：左戒指。
- `RingRight`：右戒指。

`ItemType` 只表示物品类别，实际可装备位置由 `ItemBaseDefinition.AllowedEquipmentSlots` 决定。运行时统一通过 `CanEquipTo(slot)` 检查兼容性，不能再只根据 Weapon / Armor / Accessory 推断目标槽。

## 数据与运行时结构

| 类型 | 职责 |
| --- | --- |
| `EquipmentSlot` / `EquipmentSlotMask` | 定义稳定槽位和可序列化兼容掩码 |
| `EquipmentLoadout` | 保存单个 Actor 的四槽状态，并阻止同一实例重复占用多个槽 |
| `EquipmentModel` | 保存 `CombatActor -> EquipmentLoadout` 映射并提供按槽查询 |
| `EquipmentSystem` | 执行装备、替换、卸下事务并重建角色装备效果 |
| `EquipItemCommand` / `UnequipItemCommand` | Controller 可调用的写入入口 |
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

- `LocalItem` 只允许进入 Weapon 槽物品的本地伤害解析。
- `GlobalActor`、`Skill`、`TargetTaken` 等已支持 Scope 进入角色修改器集合。
- 护甲、抗性等非伤害属性由 `CombatStatResolver` 聚合到 Actor 有效属性。
- 最大生命统一使用稳定 ID `max_health`。`CombatActor.MaxHealth`、当前生命和 HUD 均读取聚合后的有效属性；穿脱装备时保持当前生命比例，避免通过反复换装恢复生命。
- 基础伤害、类型伤害、转换、额外获得、Increase 和 More 仍由伤害管线按标签处理，不能在静态属性层重复应用。
- 非武器上的 `LocalItem` 会被忽略并给出配置 Warning。

`EquipmentConfigurationValidator` 同时校验物品基底和词条配置，包括空稳定 ID、类别与槽位不匹配、未知 `StatId`，以及 LocalItem 出现在非武器配置等问题。正式内容进入装备池前应先通过该校验。

## 武器伤害与攻击快照

`ProjectileSkillDefinition.DamageSource` 决定投射物使用技能基础伤害还是已装备武器。当前基础投射物配置为武器来源：

- 有有效武器时，使用 Weapon 槽的基础伤害和武器 LocalItem 词条；大剑当前基础范围为 20–40 物理伤害。
- 战斧使用 26–34 物理伤害；两把武器都通过对应子类与 `physical` 标签筛选词条。
- 无有效武器时，使用技能配置的 10–14 点物理伤害；技能伤害池为空时仍保留代码级 12 点保护，最小循环不会因错误配置中断。
- `AttackSnapshotFactory` 在发射时生成唯一种子并冻结来源物品 ID、已掷出的伤害包、标签、攻击者属性和修改器。
- `ProjectileController` 命中时只补充当前防御者快照；飞行途中换装、卸装或修改攻击者词条不会改变已发出的投射物。

即时伤害也在 Command 建立时转换为攻击快照，避免执行链中继续读取可变攻击者状态。

## 背包 UI

背包页展示武器、护甲、左戒指和右戒指四个可聚焦按钮，并提供候选详情、安全比较、装备 / 替换和卸下操作。

- 武器和护甲候选会自动选择唯一目标槽。
- 饰品不会隐式覆盖戒指；必须先明确选择左戒指或右戒指。
- 比较只展示能可靠解释的基础伤害范围与直接数值差异；条件词条、转换和额外获得保留原文，不生成虚假战力分。
- 成功或失败后会刷新快照并恢复合理选择和焦点，键鼠与手柄共享同一目标槽状态。

HUD 继续只保留紧凑武器摘要，完整四槽信息集中在背包页。

## 验证记录

- EditMode 全量 75/75 通过，覆盖四槽、重复实例、替换 / 卸下回滚、事件最终态、效果聚合、最大生命与 HUD 同步、属性配置校验和快照深复制。
- PlayMode 共 9 项，项目与相关包测试 7 项通过，2 项 Input System 上游不稳定用例按原标记忽略。
- 新增端到端用例覆盖键盘装备武器 / 护甲、最大生命装备后的 HUD 数值、手柄显式选择左右戒指、卸下、三档分辨率边界，以及在途投射物换装仍使用旧快照。
- `DarkFlare.Runtime`、`DarkFlare.Editor`、EditMode 与 PlayMode 测试程序集编译通过。
- 阶段 4 使用正式资产完成大剑、皮甲、铁指环和翡翠戒指的购买、武器 / 护甲 / 双戒指装备、卸下、武器打造与出售路径。

## 当前边界

- 正式内容包含大剑、战斧、皮甲、板甲、铁指环、翡翠戒指和黑曜戒指，共 2 武器、2 护甲、3 戒指；详细数值和词条兼容见[首批内容池](./content-system.md)。
- 不支持耐久、套装、唯一装备效果、纸娃娃、拖拽换装或存档。
- 暴击随机、命中 / 闪避与未实现的 Modifier Scope 不属于当前阶段。
- Actor 注销会清理运行时 Loadout；当前流程不把被清理的装备自动送回背包，注销只用于场景或架构生命周期结束。

伤害公式和词条 Scope 见 [伤害系统与词条系统设计](./damage-affix-system.md)，菜单交互见 [输入与运行时 UI](./input-ui-system.md)。
