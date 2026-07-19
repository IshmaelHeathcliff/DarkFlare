# 输入系统与 UI 设计

本文档记录玩家输入层重构与 UIToolkit UI 架构，是最小循环第 8 步“用 `Main.unity` 串成一轮完整循环”的执行依据。当前 8a、8b、8c、8d 已完成，下一步进入 8e 打造交互。

## 背景

最小循环第 1–7 步（刷怪→战斗→掉落→背包→装备→交易→打造）后端已全部就绪并通过测试与 Play 验证。装备和交易已经具备玩家 UI 入口；打造仍只能靠 `execute_code` 触发，场景内的商人 / 打造台入口也尚未串联，因此循环还不能完全由人手闭合。

当前状态：

- **输入（8a 已完成）**：`Assets/Settings/InputSystem_Actions.inputactions` 已成为唯一输入源，`PlayerController` 不再直接轮询 `Keyboard.current`/`Gamepad.current`。`GameInput` 统一管理 Gameplay/UI 模式，切到 UI 时会禁用玩家移动。
- **UI（8b 已完成）**：`Main.unity` 已有常驻 `UIRoot`，挂载 `UIDocument`、`GamePanelSettings` 与 `HudController`；HUD 显示生命、金币和当前武器摘要。
- **反应式基础（8b 已完成）**：金币、背包、装备、打造及 Actor 注册变化均有领域事件，HUD 通过 Query 读取快照并在事件到达时刷新，不做每帧轮询。
- **背包与装备（8c 已完成）**：`InventoryPanelController` 通过只读快照绘制 10×6 背包，支持选择武器并发送 `EquipItemCommand`；换装采用原子交换，旧武器安全回包，HUD 与背包由领域事件同步刷新。
- **商店（8d 已完成）**：`GameMenuController` 统一管理背包 / 商店页签、关闭和 Gameplay/UI 模式；`ShopPanelController` 通过交易快照显示商人库存与玩家背包，并发送买卖 Command。成功交易由 `TradeCompletedEvent` 驱动刷新。

## 一、输入层重构

### 目标

- **单一输入源**：以 `InputSystem_Actions.inputactions` 为唯一输入来源，勾选 Generate C# Class 生成包装类。CLAUDE.md 要求键鼠与手柄兼容，该资产已具备两套控制方案。
- **动作图精简**（面向俯视 2D ARPG）：
  - 保留 `Move`（WASD / 左摇杆，已有绑定）、`Interact`（拾取 / 确认）。
  - `Attack` 预留手动攻击（当前为自动施法，先保留自动，`Attack` 作为后续手动/切换入口）。
  - `Look` 预留鼠标 / 右摇杆瞄准。
  - 删除不适用的 `Crouch` / `Jump` / `Previous` / `Next` / `Sprint`。
  - 新增 UI 开关动作：`ToggleInventory` / `ToggleShop` / `ToggleCraft`（或统一 `ToggleMenu` + 页签），并启用一套 `UI` action map 供面板内导航（手柄可用）。
- **输入封装**：新增 `Gameplay/Input/GameInput`（`IUtility` 或场景常驻单例），封装生成的 actions，统一 `Enable`/`Disable` 与取消。`PlayerController` 与各 UI 面板都从它取输入，替换直接轮询。
- **Action Map 切换**：战斗时启用 `Player` map，打开任一面板时切到 `UI` map（关闭后切回），杜绝"开背包时角色乱走"。切换逻辑集中在 `GameInput`。

### 8a 已落地

- `Player` map 精简为 `Move`、`Look`、`Attack`、`Interact`、`ToggleMenu`；控制方案仅保留 `Keyboard&Mouse` 与 `Gamepad`。
- `ToggleMenu` 使用 `Tab` / 手柄 `Start`；`UI/Submit` 使用 `Enter` / 手柄南键；`UI/Cancel` 使用 `Escape` / 手柄东键。
- Input Actions 已启用 C# 包装类生成，输出为 `Assets/Scripts/Runtime/Gameplay/Input/InputSystem_Actions.cs`，命名空间为 `DarkFlare`。
- `Assets/Scripts/Runtime/Gameplay/Input/GameInput.cs` 以 `IUtility` 注册到 `GameArchitecture`，负责包装 actions、读取移动输入、切换 Action Map 与释放资源。
- `PlayerController` 已改从 `GameInput.Move` 读取移动；`Interact`、`Attack`、`Look` 暂为后续交互保留动作，尚未接入新的玩法入口。
- `GameInputTests` 覆盖键盘/手柄移动、菜单开关、取消返回和重复模式切换。Input System 测试框架已通过 `Packages/manifest.json` 的 `testables` 启用。
- 验证结果：Unity EditMode 24/24 通过；Runtime/Test 工程编译 0 错误；Main 场景 Play Mode 下 Gameplay → UI → Gameplay 切换正确，控制台 0 警告、0 错误。

## 二、UI 架构（UIToolkit）

### 技术选型与根节点

- 采用 UIToolkit（CLAUDE.md 优先；复杂动画等场景才用 UGUI）。
- 场景常驻一个 UI 根：`UIDocument` + `PanelSettings` 资产 + 主题样式（TSS）+ `EventSystem` 搭配 `InputSystemUIInputModule`（供指针 / 手柄导航）。
- UI 资源目录约定：UXML/USS 放 `Assets/UI/`，`PanelSettings` 放 `Assets/Settings/UI/`，Unity 默认运行时主题位于 `Assets/UI Toolkit/UnityThemes/`。

### 接入 QFramework

- 每个 UI 面板脚本实现 `IController`：
  - 用 **Query** 拉取初始状态（如 `InventoryModel.Grid`、`GetItemPriceQuery`、`GetCraftingCostQuery`）。
  - 用 **RegisterEvent** 响应状态变化刷新。
  - 用 **SendCommand** 触发操作（`EquipItemCommand` / `BuyItemCommand` / `SellItemCommand` / `CraftItemCommand`）。
  - 严守"Controller 只注册事件 + 发命令，不直接改 Model"。
- UI 所需组件用 `[SerializeField]` 标记并在 `OnValidate` 中检查 / 尝试创建（CLAUDE.md）。

### 反应式前提（关键基础设施，先于面板）

补一批领域变化事件，由对应 System/Model 在状态变更时 `SendEvent`，UI 订阅刷新——沿用现有 `CombatEvents` 的事件驱动风格（而非每帧 Query 轮询）：

| 事件 | 发送方 | 触发时机 |
| --- | --- | --- |
| `GoldChangedEvent` | `InventoryModel` 变动路径 | 金币增减（发放 / 买卖 / 打造扣费） |
| `InventoryChangedEvent` | 背包增删路径 | 拾取入包、买入、卖出移除、穿戴移出 |
| `EquipmentChangedEvent` | `CombatSystem.EquipWeapon` | 穿戴 / 卸下 |
| `ItemCraftedEvent` | `CraftingSystem.Craft` | 打造成功导致物品词条变化 |
| `TradeCompletedEvent` | `TradingSystem` | 买入 / 卖出全部状态提交完成后 |
| `ActorRegisteredEvent` / `ActorUnregisteredEvent` | `CombatSystem` | 玩家或怪物加入 / 离开战斗模型 |

血量已有 `ActorDamagedEvent` / `ActorRevivedEvent` 可直接用。补齐这批事件是本步"打基础"的实质内容之一。

### 面板结构（分步实现）

1. **HUD（常驻）**：血量条、金币、当前武器 / 词条摘要。
2. **背包面板**：复用 `InventoryModel.Grid`（`InventoryGrid.Placements`）画二维格子；点击物品弹出穿戴 / 出售 / 打造操作。
3. **商店面板**：商人库存列表 + 买价 / 背包列表 + 卖价，点击买卖。
4. **打造面板**：选背包物品 + 四个操作按钮 + 成本显示（`GetCraftingCostQuery`）。

### UI 美术资源

处理 UI 时，必要时可用 unity-mcp 的 `generate_image` 工具生成图片（图标、按钮底图、物品占位图等），生成后直接导入项目为纹理 / 精灵。用于替代占位纯色块、快速补齐 UI 视觉。原则：

- 仅在确有需要时生成（先功能后美观，能用占位方块就先用占位）。
- 生成的资源纳入常规目录（如 `Assets/UI/` 或 `Assets/Art/Textures/`），并像其他美术资源一样管理。
- 优先生成语义清晰、风格统一的小图标 / 底图，不追求高精度。

## 三、分步路线（后续逐条执行，各自 Play 验证）

- **8a 输入层（已完成）**：生成 / 精简 actions，`GameInput` 封装，`PlayerController` 改用它，`Player`/`UI` map 切换。已验证键盘 + 手柄移动、菜单开关与 map 切换；`Interact` 的玩法入口留到 8f 串联。
- **8b UI 基础 + HUD（已完成）**：`UIDocument`/`PanelSettings`/主题/`EventSystem` 根节点、UI Controller 接入 QFramework、补上表领域事件、做只读 HUD。已验证运行时玩家生命、金币和武器摘要，以及金币事件驱动刷新。
- **8c 背包 + 装备（已完成）**：背包格子面板，点击物品→穿戴（`EquipItemCommand`）；已验证物品尺寸与位置、装备操作、HUD 同步和 Gameplay/UI 模式互斥。
- **8d 商店（已完成）**：共享菜单路由与商店面板买卖（`BuyItemCommand` / `SellItemCommand`）；已验证买卖后的金币、商人库存、玩家背包、HUD 和输入模式同步。
- **8e 打造**：打造面板四操作（`CraftItemCommand`）。验证：手玩打造，词条 / 价值变化。
- **8f 循环收尾**：入口串联（`Interact` 开面板 / 走到商人），一轮完整可玩循环，感受构筑变化。

## 四、8b 已落地

- `Assets/UI/GameRoot.uxml` 组合 `Hud.uxml` 与 `Hud.uss`；`GamePanelSettings.asset` 按 1920×1080 参考分辨率缩放。
- `Main.unity` 复用唯一 `EventSystem`，`InputSystemUIInputModule` 已指向项目 `InputSystem_Actions.inputactions` 的 `UI` map；没有创建重复 EventSystem。
- `HudController` 实现 `IController`，通过 `GetHudSnapshotQuery` 获取玩家生命、金币、武器摘要，订阅战斗和领域事件刷新。
- `GameplayEvents.cs` 集中声明金币、背包、装备、打造与 Actor 注册事件；状态写入失败时不发送成功事件。
- 验证结果：Unity EditMode 28/28 通过；Runtime/Test 工程编译 0 错误；Play Mode 显示 100/100、金币 100、未装备，金币增加 7 后 HUD 同步为 107；控制台 0 警告、0 错误。

## 五、8c 已落地

### 落地结果

- `InventoryGrid.TryExchange` 与 `InventoryModel.TryExchangeItem` 先验证替换物能否放入，再一次性提交格子变化；失败时背包、装备和领域事件均保持不变。
- `CombatSystem.EquipWeapon` 与 `EquipItemCommand` 返回 `bool`，拒绝空参数、非武器、重复装备和不在背包中的物品；换装成功时旧武器回到背包。
- `GetInventorySnapshotQuery` 提供只读 `InventorySnapshot` / `InventoryItemSnapshot`，包含格子尺寸、`RectInt` 占位、显示数据、当前武器和玩家引用。
- `Inventory.uxml` / `Inventory.uss` 已通过 Template 接入 `GameRoot.uxml`；`Main.unity` 的现有 `UIRoot` 复用同一个 `UIDocument`，新增 `InventoryPanelController`，没有创建重复 `EventSystem`。
- `GameMenuController` 统一订阅 `GameInput.ModeChanged` 并管理共享遮罩、页签和关闭；`InventoryPanelController` 负责背包快照、事件刷新与穿戴 Command，不再各自持有菜单输入路由。
- 验证结果：Runtime/Test 工程编译均为 0 错误；Unity EditMode 32/32 通过；Play Mode 下背包从 1 件武器变为 0 件、当前武器与 HUD 同步为“大剑 · Rare · 0 条词缀”，Gameplay/UI map 互斥切换正确；visual tree、1920×1080 前后渲染与控制台检查通过，0 警告、0 错误。

### 范围与前置修正

- 8c 当时只完成背包浏览、物品选择和武器穿戴；商店已在 8d 完成，打造、拖拽换位、物品旋转、堆叠与重量仍留在后续步骤。
- 当前 `CombatSystem.EquipWeapon` 会直接覆盖旧武器，`EquipItemCommand` 也没有成功 / 失败返回。开放玩家操作前必须先补原子交换：新武器只能来自背包，类型必须为 `Weapon`；换装成功时旧武器回到背包，空间不足或参数无效时所有状态与事件都保持不变。
- `GameInput` 已自行监听 `Player/ToggleMenu` 与 `UI/Cancel` 并切换 Action Map。面板 Controller 订阅 `GameInput.ModeChanged` 控制显示，不重复监听底层 InputAction。首版保持 Tab / 手柄 Start 打开，Escape / 手柄东键关闭。

### 执行顺序

1. **补装备事务语义**：在 `InventoryModel` 提供无中间事件的背包物品交换能力；把 `CombatSystem.EquipWeapon` 收敛为可返回 `bool` 的安全穿戴入口，并让 `EquipItemCommand` 继承 `AbstractCommand<bool>`。覆盖首次穿戴、替换武器、非武器、物品不在背包、旧武器放不回背包等分支。
2. **新增背包快照 Query**：定义只读 `InventorySnapshot` / `InventoryItemSnapshot`，包含 10×6 尺寸、`RectInt` 占位、物品引用、名称、类型、稀有度、词缀数量和当前武器。Controller 只消费 Query 结果，不直接读取 Model。
3. **创建背包 UI 资源**：使用 unityMCP 在 `Assets/UI/` 新增 `Inventory.uxml` 与 `Inventory.uss`，通过 Template 接入 `GameRoot.uxml`。面板默认隐藏，包含格子区、选中详情、当前武器、穿戴按钮、关闭按钮和空背包提示；延续 HUD 的纯色占位风格，不新增美术资源。
4. **实现面板 Controller**：在现有 `UIRoot` 上增加 `InventoryPanelController`，复用同一个 `UIDocument`。8d 已将 `GameInput.ModeChanged`、共享遮罩、页签与关闭收敛到 `GameMenuController`；键鼠点击与手柄导航仍共用同一组选中状态。
5. **接入命令与事件刷新**：穿戴按钮只发送 `EquipItemCommand`。成功后由 `InventoryChangedEvent` / `EquipmentChangedEvent` 同步刷新背包和 HUD；失败时保留当前选择并显示简短原因，不制造成功事件。选中物品被移除时清空选择或移动到下一个可用物品。
6. **自动化验证**：补 EditMode 测试，覆盖背包快照坐标、原子换装与回滚、命令返回值、事件只在成功时发送，以及键盘 / 手柄触发 Gameplay ↔ UI 模式。执行全量 EditMode、Runtime/Test 工程编译和 `git diff --check`。
7. **Play 与视觉验证**：通过 unityMCP 先检查 `project/info`、Editor 状态和现有 `UIRoot`，创建资源后检查 visual tree；进入 `Main.unity` 完成“拾取武器 → 打开背包 → 选择 → 穿戴 → 关闭”，二次换装确认旧武器回包。分别验证键鼠和手柄导航、面板打开时玩家不移动、HUD 武器摘要变化、控制台无警告 / 错误，并用 `render_ui` 留下最终画面。

### 完成标准（已满足）

- 背包按 `InventoryGrid.Placements` 正确显示物品尺寸与位置，空背包和无玩家状态不会报错。
- Tab / Start 打开背包，Escape / 手柄东键关闭；打开期间 Gameplay map 禁用，关闭后恢复。
- 只能穿戴背包中的武器；换装不会丢失旧武器，任何失败都不会留下半完成状态或错误事件。
- HUD 与背包在穿戴后同步刷新，键鼠和手柄都能完成完整操作。
- 全量测试、编译、Play、视觉树和渲染检查通过，文档同步后再标记 8c 完成。

## 六、8d 已落地

### 落地结果

- `GetShopSnapshotQuery` 提供只读 `ShopSnapshot` / `ShopItemSnapshot`，统一返回金币、商人库存买价和玩家背包卖价；Controller 不直接读取或修改 Model。
- `TradingSystem` 在买入 / 卖出全部状态提交成功后发送 `TradeCompletedEvent`；失败分支不发送完成事件，也不改变金币、库存或背包。
- `GameMenuController` 统一管理共享菜单遮罩、背包 / 商店页签、关闭和默认焦点；`InventoryPanelController` 与 `ShopPanelController` 各自只负责页面数据和操作。
- `Shop.uxml` / `Shop.uss` 与 `GameMenu.uss` 已通过 `GameRoot.uxml` 接入现有 `UIRoot`，复用唯一 `UIDocument` 与 `EventSystem`。商店支持商人 / 玩家列表、价格、稀有度、词缀数量、金币、失败反馈和买卖按钮。
- 首版卖出物品直接离开玩家背包，不加入商人库存，因此不提供回购；多商人和场景内商人实体留到后续。

### 验证结果

- Unity EditMode 全量 35/35 通过；Runtime/Test 工程编译均为 0 错误，新脚本单独验证 0 警告、0 错误。
- Play Mode 冻结世界状态后完成真实按钮链路：买入使金币 100→85、商人库存 3→2、玩家背包 0→1；随后卖出使金币 85→89、背包 1→0，HUD 金币同步。
- 背包 / 商店页签互斥显示；关闭菜单后遮罩与页面隐藏，输入从 UI map 恢复到 Gameplay map。
- 1920×1080 渲染检查无明显重叠；Play 控制台 0 警告、0 错误。

## 约定与边界

- 本文档同时记录规划与落地状态；每个子步完成后更新对应标记和验证结果，保持文档不落后于代码。
- 美术从简：延续占位方块 / 纯色风格，UI 先功能后美观。
- 表中领域事件已按当前代码同步；后续新增事件仍以实际写入路径为准。
