# DarkFlare 目录结构

## 根目录

```text
DarkFlare/
  Assets/
  Docs/
    README.md
    design/
    docs/
  Packages/
  ProjectSettings/
  tools/
    ground_tiles/
      process_ground_tiles.py
      audit_ground_tiles.py
      ground_tile_contract.json
    visual_slice/
      prepare_assets.py
      asset_pipeline_manifest.json
      run_asset_audits.ps1
```

## Assets 目录

```text
Assets/
  AddressableAssetsData/
  Art/
    Animations/
      Clips/
      Controllers/
    Audio/
    Materials/
    Sprites/
      Characters/Player/
      Monsters/{Basic,Swift,Heavy}/
      NPCs/Merchant/
      Effects/
      Environment/{GroundTiles,VisualSlice,WorldProps}/
      Items/Equipment/
      UI/{Frames,Icons,Phase5}/
    Tiles/
      Environment/Ground/
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
        Events/
        Input/
        Interaction/
        Inventory/
        Items/
        Loot/
        Skills/
        Spawning/
        Trading/
        UI/
      UI/
      Utilities/
    Editor/                           # 程序集 DarkFlare.Editor（仅 Editor 平台）
      DarkFlare.Editor.asmdef
      ConfigCenterWindow.cs
      GroundTilemapSetup.cs
      VisualAssetSingleSpriteMigration.cs
    Tests/
      EditMode/                       # 程序集 DarkFlare.Tests.EditMode
        DarkFlare.Tests.EditMode.asmdef
        GroundTilemapTests.cs
        Phase5VisualIntegrationTests.cs
      PlayMode/                       # 程序集 DarkFlare.Tests.PlayMode
        DarkFlare.Tests.PlayMode.asmdef
  Settings/
    Scenes/
    UI/
      GamePanelSettings.asset
  TextMesh Pro/
  UI/
    GameRoot.uxml
    GameMenu.uss
    Hud.uxml
    Hud.uss
    Inventory.uxml
    Inventory.uss
    Shop.uxml
    Shop.uss
    Crafting.uxml
    Crafting.uss
  UI Toolkit/
    UnityThemes/
      UnityDefaultRuntimeTheme.tss
```

## 目录职责说明

### `Assets/AddressableAssetsData`

Addressables 的配置目录，包含资源组、模板和构建器配置。后续资源加载应优先基于这里维护，而不是使用 `Resources`。

### `Assets/Art`

美术资源目录当前包括：

- `Animations/Clips` 与 `Animations/Controllers`：角色、怪物和商人的动画资源。
- `Sprites/Characters`、`Monsters`、`NPCs`：52 张 128×128 独立动画帧。
- `Sprites/Effects`、`Environment`、`Items`、`UI`：独立投射物、世界物件、六张地表 Tile 源图、装备图标、固定 UI 图标与 UI Frame。
- `Tiles/Environment/Ground`：由地表源图自动生成的六个 Unity `Tile` 资产。
- `Textures/Prototype/PrototypeSquare.png`：64×64 的非生产回退纹理，正式场景和 Prefab 不依赖它。

项目自有运行时栅格资产统一使用 `Sprite Mode: Single`。旧 `Assets/Art/SpriteSheets/` 已在 alpha 0.1 前置迁移中清理，当前不保留生产 SpriteSheet 或 sub-sprite fileID 依赖。常规单图工具见 `tools/visual_slice/`；双层地表的生成审计和场景重建入口见 `tools/ground_tiles/` 与 `Assets/Scripts/Editor/GroundTilemapSetup.cs`。

### `Assets/Data`

项目数据目录，当前分为：

- `Preset`：适合放预设型配置资源
- `Saves`：当前为空；后续只可放 Editor 存档夹具或样例，正式运行时存档必须写入 `Application.persistentDataPath`

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

`Main.unity` 当前包含常驻 `UIRoot`（`UIDocument` + HUD / 菜单 / 背包 / 商店 / 打造 / 交互提示控制器）、唯一 `EventSystem`，以及可交互的 `Merchant` 与 `CraftingStation` 原型对象。视觉地表位于 `GroundGrid`，包含 5×5 全覆盖的 `GroundBaseTilemap` 和 11 格稀疏的 `GroundDetailTilemap`；两层均不带 Collider，玩法边界仍由独立 `WorldBounds` 提供。`InputSystemUIInputModule` 引用项目 `InputSystem_Actions.inputactions` 的 `UI` action map。

### `Assets/Scripts`

代码目录已按 **程序集（asmdef）** 分层，共五个程序集：

| 程序集 | 目录 | 平台 | 依赖 |
| --- | --- | --- | --- |
| `DarkFlare.Core` | `Runtime/Core/` | 全部 | 无（仅 `QFramework.cs`，稳定框架层，隔离后迭代玩法不再重编框架） |
| `DarkFlare.Runtime` | `Runtime/` | 全部 | `DarkFlare.Core`、`UniTask`、`Unity.InputSystem`、`Unity.Addressables`、`Unity.ResourceManager` |
| `DarkFlare.Editor` | `Editor/` | 仅 Editor | `DarkFlare.Runtime`、`DarkFlare.Core` |
| `DarkFlare.Tests.EditMode` | `Tests/EditMode/` | 仅 Editor | `DarkFlare.Runtime`、`DarkFlare.Core`、`Unity.InputSystem`、`Unity.InputSystem.TestFramework`、`UnityEngine.TestRunner`、`UnityEditor.TestRunner`、`nunit.framework.dll` |
| `DarkFlare.Tests.PlayMode` | `Tests/PlayMode/` | 全部 | 同 EditMode；覆盖场景循环、输入、装备、随机化与运行时资源加载 |

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
- `GameArchitecture.cs`（位于 `Runtime/` 根，组合根；已注册 `GameInput`、`CombatModel`/`EquipmentModel`/`InventoryModel`/`EconomyModel`、`CombatSystem`/`SpawnSystem`/`LootSystem`/`TradingSystem`/`CraftingSystem`/`GameplayPauseSystem` 与 `PrefabAssetLoader`）
- `Data/Tags/TagDefinition.cs`、`Data/Tags/TagQueryDefinition.cs`（标签目录元数据与结构化查询）
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
- `Editor/ConfigCenterWindow.cs`、`Editor/ContentConfigurationValidator.cs`
- `Gameplay/Combat`：`CombatModel.cs`、`CombatSystem.cs`、`SpawnSystem.cs`、`LootSystem.cs`、`EquipmentModel.cs`、`PrefabAssetLoader.cs`、`CombatEvents.cs`、`CombatTagContext.cs`、`CombatTagResolver.cs`、`Commands/`（`RegisterActorCommand`/`UnregisterActorCommand`/`ApplyDamageCommand`/`ReviveActorCommand`/`SpawnPlayerCommand`/`SpawnMonsterCommand`/`FireProjectileCommand`/`PickupLootCommand`/`EquipItemCommand`/`BuyItemCommand`/`SellItemCommand`/`CraftItemCommand`）、`Queries/`（`GetClosestActorQuery`/`GetItemPriceQuery`/`GetCraftingCostQuery`），以及 `DamageCalculator`/`DamagePacket`/`StatBlock`/`TagSet` 等纯逻辑。注：Command/Query 目前统一放在 `Combat/Commands`、`Combat/Queries` 下，含交易、打造等非战斗动作
- `Gameplay/Items`：`ItemInstance.cs`、`ItemGenerationOptions.cs`、`ItemGenerator.cs`（物品随机生成的纯逻辑，由 `LootSystem` 调用）
- `Gameplay/Inventory`：`InventoryGrid.cs`（纯逻辑二维格子占用、精确移动与单目标交换）、`InventoryModel.cs`（玩家背包 + 金币）、`MoveInventoryItemCommand.cs`（整理事务入口）
- `Gameplay/Trading`：`ItemValueCalculator.cs`（纯逻辑价值/买卖价计算）、`EconomyModel.cs`（商人库存 + 买卖倍率）、`TradingSystem.cs`（买卖/价格/商人 seeding）
- `Gameplay/Crafting`：`CraftingOperations.cs`（六类随机打造与原子提交）、`CraftingModels.cs`（操作 / 范围 / 结构化结果）、`CraftingRandomSeeds.cs`（独立子种子）、`CraftingSystem.cs`（背包、金币和随机根种子，无 Model）
- `Gameplay/Actors`：`CombatActor.cs`、`PlayerController.cs`、`MonsterController.cs`、`ActorTeam.cs`
- `Gameplay/Skills`：`ProjectileController.cs`
- `Gameplay/Spawning`：`MonsterSpawner.cs`
- `Gameplay/Loot`：`LootPickupController.cs`
- `Gameplay/Bootstrap`：`CombatPrototypeBootstrap.cs`、`CameraFollowTarget.cs`
- `Gameplay/Input`：`GameInput.cs`（输入封装、Gameplay/UI Action Map 切换与交互事件）、`InputSystem_Actions.cs`（由输入资产自动生成的 C# 包装类）
- `Gameplay/Interaction`：`WorldInteractionTarget.cs`、`PlayerInteractionController.cs`、`GameplayPauseSystem.cs` 与 `Commands/`，负责最近世界目标、情境菜单请求和集中暂停
- `Gameplay/Events/GameplayEvents.cs`：金币、背包、装备、打造、交易、交互焦点、菜单请求、暂停和 Actor 注册 / 注销领域事件
- `Gameplay/UI/GetHudSnapshotQuery.cs`、`HudController.cs`：生命、金币和最终有效属性的只读 HUD 快照与事件驱动控制器
- `Gameplay/UI/GetInventorySnapshotQuery.cs`、`InventoryPanelController.cs`：只读背包快照、共享 10×6 格子、四槽装备、拖拽与拿起 / 放置控制器
- `Gameplay/UI/ItemTooltipView.cs`、`ItemDragQueries.cs`、`MerchantGridLayout.cs`：唯一物品浮窗、拖拽目标只读查询和商人确定性虚拟格子排布
- `Gameplay/UI/GetShopSnapshotQuery.cs`、`ShopPanelController.cs`：只读商店快照、商人格子与共享玩家背包的买卖交互控制器
- `Gameplay/UI/GetCraftingSnapshotQuery.cs`、`CraftingPanelController.cs`：复用共享玩家背包选择，提供稀有度、任意 / 前缀 / 后缀范围和 14 个随机打造变体，不保留具体词条选择
- `Gameplay/UI/GameMenuAccess.cs`、`GameMenuController.cs`：背包 / 商店 / 打造情境访问范围、共享菜单遮罩、关闭和 Gameplay/UI 输入路由
- `Gameplay/UI/InteractionPromptController.cs`：显示当前世界交互目标，并在 UI 模式或目标失效时隐藏
- `Tests/EditMode/Alpha01TagMigrationCharacterizationTests.cs` 与既有 EditMode 测试：覆盖标签查询、域隔离、物品—词条候选矩阵、伤害血统、旧接口兼容、纯逻辑、原子换装、交易和打造事务语义、键鼠 / 手柄输入、Action Map 切换、交互消息、暂停及 HUD/背包/商店/打造快照。归属 `DarkFlare.Tests.EditMode` 程序集

`UI`、`Utilities` 目前主要是占位，为后续模块扩展预留。

**首版单场景循环已完成**：当前已覆盖输入、UIToolkit 根 / HUD、背包装备、商店、打造与场景交互入口。运行流程见 [`gameplay-loop.md`](gameplay-loop.md)，输入与 UI 结构见 [`input-ui-system.md`](input-ui-system.md)；完成过程保存在 [`plan/archive/`](plan/archive/README.md)。

### `Assets/Settings`

项目资源级设置目录。当前可见内容主要用于：

- 输入系统配置（`InputSystem_Actions.inputactions`，含 `Keyboard&Mouse` 与 `Gamepad` 两套控制方案；现已作为唯一输入源并自动生成 C# 包装类）
- UIToolkit 面板配置（`UI/GamePanelSettings.asset`，参考分辨率 1920×1080）
- URP 配置
- 场景相关设置资源

界面结构与样式位于 `Assets/UI/`；`ItemWorkbench.uxml/.uss` 定义共享装备与玩家背包，`GameRoot.uxml` 提供顶层 tooltip / drag overlay。Unity 默认运行时主题位于 `Assets/UI Toolkit/UnityThemes/`。

### `Assets/TextMesh Pro`

TextMesh Pro 默认资源目录。

## 非 Assets 目录

### `Docs`

项目文档统一容器，入口为 `Docs/README.md`。

#### `Docs/design`

游戏设计文档目录，主要由人工维护。Agent 只有在纯游戏设计任务中显式使用 `/design` 或 `$design` Skill 时才允许读取或修改。

#### `Docs/docs`

Agent 管理的项目文档目录，包含结构说明、开发约定、模块文档、执行计划、归档与验收资源。

### `Packages`

Unity 包管理目录。当前关键依赖包括：

- `com.coplaydev.unity-mcp`
- `com.cysharp.unitask`
- `com.kyrylokuzyk.primetween`
- `com.unity.addressables`
- `com.unity.inputsystem`
- `com.unity.localization`
- `com.unity.pipeline`
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
- 战斗、掉落、背包、交易、打造、输入、HUD、情境菜单与世界交互入口已有首版可运行内容

因此，后续工作重点不在“再拆目录”，而在把每一层真正填上首批可运行内容。
