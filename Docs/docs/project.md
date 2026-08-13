# DarkFlare 项目概览

## 项目状态

`DarkFlare` 已完成首版单场景最小循环、初步体验优化与 `alpha 0.1` 封板。现有基线可以在 `Main.unity` 中完成战斗、掉落、拾取、四槽装备、交易、打造和返回战斗的完整循环。

alpha 版本内的阶段使用三段式名称：`alpha 0.1` 的首个阶段为 `alpha 0.1.0`，后续依次为 `alpha 0.1.1`、`alpha 0.1.2`。已完成版本记录见[归档的 alpha 0.1 开发计划](./plan/archive/alpha-0.1-plan.md)。

## 游戏定位

项目目标是类暗黑、流放之路的构筑驱动 RPG，并融合类塔科夫的背包资源管理、物资取舍和跑商经营。首个阶段围绕同一套伤害、词条、物品和经济数据验证“战斗 → 掉落 → 整理 → 交易 / 打造 → 再战斗”的单场景循环。

## 当前技术栈

- Unity `6000.4.3f1`，URP
- QFramework 分层与项目内 `QFramework.cs`
- UniTask 异步
- Addressables 资源加载
- Input System 键鼠 / 手柄输入
- UI Toolkit 运行时界面
- Odin Inspector 配置与 Editor 工具
- PrimeTween 动画补间
- unity-mcp 编辑器自动化与验证

当前构建场景只有 `Assets/Scenes/Main.unity`。

## 核心入口

### 架构与程序集

- `Assets/Scripts/Runtime/Core/QFramework.cs`：独立 `DarkFlare.Core` 程序集。
- `Assets/Scripts/Runtime/GameArchitecture.cs`：组合根，注册输入 Utility、战斗 / 装备 / 背包 / 经济 Model，以及战斗、生成、掉落、交易、打造 System。
- `Assets/Scripts/Runtime/`：`DarkFlare.Runtime` 程序集。
- `Assets/Scripts/Tests/EditMode/`：`DarkFlare.Tests.EditMode`，初步体验优化收尾时全量 98/98 通过。

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
- 各面板 Controller 只通过 Query、Command 与领域事件工作，不直接修改 Model。

### 编辑器工具

- `Assets/Scripts/Editor/ConfigCenterWindow.cs` 基于 Odin 提供配置中心。
- 按 `CreateAssetMenu(menuName = "DarkFlare/Data/...")` 自动发现配置类型，支持按类型浏览、创建和直接编辑真实配置资产。

## 当前资源与配置

- `Assets/Data/Preset` 已有玩家、技能、怪物、刷怪、掉落、物品、词条、商人和打造配置。
- 玩家、怪物、投射物和掉落物 Prefab 位于 `Assets/Prefabs`，通过 Addressables 加载。
- `Assets/UI` 已有 `GameRoot`、`Hud`、`Inventory`、`Shop`、`Crafting` 的 UXML / USS。
- 地图、玩家、三种怪物、七件装备、商人、打造台和运行时 UI 已完成首批视觉接入；`PrototypeSquare.png` 仅保留为调试回退。

## 当前边界

首版已经完成“战斗 → 拾取 → 装备 / 交易 / 打造 → 再战斗”的人手循环，但仍是用于验证系统闭环的功能原型：

- 仅有 `Main.unity` 单场景，没有撤离、场景切换或存档闭环。
- 背包没有拖拽换位、旋转、堆叠和重量；装备已实现武器、护甲、左戒指和右戒指四槽，但没有耐久、套装、纸娃娃或唯一装备特效。
- 交易没有回购或多商人独立库存；打造没有配方、材料和批量操作。
- 战斗内容密度、场景规模和 UI 功能深度仍属于原型基线；现有首批视觉不视为最终美术质量。

完整流程与模块边界见 [`gameplay-loop.md`](./gameplay-loop.md)，输入、菜单和场景交互结构见 [`input-ui-system.md`](./input-ui-system.md)。

## 文档维护约定

- 文档内容以代码和目录现状为准。
- 目录调整后同步更新 `project-structure.md`。
- 新增核心模块时补充项目概览和对应模块文档。
