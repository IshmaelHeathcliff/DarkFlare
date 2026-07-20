# 输入与运行时 UI

## 模块职责

输入与运行时 UI 模块负责统一键鼠 / 手柄输入、Gameplay / UI 模式切换、HUD 展示、背包 / 商店 / 打造菜单，以及场景交互提示和菜单暂停。

首版复用一个 `UIDocument`、一个 `PanelSettings` 和唯一 `EventSystem`。界面 Controller 通过 Query、Command 与领域 Event 接入 QFramework，不直接修改运行时 Model。

## 输入层

`Assets/Settings/InputSystem_Actions.inputactions` 是唯一输入源，并生成 `InputSystem_Actions.cs`。`GameInput` 作为 `IUtility` 注册到 `GameArchitecture`，统一管理：

- `Player` Action Map：移动、瞄准、攻击预留、世界交互和菜单开关。
- `UI` Action Map：导航、提交、取消和指针操作。
- Gameplay 模式下只启用 `Player`，UI 模式下只启用 `UI`。
- `PlayerController` 从 `GameInput.Move` 读取移动，不直接轮询设备。
- `InteractPerformed` 只在 Gameplay Action Map 有效，用于世界目标交互。
- `ModeChanged` 驱动菜单显示、默认焦点和玩法暂停状态。
- `Dispose` 解除输入订阅并释放生成的 Actions。

首版关键绑定：

| 操作 | 键鼠 | 手柄 |
| --- | --- | --- |
| 移动 | WASD | 左摇杆 |
| 世界交互 | E | 北键 |
| 打开随身背包 | Tab | Start |
| UI 提交 | Enter | 南键 |
| UI 返回 / 关闭 | Escape | 东键 |

`Look` 与 `Attack` 仍作为后续手动瞄准和攻击入口保留；当前战斗原型继续使用自动攻击逻辑。

## UI 组成

`Assets/UI/GameRoot.uxml` 组合 HUD、共享菜单遮罩和三个功能模板：

- `Hud.uxml` / `Hud.uss`
- `Inventory.uxml` / `Inventory.uss`
- `Shop.uxml` / `Shop.uss`
- `Crafting.uxml` / `Crafting.uss`
- `GameMenu.uss`

`Main.unity/UIRoot` 挂载 `UIDocument`、`GameMenuController`、四个数据显示 Controller 和 `InteractionPromptController`。`InputSystemUIInputModule` 引用同一份 Input Actions 的 `UI` Action Map。

| Controller | 只读数据入口 | 写入入口 | 主要刷新来源 |
| --- | --- | --- | --- |
| `HudController` | `GetHudSnapshotQuery` | 无 | Actor、生命、金币、装备事件 |
| `InventoryPanelController` | `GetInventorySnapshotQuery` | `EquipItemCommand` | 背包、装备事件 |
| `ShopPanelController` | `GetShopSnapshotQuery` | `BuyItemCommand`、`SellItemCommand` | 交易、金币、背包事件 |
| `CraftingPanelController` | `GetCraftingSnapshotQuery` | `CraftItemCommand` | 打造、金币、背包事件 |
| `InteractionPromptController` | 交互焦点消息 | 无 | 焦点、输入模式和 Actor 状态变化 |

所有列表和详情都来自只读快照。操作成功后由对应 System / Model 发送领域 Event，再触发 HUD 和面板重新查询；失败分支不伪造成功事件。

## 菜单上下文

`GameMenuController` 统一管理遮罩、页签、关闭行为和默认焦点。`GameMenuAccess` 按打开来源限制功能：

| 来源 | 可用页面 | 初始页面 |
| --- | --- | --- |
| Tab / Start | 背包 | 背包 |
| 商人交互 | 背包、商店 | 商店 |
| 打造台交互 | 背包、打造 | 打造 |

不可用页签保留可见但禁用，避免玩家在任意位置远程交易或打造。关闭菜单后访问范围恢复为随身背包上下文。

## 世界交互与暂停

1. `PlayerInteractionController` 维护进入触发范围的 `WorldInteractionTarget`，按距离选择最近有效目标。
2. 焦点变化通过 `SetInteractionFocusCommand` 发布消息，`InteractionPromptController` 显示 `E / Y · {目标名称}`。
3. 玩家触发交互后，`OpenGameMenuCommand` 请求打开目标对应的菜单上下文。
4. `GameMenuController` 切换到 UI 模式，并通过 `SetGameplayPausedCommand` 请求 `GameplayPauseSystem` 暂停玩法时间。
5. 取消、关闭、对象禁用或销毁时恢复原时间倍率和 Gameplay 输入；目标仍有效时重新显示提示。

`Main.unity` 中的 `Merchant` 与 `CraftingStation` 使用独立触发范围。动态生成的玩家通过 Player Prefab 上的 `PlayerInteractionController` 接入交互，不依赖场景预放玩家。

## 首版验证记录

- 输入阶段覆盖键盘 / 手柄移动、菜单开关、UI 取消和 Action Map 互斥。
- HUD、背包、商店、打造均完成真实 UI 操作链路与事件刷新验证。
- 场景交互阶段全量 EditMode 45 项通过，E / 手柄北键、菜单上下文、暂停恢复和目标失效路径均已覆盖。
- 1920×1080 下完成 visual tree、渲染和 Play 控制台检查。

以上数据是首版收尾时的验证记录；修改输入资产、菜单路由、UXML 或场景组件后，应重新验证键鼠与手柄两条路径。

## 当前限制

- UI 以功能和占位样式为主，尚未建立正式视觉规范、动画和音效反馈。
- 背包不支持拖拽、旋转、堆叠和重量，列表选择与穿戴是当前主要交互。
- 商店和打造共用首版单实例运行时数据，不支持多商人或多打造台配置隔离。
- `Attack`、`Look` 尚未接入手动战斗操作。
- PlayMode 测试程序集仍为空，当前自动化覆盖主要来自 EditMode 测试。

完整玩法链见 [首版玩法循环](./gameplay-loop.md)，打造页的数据和事务规则见 [打造系统](./crafting-system.md)。
