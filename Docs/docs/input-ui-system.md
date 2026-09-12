# 输入与运行时 UI

## 模块职责

输入与运行时 UI 模块负责统一键鼠 / 手柄输入、Gameplay / UI 模式切换、HUD 展示、背包 / 商店 / 打造菜单、当前 Session 保存区，以及场景交互提示和菜单暂停。Application 级 FrontEnd、场景 Busy、Modal、Toast 与 Fatal 由独立 Shell 管理。

Main 玩法 UI 复用一个 `UIDocument` 和一个 `PanelSettings`；常驻 Bootstrap 持有 Application Shell 与唯一 `EventSystem`。界面 Controller 通过 Query、Command 与领域 Event 接入 QFramework，不直接修改运行时 Model。

Main / Shell 在模块 USS 后加载公共 `Components.uss`，按钮按下只改变材质和轮廓，不缩放或改变边框盒尺寸。物品 Focus / Hover 服从共享选择，投放反馈优先；下拉弹层通过共享 Panel 的运行主题着色。皮肤不新增焦点 / 命中层，见[组件规范](./ui-component-style.md)。

## 输入层

背包现为十个真实装备槽，导航按视觉矩形而非枚举编号。商店完整商品网格采用固定视口纵向滚动：同窗方向可选中屏外商品并显露，跨窗只选择视口内可见邻居。唯一目标装备自动定位到对应槽，多目标戒指保持显式选择。详见[物品工作台](./item-ui-workbench.md)。

`Assets/Settings/InputSystem_Actions.inputactions` 是唯一输入源，并生成 `InputSystem_Actions.cs`。运行时只由 Application 级 `ApplicationInputService` 创建并持有一个 `InputSystem_Actions` 实例；Session 级 `GameInput` 作为 `IUtility` Adapter 注册到 `GameArchitecture`，只转发既有玩法 API，不创建或销毁 Action Asset。

- `Player` Action Map：移动、瞄准、攻击预留、世界交互、背包开关与暂停。
- `UI` Action Map：导航、提交、取消、物品拿起 / 放置、暂停、窗口循环和指针操作。
- Gameplay 额外启用 UI Point / Click，支持 HUD 鼠标入口；Navigate / Submit 保持关闭，不占用移动方向。`IsUiEnabled` 以 Navigate 是否启用判断，表示完整 UI 导航可用性。挂起时包括 Point / Click 在内的动作全部禁用。
- 玩法物品窗口打开时，单独启用原 `Player.ToggleMenu`，不启用移动、战斗或交互；保留原 Action / Binding ID 和有效 overrides。Shell 所有权、挂起或 Session 结束后禁用窗口专用动作。Gameplay 可用性以移动 Action 是否启用判断，不能再以整个 Player Map 是否存在一个启用动作判断。
- 无 Session 的 FrontEnd 默认使用 UI Context；Session 创建进入 Gameplay，菜单打开切到 UI，Session 结束恢复 UI。
- `ApplicationInputService` 区分 `RequestedContext` 与实际 `CurrentContext`。普通模式切换更新请求；Settings / Modal 等应用层通过 `AcquireUiContext` 持有独立 UI 所有权，最后一个所有者释放后采用最新请求。任一 suspension lease 有效时两个 Map 都禁用，UI 所有权不能绕过 suspension。
- `PlayerController` 从 `GameInput.Move` 读取移动，不直接轮询设备。
- `InteractPerformed` 只在 Gameplay Action Map 有效，用于世界目标交互。
- `GameInput.CurrentMode` / `ModeChanged` 反映 Session 请求，驱动玩法菜单与本组暂停；Shell 的 `BlocksGameplay` 单独控制底层可见性，不用有效 UI Map 推断背包是否打开。
- `GameInput.Dispose()` 只解除 Session 订阅并恢复 UI Context；只有 Application Shutdown 才释放生成的 Actions。

`Bootstrap.unity/EventSystem` 上的 `ApplicationInputModuleBinder` 将 `InputSystemUIInputModule` 绑定到同一运行时 Asset，并显式建立 Point、Navigate、Submit、Cancel、Click、Scroll 和追踪设备 ActionReference。绑定和解绑都会先停用 UI Module，避免旧 Action 仍处于启用状态；服务关闭前先发出 `Closing`，Binder 清空引用后才允许销毁 Asset。同场景重建 `ApplicationHost` 时，`InputReady` 事件会把模块重新绑定到新的 Application owner。

背包开关、暂停与 Cancel 回调内的 Map 变更统一延后到 `onAfterUpdate`。共享 Asset 下，未消费的 UI Cancel 不在 Action 回调分发中途直接禁用 UI Map，而是在当前 `InputSystem.onAfterUpdate` 阶段切回 Gameplay。Application 层在 Cancel 回调中释放最后一个 UI 所有者时，也把实际 Context 刷新延后到该阶段，并采用届时的最新请求；服务销毁会取消尚未执行的刷新。这样 UI Module 能先完成同一 CallbackContext 的处理，避免读取失效 control 索引，同时玩家观感仍保持在同一帧内关闭菜单。

Cancel 先由 Application Shell 处理 Fatal、Busy、Modal、Settings，再交给 Session 的暂停页、物品动作菜单、拖放、展开的来源选择器与活动窗口处理；每级在首个所有者消费后停止分发，一次输入只处理一层。Shell 阻挡期间不向玩法物品界面转发导航和拿起动作，也不执行排队中的 Gameplay 切换。关闭底层菜单只更新请求，不能释放上层 UI / pause lease。

### 重绑定、设备族与 Glyph

`ApplicationInputService` 维护正式可重绑定 Action / Composite Part 清单，并把 Binding Overrides JSON 与 `GlyphPreference` 保存到 Settings V1。一次变更按“临时覆盖 → 合同 / 冲突校验 → Settings 提交 → 失败回滚”执行；交互捕获期间持有 Rebinding suspension lease，Escape、超时或设备拔出都恢复旧覆盖。恢复默认只移除 runtime override，不修改 `InputSystem_Actions.inputactions`。

设备显示分为 `KeyboardMouse` 与 `Gamepad`。活动设备输入会更新设备族；玩家偏好可覆盖自动选择。交互提示与设置页只显示当前显示族，不再拼接两套绑定。`InputGlyphResolver` 把规范 control path 映射到 `Assets/Art/UI/InputGlyphs` 的 8 张独立 64×64 单 Sprite；未知控件回退为可读 Keycap 文本，不暴露原始 control path。

Point / Look 在 Context 切换后会回放当前指针位置；相同控件的未变化位置不视为新设备活动，避免手柄返回时 Glyph 误切键盘。真实位置变化与按钮输入仍更新活动设备族。

当前默认绑定（新增暂停与窗口循环均可重绑定）：

| 操作 | 键鼠 | 手柄 |
| --- | --- | --- |
| 移动 | WASD | 左摇杆 |
| 世界交互 | E | 北键 |
| 切换随身背包（Gameplay / 物品 UI） | Tab | Start |
| Gameplay 打开暂停 | Escape | Select |
| 物品 UI 切换暂停 | P | Select |
| 上一个 / 下一个窗口 | [ / ] | 左 / 右肩键 |
| UI 提交 | Enter | 南键 |
| 物品拿起 / 放置 | Space | 西键 |
| 按住对比装备 | 左 Shift | 左扳机 |
| UI 返回 / 关闭 | Escape | 东键 |

`Look` 与 `Attack` 仍作为后续手动瞄准和攻击入口保留；当前战斗原型继续使用自动攻击逻辑。

### 编辑器进入 Play Mode 约束

当前输入与 QFramework 生命周期的提交基线为 **Reload Scene Only**：`ProjectSettings/EditorSettings.asset` 中选项启用，`EnterPlayModeOptions = DisableDomainReload`（数值 `1`），保留 Scene Reload。2026-09-12 完成[静态状态、输入与连续启停专项审计](./infrastructure/domain-reload-compatibility.md)后，由用户确认采用该设置。

专项测试可以临时调整 Enter Play Mode Options。测试完成、失败或中止后都必须恢复上述基线；输入与生命周期修改需要回归验证首次运行、停止后再次运行、键盘 / 手柄移动、Gameplay / UI Action Map 切换及菜单关闭后恢复移动。

提交前检查 `ProjectSettings/EditorSettings.asset` 的实际值和 Git diff，避免纳入测试遗留配置。新增静态状态必须明确其重置或保留合同，事件订阅与输入资产必须按生命周期释放；游戏内直接停止的 Abandoned 隔离边界见兼容检查报告。历史验收中恢复为 `0` 的记录保持原意，不再作为当前基线。

## UI 组成

`Assets/UI/GameRoot.uxml` 组合 HUD、共享菜单遮罩和三个功能模板：

- `Hud.uxml` / `Hud.uss`
- `Inventory.uxml` / `Inventory.uss`
- `Shop.uxml` / `Shop.uss`
- `Crafting.uxml` / `Crafting.uss`
- `ItemWorkbench.uxml` / `ItemWorkbench.uss`
- `GameMenu.uss`
- `Theme.uss`

`Main.unity/UIRoot` 挂载 `UIDocument`、`GameMenuController`、四个数据显示 Controller 和 `InteractionPromptController`。常驻 Bootstrap 的 `InputSystemUIInputModule` 与 Application / Session 共享同一个运行时 Input Actions 实例。

`Bootstrap.unity` 挂载 `ApplicationShell.uxml/.uss` 和唯一 EventSystem。Shell 负责 FrontEnd Page、Busy、Modal、Toast、Fatal 与跨场景焦点；Main 不再序列化 EventSystem，隐藏的 Application 层不得抢占玩法 UI 焦点。两个文档共用 PanelSettings，Bootstrap 的 `UIDocument.sortingOrder` 为 200，Main 为 100，保证应用层位于 HUD 和物品界面之上并正确接收指针。

当前窗口结构、详情、安全区与导航以[物品 UI 工作台](./item-ui-workbench.md)为准：背包、商店、打造与属性四种窗口独立显示，商店 / 打造默认联动背包，并可打开属性；暂停有独立入口。商店 / 打造与背包同为 560 px，窗口组居中，背包打开时隐藏重复来源列表。顶部标签栏关闭直接清理所有窗口返回游戏，各窗口标题栏关闭仍作用于单窗。背包标题栏与 HUD 打开[独立属性详情](./attribute-details.md)，组标题与展开项参与跨窗导航。物品详情不再显示图标；普通详情优先位于组合窗口外侧，三窗拥挤时临时替换属性阅读区，切回属性恢复。按住 `UI/Compare` 显示兼容槽已装备物品的并排信息栏，双戒指同时比较左右槽；松开、取消、暂停与关闭清理对比。动作菜单捕获方向导航并约束焦点，拖放跨窗允许高度不对齐的合法目标，普通浏览仍限制方向夹角。`Windows.uss` 与 `Attributes.uss` 定义组合布局。

`alpha 0.2.3` 已将 Game Menu、HUD、背包 / 四槽装备、商店、打造、共享物品详情和场景交互提示的动态界面文本接入 Application 级 Localization Service。快照只传递属性 ID、数值、装备槽、伤害与修改器等语义数据，Controller / View 在当前 Locale 下解析显示文本；Locale 变化只重绘现有状态，不重新查询或改动背包、装备、拖拽、交易、打造和焦点。商店反馈保存 `LocalizedMessage`，打造结果保存领域 `CraftingResult`，避免缓存旧语言字符串。`alpha 0.2.5` 以后交互提示从 Application 输入服务取得当前显示设备族的绑定与 Glyph。

`alpha 0.2.4` 把 Continue、NewGame 和语言入口迁到无 Session 的 FrontEnd；游戏菜单保留 Save、Return FrontEnd 和 Close。Scene Flow 事务由 Application Busy 阻断底层输入，确认返回与可恢复错误使用 Modal，非阻塞结果使用 Toast；键鼠和手柄共享同一焦点恢复合同。

同阶段已补齐正式内容名称和字体链：UI Toolkit 使用动态 `GameCjkFont` 并回退 `GameLatinFont`，TextMesh Pro 使用 `QiushuiShotai SDF` 并回退 `LiberationSans SDF`。`Phase1UxPlayModeTests` 对 `zh-Hans`、`en`、`qps-ploc` 以当前 1920×1080 正式画布验证关键区域边界、重叠、文本测量与可操作性。设置存储、Key 和字体维护规则见[用户设置与本地化](./infrastructure/user-settings-localization.md)。

鼠标物品操作采用点击与拖拽分离：左键按下不会立即拿起，只有持续按住并移动超过 10 px 才进入拖拽；直接松开只固定选择。背包物品与装备槽共用唯一选择描边，键盘焦点或鼠标悬浮预览会临时接管该描边，离开后再恢复固定选择，任意时刻不会出现两个同级高亮。商店上下文中，玩家背包物品支持右键直接出售，商人库存仍需左键选择并使用购买按钮；四向浏览按实际屏幕矩形选择邻居，可自然跨入相邻可见窗口。打造目标放入 2×3 槽位后从背包视图移除，取回后恢复。物品详情在 4–6 条显式词缀时自动切换紧凑排版，优先显示词条效果，词条名降为小号辅助信息，并继续保持无滚动完整展开。

alpha 0.3.2 将 HUD 改为底部双资源仪表与技能状态区，背包 / 属性 / 暂停入口沿用菜单路由。法力不足、暂停和冷却由共享当前状态驱动，恢复后立即解除旧提示。HUD 与 Shell 共用 `InputGlyphs.uss`，设备和绑定变化更新真实提示。布局、动效与生命周期见[战斗 HUD](./hud-system.md)。
`GetHudSnapshotQuery` 读取有效属性和资源快照，背包只显示三主属性摘要。独立属性使用静态解释与动态状态两类 Query，展示来源、条件及空态；抗性限制在 `-100%` 至 `75%`，暴击率限制在 `0%` 至 `100%`，倍率与百分比明确区分。装备 / Actor 变化重建解释，资源事件与可见时的低频刷新只更新动态区；Locale 切换只重排文本。

装备槽图标使用与背包占格相同的 `scale-to-fit` 规则，并占满槽位扣除统一 4 px 内边距后的区域；当前窗口装备区隐藏常驻槽位标签，图标不受文字挤压；物品详情提供名称。武器、护甲装备后的图标可见范围不得小于其 2×3 背包占格中的图标，戒指槽同样不得小于 1×1 背包占格。

| Controller | 只读数据入口 | 写入入口 | 主要刷新来源 |
| --- | --- | --- | --- |
| `HudController` | `GetHudSnapshotQuery`、轻量 `GetAttributeRuntimeQuery` | 无 | Actor、生命 / 法力资源、技能释放、金币、装备事件；Locale / Glyph / Motion |
| `InventoryPanelController` | `GetInventorySnapshotQuery`、`GetHudSnapshotQuery` | 背包移动、精确装备 / 卸下与戒指交换 Commands | 背包、装备、Actor 事件 |
| `ShopPanelController` | `GetShopSnapshotQuery` | `BuyItemCommand`、`SellItemCommand` | 交易、金币、背包事件 |
| `CraftingPanelController` | `GetCraftingSnapshotQuery` | 槽内目标的 `CraftItemCommand` | 打造、金币、背包事件 |
| `GameMenuController` | 当前 Session 的 `SessionSaveFacade` | 保存 `auto`、请求返回 FrontEnd | Session 绑定、存档操作完成和 Busy 状态 |
| `InteractionPromptController` | 交互焦点消息 | 无 | 焦点、输入模式和 Actor 状态变化 |
| `ApplicationSettingsController` | Settings、Input、Audio、Accessibility 服务 | 音量、绑定、Glyph 偏好、Reduce Motion | 页面打开、设置 / 绑定 / 设备族变化 |

所有格子和详情都来自只读快照。打造页不再维护词缀列表，而是取得槽内物品 14 个操作变体的成本、可用性和失败原因；背包选择仅作为详情与“放入”候选，不能直接执行打造。鼠标拖入或键盘 / 手柄确认“放入打造槽”后才锁定目标，取回或目标离开背包时立即禁用全部操作。操作成功后由对应 System / Model 发送领域 Event，再触发 HUD 和面板重新查询，失败分支不伪造成功事件。

## 菜单上下文

`GameMenuController` 分别管理打开集合、活动窗口、独立暂停页、当前 Session 保存、返回确认请求与各窗有效焦点。`GameMenuAccess` 按打开来源限制功能：

| 来源 | 可用页面 | 初始页面 |
| --- | --- | --- |
| Tab / Start | 背包 | 背包 |
| 商人交互 | 背包、商店 | 商店 |
| 打造台交互 | 背包、打造 | 打造 |

不可用商店 / 打造页签隐藏并退出布局与导航，避免玩家在任意位置远程交易或打造。保存、设置与返回前台位于独立暂停页；保存 Busy 时局部按钮禁用，跨场景 Busy 由 Application Shell 接管。关闭菜单后访问范围恢复为随身背包上下文。

## 世界交互与暂停

1. `PlayerInteractionController` 维护进入触发范围的 `WorldInteractionTarget`，按距离选择最近有效目标。
2. 焦点变化通过 `SetInteractionFocusCommand` 发布消息，`InteractionPromptController` 使用本地化模板显示 `{当前绑定显示文本} · {目标名称}`。
3. 玩家触发交互后，`OpenGameMenuCommand` 请求打开目标对应的菜单上下文。
4. `GameMenuController` 切换到 UI 模式，并通过 `SetGameplayPausedCommand` 请求 `GameplayPauseSystem` 获取 Application `GameTimeService` 的菜单 pause lease。
5. 取消或关闭最后一个窗口 / 暂停页时释放本组 lease、恢复 Gameplay 输入；目标禁用或销毁只撤销相关功能窗口；返回 FrontEnd 会由 Scene Flow 强制清理全部 pause lease。

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
- alpha 0.2.3 完成三语言 × 三分辨率布局矩阵；项目自有 PlayMode `48/48`、完整 PlayMode 52 项中 50 项通过、0 失败，2 项 Input System 上游用例按原标记跳过。
- alpha 0.2.4 完成 Application Shell、FrontEnd、Busy / Modal / Toast / Fatal、返回确认和焦点隔离；EditMode `360/360`、项目 PlayMode `52/52`，完整 PlayMode 54 项中 52 项通过、0 失败，仍只跳过相同 2 项上游用例。
- alpha 0.2.5 完成 Application 输入唯一 owner、Session Adapter、Context / suspension lease、重绑定、设备族、Glyph 与共享 Settings Page；三轮 FrontEnd → Main → FrontEnd 的 Asset Instance ID 保持不变。全量 EditMode `409/409`、项目 PlayMode `53/53`；完整 PlayMode 57 项中 55 项通过、0 失败，2 项仍为 Input System 上游既有 Ignore。

以上数据是首版收尾时的验证记录；修改输入资产、菜单路由、UXML 或场景组件后，应重新验证键鼠与手柄两条路径。

## 当前限制

- HUD、背包、商店、打造、详情和设置页已完成首版统一视觉；菜单切换仍以即时显隐为主，音频只接入首个 UI Confirm Cue。
- 背包已支持鼠标拖拽与键盘 / 手柄拿起—放置的精确移动、单目标交换和装备换位；仍不支持旋转、堆叠、重量和自动整理。
- 商店和打造共用首版单实例运行时数据；商人运行时库存已随 `auto` 存档恢复，但仍不支持多商人独立状态。
- `Attack`、`Look` 尚未接入手动战斗操作。
- PlayMode 已包含角色动画状态与阶段 0 背包体验两项项目测试；包内测试另有上游不稳定用例按原标记跳过。

物品的 Enter / 手柄南键打开动作菜单，Space / 手柄西键拿起或放置；右键执行当前可见上下文动作。共享会话独占指针捕获与放置导航，设备断开和上层接管取消未提交事务。

完整工作台结构见[物品 UI 工作台](./item-ui-workbench.md)，场景状态和应用 Shell 见[游戏状态、场景流与应用 UI 外壳](./infrastructure/game-state-scene-flow-ui-shell.md)，存档入口与错误语义见[本地存档与 Session 恢复](./infrastructure/local-save.md)，设置与语言见[用户设置与本地化](./infrastructure/user-settings-localization.md)，玩法链见[首版玩法循环](./gameplay-loop.md)，装备规则见[装备系统](./equipment-system.md)，打造页的数据和事务规则见[打造系统](./crafting-system.md)。
