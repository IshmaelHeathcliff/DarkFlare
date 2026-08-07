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
- `Theme.uss`

`Main.unity/UIRoot` 挂载 `UIDocument`、`GameMenuController`、四个数据显示 Controller 和 `InteractionPromptController`。`InputSystemUIInputModule` 引用同一份 Input Actions 的 `UI` Action Map。

阶段 0 为背包页接入深石板面板、普通 / 焦点 / 禁用格子纹理和大剑武器图标。纹理通过 USS 静态引用，动态按钮只增加表现子元素；选择、装备、事件刷新和默认焦点逻辑不变。视觉状态同时使用边框形状、亮度和局部色彩，手柄焦点不依赖鼠标悬停。

阶段 1 抽取了共用 `ItemDetailSnapshot`、`ItemDetailFormatter` 和 `ItemDetailView`。背包、商店、打造现在显示一致的基础伤害、隐式、前缀和后缀中文详情；鼠标悬停与手柄焦点只临时预览，点击或 Submit 固定选择。背包和打造按 `InstanceId + 后备索引` 恢复固定选择。

商店额外使用表现层 `ShopViewState` 保存商人 / 玩家两列的选择、后备索引和滚动偏移，以及活动来源、焦点目标和交易反馈。列表重建后优先恢复同一实例；交易移除当前物品时选择原索引下一件，没有下一件时选择上一件，原列为空才切换来源。布局完成后通过带代次的 UI Toolkit 调度恢复滚动与焦点，页签往返和关闭后重新与同一商人交互仍保留本次运行期状态。

阶段 2 把背包装备区扩展为武器、护甲、左戒指和右戒指四个可聚焦按钮。武器与护甲候选自动确定唯一目标槽；饰品必须先明确选择左或右戒指，装备按钮才会启用。选中槽位可以查看当前物品并卸下，选中背包候选则显示目标槽比较。操作完成后统一重查快照并恢复合理选择、目标槽和焦点，键鼠与手柄共享同一状态机。

阶段 5 以 `Theme.uss` 统一 HUD、背包、商店、打造和详情的背景、边框、稀有度与按钮状态。动态物品图标通过 `SpriteAssetLoader` 预热并由 `ItemVisualPresenter` 写入各列表、装备槽、详情和 HUD；固定生命、金币、页签、槽位及关闭图标继续由 USS 引用。空槽使用剪影，图标缺失使用统一回退，所有页面保留原有选择、滚动和焦点恢复逻辑。

| Controller | 只读数据入口 | 写入入口 | 主要刷新来源 |
| --- | --- | --- | --- |
| `HudController` | `GetHudSnapshotQuery` | 无 | Actor、生命、金币、装备事件 |
| `InventoryPanelController` | `GetInventorySnapshotQuery` | `EquipItemCommand`、`UnequipItemCommand` | 背包、装备事件 |
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
- 阶段 0 收尾时全量 EditMode 48 项通过，E / 手柄北键、菜单上下文、暂停恢复和目标失效路径均已覆盖。
- 阶段 0 体验专项 PlayMode 使用虚拟键盘和手柄验证 Tab / Esc、Start / B、默认焦点、物品选中与装备，并在三档 16:9 分辨率检查主要元素边界。
- 1920×1080 下完成 visual tree、渲染和 Play 控制台检查。
- 阶段 1 全量 EditMode 60/60 通过；PlayMode 8 项中 6 项通过、2 项为包内既有忽略测试。商店购买状态链及商店 / 打造 1280×720、1920×1080、2560×1440 布局边界均通过。
- 阶段 2 全量 EditMode 70/70 通过；PlayMode 9 项中 7 项通过、2 项为包内既有忽略测试。真实 Main 菜单中已覆盖键盘装备武器 / 护甲、手柄显式选择左右戒指、卸下，以及背包装备区三档分辨率边界。
- 阶段 5 全量 EditMode 92/92 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游用例按原标记跳过、0 失败。1280×720 背包、1920×1080 商店与 2560×1440 打造实机渲染均无越界，按钮的焦点、选中与禁用状态清晰，最终 Console 为 0 错误、0 警告。

以上数据是首版收尾时的验证记录；修改输入资产、菜单路由、UXML 或场景组件后，应重新验证键鼠与手柄两条路径。

## 当前限制

- HUD、背包、商店、打造和详情已完成首版统一视觉；菜单切换仍以即时显隐为主，暂未加入完整过场动画与音效。
- 背包不支持拖拽、旋转、堆叠和重量，列表选择、目标槽选择、穿戴和卸下是当前主要交互。
- 商店和打造共用首版单实例运行时数据；商店状态可在同一商人运行期内保留，但不支持多商人独立状态或持久化。
- `Attack`、`Look` 尚未接入手动战斗操作。
- PlayMode 已包含角色动画状态与阶段 0 背包体验两项项目测试；包内测试另有上游不稳定用例按原标记跳过。

完整玩法链见 [首版玩法循环](./gameplay-loop.md)，装备规则见 [装备系统](./equipment-system.md)，打造页的数据和事务规则见 [打造系统](./crafting-system.md)。
