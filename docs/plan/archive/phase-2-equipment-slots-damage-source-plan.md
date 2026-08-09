# 阶段 2：装备槽与伤害来源执行计划

## 状态

- 状态：已完成并归档
- 建立日期：2026-08-05
- 完成日期：2026-08-05
- 所属计划：[初版体验优化计划](./initial-experience-optimization/README.md)
- 关联子计划：[UX 与装备系统](./initial-experience-optimization/ux-equipment-plan.md)
- 前置阶段：[阶段 1：UX 快速改进](./phase-1-ux-quick-improvement-plan.md)
- 后续阶段：[阶段 3：随机化与掉落规则](./initial-experience-optimization/README.md#阶段-3随机化与掉落规则)
- 目标场景：`Assets/Scenes/Main.unity`

## 完成结果

- 已实现武器、护甲、左戒指和右戒指四槽 Loadout，以及原子装备、替换和卸下事务。
- 已实现四槽效果集中重建、LocalItem 分流、非伤害属性聚合和配置校验。
- 已接入武器基础伤害、空手技能回退与发射时 `AttackSnapshot`；在途投射物不受换装影响。
- 背包页已接入四槽、显式戒指选槽、安全比较、装备 / 替换 / 卸下和焦点恢复。
- 全量 EditMode 70/70 通过；PlayMode 9 项中 7 项通过、2 项包内既有用例按原标记忽略；Runtime / Editor 与测试程序集编译通过。
- 当前模块说明见 [装备系统](../../equipment-system.md)。

## 阶段目标

把当前“单武器槽 + 穿戴时覆盖全部角色词条 + 命中时读取实时攻击者”的临时链路，升级为可支撑后续装备内容的首版正式边界：

1. 玩家拥有武器、护甲、左戒指、右戒指四个显式槽位。
2. 装备、替换和卸下均与背包组成原子事务，任何失败都不丢物品、不改属性、不发送成功事件。
3. 多件装备的全局效果集中聚合，武器局部效果只修改武器自身伤害。
4. 玩家投射物使用发射瞬间建立的攻击快照；飞行途中换装不会改变该投射物。
5. 背包可用键鼠和手柄完成查看、选择目标槽、比较、装备、替换和卸下。

阶段完成后，系统层已经能够承载后续护甲、戒指、词条池和装备池填充；阶段 2 本身不批量制作正式内容。

## 当前实现基线

### 装备与背包

- `EquipmentModel` 仅用 `Dictionary<CombatActor, ItemInstance>` 保存单件武器，没有槽位、只读 Loadout 或 Actor 清理接口。
- `EquipItemCommand` 不接收槽位，固定调用 `CombatSystem.EquipWeapon`。
- `CombatSystem.EquipWeapon` 同时负责类型验证、背包交换、装备写入、角色修改器替换和事件发送，职责过多。
- 替换武器依赖 `InventoryGrid.TryExchange`，格子变更本身是原子的；但 `InventoryModel.TryExchangeItem` 会在装备状态写入前同步发送背包事件，观察者可能看到事务中间态。
- 没有卸装入口。背包已满时也没有“卸装失败且全部状态不变”的契约。
- `EquipmentChangedEvent` 只提供 `PreviousWeapon / CurrentWeapon`，无法表达槽位或卸装。

### 物品配置

- `ItemType` 已包含 `Weapon`、`Armor`、`Accessory`，但 `ItemBaseDefinition` 没有可装备槽配置。
- 当前唯一正式物品基底是大剑，已有 20–40 物理基础伤害，但这段伤害尚未进入玩家攻击。
- 当前基础投射物技能没有配置伤害，命中时回退固定 12 点物理伤害。
- 护甲、暴击和抗性等 `StatDefinition` 资产的稳定 ID 与 `StatIds` 使用的 snake_case 不一致；阶段 2 接入装备属性前必须校准受支持 ID，不能继续让配置看似存在但运行时查询不到。

### 修改器与伤害

- 穿戴武器后直接调用 `actor.SetModifiers(weapon.CollectModifiers())`，新武器会覆盖角色全部装备效果。
- `LocalItem` 与 `GlobalActor` 没有在穿戴链路中分流；局部武器词条可能被错误当作角色全局词条。
- `StatAggregator` 已存在，但当前装备与命中流程没有使用它；装备提供的护甲、抗性等属性不能可靠进入防御快照。
- `ProjectileController` 在 `OnTriggerEnter2D` 时才生成随机种子、创建技能伤害包并发送 `ApplyDamageCommand`。
- `CombatSystem.ApplyDamage` 也在命中时读取攻击者 `Stats / Modifiers`。`DamageContext` 虽然会复制数据，但复制时机太晚，无法隔离投射物飞行途中的换装。
- `DamageContext.SourceItemId` 当前始终为空。

### UI

- `InventorySnapshot` 只有 `CurrentWeapon` 和武器摘要，`InventoryItemSnapshot.CanEquip` 仍通过 `ItemType.Weapon` 判断。
- 背包页只有一个“当前武器”摘要和一个装备按钮，没有四槽展示、目标戒指槽选择、卸装或数值比较。
- 阶段 1 已建立共用 `ItemDetailSnapshot / ItemDetailView`、固定选择和三档分辨率回归，本阶段应在其上扩展，不重新制作三套详情。
- 当前自动化基线为 EditMode 60/60；PlayMode 8 项中项目测试通过，2 项包内既有测试按原标记忽略。

## 范围决策

- 运行时槽位固定为 `Weapon`、`Armor`、`RingLeft`、`RingRight`；左右戒指从模型、Command、Event 和 UI 起始即显式区分。
- `ItemType` 继续表示内容类别；新增序列化的允许槽位掩码决定可装备性。武器、护甲和饰品不能只靠 `ItemType` 在运行时猜槽位。
- 大剑资产显式迁移为只允许 `Weapon`；阶段 2 不创建最终护甲 / 戒指资产，EditMode 与 PlayMode 使用临时配置验证四槽，正式装备池留到阶段 4。
- 新增独立 `EquipmentSystem` 负责穿戴事务和效果重建；`CombatSystem` 回归伤害与生死流程，不继续管理背包交换。
- 任何领域事件都在背包、Loadout 和角色装备效果全部提交后发送。同步事件回调查询到的必须是最终状态。
- `LocalItem` 只进入武器来源伤害解析；其余已支持装备修改器作为角色效果聚合。未支持的 `Area / Temporary / Trigger / Chance / Limit` 不在本阶段扩写语义。
- 首版投射物伤害来源只区分“技能基础伤害”和“已装备武器伤害”。当前基础投射物配置为使用武器；没有有效武器时保留技能的 12 点回退伤害，避免空手完全失去攻击能力。
- 武器伤害、攻击者属性、攻击者装备词条、来源物品 ID、上下文标签和随机结果在发射时冻结；防御者属性在命中时快照，符合“攻击已发出、目标仍可在命中前改变防御”的规则。
- 阶段 2 不新增暴击随机、命中 / 闪避、装备耐久、套装、纸娃娃、拖拽或存档。
- HUD 继续只显示紧凑武器摘要；四槽完整信息集中在背包页，避免扩大常驻 HUD。

## 目标结构

### 四槽 Loadout

```text
EquipmentSlot
├── Weapon
├── Armor
├── RingLeft
└── RingRight

EquipmentLoadout
├── Get(slot)
├── TrySet(slot, item)
├── Contains(item)
└── Slots（只读枚举）

EquipmentModel
└── CombatActor -> EquipmentLoadout
```

- 增加带稳定位值的 `[Flags] EquipmentSlotMask`，用于 `ItemBaseDefinition.AllowedEquipmentSlots` 序列化。
- `ItemBaseDefinition.CanEquipTo(slot)` 是唯一兼容性入口。
- `EquipmentLoadout` 不访问背包、不发送 Event，只维护一个 Actor 的槽位状态并禁止同一实例同时出现在多个槽。
- `EquipmentModel` 提供获取 Loadout、按槽查询和 Actor 清理；架构退出与 Actor 注销后不能遗留引用。

### 装备事务

```text
EquipItemCommand(actor, item, targetSlot)
UnequipItemCommand(actor, targetSlot)
                │
                ▼
EquipmentSystem
├── 验证 Actor / 物品 / 槽位 / 背包归属
├── 预检背包放置或交换
├── 提交 InventoryGrid + EquipmentLoadout
├── 重建装备效果
└── 发送 InventoryChangedEvent / EquipmentChangedEvent
```

事务规则：

1. 首次装备：物品必须在背包且兼容目标槽；成功后从背包移除并进入槽位。
2. 同槽替换：新物品从背包移除，旧物品必须能放回背包；使用新物品原占用格作为可释放空间参与放置判断。
3. 卸装：目标槽必须有物品，且背包必须有完整可用空间；成功后物品进入背包并清空槽位。
4. 同一实例已在任意槽时拒绝再次装备；装备到原槽同一实例视为无变化，不发送事件。
5. 任一验证或格子预检失败时，背包、Loadout、Actor 效果和事件数量全部不变。
6. 状态全部提交后，按固定顺序发布背包移除 / 添加事件，再发布包含 `Slot / PreviousItem / CurrentItem` 的 `EquipmentChangedEvent`。

`InventoryModel` 需要提供专用于装备事务的无事件提交入口，或等价的事务对象；禁止在最终装备状态写入前发送同步事件。

### 装备效果聚合

```text
四槽物品修改器
├── LocalItem ──> WeaponDamageResolver
└── 角色效果 ──> CombatStatResolver / DamageContext
```

- 每次装备成功后从四槽重新收集，不做逐槽增减，避免替换 / 卸装时残留旧效果。
- `LocalItem` 修改器只与 Weapon 槽物品的基础伤害一起解析；护甲和戒指上的 `LocalItem` 视为配置错误并给出明确 Warning。
- `GlobalActor` 的护甲、抗性等非伤害属性通过统一纯函数解析到战斗快照。
- `Damage / PhysicalDamage / FireDamage / ColdDamage / LightningDamage / ChaosDamage` 的 Flat / Increase / More，以及 Conversion / GainAsExtra，继续由伤害管线按攻击标签处理，不能先静态聚合后再重复应用。
- `Skill` 和 `TargetTaken` 修改器保留上下文匹配；未支持 Scope 不静默套用。
- 增加配置校验，至少覆盖允许槽位、空稳定 ID、受支持 `StatIds`、LocalItem 放置位置和资产兼容性。

### 发射时攻击快照

```text
FireProjectileCommand
├── 生成一次攻击种子
├── 查询 Weapon 槽
├── AttackSnapshotFactory
│   ├── 选择技能或武器基础伤害
│   ├── 应用武器 LocalItem
│   ├── 合并技能 / 武器 / 伤害标签
│   └── 复制攻击者属性与角色修改器
└── SpawnProjectile(snapshot)
        └── 命中时只补充 DefenderSnapshot 并结算
```

建议的 `AttackSnapshot` 至少包含：

- `AttackerId`、攻击者阵营。
- `SkillId`、`SourceItemId`。
- 攻击随机种子和已经掷出的基础伤害包。
- 技能、武器和伤害上下文标签。
- 攻击者属性副本、角色修改器副本。
- 当前命中 / 暴击占位状态；本阶段不新增暴击随机。

`ProjectileController` 不再在命中时读取武器、攻击者属性或重新掷基础伤害。投射物仍可保留 Owner 引用用于现有动画 / 调试，但伤害正确性不能依赖 Owner 的实时装备。

### 背包装备 UI

```text
InventoryPage
├── 背包格子
├── 四槽装备栏
│   ├── 武器
│   ├── 护甲
│   ├── 左戒指
│   └── 右戒指
└── 详情 / 比较 / 操作
    ├── 共用 ItemDetailView
    ├── 当前槽位与候选摘要
    ├── 可解释差异行
    ├── 装备 / 替换
    └── 卸下
```

- 四个槽位均为可聚焦 Button，显示中文槽名、空槽状态、装备名称和稀有度；阶段 2 使用现有纹理与类型占位，不新增正式图标批次。
- 选择武器或护甲时可自动确定唯一目标槽；选择饰品时必须显式选择左戒指或右戒指，未选目标槽时装备按钮禁用并显示提示。
- 选中槽位时可查看已装备物品并执行卸下；选中背包候选时详情仍使用阶段 1 的临时预览 / 固定选择契约。
- 装备或卸下成功后保持目标槽选择，背包固定选择按 `InstanceId + 后备索引` 恢复；焦点落在合理槽位或邻近背包物品。
- 比较只对同一目标槽生成。武器基础伤害范围和可直接比较的数值显示当前值、候选值与正负差异；条件词条、转换和额外获得保留原文，不伪造单一“战力”或误导性绿色数字。
- 操作按钮保持固定可达；详情和比较正文滚动。继续验收 1280×720、1920×1080、2560×1440。

## 实施顺序

### 2.0 建立装备与攻击快照回归基线

1. 固化当前武器首次装备、替换、背包满失败和现有 12 点技能回退伤害。
2. 增加可稳定复现“投射物飞行中换装会改变命中结果”的 PlayMode / 纯逻辑基线。
3. 记录现有大剑基础伤害未参与攻击、`SourceItemId` 为空、LocalItem 未分流的断言。
4. 校对当前正式 Stat / Item / Skill 资产的稳定 ID 和序列化字段，形成迁移清单。

完成门槛：测试能覆盖当前单武器事务，并明确证明阶段 2 要修复的伤害来源与快照时机。

### 2.1 建立四槽模型与原子事务

1. 新增 `EquipmentSlot / EquipmentSlotMask / EquipmentLoadout`，扩展 `EquipmentModel`。
2. 为 `ItemBaseDefinition` 增加允许槽位、中文 Inspector 和校验；迁移大剑资产。
3. 新增 `EquipmentSystem`，将装备逻辑从 `CombatSystem` 移出。
4. 改造 `EquipItemCommand` 并新增 `UnequipItemCommand`。
5. 为 `InventoryModel / InventoryGrid` 增加无中间事件的装备事务提交能力。
6. 扩展 `EquipmentChangedEvent`，处理 Actor 注销和架构退出清理。

完成门槛：四槽首次装备、替换、卸装、左右戒指独立和所有失败回滚在 EditMode 全部通过。

### 2.2 分流并聚合装备效果

1. 新增统一装备效果收集器，每次成功变更从 Loadout 全量重建。
2. 把 LocalItem 与角色 / 技能 / 目标效果分流，禁止局部武器词条污染角色全局修改器。
3. 接入战斗属性解析，确保装备护甲和抗性进入防御快照，伤害修改器只应用一次。
4. 校准当前受支持 `StatDefinition` 稳定 ID，并增加配置校验测试。
5. 验证替换或卸下一件装备只移除该件提供的效果，其他槽不受影响。

完成门槛：多装备属性组合结果可解释，无重复应用、残留修改器或 Scope 串线。

### 2.3 接入武器伤害与发射时快照

1. 为投射物技能增加明确的伤害来源配置，迁移基础投射物为武器来源。
2. 实现武器基础伤害掷点和 LocalItem 解析；没有有效武器时走技能回退。
3. 新增不可变 `AttackSnapshot` 与工厂，在 `FireProjectileCommand` 中建立。
4. 调整 `SpawnSystem / ProjectileController / ApplyDamageCommand / CombatSystem`，命中时不再读取攻击者实时装备。
5. 把 `SourceItemId` 和完整标签写入 `DamageContext`，保留防御者命中时快照。

完成门槛：大剑 20–40 伤害实际进入玩家攻击；发射后换武器、卸装或修改词条均不改变在途投射物结果。

### 2.4 接入四槽 UI 与比较

1. 扩展 `InventorySnapshot`，加入四槽快照、目标槽、候选兼容性和比较数据。
2. 使用 UIToolkit 增加四槽装备栏、空槽、选中 / 焦点状态、装备 / 替换 / 卸下操作。
3. 实现戒指显式选槽状态机，统一鼠标、键盘和手柄路径。
4. 复用 `ItemDetailView`，加入当前槽摘要和安全的差异格式，不改商店 / 打造详情契约。
5. 保持成功 / 失败反馈、固定选择、滚动和焦点恢复。

完成门槛：键鼠与手柄均可完成四槽装备、替换和卸下；1280×720 下所有槽、详情和操作按钮可达且不重叠。

### 2.5 综合验收与归档

1. 运行全量 EditMode、PlayMode、Runtime / Editor 编译和 Console 检查。
2. 在真实 `Main.unity` 完成购买武器、四槽临时装备注入、比较、替换、卸装、返回战斗流程。
3. 验证发射后立即换装的在途投射物快照。
4. 验证三档分辨率以及键鼠 / 手柄焦点和返回路径。
5. 新增或更新装备模块文档、玩法循环、输入 UI 和伤害词条文档；完成后将本计划移入 `docs/plan/archive/`。

## 自动化测试重点

### EditMode

- 允许槽位掩码与 ItemType 配置校验。
- 四槽 Get / Set / Enumerate，同一实例禁止进入多个槽。
- 首次装备、同槽替换、卸装和左右戒指独立操作。
- 不兼容槽、物品不在背包、重复实例、无变化和空槽卸装全部失败且无 Event。
- 背包满、旧物品尺寸更大时替换 / 卸装原子回滚。
- 事件仅在完整提交后发送，回调 Query 只能观察到最终状态。
- 四槽全局效果合并；替换或卸下一件只移除其效果。
- LocalItem 只影响武器基础伤害，不进入其他技能或角色全局修改器。
- 武器伤害使用固定 seed 可复现；无武器时保留技能回退。
- `AttackSnapshot` 深复制伤害包、Stats、Modifiers 和 Tags；创建后修改运行时对象不影响结果。
- 装备护甲 / 抗性进入防御，伤害 Increase / More 不重复应用。
- 装备快照和比较对武器、护甲、左右戒指返回正确目标槽与差异。

### PlayMode

- 键鼠选择武器 / 护甲并装备、替换、卸下。
- 手柄选择饰品后显式选择左 / 右戒指槽，两个槽互不覆盖。
- 操作后固定选择、目标槽、焦点和反馈合理恢复。
- 背包空间不足时 UI 显示失败，装备和背包不变。
- 投射物发射后立即换装，命中仍使用发射时武器伤害和词条。
- 四槽变更后 HUD 武器摘要、背包详情和战斗结果同步。
- 1280×720、1920×1080、2560×1440 下槽位、比较、滚动、操作和关闭入口不越界。
- 阶段 1 商店 / 打造状态与详情测试继续通过。

## 验收矩阵

| 场景 | 通过标准 |
| --- | --- |
| 首次装备四类槽位 | 物品离开背包并只进入目标槽，属性和 UI 同步 |
| 替换装备 | 新物品进入槽位，旧物品完整回包，其他槽不变 |
| 卸下装备 | 背包有空间时成功；无空间时所有状态不变 |
| 左右戒指 | 必须显式选槽，可分别装备、替换和卸下 |
| 不兼容物品 | 按钮禁用或 Command 失败，不改状态、不发成功 Event |
| 多装备效果 | 四槽效果合并一次，卸下任意一件只移除该件效果 |
| LocalItem | 只改变 Weapon 槽物品的来源伤害，不污染其他来源 |
| 大剑基础伤害 | 玩家攻击使用大剑 20–40 基础范围，不再固定为 12 |
| 空手回退 | 无有效武器时仍使用技能回退伤害，不阻断最小循环 |
| 在途投射物 | 发射后换装 / 卸装不改变该投射物最终伤害 |
| 防御装备 | 护甲 / 抗性进入防御快照且只应用一次 |
| 详情比较 | 对应槽当前 / 候选和可比较差异正确，条件词条不伪造战力 |
| 键鼠 / 手柄 | 均可选槽、装备、替换、卸下和返回 |
| 三档分辨率 | 四槽、详情、比较、操作按钮和关闭入口不重叠、不越界 |
| 回归 | 交易、打造、暂停、死亡动画、掉落和拾取语义不变 |

## 预计影响文件

### 新增候选

- `Assets/Scripts/Runtime/Gameplay/Combat/EquipmentSlot.cs`
- `Assets/Scripts/Runtime/Gameplay/Combat/EquipmentLoadout.cs`
- `Assets/Scripts/Runtime/Gameplay/Combat/EquipmentSystem.cs`
- `Assets/Scripts/Runtime/Gameplay/Combat/AttackSnapshot.cs`
- `Assets/Scripts/Runtime/Gameplay/Combat/AttackSnapshotFactory.cs`
- `Assets/Scripts/Runtime/Gameplay/Combat/WeaponDamageResolver.cs`
- `Assets/Scripts/Runtime/Gameplay/Combat/Commands/UnequipItemCommand.cs`
- 装备事务、聚合、攻击快照和阶段 2 PlayMode 测试
- `docs/equipment-system.md`

### 修改候选

- `GameArchitecture.cs`
- `EquipmentModel.cs`
- `CombatSystem.cs`
- `CombatActor.cs`
- `EquipItemCommand.cs`
- `ApplyDamageCommand.cs`
- `FireProjectileCommand.cs`
- `SpawnSystem.cs`
- `ProjectileController.cs`
- `DamageContext.cs`
- `DamageCalculator.cs`
- `InventoryGrid.cs`、`InventoryModel.cs`
- `ItemBaseDefinition.cs`、`ProjectileSkillDefinition.cs`
- `GameplayEvents.cs`
- `GetInventorySnapshotQuery.cs`、`GetHudSnapshotQuery.cs`
- `InventoryPanelController.cs`
- `Inventory.uxml`、`Inventory.uss`
- 大剑、基础投射物和受支持 Stat 配置资产
- 玩法、输入 UI、伤害词条与计划索引文档

实际文件以实现时的最小结构为准；若纯函数职责足够小可以合并，但不能把装备事务重新塞回 `CombatSystem` 或 UI Controller。

## 风险与控制

- **同步 Event 暴露中间态**：背包与 Loadout 全部提交后才发送任何 Event；测试在回调内立即 Query 验证。
- **左右戒指隐式覆盖**：Command 必须接收目标槽；饰品 UI 未显式选槽时禁止执行。
- **背包满导致物品丢失**：先完成格子预检，事务提交后不再执行可能失败的步骤；覆盖尺寸不等的替换测试。
- **修改器重复应用**：伤害类与非伤害类、LocalItem 与角色效果使用单一分流器；同一 Modifier 只能进入一条计算路径。
- **配置稳定 ID 漂移**：迁移受支持 Stat 资产并增加校验；运行时不通过中文名或文件名猜 ID。
- **快照时机仍然过晚**：种子、伤害包、来源物品和攻击者效果必须在创建投射物前生成并复制。
- **防御也被错误冻结**：只冻结攻击者；防御者继续在实际命中时取快照。
- **UI 高度再次溢出**：四槽栏和操作区固定，详情 / 比较滚动；三档分辨率增加容器级边界断言。
- **阶段 4 内容反向重构**：槽位兼容性和效果分流先稳定；正式装备只填配置，不再改变事务协议。

## 本阶段不做

- 正式护甲 / 戒指装备池、掉落权重、商店库存和词条池填充。
- 怪物随机生命、随机伤害配置和掉落概率。
- 暴击随机、命中、闪避、格挡、异常状态或持续伤害。
- 背包拖拽、旋转、堆叠、排序、筛选和重量限制。
- 装备耐久、品质升级、套装、唯一特效和纸娃娃换装。
- 多角色切换、装备存档、局外仓库或角色模板预设。
- 正式护甲 / 戒指图标；阶段 2 只建立可替换的槽位表现。
