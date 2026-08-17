# 输入与运行时 UI

## 模块职责

输入与运行时 UI 模块负责统一键鼠 / 手柄输入、Gameplay / UI 模式切换、HUD 展示、背包 / 商店 / 打造菜单，以及场景交互提示和菜单暂停。

首版复用一个 `UIDocument`、一个 `PanelSettings` 和唯一 `EventSystem`。界面 Controller 通过 Query、Command 与领域 Event 接入 QFramework，不直接修改运行时 Model。

## 输入层

`Assets/Settings/InputSystem_Actions.inputactions` 是唯一输入源，并生成 `InputSystem_Actions.cs`。`GameInput` 作为 `IUtility` 注册到 `GameArchitecture`，统一管理：

- `Player` Action Map：移动、瞄准、攻击预留、世界交互和菜单开关。
- `UI` Action Map：导航、提交、取消、物品拿起 / 放置和指针操作。
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
| 物品拿起 / 放置 | Space | 西键 |
| UI 返回 / 关闭 | Escape | 东键 |

`Look` 与 `Attack` 仍作为后续手动瞄准和攻击入口保留；当前战斗原型继续使用自动攻击逻辑。

### 编辑器进入 Play Mode 约束

当前输入与 QFramework 生命周期的可提交基线要求 `ProjectSettings/EditorSettings.asset` 中的 `EnterPlayModeOptions` 保持为 `0`，即不启用 `DisableDomainReload` 或 `DisableSceneReload`。关闭 Domain Reload 会保留静态架构和输入对象状态，曾导致进入 Play Mode 后移动输入失效。

为加快专项测试，可以在测试期间临时调整 Enter Play Mode Options，但该值只属于本地测试环境，不是可提交的项目配置。测试完成、失败或中止后都必须恢复为 `0`，再回归验证首次运行、停止后再次运行、键盘 / 手柄移动、Gameplay / UI Action Map 切换及菜单关闭后恢复移动。

提交前必须检查 `ProjectSettings/EditorSettings.asset` 的实际值和 Git diff：`EnterPlayModeOptions` 非 `0` 时禁止提交；只有测试产生的 Editor Settings 变动或 Unity 自动格式化差异也不得直接纳入提交。若未来希望持久启用快速进入 Play Mode，必须先完成 `GameArchitecture` 静态状态、`GameInput` Action 生命周期和场景对象重建的专项审计，并独立评审该配置变更。

## UI 组成

`Assets/UI/GameRoot.uxml` 组合 HUD、共享菜单遮罩和三个功能模板：

- `Hud.uxml` / `Hud.uss`
- `Inventory.uxml` / `Inventory.uss`
- `Shop.uxml` / `Shop.uss`
- `Crafting.uxml` / `Crafting.uss`
- `ItemWorkbench.uxml` / `ItemWorkbench.uss`
- `GameMenu.uss`
- `Theme.uss`

`Main.unity/UIRoot` 挂载 `UIDocument`、`GameMenuController`、四个数据显示 Controller 和 `InteractionPromptController`。`InputSystemUIInputModule` 引用同一份 Input Actions 的 `UI` Action Map。

阶段 0 为背包页接入深石板面板、普通 / 焦点 / 禁用格子纹理和大剑武器图标。纹理通过 USS 静态引用，动态按钮只增加表现子元素；选择、装备、事件刷新和默认焦点逻辑不变。视觉状态同时使用边框形状、亮度和局部色彩，手柄焦点不依赖鼠标悬停。

阶段 1 抽取了共用 `ItemDetailSnapshot`、`ItemDetailFormatter` 和 `ItemDetailView`。背包、商店、打造现在显示一致的基础伤害、隐式、前缀和后缀中文详情；鼠标悬停与手柄焦点只临时预览，点击或 Submit 固定选择。背包和打造按 `InstanceId + 后备索引` 恢复固定选择。

商店额外使用表现层 `ShopViewState` 保存商人 / 玩家两列的选择、后备索引、活动来源、焦点目标和交易反馈。列表重建后优先恢复同一实例；交易移除当前物品时选择原索引下一件，没有下一件时选择上一件，原列为空才切换来源。商人背包现为固定 10×6 网格，不再保存滚动偏移；布局完成后只按刷新代次恢复焦点，页签往返和关闭后重新与同一商人交互仍保留本次运行期状态。

阶段 2 把背包装备区扩展为武器、护甲、左戒指和右戒指四个可聚焦按钮。武器与护甲候选自动确定唯一目标槽；饰品必须先明确选择左或右戒指，装备按钮才会启用。选中槽位可以查看当前物品并卸下，选中背包候选显示目标槽位，不再占用右侧区域显示装备比较。操作完成后统一重查快照并恢复合理选择、目标槽和焦点，键鼠与手柄共享同一状态机。

阶段 5 以 `Theme.uss` 统一 HUD、背包、商店、打造和详情的背景、边框、稀有度与按钮状态。动态物品图标通过 `SpriteAssetLoader` 预热并由 `ItemVisualPresenter` 写入各列表、装备槽、详情和 HUD；固定生命、金币、页签、槽位及关闭图标继续由 USS 引用。空槽使用剪影，图标缺失使用统一回退，所有页面保留原有选择和焦点恢复逻辑。

alpha 0.1.0 将三套物品页面合并为共享工作台：扩容后的四槽装备区位于共享玩家列上方，10×6 背包位于下方；该列在商店和打造中最大占内容区 49%，右侧上下文获得主要空间。商店使用更大的固定 10×6 商人背包且不显示滚动条，玩家与商人格内只保留图标。装备区保留两个非交互扩展位置，但不提前增加领域槽位。48 px 背包格与 4 px 间隔同时作为装备槽尺寸基准：武器/护甲和打造槽为 100×152，左右戒指为 60×60 方形；背包面板压缩为 408 px，装备区取得共享列的剩余高度。三个右侧模板仅当前页参与布局，因此背包、商店、打造与共享玩家列始终等高。运行时只有一个顶层 `ItemTooltipView`，以 360×680 固定展开且不使用滚动条，顶部与主面板对齐；玩家/打造内容停靠左侧，商人内容停靠右侧。拖放后等待指针离开并再次主动预览再显示，整个浮窗子树输入穿透；显示时先隐藏完成几何定位，再切换为可见。菜单每次重新打开都会清空玩家物品选择，默认焦点落在当前页签，没有物品预览时隐藏浮窗。主面板采用 1160×940 纵向布局，面板与槽位使用统一 1 px USS 细边框，不再缩放旧粗 Frame。

鼠标物品操作采用点击与拖拽分离：左键按下不会立即拿起，只有持续按住并移动超过 10 px 才进入拖拽；直接松开只固定选择。背包物品与装备槽共用唯一选择描边，键盘焦点或鼠标悬浮预览会临时接管该描边，离开后再恢复固定选择，任意时刻不会出现两个同级高亮。商店上下文中，玩家背包物品支持右键直接出售，商人库存仍需左键选择并使用购买按钮；玩家背包最右侧继续向右会转入商人背包。打造目标放入 2×3 槽位后从背包视图移除，取回后恢复。物品详情在 4–6 条显式词缀时自动切换紧凑排版，优先显示词条效果，词条名降为小号辅助信息，并继续保持无滚动完整展开。

alpha 0.1.0 的 HUD 移除武器卡片和属性详情，只保留生命、金币与交互提示。“当前属性”移动到背包页右侧，与装备操作共用上下文区域。alpha 0.1.4 在生命条下增加当前 / 最大法力条与短暂的技能拒绝提示；法力不足时显示所需数值，下次成功释放时清除，不把属性详情重新放回 HUD。
`GetHudSnapshotQuery` 直接读取 `CombatActor.Stats` 和资源快照，并按 `StatIds.All` 的稳定顺序展示当前登记的全部 23 项属性；没有玩家时也保留完整字段并显示占位值。抗性显示与伤害结算一致，限制在 `-100%` 至 `75%`，百分比属性统一带 `%`，换装或资源事件后重新查询。

装备槽图标使用与背包占格相同的 `scale-to-fit` 规则，并占满槽位扣除统一 4 px 内边距后的区域；槽位名称改为底部半透明覆盖标签，不再参与 Flex 排版压缩图标。武器、护甲装备后的图标可见范围不得小于其 2×3 背包占格中的图标，戒指槽同样不得小于 1×1 背包占格。

| Controller | 只读数据入口 | 写入入口 | 主要刷新来源 |
| --- | --- | --- | --- |
| `HudController` | `GetHudSnapshotQuery` | 无 | Actor、生命 / 法力资源、技能释放、金币、装备事件 |
| `InventoryPanelController` | `GetInventorySnapshotQuery`、`GetHudSnapshotQuery` | 背包移动、精确装备 / 卸下与戒指交换 Commands | 背包、装备、Actor 事件 |
| `ShopPanelController` | `GetShopSnapshotQuery` | `BuyItemCommand`、`SellItemCommand` | 交易、金币、背包事件 |
| `CraftingPanelController` | `GetCraftingSnapshotQuery` | 槽内目标的 `CraftItemCommand` | 打造、金币、背包事件 |
| `InteractionPromptController` | 交互焦点消息 | 无 | 焦点、输入模式和 Actor 状态变化 |

所有格子和详情都来自只读快照。打造页不再维护词缀列表，而是取得槽内物品 14 个操作变体的成本、可用性和失败原因；背包选择仅作为详情与“放入”候选，不能直接执行打造。鼠标拖入或键盘 / 手柄确认“放入打造槽”后才锁定目标，取回或目标离开背包时立即禁用全部操作。操作成功后由对应 System / Model 发送领域 Event，再触发 HUD 和面板重新查询，失败分支不伪造成功事件。

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
- HUD 属性窗口专项 EditMode 26/26、PlayMode 1/1 通过；PlayMode 覆盖键鼠 / 手柄换装、属性刷新及
  1280×720、1920×1080、2560×1440 三档布局边界。
- 阶段 0 收尾时全量 EditMode 48 项通过，E / 手柄北键、菜单上下文、暂停恢复和目标失效路径均已覆盖。
- 阶段 0 体验专项 PlayMode 使用虚拟键盘和手柄验证 Tab / Esc、Start / B、默认焦点、物品选中与装备；自阶段 6 起主要元素边界自动化只检查 1920×1080。
- 1920×1080 下完成 visual tree、渲染和 Play 控制台检查。
- 阶段 1 全量 EditMode 60/60 通过；PlayMode 8 项中 6 项通过、2 项为包内既有忽略测试。商店购买状态链及商店 / 打造 1280×720、1920×1080、2560×1440 布局边界均通过。
- 阶段 2 全量 EditMode 70/70 通过；PlayMode 9 项中 7 项通过、2 项为包内既有忽略测试。真实 Main 菜单中已覆盖键盘装备武器 / 护甲、手柄显式选择左右戒指、卸下，以及背包装备区三档分辨率边界。
- 阶段 5 全量 EditMode 92/92 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游用例按原标记跳过、0 失败。1280×720 背包、1920×1080 商店与 2560×1440 打造实机渲染均无越界，按钮的焦点、选中与禁用状态清晰，最终 Console 为 0 错误、0 警告。
- 阶段 6 将常规布局回归收敛为 1920×1080；键盘 Tab / Esc、手柄 Start / B、默认焦点、暂停恢复、商店状态、四槽装备与打造路径继续通过。正式世界、背包、商店和打造截图均无关键遮挡或越界。
- alpha 0.1.0 修正后的领域、输入和 UI 结构 EditMode 为 9/9，相关 PlayMode 4/4。真实 Mouse 已覆盖背包换位、拖拽装备和装备拖回；1280×720、1920×1080、2560×1440 均通过纵向布局、共享列占比和边界验收。
- alpha 0.1.4 的法力 HUD 与资源事件专项 EditMode 6/6、Main PlayMode 1/1 通过；全量 PlayMode 继续通过三档布局回归，1280×720 真实 Main 中生命 / 法力条无越界，停止运行后 Console 为零错误。

以上数据是首版收尾时的验证记录；修改输入资产、菜单路由、UXML 或场景组件后，应重新验证键鼠与手柄两条路径。

## 当前限制

- HUD、背包、商店、打造和详情已完成首版统一视觉；菜单切换仍以即时显隐为主，暂未加入完整过场动画与音效。
- 背包已支持鼠标拖拽与键盘 / 手柄拿起—放置的精确移动、单目标交换和装备换位；仍不支持旋转、堆叠、重量和自动整理。
- 商店和打造共用首版单实例运行时数据；商店状态可在同一商人运行期内保留，但不支持多商人独立状态或持久化。
- `Attack`、`Look` 尚未接入手动战斗操作。
- PlayMode 已包含角色动画状态与阶段 0 背包体验两项项目测试；包内测试另有上游不稳定用例按原标记跳过。

完整工作台结构见[物品 UI 工作台](./item-ui-workbench.md)，玩法链见[首版玩法循环](./gameplay-loop.md)，装备规则见[装备系统](./equipment-system.md)，打造页的数据和事务规则见[打造系统](./crafting-system.md)。
