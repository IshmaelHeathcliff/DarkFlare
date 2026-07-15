# DarkFlare 目录结构

## 根目录

```text
DarkFlare/
  Assets/
  design/
  docs/
  Packages/
  ProjectSettings/
```

## Assets 目录

```text
Assets/
  AddressableAssetsData/
  Art/
    Animaitons/
      AnimationClips/
      Animators/
    Audio/
    Materials/
    Textures/
      Prototype/
  Data/
    Preset/
    Saves/
  Plugins/
    Roslyn/
    Sirenix/
  Prefabs/
    Combat/
    Loot/
  Scenes/
    Main.unity
  Scripts/
    Runtime/                          # 程序集 DarkFlare.Runtime
      DarkFlare.Runtime.asmdef
      GameArchitecture.cs
      Core/                           # 程序集 DarkFlare.Core（仅 QFramework）
        DarkFlare.Core.asmdef
        QFramework.cs
      Data/
        Actors/
        Affixes/
        Crafting/
        Items/
        Loot/
        Monsters/
        Skills/
        Stats/
        Tags/
        Trading/
      Gameplay/
        Actors/
        Bootstrap/
        Combat/
          Commands/
          Queries/
        Crafting/
        Inventory/
        Items/
        Loot/
        Skills/
        Spawning/
        Trading/
      UI/
      Utilities/
    Editor/                           # 程序集 DarkFlare.Editor（仅 Editor 平台）
      DarkFlare.Editor.asmdef
      ConfigCenterWindow.cs
    Tests/
      EditMode/                       # 程序集 DarkFlare.Tests.EditMode
        DarkFlare.Tests.EditMode.asmdef
      PlayMode/                       # 程序集 DarkFlare.Tests.PlayMode（占位）
        DarkFlare.Tests.PlayMode.asmdef
  Settings/
    Scenes/
  TextMesh Pro/
```

## 目录职责说明

### `Assets/AddressableAssetsData`

Addressables 的配置目录，包含资源组、模板和构建器配置。后续资源加载应优先基于这里维护，而不是使用 `Resources`。

### `Assets/Art`

美术资源目录，当前已预留：

- `Animaitons`
- `Audio`
- `Materials`
- `Textures`
  - `Prototype`：占位方块贴图（`PrototypeSquare.png`），供 `Prefabs/Combat`、`Prefabs/Loot` 下的 Prefab 共用，正式美术接入后应替换并清理

说明：
当前目录名为 `Animaitons`，文档按现状记录，若后续修正拼写，需要同步调整文档与资源引用。

### `Assets/Data`

项目数据目录，当前分为：

- `Preset`：适合放预设型配置资源
- `Saves`：适合放存档或运行期持久化数据

### `Assets/Plugins`

第三方插件目录，当前已可确认：

- `Sirenix`：Odin Inspector
- `Roslyn`：编译相关依赖

### `Assets/Prefabs`

预制体目录，当前已有战斗原型业务资源：

- `Combat/Player.prefab`
- `Combat/Monster_Basic.prefab`
- `Combat/Projectile_Default.prefab`
- `Loot/LootPickup.prefab`

四者都已标记为 Addressable（key 分别是 `Combat/Player`、`Combat/Monster/Basic`、`Combat/Projectile/Default`、`Loot/Pickup`），由 `SpawnSystem`/`LootSystem` 通过共用的 `PrefabAssetLoader` 预热后实例化，不再运行时现造 GameObject。

### `Assets/Scenes`

场景目录。当前仅有默认场景：

- `Main.unity`

同时 `ProjectSettings/EditorBuildSettings.asset` 里当前也只注册了这个场景。

### `Assets/Scripts`

代码目录已按 **程序集（asmdef）** 分层，共四个程序集：

| 程序集 | 目录 | 平台 | 依赖 |
| --- | --- | --- | --- |
| `DarkFlare.Core` | `Runtime/Core/` | 全部 | 无（仅 `QFramework.cs`，稳定框架层，隔离后迭代玩法不再重编框架） |
| `DarkFlare.Runtime` | `Runtime/` | 全部 | `DarkFlare.Core`、`UniTask`、`Unity.InputSystem`、`Unity.Addressables`、`Unity.ResourceManager` |
| `DarkFlare.Editor` | `Editor/` | 仅 Editor | `DarkFlare.Runtime`、`DarkFlare.Core` |
| `DarkFlare.Tests.EditMode` | `Tests/EditMode/` | 仅 Editor | `DarkFlare.Runtime`、`DarkFlare.Core`、`UnityEngine.TestRunner`、`UnityEditor.TestRunner`、`nunit.framework.dll` |
| `DarkFlare.Tests.PlayMode` | `Tests/PlayMode/` | 全部 | 同 EditMode（当前为占位，暂无测试） |

依赖方向单向向上、无环：`Core ← Runtime ← {Editor, Tests}`。`GameArchitecture.cs` 作为组合根依赖全部玩法模块，因此位于 `Runtime/` 根而非 `Core/`。测试程序集带 `defineConstraints: ["UNITY_INCLUDE_TESTS"]`，仅在测试运行时参与编译，不进入 Player 包。Odin 等预编译 DLL 默认对所有程序集可见，无需在 asmdef 中显式引用。

`Runtime/` 下的子目录职责：

- `Core`：基础架构与全局入口（独立成 `DarkFlare.Core` 程序集）
- `Data`：数据定义与配置类型
- `Gameplay`：玩法逻辑
- `UI`：界面逻辑（占位）
- `Utilities`：通用工具（占位）

> 下列文件清单中，`Core/`、`Data/`、`Gameplay/`、`UI/`、`Utilities/` 路径均相对 `Scripts/Runtime/`；`Editor/` 相对 `Scripts/`；测试相对 `Scripts/Tests/`。

当前代码已覆盖以下基础层：

- `Core/QFramework.cs`（`DarkFlare.Core` 程序集）
- `GameArchitecture.cs`（位于 `Runtime/` 根，组合根；已注册 `CombatModel`/`EquipmentModel`/`InventoryModel`/`EconomyModel`/`CombatSystem`/`SpawnSystem`/`LootSystem`/`TradingSystem`/`CraftingSystem`/`PrefabAssetLoader`）
- `Data/Tags/TagDefinition.cs`
- `Data/Stats/StatDefinition.cs`
- `Data/Actors/CharacterDefinition.cs`
- `Data/Affixes/AffixDefinition.cs`
- `Data/Items/ItemBaseDefinition.cs`
- `Data/Loot/LootTableDefinition.cs`
- `Data/Skills/ProjectileSkillDefinition.cs`
- `Data/Monsters/MonsterDefinition.cs`
- `Data/Monsters/MonsterSpawnDefinition.cs`
- `Data/Trading/TraderDefinition.cs`
- `Data/Crafting/CraftingDefinition.cs`
- `Editor/ConfigCenterWindow.cs`
- `Gameplay/Combat`：`CombatModel.cs`、`CombatSystem.cs`、`SpawnSystem.cs`、`LootSystem.cs`、`EquipmentModel.cs`、`PrefabAssetLoader.cs`、`CombatEvents.cs`、`Commands/`（`RegisterActorCommand`/`UnregisterActorCommand`/`ApplyDamageCommand`/`ReviveActorCommand`/`SpawnPlayerCommand`/`SpawnMonsterCommand`/`FireProjectileCommand`/`PickupLootCommand`/`EquipItemCommand`/`BuyItemCommand`/`SellItemCommand`/`CraftItemCommand`）、`Queries/`（`GetClosestActorQuery`/`GetItemPriceQuery`/`GetCraftingCostQuery`），以及原有的 `DamageCalculator`/`StatBlock`/`TagSet` 等纯逻辑。注：Command/Query 目前统一放在 `Combat/Commands`、`Combat/Queries` 下，含交易、打造等非战斗动作
- `Gameplay/Items`：`ItemInstance.cs`、`ItemGenerationOptions.cs`、`ItemGenerator.cs`（物品随机生成的纯逻辑，由 `LootSystem` 调用）
- `Gameplay/Inventory`：`InventoryGrid.cs`（纯逻辑二维格子占用）、`InventoryModel.cs`（玩家背包 + 金币，持有单个 `InventoryGrid`）
- `Gameplay/Trading`：`ItemValueCalculator.cs`（纯逻辑价值/买卖价计算）、`EconomyModel.cs`（商人库存 + 买卖倍率）、`TradingSystem.cs`（买卖/价格/商人 seeding）
- `Gameplay/Crafting`：`CraftingOperations.cs`（纯逻辑四种词条操作）、`CraftingSystem.cs`（打造金币成本 + seeding，无 Model）
- `Gameplay/Actors`：`CombatActor.cs`、`PlayerController.cs`、`MonsterController.cs`、`ActorTeam.cs`
- `Gameplay/Skills`：`ProjectileController.cs`
- `Gameplay/Spawning`：`MonsterSpawner.cs`
- `Gameplay/Loot`：`LootPickupController.cs`
- `Gameplay/Bootstrap`：`CombatPrototypeBootstrap.cs`、`CameraFollowTarget.cs`
- `Tests/EditMode/MonsterSpawnDefinitionTests.cs`、`LootTableDefinitionTests.cs`、`DamageCalculatorTests.cs`、`InventoryGridTests.cs`、`ItemValueCalculatorTests.cs`、`CraftingOperationsTests.cs`：EditMode 测试，分别覆盖 `MonsterSpawnDefinition.PickMonster`、`LootTableDefinition.PickItem` 权重逻辑、带 Increase 词条的伤害结算、背包格子占用、交易价值计算、打造词条操作。归属 `DarkFlare.Tests.EditMode` 程序集，通过显式引用 `DarkFlare.Runtime`/`DarkFlare.Core` 使用运行时代码，并引用 `nunit.framework.dll` 与 TestRunner

`UI`、`Utilities` 目前主要是占位，为后续模块扩展预留。

**规划中（第 8 步"串成完整循环"，设计见 [`input-ui-design.md`](input-ui-design.md)）**：`Gameplay/Input`（`GameInput` 封装 `InputSystem_Actions` 生成的包装类，替换 `PlayerController` 直接轮询，含 `Player`/`UI` action map 切换）、`Gameplay/UI`（UIToolkit 面板脚本：HUD、背包、商店、打造，均实现 `IController` 接入 QFramework）。UXML/USS 约定放 `Assets/UI/`。这些目录尚未创建，待各子步落地后回填。

### `Assets/Settings`

项目资源级设置目录。当前可见内容主要用于：

- 输入系统配置（`InputSystem_Actions.inputactions`，含 `Keyboard&Mouse` 与 `Gamepad` 两套控制方案；第 8 步起改为唯一输入源并生成 C# 包装类）
- URP 配置
- 场景相关设置资源

规划中：UIToolkit 的 `PanelSettings` 与主题样式资源将放此处。

### `Assets/TextMesh Pro`

TextMesh Pro 默认资源目录。

## 非 Assets 目录

### `design`

设计资料目录，当前未见明确内容。适合后续放玩法草案、流程稿、界面草图等非运行时资源。

### `docs`

项目文档目录。当前已建立基础文档，应作为结构说明、开发约定和系统设计的统一入口。

### `Packages`

Unity 包管理目录。当前关键依赖包括：

- `com.coplaydev.unity-mcp`
- `com.cysharp.unitask`
- `com.kyrylokuzyk.primetween`
- `com.unity.addressables`
- `com.unity.inputsystem`
- `com.unity.ugui`
- `com.unity.render-pipelines.universal`

### `ProjectSettings`

Unity 工程级设置目录，包括版本、构建场景、图形设置等。

## 当前结构判断

当前结构已经具备继续开发的基础条件，特点是：

- 资源目录、代码目录、设置目录已经先分层
- 代码已按程序集（asmdef）拆分为 `Core`/`Runtime`/`Editor`/`Tests` 四层，编译与测试边界清晰
- 插件和核心依赖已经接入
- 代码架构入口已经就位
- 业务模块、配置定义和具体内容仍基本为空

因此，后续工作重点不在“再拆目录”，而在把每一层真正填上首批可运行内容。
