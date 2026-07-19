# DarkFlare 项目概览

## 项目状态

`DarkFlare` 已完成单场景最小循环第 1–7 步后端，以及第 8a–8e 的输入、HUD、背包 / 装备、商店和打造交互。当前可以在 `Main.unity` 中战斗、掉落、拾取，并通过共享营地菜单整理装备、买卖和打造；下一步是 8f 场景交互入口与完整循环收尾。

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
- `Assets/Scripts/Tests/EditMode/`：`DarkFlare.Tests.EditMode`，当前全量 42/42 通过。

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
- 原型美术仍以 `Assets/Art/Textures/Prototype/PrototypeSquare.png` 等占位资源为主。

## 当前边界与下一步

当前 UI 可从共享菜单直接访问，但世界中的商人 / 打造台实体和 `Interact` 入口尚未接入。8f 应完成：

1. 定义场景交互目标与距离判断。
2. 把 `Interact` 接到商人或打造台，并打开对应菜单页。
3. 验证“战斗 → 拾取 → 装备 / 出售 / 打造 → 关闭菜单 → 再战斗”的完整人手循环。

配方、打造材料、回购、多装备槽、物品旋转 / 堆叠 / 重量和正式美术不属于当前最小范围。

## 文档维护约定

- 文档内容以代码和目录现状为准。
- 目录调整后同步更新 `project-structure.md`。
- 新增核心模块时补充项目概览和对应模块文档。
