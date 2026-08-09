# 阶段 1：UX 快速改进执行计划

## 状态

- 状态：已完成，归档日期 2026-08-04
- 建立日期：2026-08-04
- 所属计划：[初版体验优化计划](./initial-experience-optimization/README.md)
- 关联子计划：[UX 与装备系统](./initial-experience-optimization/ux-equipment-plan.md)
- 前置阶段：[阶段 0.5：视觉体验修正](./phase-0.5-visual-experience-fix-plan.md)
- 目标场景：`Assets/Scenes/Main.unity`

## 阶段目标

在不扩展装备槽和战斗数值来源的前提下，解决当前菜单中两类直接影响连续操作的问题：

1. 背包、商店和打造对同一件物品显示的信息不一致，无法完整查看隐式、前缀和后缀。
2. 商店刷新、交易和页签切换后会丢失列表位置、合理选择或手柄焦点。

完成后，玩家应能使用键鼠或手柄连续完成“查看物品 → 买入 / 卖出 → 页签往返 → 再次交易”，过程中无需重新寻找原位置，也不会看到内部属性 ID。

## 当前实现基线

### 商店状态

- `ShopPanelController.RefreshShop` 每次调用都会 `Clear` 两列并重建全部按钮。
- 当前只保存一个 `_selectedItem + _selectedSource`，并通过对象引用判断同一物品是否仍存在。
- 选中物品消失后固定回退到商人第一件，再回退到玩家第一件；没有“原索引下一件 / 上一件”的规则。
- 商人列和玩家列的 `ScrollView` 没有名称，Controller 没有持有滚动偏移。
- 没有记录刷新前的 `focusController.focusedElement`；`GameMenuController.ApplyPage` 会在面板刷新后立即调用 `FocusDefault`。
- 关闭页面再打开会再次刷新。当前同一次运行期内的列选择、滚动和焦点都没有独立状态对象。
- 交易结果提示由 Controller 临时写入 Label，后续刷新或选择可能立即被通用提示覆盖。

### 物品详情

- `InventoryItemSnapshot`、`ShopItemSnapshot`、`CraftingItemSnapshot` 重复保存名称、类型、稀有度和词缀数量。
- 背包与商店只显示“词缀 N 条”，没有隐式、前缀、后缀和具体数值。
- 打造页拥有独立的 `CraftingAffixSnapshot`，但当前直接使用 `ModifierInstance.StatId`，会向玩家显示 `damage` 等内部 ID。
- 物品类型、稀有度和修改器文本分别由各 Controller 自行翻译，已经存在重复和漂移风险。
- `ModifierInstance` 只保留稳定 `StatId`；中文名仍存在于 `StatModifierDefinition.Stat.DisplayName`。运行时修改器与其配置定义按创建顺序一一对应。
- 当前只有大剑的静态 USS 图标，没有通用的物品基底图标配置。本阶段不能把批量图标生产混入 UX 修正。

### 现有交互

- 鼠标点击或手柄 Submit 会触发 Button 的选择回调。
- Tooltip 只对指针友好，不能作为手柄查看词条的唯一入口。
- 背包和打造也只按对象引用保留当前选择；物品消失时直接选择第一件。
- 当前项目级基线为 EditMode 51/51、PlayMode 3/3 通过。

## 范围决策

- 阶段 1 只处理 **共用详情 + 选择 / 滚动 / 焦点状态**，不修改 `EquipmentModel`、装备 Command、伤害计算或攻击快照。
- 保留当前单武器装备语义。详情区可以显示“当前武器”摘要，但多槽位对应关系和数值差异比较放到阶段 2。
- 不修改 `ModifierInstance` 的战斗语义。详情快照通过物品基底 / 词缀定义与运行时修改器的相同索引取得 `StatDefinition.DisplayName`。
- 如果配置和运行时修改器数量不一致，按稳定顺序显示可配对项，其余项使用安全中文占位并记录 Debug Warning，不回退显示原始 ID。
- 新增共用 `ItemDetailSnapshot`，交易价格、打造成本和格子位置继续留在各自外层快照，避免把界面上下文混入物品本体详情。
- 商店状态属于表现层，不写入 QFramework Model，不进入存档，也不发送领域 Event。
- 当前大剑继续复用既有静态图标；共用详情视图预留图标容器和类别样式。按物品基底配置正式图标留到内容 / 批量视觉阶段。
- UI 重建后的滚动和焦点恢复使用 UI Toolkit 调度并带刷新代次，禁止通过 `Update`、协程或无取消异步轮询等待布局。

## 目标结构

### 共用详情读模型

```text
ItemDetailSnapshot
├── 身份：InstanceId、DisplayName、Type、Rarity、ItemLevel
├── 基础：GridSize、Weight、BaseValue、CalculatedValue
├── 伤害：DamageDetailSnapshot[]
├── 隐式：ModifierDetailSnapshot[]
├── 前缀：AffixDetailSnapshot[]
└── 后缀：AffixDetailSnapshot[]
    └── ModifierDetailSnapshot[]
```

建议职责：

| 类型 | 职责 |
| --- | --- |
| `ItemDetailSnapshot` | 保存与界面上下文无关的完整物品只读信息 |
| `AffixDetailSnapshot` | 保存词缀类型、中文名和其修改器明细 |
| `ModifierDetailSnapshot` | 保存中文属性名、操作、作用域、数值和最终格式化文本 |
| `DamageDetailSnapshot` | 保存伤害类型、最小 / 最大值和标签摘要 |
| `ItemDetailSnapshotFactory` | 从 `ItemInstance` 及配置定义建立详情，不访问或修改 Model |
| `ItemDetailFormatter` | 统一类型、稀有度、伤害类型、修改器操作和数值中文格式 |

外层快照调整：

- `InventoryItemSnapshot`：保留 `Placement`、`CanEquip`，新增或持有 `ItemDetailSnapshot`。
- `ShopItemSnapshot`：保留 `Source`、`Price`，持有同一详情快照。
- `CraftingItemSnapshot`：保留打造容量、售价和可操作词缀引用，展示信息改从共用详情读取。
- 在过渡期可保留只读转发属性减少一次性改动，完成后删除三套重复格式化方法。

### 共用详情视图

```text
ItemDetailView
├── 图标 / 名称 / 类型 / 稀有度 / 等级
├── 基础属性与伤害
├── 隐式修改器
├── 前缀
├── 后缀
└── 页面上下文插槽：价格、容量、操作按钮、反馈
```

- 使用一份 UIToolkit 详情模板和样式，由三个页面在各自作用域内查询元素；不得从整个 `UIDocument` 根节点查询重复名称。
- 详情正文使用可滚动区域，页面操作按钮和反馈留在固定底部，保证 1280×720 下仍能完成操作。
- 空分组显示“无隐式 / 无前缀 / 无后缀”，不隐藏整块导致布局跳动。
- Tooltip 只保留列表摘要；完整信息必须在固定详情区可见。
- 鼠标 Hover / 手柄焦点可以临时预览，点击或 Submit 固定选择；离开临时预览后恢复固定选择。
- 页签切换回来时恢复固定选择，不把上次 Hover 项误写为持久选择。

### 商店 ViewState

```text
ShopViewState
├── ActiveSource
├── Merchant: ItemListViewState
├── Player: ItemListViewState
├── FocusTarget
└── Feedback

ItemListViewState
├── SelectedInstanceId
├── FallbackIndex
└── ScrollOffset
```

恢复规则：

1. 刷新前分别捕获两列选择、索引和滚动，并识别当前焦点是物品、交易按钮还是其他控件。
2. 重建后先按 `InstanceId` 恢复每列选择。
3. 如果物品因交易离开原列，仍停留在原来源列，并优先选择原索引的下一件；没有下一件时选择上一件。
4. 原列为空时才切换到另一列；两列都为空时聚焦关闭按钮或可用页签。
5. 布局完成后按刷新代次恢复滚动，再恢复焦点并 `ScrollTo` 目标按钮；旧代次回调不得覆盖新刷新。
6. 成功 / 失败交易反馈保留到下一次固定选择或下一次交易，不被通用可购买提示立即覆盖。
7. 页签往返和关闭后重新与同一商人交互时保留本次运行期状态；组件禁用或架构退出时清空。

背包和打造至少改为按 `InstanceId + 后备索引` 恢复固定选择，避免详情模型接入后继续依赖对象引用。商店是本阶段完整滚动和焦点恢复的验收重点。

## 实施顺序

### 1.0 建立 UX 回归基线

1. 在 EditMode 为现有快照、词缀格式和交易失败回滚补充基线断言。
2. 在 PlayMode 记录商店初始页签、两列选择、滚动、焦点和反馈行为。
3. 使用现有商人库存，并在测试中注入足以产生滚动的临时库存 / 背包物品；测试数据不得写回正式资产。
4. 留存 1280×720 商店和打造详情基线截图，作为长详情布局对照。

完成门槛：测试能够稳定复现“交易后跳到首项、滚动回顶部、焦点被默认项覆盖”和“打造显示内部属性 ID”。

### 1.1 抽取共用详情快照与格式化

1. 新增共用详情、词缀、修改器和伤害只读快照。
2. 实现纯 `ItemDetailSnapshotFactory` 和 `ItemDetailFormatter`。
3. 中文属性名优先来自 `StatDefinition.DisplayName`；缺失时使用“未配置属性”，并输出带物品 / 词缀上下文的 Debug Warning。
4. 统一以下文本：物品类型、稀有度、伤害类型、Flat / Increase / More / Override / Conversion / GainAsExtra、数值符号和百分比。
5. 让背包、商店、打造快照组合同一详情，并删除 Controller 内重复翻译。

验证：同一 `ItemInstance` 从三个 Query 取得的详情完全一致；打造后刷新只改变实际变化的词缀和价值。

### 1.2 接入共用详情 UI

1. 建立共享详情模板、样式和 `ItemDetailView` 绑定器。
2. 使用 unityMCP 检查 / 调整三页实际布局和焦点导航，不直接修改打开场景 YAML。
3. 背包显示完整详情和当前武器摘要；商店叠加来源与买卖价格；打造叠加容量、选中词缀和操作成本。
4. 接入临时预览与固定选择，保证鼠标和手柄都无需 Tooltip 即可查看词条。
5. 明确空数据、无词缀、配置缺失和超长名称的降级表现。

验证：三页对同一物品显示相同名称、稀有度、基础伤害、隐式、前缀和后缀；界面中不出现原始 `StatId`。

### 1.3 实现商店状态恢复

1. 为两个 `ScrollView` 增加稳定名称并绑定到 Controller。
2. 新增 `ShopViewState`、列状态和纯选择恢复器。
3. 将 `RefreshShop` 拆为捕获状态、查询、重建、同步选择、延迟恢复布局五个明确阶段。
4. 交易前记录发生交易的来源和索引；交易完成 Event 刷新时使用邻近选择规则。
5. 调整 `GameMenuController.ApplyPage` 与面板焦点契约，避免同步默认焦点覆盖延迟恢复。
6. 把交易反馈纳入 ViewState，并在选择 / 下一次交易时明确清除。
7. 背包、商店、打造页签往返后恢复各自固定选择。

验证：连续买入 5 次、连续卖出 5 次、商店 ↔ 背包往返和关闭 / 重开后，原列、邻近项、滚动与焦点均符合规则。

### 1.4 综合验收与归档

1. 运行详情工厂、格式化、选择恢复器和现有交易原子性的全量 EditMode。
2. 运行阶段 1 PlayMode：键鼠与虚拟手柄分别完成选择、预览、购买、出售、页签往返和关闭 / 重开。
3. 检查 1280×720、1920×1080、2560×1440 的长词条、空列表、滚动和操作按钮边界。
4. 检查 Console、重复事件订阅、延迟布局回调和组件禁用后的清理。
5. 更新 `input-ui-system.md` 和 `gameplay-loop.md`，完成后将本计划移动到 `Docs/docs/plan/archive/`。

## 自动化测试重点

### EditMode

- 同一物品在背包、商店、打造快照中生成相同详情。
- 隐式、前缀和后缀按定义顺序生成，中文属性名正确。
- Flat、Increase、More、Override、Conversion、GainAsExtra 格式正确。
- 定义 / 实例缺失或数量不一致时安全降级，不显示内部 ID。
- 商店选择恢复覆盖：同 ID、移除中间项、移除末项、原列为空、两列为空。
- 交易成功 / 失败仍保持金币、库存和背包原子性。

### PlayMode

- 鼠标 Hover 临时预览，离开后恢复固定选择；点击固定。
- 手柄导航 / Submit 可以查看详情并执行交易，B 返回路径正常。
- 购买 / 出售后焦点停留在原列合理邻近项。
- 两列滚动偏移在刷新、页签往返和重新打开后恢复。
- 交易反馈不会在同一刷新帧被通用提示覆盖。
- 三档分辨率下详情正文可滚动，操作按钮、反馈和关闭入口始终可达。

## 验收矩阵

| 场景 | 通过标准 |
| --- | --- |
| 背包查看装备 | 显示基础伤害、隐式、前缀、后缀和中文数值，不只显示数量 |
| 商店查看同一装备 | 物品详情与背包一致，只额外显示来源和交易价格 |
| 打造查看同一装备 | 基础详情一致，只额外显示容量、选中词缀和成本 |
| 内部 ID | 玩家可见 UI 中不出现 `damage` 等稳定 ID |
| 连续购买 / 出售 | 原来源列保留，消失项按下一件 / 上一件规则降级 |
| 商店滚动 | 刷新、页签往返、关闭 / 重开不跳回顶部 |
| 手柄焦点 | 重建后焦点恢复到合理物品或操作按钮，不丢到不可见节点 |
| 鼠标预览 | Hover 不覆盖固定选择，离开后详情恢复 |
| 空列表 | 原列为空时合理切列，两列为空时仍能关闭菜单 |
| 交易反馈 | 成功 / 失败信息持续到下一次选择或交易 |
| 三档分辨率 | 长详情可滚动，操作按钮和关闭入口不越界 |
| 回归 | 暂停、输入模式、购买 / 出售事务和装备命令语义不变 |

## 预计影响文件

### 新增

- `Assets/Scripts/Runtime/Gameplay/UI/ItemDetailSnapshot.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/ItemDetailSnapshotFactory.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/ItemDetailFormatter.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/ItemDetailView.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/ShopViewState.cs`
- `Assets/UI/ItemDetail.uxml`
- `Assets/UI/ItemDetail.uss`
- 阶段 1 的 EditMode / PlayMode 测试文件

### 修改

- `Assets/Scripts/Runtime/Gameplay/UI/GetInventorySnapshotQuery.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/GetShopSnapshotQuery.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/GetCraftingSnapshotQuery.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/InventoryPanelController.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/ShopPanelController.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/CraftingPanelController.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/GameMenuController.cs`
- `Assets/UI/Inventory.uxml`、`Shop.uxml`、`Crafting.uxml`
- 对应 USS、模块文档和计划索引

实际文件以实施时的最小可用结构为准，不为了匹配预估文件列表拆出无价值类型。

## 风险与控制

- **共用快照变成万能 DTO**：物品本体详情与页面上下文分离，价格、容量、格子和操作能力留在外层快照。
- **中文名依赖索引配对**：对定义 / 实例数量做显式校验和降级测试；阶段 1 不改变战斗用 `ModifierInstance`。
- **布局回调覆盖新状态**：每次刷新递增代次，延迟回调执行前检查代次和元素是否仍附着面板。
- **焦点与滚动互相拉扯**：先恢复滚动，再 `ScrollTo` 和 Focus；同一帧只允许一个恢复入口。
- **事件同步导致重复刷新**：交易 Command 只依赖领域 Event 刷新一次，按钮回调不再额外无条件重建。
- **Hover 干扰手柄**：临时预览和固定选择分离；输入模式切换时清除指针预览。
- **长词条挤压操作区**：详情正文独立滚动，操作区固定；三档分辨率实际截图验收。
- **阶段 2 再次重构**：详情模型不写死武器槽，当前武器摘要作为页面上下文，后续可替换为对应槽位比较。

## 本阶段不做

- 护甲、左戒指、右戒指槽及卸装。
- 多装备修改器聚合、武器基础伤害接入和在途投射物快照。
- 多商人独立 ViewState、商店存档、回购或刷新计时。
- 物品正式图标字段、全部装备图标和 Addressables 图标加载。
- 背包拖拽、旋转、堆叠、排序或筛选。
- 词条等级段、复杂 Tooltip、完整属性面板和数值模拟器。

## 完成记录

- 背包、商店和打造已组合统一 `ItemDetailSnapshot`，共用中文格式化与 `ItemDetailView`；完整展示基础伤害、隐式、前缀和后缀，不再向玩家暴露 `StatId`。
- 三页支持鼠标悬停 / 手柄焦点临时预览与点击 / Submit 固定选择；背包和打造使用 `InstanceId + 后备索引` 恢复选择。
- 商店通过 `ShopViewState` 保留两列选择、后备索引、滚动、活动来源、焦点和交易反馈；购买后可恢复原列邻近项，页签往返及关闭重开保持状态。
- 1280×720、1920×1080、2560×1440 下商店与打造主要元素边界验收通过。
- Unity 全量 EditMode 60/60 通过；PlayMode 8 项中 6 项通过、2 项为包内既有忽略测试；脚本编译与 Console 均为 0 错误。
