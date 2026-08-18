# DarkFlare 项目概览

## 项目状态

`DarkFlare` 已完成首版单场景最小循环、初步体验优化、`alpha 0.1` 封板和 `alpha 0.2.0` 应用生命周期底座，现正继续推进 `alpha 0.2` 基础设施。现有基线可以在 `Main.unity` 中完成战斗、掉落、拾取、四槽装备、交易、打造和返回战斗的完整循环，并能安全停止、重建当前玩法 Session。

alpha 版本内的阶段使用三段式名称：`alpha 0.1` 的首个阶段为 `alpha 0.1.0`，后续依次为 `alpha 0.1.1`、`alpha 0.1.2`；`alpha 0.2` 同样从 `alpha 0.2.0` 开始。已完成版本记录见[计划归档](./plan/archive/README.md)，当前版本见[alpha 0.2 基础设施开发计划](./plan/alpha-0.2-plan.md)。

## 游戏定位

项目目标是类暗黑、流放之路的构筑驱动 RPG，并融合类塔科夫的背包资源管理、物资取舍和跑商经营。首个阶段围绕同一套伤害、词条、物品和经济数据验证“战斗 → 掉落 → 整理 → 交易 / 打造 → 再战斗”的单场景循环。

## 当前技术栈

- Unity `6000.4.3f1`，URP
- QFramework 分层与项目内 `QFramework.cs`
- UniTask 异步
- Addressables 资源加载
- Input System 键鼠 / 手柄输入
- Unity Localization `1.5.12`（当前仅安装包，运行时接入列入 `alpha 0.2`）
- UI Toolkit 运行时界面
- Odin Inspector 配置与 Editor 工具
- PrimeTween 动画补间
- unity-mcp 编辑器自动化与验证

当前构建场景只有 `Assets/Scenes/Main.unity`。

## 核心入口

### 架构与程序集

- `Assets/Scripts/Runtime/Core/QFramework.cs`：独立 `DarkFlare.Core` 程序集。
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationBootstrap.cs`：在场景加载前创建唯一应用宿主，并在 Subsystem Registration 重置静态状态。
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationHost.cs`：拥有 Application / Profile / Session / Scene 生命周期和统一取消入口，以 latest-wins 协调当前单场景兼容请求。
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/GameArchitectureProvider.cs`：`GameArchitecture` 的唯一正式创建、访问与销毁入口，以所有者 lease 和单调 generation 隔离连续 Session。
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/SceneSessionBinding.cs`：让场景预置组件只绑定同场景且已 Running、lease 有效的 Session。
- `Assets/Scripts/Runtime/GameArchitecture.cs`：Session 组合根，注册输入 Utility、战斗 / 装备 / 背包 / 经济 Model，战斗、生成、掉落、交易、打造 System，以及 `SessionObjectRegistry`。
- `Assets/Scripts/Runtime/`：`DarkFlare.Runtime` 程序集。
- `Assets/Scripts/Tests/EditMode/`：`DarkFlare.Tests.EditMode`，`alpha 0.2.0` 完成基线全量 `255/255` 通过。
- `Assets/Scripts/Tests/PlayMode/`：`DarkFlare.Tests.PlayMode`，`alpha 0.2.0` 项目自有测试 `45/45` 通过；完整运行 `49` 项中 `47` 项通过、`0` 失败，另有 `2` 项 Input System 包集成测试因上游 issue 1252825 跳过。

生命周期的职责、状态、事务和禁止事项见[应用生命周期与会话作用域](./infrastructure/application-lifecycle.md)。

### 玩法模块

- 战斗与属性：`Gameplay/Combat`、`Data/Stats`、`Data/Tags`、`Data/Affixes`。
- 物品、掉落与背包：`Gameplay/Items`、`Gameplay/Loot`、`Gameplay/Inventory`、对应 `Data` 配置。
- 装备：`EquipmentModel`、`EquipItemCommand` 与 `InventoryPanelController`。
- 交易：`EconomyModel`、`TradingSystem`、`TraderDefinition` 与 `ShopPanelController`。
- 打造：`CraftingOperations`、`CraftingSystem`、`CraftingDefinition` 与 `CraftingPanelController`。
- 输入：`GameInput` 统一管理 Gameplay / UI Action Map，`InputSystem_Actions.inputactions` 是唯一输入源。

### UI

- `Assets/UI/GameRoot.uxml` 组合 HUD、背包、商店和打造模板。
- `Main.unity/UIRoot` 复用一个 `UIDocument` 和唯一 `EventSystem`。
- `GameMenuController` 管理共享遮罩、三页签、关闭和输入模式。
- 各面板 Controller 通过 `SceneSessionBinding` 等待有效 Session，只通过 Query、Command 与领域事件工作，不直接修改 Model。

### 编辑器工具

- `Assets/Scripts/Editor/ConfigCenterWindow.cs` 基于 Odin 提供配置中心。
- 按 `CreateAssetMenu(menuName = "DarkFlare/Data/...")` 自动发现配置类型，支持按类型浏览、创建和直接编辑真实配置资产。

## 当前资源与配置

- `Assets/Data/Preset` 已有玩家、技能、怪物、刷怪、掉落、物品、词条、商人和打造配置。
- 玩家、怪物、投射物和掉落物 Prefab 位于 `Assets/Prefabs`，通过 Addressables 加载。
- Prefab Addressables 加载按 GUID 建立跨调用单飞任务；Sprite 在单次预热请求内按 GUID 去重，加载器保存并精确释放实际句柄。
- `Assets/UI` 已有 `GameRoot`、`Hud`、`Inventory`、`Shop`、`Crafting` 的 UXML / USS。
- 地图、玩家、三种怪物、七件装备、商人、打造台和运行时 UI 已完成首批视觉接入；`PrototypeSquare.png` 仅保留为调试回退。

## 当前边界

首版已经完成“战斗 → 拾取 → 装备 / 交易 / 打造 → 再战斗”的人手循环，但仍是用于验证系统闭环的功能原型：

- 仅有 `Main.unity` 单场景；应用宿主可按 latest-wins 协调该场景卸载、直接重载和快速重复请求，但尚无 Boot / FrontEnd / Loading 状态机或完整 SceneFlow。
- 当前没有正式存档路径、DTO、槽位、原子写入、备份、迁移或读档闭环。
- Unity Localization 包已经安装，但没有 Locale、String Table、运行时切换服务或玩家可见文本迁移。
- 背包没有拖拽换位、旋转、堆叠和重量；装备已实现武器、护甲、左戒指和右戒指四槽，但没有耐久、套装、纸娃娃或唯一装备特效。
- 交易没有回购或多商人独立库存；打造没有配方、材料和批量操作。
- 战斗内容密度、场景规模和 UI 功能深度仍属于原型基线；现有首批视觉不视为最终美术质量。

完整流程与模块边界见 [`gameplay-loop.md`](./gameplay-loop.md)，生命周期见[应用生命周期与会话作用域](./infrastructure/application-lifecycle.md)，输入、菜单和场景交互结构见 [`input-ui-system.md`](./input-ui-system.md)。

## 文档维护约定

- 文档内容以代码和目录现状为准。
- 目录调整后同步更新 `project-structure.md`。
- 新增核心模块时补充项目概览和对应模块文档。
