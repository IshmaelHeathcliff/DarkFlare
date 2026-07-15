# 输入系统与 UI 设计

本文档规划玩家输入层重构与 UIToolkit UI 架构，是最小循环第 8 步"用 `Main.unity` 串成一轮完整循环"的前置基础设施，供后续分步（8a–8f）逐条执行。

## 背景

最小循环第 1–7 步（刷怪→战斗→掉落→背包→装备→交易→打造）后端已全部就绪并通过测试与 Play 验证，但**装备、交易、打造目前只能靠 `execute_code` 触发**，玩家手上没有操作入口，循环无法由人手玩闭合。要让玩家真正走一轮并"感到构筑变化"，需先补两块基础设施：**玩家输入层**与 **UI**。

现状：

- **输入**：`Assets/Scripts/Gameplay/Actors/PlayerController.cs` 直接轮询 `Keyboard.current`/`Gamepad.current`，未使用已存在的 `Assets/Settings/InputSystem_Actions.inputactions`（含 `Keyboard&Mouse` 与 `Gamepad` 两套控制方案，动作为默认模板 `Move`/`Look`/`Attack`/`Interact`/`Crouch`/`Jump`/`Previous`/`Next`/`Sprint`）。直接轮询无法重绑定、无法与 UI 导航统一、面板打开时会误移动角色。
- **UI**：完全没有（`Assets/Scripts/UI` 为空占位，无 UXML/USS/UIDocument）。UIToolkit（Unity 6 内置）与 TextMeshPro 可用。
- **反应式基础**：目前只有战斗事件（`ActorDamagedEvent`/`ActorDiedEvent`/`ActorRevivedEvent`，见 `Gameplay/Combat/CombatEvents.cs`），**没有金币、背包、装备、打造的变化事件**，UI 无法响应式刷新。

## 一、输入层重构

### 目标

- **单一输入源**：以 `InputSystem_Actions.inputactions` 为唯一输入来源，勾选 Generate C# Class 生成包装类。CLAUDE.md 要求键鼠与手柄兼容，该资产已具备两套控制方案。
- **动作图精简**（面向俯视 2D ARPG）：
  - 保留 `Move`（WASD / 左摇杆，已有绑定）、`Interact`（拾取 / 确认）。
  - `Attack` 预留手动攻击（当前为自动施法，先保留自动，`Attack` 作为后续手动/切换入口）。
  - `Look` 预留鼠标 / 右摇杆瞄准。
  - 删除不适用的 `Crouch` / `Jump` / `Sprint`。
  - 新增 UI 开关动作：`ToggleInventory` / `ToggleShop` / `ToggleCraft`（或统一 `ToggleMenu` + 页签），并启用一套 `UI` action map 供面板内导航（手柄可用）。
- **输入封装**：新增 `Gameplay/Input/GameInput`（`IUtility` 或场景常驻单例），封装生成的 actions，统一 `Enable`/`Disable` 与取消。`PlayerController` 与各 UI 面板都从它取输入，替换直接轮询。
- **Action Map 切换**：战斗时启用 `Player` map，打开任一面板时切到 `UI` map（关闭后切回），杜绝"开背包时角色乱走"。切换逻辑集中在 `GameInput`。

## 二、UI 架构（UIToolkit）

### 技术选型与根节点

- 采用 UIToolkit（CLAUDE.md 优先；复杂动画等场景才用 UGUI）。
- 场景常驻一个 UI 根：`UIDocument` + `PanelSettings` 资产 + 主题样式（TSS）+ `EventSystem` 搭配 `InputSystemUIInputModule`（供指针 / 手柄导航）。
- UI 资源目录约定：UXML/USS 放 `Assets/UI/`，`PanelSettings` 与主题放 `Assets/Settings/`。

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

- **8a 输入层**：生成 / 精简 actions，`GameInput` 封装，`PlayerController` 改用它，`Player`/`UI` map 切换。验证：键鼠 + 手柄移动 / 交互正常，切 map 时不穿透移动。
- **8b UI 基础 + HUD**：`UIDocument`/`PanelSettings`/主题/`EventSystem` 根节点、UI Controller 接入 QFramework、补上表领域事件、做只读 HUD。验证：受击掉血、`execute_code` 改金币 / 穿戴时 HUD 实时刷新。
- **8c 背包 + 装备**：背包格子面板，点击物品→穿戴（`EquipItemCommand`）。验证：手玩穿戴，伤害 / HUD 变化。
- **8d 商店**：商店面板买卖（`BuyItemCommand` / `SellItemCommand`）。验证：手玩买卖，金币 / 背包变化。
- **8e 打造**：打造面板四操作（`CraftItemCommand`）。验证：手玩打造，词条 / 价值变化。
- **8f 循环收尾**：入口串联（`Interact` 开面板 / 走到商人），一轮完整可玩循环，感受构筑变化。

## 约定与边界

- 本文档为规划，尚未实现；实现随各子步推进，届时回填"已落地"标记，保持文档不落后于代码（CLAUDE.md）。
- 美术从简：延续占位方块 / 纯色风格，UI 先功能后美观。
- 表中领域事件为规划名，实现时以代码为准并同步本表。
