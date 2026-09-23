# DarkFlare 项目概览

## 项目状态

2026-09-23：**Alpha 0.6.1 普通怪物完成**，M1–M6 与[执行计划](./plan/archive/alpha-0.6.1-monster-behavior-plan.md)已归档。当前交付 `0.6.1-alpha` / core v7 / Schema 6 / 地图 V2，见[模块](./behavior/monsters.md)和[验收](./behavior/acceptance/0.6.1/README.md)。完整 EditMode 808/808、PlayMode 92/92，Windows Player、跨进程恢复及 500 / 1000 性能矩阵通过，覆盖调整后的 300 / 600 单位目标。下一阶段 0.6.2 守卫 Boss。

Alpha 0.5 的 A–I 全部完成；当前交付 `0.6.1-alpha`，已增加通用行为基础和普通怪物行为。四地表地图、手绘 / AI 编辑、Y 排序、城镇交易打造、野外刷怪和 Flow Field / A* 批量寻路已形成完整链路，详见 [I 综合验收](./maps/acceptance/i/README.md)。总计划与配套方案已归档。

2026-09-22：[Alpha 0.6 总计划](./plan/alpha-0.6-plan.md)的 **0.6.0 已完成**：HFSM 与行为树独立置于现有 Core，交付组合样例、Animator 桥接、Editor 实时图与开发 Player 面板。完整 EditMode 788/788、PlayMode 87/87，独立 Windows Player 及核心规模验证通过，见[行为模块及验收](./behavior/README.md)。0.6.1 普通怪物也已完成；守卫 Boss、有限总量 / 范围触发刷怪及手动重置继续按计划推进。

`DarkFlare` 已完成首版最小循环、初步体验优化、`alpha 0.1` 封板和 `alpha 0.2.0–0.2.7` 基础设施封板。当前可从常驻 `Bootstrap.unity` 的 FrontEnd 新建或继续游戏，additive 进入 `Main.unity` 完成战斗、掉落、十槽装备、交易和打造，暂停后保存并安全返回前台；FrontEnd 可二次确认删除自动档，共享设置页可恢复完整默认设置，中英语言、音量、按键、设备 Glyph 和降低动态效果即时收敛。

alpha 版本内的阶段使用三段式名称：`alpha 0.1` 的首个阶段为 `alpha 0.1.0`，后续依次为 `alpha 0.1.1`、`alpha 0.1.2`；`alpha 0.2` 同样从 `alpha 0.2.0` 开始。当前版本为 `0.6.1-alpha`；完成记录见[alpha 0.2 综合验收记录](./infrastructure/alpha-0.2-acceptance.md)与[计划归档](./plan/archive/README.md)。

2026-09-18：[Alpha 0.5 总计划](plan/archive/alpha-0.5-plan.md)已完成 [0.5.0 地图基础 A–D](plan/archive/alpha-0.5.0-map-foundation-plan.md)，包含数据 / 导入、Wang 解析与预览、正式四材质发布，以及[同源碰撞和静态通行](./maps/map-geometry.md)。D 已提交 `e396485`。[0.5.1 手工 / AI 地图编辑器](./maps/map-editor.md)的 E1–E5 也已完成并[归档](./plan/archive/alpha-0.5.1-map-editor-plan.md)，E 验证为 EditMode 697/697、PlayMode 77/77，见 [E 验收](./maps/acceptance/e/README.md)。[0.5.2 世界层级 F1–F5](./plan/archive/alpha-0.5.2-world-sorting-plan.md)已完成，EditMode 707/707、PlayMode 78/78；城镇 / 野外与生命周期 G 已完成；Flow Field / A* / Job / Burst 已由 H 完成。目标为 500 活动怪物、1000 压力测试，地图离场暂停并保留状态。Main 已成为通用双地图玩法容器。

- Alpha 0.3：独立物品窗口、统一拖放、实时属性、暗黑像素 HUD、共享皮肤与十槽二十件装备已完成，后续补充物品窗口布局与交互修正。
- Alpha 0.4.0：[统一物品系统](./item-system.md)完成金币与材料入栏、堆叠 / 消耗、材料打造与旧档迁移。
- Alpha 0.4.1：[状态系统](./status-system.md)完成叠层、角色效果、七异常、抗性、时间 / 存档和显示；另已补充分档显示与右上角 HUD。
- Alpha 0.4.2：[统一使用](./item-use-system.md)与[消耗品](./consumables.md)完成回血、回蓝、临时状态和驱散；core v5、Schema 3。2026-09-15 EditMode 563/563、PlayMode 74/74 及独立 Windows Player 验收通过，见[交付证据](./assets/acceptance/alpha-0.4-item-use-acceptance/README.md)。
- 后续[随机商店](./trading-system.md)已完成每批 16 件、随机品质 / 词条、购买减单件、免费刷新与库存恢复，运行版本仍为 `0.4.2-alpha`。2026-09-16 EditMode 568/568、PlayMode 75/75 通过；该轮未重建 Windows Player，见[验收记录](./assets/acceptance/random-shop/README.md)。

上述 A–I 数字为历史验收记录；I 的大规模切图 / 保存数据见 [I 验收](./maps/acceptance/i/README.md)。当前版本 `0.6.1-alpha`、core v7、Schema 6、地图文档 V2；当前行为与正式 Player 数据见 [0.6.1 验收](./behavior/acceptance/0.6.1/README.md)。大规模切图 / 保存的耗时与内存边界单独记录，不能用行为或导航稳态预算代替。

## 游戏定位

项目目标是类暗黑、流放之路的构筑驱动 RPG，并融合类塔科夫的背包资源管理、物资取舍和跑商经营。首个阶段围绕同一套伤害、词条、物品和经济数据验证“战斗 → 掉落 → 整理 → 交易 / 打造 → 再战斗”的单场景循环。

## 当前技术栈

- Unity `6000.6.0f1`，URP `17.6.0`；[升级兼容记录](./infrastructure/unity-6.6-upgrade.md)
- QFramework 分层与项目内 `QFramework.cs`
- UniTask 异步
- Addressables 资源加载
- Input System 键鼠 / 手柄输入
- Unity Localization `1.5.13`，已接入 `zh-Hans`、`en`、测试用 `qps-ploc`、六张职责表和运行时切换服务
- UI Toolkit 运行时界面
- Odin Inspector 配置与 Editor 工具
- PrimeTween 动画补间
- unity-mcp 编辑器自动化与验证

当前构建场景为 `Assets/Scenes/Bootstrap.unity`（index 0）和 `Assets/Scenes/Main.unity`（index 1）。

## 核心入口

### 架构与程序集

- `Assets/Scripts/Runtime/Core/QFramework.cs`：独立 `DarkFlare.Core` 程序集。
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationBootstrap.cs`：在场景加载前创建唯一应用宿主，并在 Subsystem Registration 重置静态状态。
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationHost.cs`：拥有 Application / Profile / Session / Scene 生命周期、内容目录、存档协调、Scene Flow、Time、Input、Audio、Accessibility、Platform、Logger 与 Resource Service，向场景流提供 Session 底层事务。
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/GameArchitectureProvider.cs`：`GameArchitecture` 的唯一正式创建、访问与销毁入口，以所有者 lease 和单调 generation 隔离连续 Session。
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/SceneSessionBinding.cs`：让场景预置组件只绑定同场景且已 Running、lease 有效的 Session。
- `Assets/Scripts/Runtime/Infrastructure/Content/`：`ContentId`、内容类型登记、目录资产与不可变运行时目录。
- `Assets/Scripts/Runtime/Infrastructure/Identity/StableInstanceIds.cs`：玩家、物品、运行、怪物、世界掉落和存档槽位的强类型身份与受控生成器。
- `Assets/Scripts/Runtime/Infrastructure/Flow/`：Game Flow 合同、集中场景配置、Scene Loader 与 `SceneFlowService`；统一启动、NewGame、Continue、取消 / 替代、恢复和返回前台。
- `Assets/Scripts/Runtime/Infrastructure/Time/GameTimeService.cs`：应用级 pause lease 与 Runtime 唯一 `Time.timeScale` 写入入口。
- `Assets/Scripts/Runtime/Infrastructure/Input/`：Application 级唯一 Action Asset owner、Context / suspension lease、重绑定、设备族、Glyph 与 Bootstrap UI Module 绑定。
- `Assets/Scripts/Runtime/Infrastructure/Audio/`：AudioMixer 配置、Addressables Cue、Source 池、有主播放句柄与平台暂停。
- `Assets/Scripts/Runtime/Infrastructure/Diagnostics/`：结构化日志、稳定事件目录、环形缓冲、异常监控、first-failure-wins 与玩家错误目录。
- `Assets/Scripts/Runtime/Infrastructure/Resources/`：Addressables 唯一后端、Application Resource Service、owner / lease、单航班与资源诊断。
- `Assets/Scripts/Runtime/Infrastructure/Accessibility/`、`Platform/`：Reduce Motion Profile，以及 Focus / Suspend / Resume / Quit 编排。
- `Assets/Scripts/Runtime/Infrastructure/UI/`：Application Shell、FrontEnd、共享 Settings Page、Busy / Modal / Toast / Fatal 表现和焦点恢复。
- `Assets/Scripts/Runtime/Infrastructure/Persistence/`：纯 DTO、运行时映射、版本迁移、确定性 JSON、代际 Storage、快照 / 恢复准备、Restore initializer、Profile 级 SaveCoordinator 和 Session Facade。
- `Assets/Scripts/Runtime/Infrastructure/Settings/`：Settings V1、原子存储、迁移、Application 级 Localization Service、语义消息与正式内容本地化引用。
- `Assets/Scripts/Runtime/GameArchitecture.cs`：Session 组合根，注册输入 Utility、战斗 / 装备 / 背包 / 经济 Model，战斗、生成、掉落、交易、打造 System，以及 `SessionObjectRegistry`。
- `Assets/Scripts/Runtime/`：`DarkFlare.Runtime` 程序集。
- `Assets/Scripts/Tests/EditMode/`：`DarkFlare.Tests.EditMode`，2026-09-07 测试审计后 `424/424` 通过；原封板 `443/443` 为历史记录。
- `Assets/Scripts/Tests/PlayMode/`：`DarkFlare.Tests.PlayMode`，2026-09-07 测试审计后 `56/56` 通过、0 失败 / 跳过。此前含包测试的 60 项运行结果保留在 alpha 0.2 综合验收记录，本轮限定项目自有程序集。

Alpha 0.3 前置[测试审计](./testing/test-suite-audit.md)已完成；新增与受影响测试须遵循[测试维护规范](./testing/test-maintenance.md)，临时验证完成后清理。

生命周期的职责、状态、事务和禁止事项见[应用生命周期与会话作用域](./infrastructure/application-lifecycle.md)；内容、实例身份、DTO 与迁移规则见[稳定身份、内容目录与迁移框架](./infrastructure/content-identity-migration.md)；文件格式、代际存储、保存和 Restore 流程见[本地存档与 Session 恢复](./infrastructure/local-save.md)；设置存储、语言切换、内容名称和字体合同见[用户设置与本地化](./infrastructure/user-settings-localization.md)；Bootstrap / Main 拓扑、状态、场景事务、Time Service 和 Shell 见[游戏状态、场景流与应用 UI 外壳](./infrastructure/game-state-scene-flow-ui-shell.md)；音频见 [Application Audio](./infrastructure/application-audio.md)；日志、玩家错误和资源所有权见[日志、错误处理与 Addressables 资源治理](./infrastructure/logging-error-addressables-governance.md)；降低动态效果与平台挂起见[可访问性与平台生命周期](./infrastructure/accessibility-platform-lifecycle.md)。

### 玩法模块

- 战斗与属性：`Gameplay/Combat`、`Data/Stats`、`Data/Tags`、`Data/Affixes`。
- 物品、掉落与背包：`Gameplay/Items`、`Gameplay/Loot`、`Gameplay/Inventory`、对应 `Data` 配置。
- 装备：`EquipmentModel`、`EquipItemCommand` 与 `InventoryPanelController`。
- 交易：`EconomyModel`、`TradingSystem`、`TraderDefinition` 与 `ShopPanelController`。
- 打造：`CraftingOperations`、`CraftingSystem`、`CraftingDefinition` 与 `CraftingPanelController`。
- 输入：`ApplicationInputService` 是唯一 Action Asset owner；Session `GameInput` 只适配 Gameplay API，`InputSystem_Actions.inputactions` 是唯一输入源。

### UI

- `Assets/UI/ApplicationShell.uxml` 组合 FrontEnd、共享 Settings Page、Busy、Modal、Toast 与 Fatal 覆盖层，常驻 Bootstrap。
- `Assets/UI/GameRoot.uxml` 组合 Main 的 HUD、背包、商店、打造和独立属性模板。
- 唯一 `EventSystem` 位于 Bootstrap；Main 的 `UIRoot` 只保留玩法 PanelRenderer 与场景 Controller。
- `GameMenuController` 管理共享遮罩、四类窗口页签、设置、保存、返回前台、关闭和玩法输入模式；NewGame、Continue、语言与设置入口位于 FrontEnd。
- 各面板 Controller 通过 `SceneSessionBinding` 等待有效 Session，只通过 Query、Command 与领域事件工作，不直接修改 Model。

### 编辑器工具

- `Assets/Scripts/Editor/ConfigCenterWindow.cs` 基于 Odin 提供配置中心。
- 按 `CreateAssetMenu(menuName = "DarkFlare/Data/...")` 自动发现配置类型，支持按类型浏览、创建和直接编辑真实配置资产。

## 当前资源与配置

- `Assets/Data/Preset` 已有玩家、技能、怪物、刷怪、掉落、物品、词条、商人和打造配置；唯一正式内容目录 `core` v7 收录全部正式配置，其中二十件独立装备覆盖十槽，四种药水接入统一使用。
- 玩家、怪物、投射物和掉落物 Prefab 位于 `Assets/Prefabs`，通过 Addressables 加载。
- Prefab、Sprite、Audio 与 Application 配置统一通过 Resource Service 按稳定键单航班加载；Application / Session owner 持有 Asset Lease，并在关闭后回到资源基线。
- `Assets/UI` 已有 `GameRoot`、`Hud`、`Inventory`、`Shop`、`Crafting` 的 UXML / USS。
- `Assets/UI` 另有 Application Shell UXML / USS；`Assets/Settings/Scenes/SceneFlowConfiguration.asset` 集中注册 Bootstrap 与 Main。
- 正式本地存档写入 `Application.persistentDataPath/DarkFlare/Saves/<slot>`；项目 Assets 中不保存玩家运行时数据。
- 用户设置写入独立的 `Application.persistentDataPath/DarkFlare/Settings`；当前开放语言、四类音量与静音、输入重绑定、Glyph 偏好和降低动态效果。
- `Assets/Localization` 已有 `ui`、`system`、`items`、`stats`、`affixes`、`monsters` 六张中英表；正式内容名称引用与字体 fallback 均由策略测试校验。
- `Assets/Art/UI/InputGlyphs` 保存 8 张独立 64×64 单 Sprite Glyph；`Assets/Audio` 保存四组 Mixer、Addressables 配置和首个 `ui.confirm` Cue。
- DarkFlare 自有 Addressables 条目位于 `DarkFlare-*` 组，使用规范小写地址和 `df.*` 生命周期 / 类型 Label；默认组为空。
- 地图、玩家、三种怪物、二十件装备、商人、打造台和运行时 UI 已完成视觉接入；`PrototypeSquare.png` 仅保留为调试回退。

## 当前边界

首版已经完成“战斗 → 拾取 → 装备 / 交易 / 打造 → 再战斗”的人手循环，但仍是用于验证系统闭环的功能原型：

- 当前场景流使用常驻 Bootstrap 与单一 Main 玩法容器；Main 内已支持城镇 / 野外地图切换、离场冻结和恢复，尚无关卡选择、Profile 选择或 Addressables Scene。
- 当前已有 `auto` 槽位、确定性 JSON、Payload SHA-256、两代有效文件保留、损坏回退、单写者协调、Restore Session、二次确认删除和退出前有界 Flush；尚无手动槽位管理、Profile 选择或云同步。
- Settings V1 已有语言、音频、输入与 Reduce Motion 真实消费者；Text Scale、High Contrast、Screen Shake 和 Display Mode 仍为数据预留，不在玩家 UI 中开放。
- 背包已支持拖拽换位、单目标原子交换和键鼠 / 手柄拿起放置，已支持物品堆叠与材料消耗，尚无旋转和重量；装备已实现十槽及精确拖入 / 拖回，每槽至少两件正式基底，但没有耐久、套装、完整纸娃娃或唯一装备特效。
- 交易没有回购或多商人独立库存；打造已消耗锻造矿石，尚无配方和批量操作。
- 战斗内容密度、场景规模和 UI 功能深度仍属于原型基线；现有首批视觉不视为最终美术质量。

完整流程与模块边界见 [`gameplay-loop.md`](./gameplay-loop.md)，生命周期见[应用生命周期与会话作用域](./infrastructure/application-lifecycle.md)，稳定身份和迁移见[稳定身份、内容目录与迁移框架](./infrastructure/content-identity-migration.md)，本地持久化见[本地存档与 Session 恢复](./infrastructure/local-save.md)，设置与语言见[用户设置与本地化](./infrastructure/user-settings-localization.md)，输入、菜单和场景交互结构见 [`input-ui-system.md`](./input-ui-system.md)。

## 文档维护约定

- 文档内容以代码和目录现状为准。
- 目录调整后同步更新 `project-structure.md`。
- 新增核心模块时补充项目概览和对应模块文档。
