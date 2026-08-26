# DarkFlare 目录结构

## 根目录

```text
DarkFlare/
  Assets/
  Docs/
    README.md
    design/
    docs/
      infrastructure/
        accessibility-platform-lifecycle.md
        alpha-0.2-acceptance.md
        alpha-0.2-runtime-contract.md
        application-lifecycle.md
        application-audio.md
        content-identity-migration.md
        local-save.md
        user-settings-localization.md
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
    UI/
      InputGlyphs/
  Audio/
    AudioServiceConfiguration.asset
    DarkFlareAudioMixer.mixer
    UI/
      ui_confirm.wav
  Data/
    Preset/
    Saves/
  Localization/
    Locales/
    Tables/
  Plugins/
    Roslyn/
    Sirenix/
  Prefabs/
    Combat/
    Loot/
  Scenes/
    Bootstrap.unity
    Main.unity
  Scripts/
    Runtime/                          # 程序集 DarkFlare.Runtime
      DarkFlare.Runtime.asmdef
      GameArchitecture.cs
      Core/                           # 程序集 DarkFlare.Core（仅 QFramework）
        DarkFlare.Core.asmdef
        QFramework.cs
      Infrastructure/
        Accessibility/
          AccessibilityContracts.cs
          AccessibilityService.cs
        Audio/
          AudioClipLoader.cs
          AudioService.cs
          AudioServiceConfiguration.cs
          AudioServiceContracts.cs
        Content/
        Flow/
          GameFlowContracts.cs
          SceneFlowConfiguration.cs
          SceneFlowService.cs
          SceneLoader.cs
          UnitySceneLoader.cs
        Identity/
        Input/
          ApplicationInputContracts.cs
          ApplicationInputModuleBinder.cs
          ApplicationInputService.cs
          InputGlyphResolver.cs
        Lifecycle/
          ApplicationBootstrap.cs
          ApplicationDataPathProviderFactory.cs
          ApplicationHost.cs
          ComponentLifecycle.cs
          GameArchitectureProvider.cs
          GameSessionHost.cs
          LifecycleModels.cs
          LifecycleScope.cs
          LifecycleTaskGroup.cs
          SceneSessionBinding.cs
          SessionObjectRegistry.cs
        Persistence/
          IdentityDtos.cs
          JsonMigrationPipeline.cs
          LocalSaveStorage.cs
          RestoreGameSessionInitializer.cs
          RuntimeStateMapper.cs
          SaveCoordinator.cs
          SaveDataValidator.cs
          SaveDtos.cs
          SavePathProvider.cs
          SaveRestorePreparer.cs
          SaveSerializer.cs
          SessionSaveFacade.cs
          SessionSnapshotSource.cs
          VersionContracts.cs
        Settings/
          LocalSettingsStorage.cs
          LocalizationService.cs
          LocalizedContentReference.cs
          LocalizedMessage.cs
          SettingsModels.cs
          SettingsPathProvider.cs
          SettingsSerializer.cs
          SettingsService.cs
        Time/
          GameTimeService.cs
        Platform/
          PlatformLifecycleContracts.cs
          PlatformLifecycleService.cs
        UI/
          ApplicationFrontEndController.cs
          ApplicationSettingsController.cs
          ApplicationShellBootstrap.cs
          ApplicationShellController.cs
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
          CombatPrototypeBootstrap.cs
          GameplaySceneConfiguration.cs
          NewGameSessionInitializer.cs
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
      PseudoLocalizationUtility.cs
      VisualAssetSingleSpriteMigration.cs
    Tests/
      EditMode/                       # 程序集 DarkFlare.Tests.EditMode
        DarkFlare.Tests.EditMode.asmdef
        AbandonedGenerationIsolationTests.cs
        AccessibilityAndPlatformLifecycleTests.cs
        Alpha025ProductionAssetTests.cs
        ApplicationInputServiceTests.cs
        ApplicationLifecycleTests.cs
        ArchitectureExceptionSafetyTests.cs
        AudioServiceTests.cs
        GroundTilemapTests.cs
        InfrastructurePolicyExceptions.json
        InfrastructurePolicyTests.cs
        LocalSaveStorageTests.cs
        Phase5VisualIntegrationTests.cs
        PrefabAssetLoaderTests.cs
        SaveCoordinatorTests.cs
        SaveDataContractTests.cs
        SaveRestorePreparerTests.cs
        SaveSerializerTests.cs
      PlayMode/                       # 程序集 DarkFlare.Tests.PlayMode
        DarkFlare.Tests.PlayMode.asmdef
        ApplicationHostSceneTransitionPlayModeTests.cs
        ApplicationLifecyclePlayModeTests.cs
        Alpha025InputOwnershipPlayModeTests.cs
        SceneSessionComponentBindingPlayModeTests.cs
        SaveCaptureRestorePlayModeTests.cs
        SessionObjectRegistryOwnershipPlayModeTests.cs
  Settings/
    Scenes/
    UI/
      Fonts/
        GameCjkFont.asset
        GameLatinFont.asset
      GamePanelSettings.asset
      GamePanelTextSettings.asset
  TextMesh Pro/
    Fonts/
    Resources/Fonts & Materials/
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

- `Preset`：适合放预设型配置资源；其中 `Content/正式内容目录.asset` 是 Application 级唯一正式内容目录
- `Saves`：当前为空；只可放 Editor 历史 Schema / 损坏夹具或样例，正式运行时存档写入 `Application.persistentDataPath/DarkFlare/Saves`

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

场景目录。当前采用最小双场景拓扑：

- `Bootstrap.unity`：Build index 0，常驻 Application Shell 与唯一 EventSystem
- `Main.unity`

`ProjectSettings/EditorBuildSettings.asset` 按 Bootstrap 0、Main 1 注册；Main 只通过 Scene Flow additive 加载。

`Bootstrap.unity` 的 `ApplicationShellBootstrap` 引用正式内容目录、Scene Flow 配置、Application Shell UXML 与 Panel Settings。`Main.unity` 当前包含玩法 `UIRoot`（`UIDocument` + HUD / 菜单 / 背包 / 商店 / 打造 / 交互提示控制器）、唯一 Gameplay 配置提供者，以及可交互的 `Merchant` 与 `CraftingStation` 原型对象；Main 不再包含 EventSystem。视觉地表位于 `GroundGrid`，包含 5×5 全覆盖的 `GroundBaseTilemap` 和 11 格稀疏的 `GroundDetailTilemap`；两层均不带 Collider，玩法边界仍由独立 `WorldBounds` 提供。唯一 `ApplicationHost` 不序列化在场景中，而由 `ApplicationBootstrap` 在 `BeforeSceneLoad` 创建。

### `Assets/Scripts`

代码目录已按 **程序集（asmdef）** 分层，共五个程序集：

| 程序集 | 目录 | 平台 | 依赖 |
| --- | --- | --- | --- |
| `DarkFlare.Core` | `Runtime/Core/` | 全部 | 无（仅 `QFramework.cs`，稳定框架层，隔离后迭代玩法不再重编框架） |
| `DarkFlare.Runtime` | `Runtime/` | 全部 | `DarkFlare.Core`、`UniTask`、`Unity.InputSystem`、`Unity.Addressables`、`Unity.ResourceManager`、`Unity.Newtonsoft.Json`、`Unity.Localization` |
| `DarkFlare.Editor` | `Editor/` | 仅 Editor | `DarkFlare.Runtime`、`DarkFlare.Core` |
| `DarkFlare.Tests.EditMode` | `Tests/EditMode/` | 仅 Editor | `DarkFlare.Runtime`、`DarkFlare.Core`、`Unity.InputSystem`、`Unity.InputSystem.TestFramework`、`Unity.Addressables.Editor`、`Unity.Newtonsoft.Json`、`UnityEngine.TestRunner`、`UnityEditor.TestRunner`、`nunit.framework.dll`、`Newtonsoft.Json.dll` |
| `DarkFlare.Tests.PlayMode` | `Tests/PlayMode/` | 全部 | `DarkFlare.Runtime`、`DarkFlare.Core`、`UniTask` 及 Unity 测试依赖；覆盖场景循环、生命周期、输入、装备、随机化与运行时资源加载 |

依赖方向单向向上、无环：`Core ← Runtime ← {Editor, Tests}`。`GameArchitecture.cs` 作为组合根依赖全部玩法模块，因此位于 `Runtime/` 根而非 `Core/`。测试程序集带 `defineConstraints: ["UNITY_INCLUDE_TESTS"]`，仅在测试运行时参与编译，不进入 Player 包。Odin 等预编译 DLL 默认对所有程序集可见，无需在 asmdef 中显式引用。

`Runtime/` 下的子目录职责：

- `Core`：基础架构与全局入口（独立成 `DarkFlare.Core` 程序集）
- `Infrastructure`：应用宿主、作用域、Session、统一任务取消、架构所有权、内容目录、稳定实例身份、DTO / 迁移、本地存储、Restore、用户设置、本地化、场景流、应用时间、输入、音频、可访问性、平台生命周期、结构化日志、资源服务和 UI 外壳
- `Data`：数据定义与配置类型
- `Gameplay`：玩法逻辑
- `UI`：界面逻辑（占位）
- `Utilities`：通用工具（占位）

> 下列文件清单中，`Core/`、`Infrastructure/`、`Data/`、`Gameplay/`、`UI/`、`Utilities/` 路径均相对 `Scripts/Runtime/`；`Editor/` 相对 `Scripts/`；测试相对 `Scripts/Tests/`。

当前代码已覆盖以下基础层：

- `Core/QFramework.cs`（`DarkFlare.Core` 程序集）
- `Infrastructure/Lifecycle`：`ApplicationBootstrap.cs`、`ApplicationHost.cs`、`GameSessionHost.cs`、`GameArchitectureProvider.cs`、`LifecycleScope.cs`、`LifecycleTaskGroup.cs`、`LifecycleModels.cs`、`ComponentLifecycle.cs`、`SceneSessionBinding.cs`、`SessionObjectRegistry.cs`；实现唯一宿主、Application / Profile / Session / Scene / Component 作用域、NewGame 事务边界、latest-wins 场景协调、lease / generation 隔离、场景组件重绑、统一取消和运行时对象精确注销
- `Infrastructure/Content`：`ContentId.cs`、`ContentDefinitionMetadata.cs`、`ContentCatalogDefinition.cs`、`ContentCatalog.cs`；实现 12 类内容登记、规范 ContentId、目录验证及不可变强类型查询
- `Infrastructure/Identity/StableInstanceIds.cs`：实现 Player / Item / Run / Monster / WorldDrop / SaveSlot 强类型 ID，以及 Profile / Session 所有的可注入生成器
- `Infrastructure/Persistence`：`IdentityDtos.cs`、`RuntimeStateMapper.cs`、`VersionContracts.cs`、`JsonMigrationPipeline.cs`、`SaveDtos.cs`、`SaveDataValidator.cs`、`SaveSerializer.cs`、`SavePathProvider.cs`、`LocalSaveStorage.cs`、`SessionSnapshotSource.cs`、`SaveRestorePreparer.cs`、`RestoreGameSessionInitializer.cs`、`SaveCoordinator.cs`、`SessionSaveFacade.cs`；实现纯 DTO、分离对象图映射、独立版本域、逐级迁移、确定性 JSON、代际文件存储、损坏回退、Session 快照 / 恢复和 Profile 级单写者协调
- `Infrastructure/Settings`：`SettingsModels.cs`、`SettingsSerializer.cs`、`SettingsPathProvider.cs`、`LocalSettingsStorage.cs`、`SettingsService.cs`、`LocalizationService.cs`、`LocalizedContentReference.cs`、`LocalizedMessage.cs`；实现 Settings V1、原子持久化、迁移、Application 启动门禁、Locale latest-wins、表预热、缺失回退和语义文本引用
- `Infrastructure/Flow`：`GameFlowContracts.cs`、`SceneFlowConfiguration.cs`、`SceneLoader.cs`、`UnitySceneLoader.cs`、`SceneFlowService.cs`；实现稳定状态 / SceneId、集中场景注册、唯一 Runtime Scene API、latest-wins 事务、阶段进度和失败补偿
- `Infrastructure/Time/GameTimeService.cs`：实现有主 pause lease、基础倍率恢复与唯一 `Time.timeScale` 写入
- `Infrastructure/Input`：`ApplicationInputService.cs`、`ApplicationInputModuleBinder.cs`、`ApplicationInputContracts.cs`、`InputGlyphResolver.cs`；实现唯一 Action Asset owner、Context / suspension lease、重绑定、设备族、Glyph 和 Bootstrap UI Module 绑定
- `Infrastructure/Audio`：`AudioService.cs`、`AudioServiceConfiguration.cs`、`AudioServiceContracts.cs`、`AudioClipLoader.cs`；实现四类 Mixer 路由、Addressables Cue、Source 池、有主句柄和精确释放
- `Infrastructure/Diagnostics`：`ApplicationLogging.cs`、`PlayerErrorCatalog.cs`；实现日志门面、事件 ID、Console / Ring Buffer Sink、全局异常监控、致命失败协调和玩家错误映射
- `Infrastructure/Resources`：`ApplicationResources.cs`；实现 Addressables 唯一静态后端、Application 级单航班、owner / lease、结构化结果与诊断快照
- `Infrastructure/Accessibility`：`AccessibilityContracts.cs`、`AccessibilityService.cs`；实现 Settings 驱动的 MotionProfile
- `Infrastructure/Platform`：`PlatformLifecycleContracts.cs`、`PlatformLifecycleService.cs`；实现 Focus / Suspend / Resume、检查点和输入 / 时间 / 音频编排
- `Infrastructure/UI`：`ApplicationShellBootstrap.cs`、`ApplicationShellController.cs`、`ApplicationFrontEndController.cs`、`ApplicationSettingsController.cs`；实现 Bootstrap FrontEnd、共享 Settings Page、Busy / Modal / Toast / Fatal 和焦点恢复
- `GameArchitecture.cs`（位于 `Runtime/` 根，Session 组合根；由 `GameArchitectureProvider` 独占创建与销毁，已注册 `GameInput`、`CombatModel`/`EquipmentModel`/`InventoryModel`/`EconomyModel`、`CombatSystem`/`SpawnSystem`/`LootSystem`/`TradingSystem`/`CraftingSystem`/`GameplayPauseSystem`、`PrefabAssetLoader` 与 `SessionObjectRegistry`）
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
- `Gameplay/Bootstrap`：`CombatPrototypeBootstrap.cs`、`GameplaySceneConfiguration.cs`、`NewGameSessionInitializer.cs`、`CameraFollowTarget.cs`；Main Bootstrap 只组装场景配置，由 Scene Flow 选择 initializer，NewGame initializer 负责可回滚的新游戏事务，Restore initializer 位于 `Infrastructure/Persistence`
- `Gameplay/Input`：`GameInput.cs`（Session 非所有权输入 Adapter 与玩法事件）、`InputSystem_Actions.cs`（由输入资产自动生成的 C# 包装类）
- `Gameplay/Interaction`：`WorldInteractionTarget.cs`、`PlayerInteractionController.cs`、`GameplayPauseSystem.cs` 与 `Commands/`，负责最近世界目标、情境菜单请求和集中暂停
- `Gameplay/Events/GameplayEvents.cs`：金币、背包、装备、打造、交易、交互焦点、菜单请求、暂停和 Actor 注册 / 注销领域事件
- `Gameplay/UI/GetHudSnapshotQuery.cs`、`HudController.cs`：生命、金币和最终有效属性的只读 HUD 快照与事件驱动控制器
- `Gameplay/UI/GetInventorySnapshotQuery.cs`、`InventoryPanelController.cs`：只读背包快照、共享 10×6 格子、四槽装备、拖拽与拿起 / 放置控制器
- `Gameplay/UI/ItemTooltipView.cs`、`ItemDragQueries.cs`、`MerchantGridLayout.cs`：唯一物品浮窗、拖拽目标只读查询和商人确定性虚拟格子排布
- `Gameplay/UI/GetShopSnapshotQuery.cs`、`ShopPanelController.cs`：只读商店快照、商人格子与共享玩家背包的买卖交互控制器
- `Gameplay/UI/GetCraftingSnapshotQuery.cs`、`CraftingPanelController.cs`：复用共享玩家背包选择，提供稀有度、任意 / 前缀 / 后缀范围和 14 个随机打造变体，不保留具体词条选择
- `Gameplay/UI/GameMenuAccess.cs`、`GameMenuController.cs`：背包 / 商店 / 打造情境访问范围、共享菜单遮罩、`auto` 保存、返回前台、关闭和 Gameplay/UI 输入路由
- `Gameplay/UI/InteractionPromptController.cs`：显示当前世界交互目标，并在 UI 模式或目标失效时隐藏
- `Tests/EditMode/ApplicationLifecycleTests.cs`、`ArchitectureExceptionSafetyTests.cs`、`AbandonedGenerationIsolationTests.cs`、`PrefabAssetLoaderTests.cs`：覆盖作用域停止 / 超时、活动任务跟踪、后代 `Abandoned` 污点传播、旧 generation 隔离、架构初始化与反初始化异常安全、Prefab Addressables GUID 单飞和精确句柄
- `Tests/EditMode/InfrastructurePolicyTests.cs` 与 `InfrastructurePolicyExceptions.json`：扫描架构直连、未登记异步、直接文件 IO / PlayerPrefs / 场景加载，并用正反向夹具验证规则
- `Tests/EditMode/ContentIdentityTests.cs`、`InstanceIdentityAndDtoTests.cs`、`MigrationPipelineTests.cs`：覆盖内容目录完整性、强类型实例 ID、DTO 纯度与对象图 round-trip、迁移链和失败输入不变
- `Tests/EditMode/SaveDataContractTests.cs`、`SaveSerializerTests.cs`、`LocalSaveStorageTests.cs`、`SaveRestorePreparerTests.cs`、`SaveCoordinatorTests.cs`：覆盖完整存档 DTO、校验、SHA-256 / 迁移、临时写和两代文件、损坏回退、内容预检、请求合并、Flush 超时与异常结算
- `Tests/EditMode/Settings*Tests.cs`、`LocalizationServiceTests.cs`、`LocalizationPolicyTests.cs`：覆盖 Settings 合同与存储、Locale 并发切换、六张职责表、正式内容引用、硬编码文本策略和字体字符集
- `Tests/EditMode/Alpha024*Tests.cs` 与 `Tests/PlayMode/Alpha024SceneFlowPlayModeTests.cs`：覆盖 Scene Flow 合同 / 配置、场景与时间策略、Bootstrap 冷启动、NewGame / Continue、取消 / 替代、恢复、暂停、返回和三次循环
- `Tests/EditMode/ApplicationInputServiceTests.cs`、`AudioServiceTests.cs`、`AccessibilityAndPlatformLifecycleTests.cs`、`Alpha025ProductionAssetTests.cs` 与 `Tests/PlayMode/Alpha025InputOwnershipPlayModeTests.cs`：覆盖 Application 输入、重绑定 / Glyph、Audio、Reduce Motion、Platform 生命周期、生产资产和设置页双入口
- `Tests/EditMode/Alpha026DiagnosticsTests.cs`、`Alpha026AddressablesGovernanceTests.cs` 与 `Tests/PlayMode/Alpha024SceneFlowPlayModeTests.cs`：覆盖日志 / 失败 / 玩家错误合同、资源单航班和 owner 释放、15 个 Addressables 条目，以及三轮 Session 资源基线回收
- `Tests/EditMode/Alpha027Acceptance*`、`MigrationFixture*` 与 `Tests/Fixtures/Migration/`：冻结 0.2.7 版本、覆盖清单、构建记录和 Save / Settings V0 迁移样本
- `Tests/PlayMode/Alpha027*`：覆盖每次 Test Runner 隔离数据根、完整删除 / 重置 / 重启路径，以及三语言 × 三分辨率 Shell 矩阵
- `Tests/EditMode/Alpha01TagMigrationCharacterizationTests.cs` 与既有 EditMode 测试：覆盖标签查询、域隔离、物品—词条候选矩阵、伤害血统、旧接口兼容、纯逻辑、原子换装、交易和打造事务语义、键鼠 / 手柄输入、Action Map 切换、交互消息、暂停及 HUD/背包/商店/打造快照。归属 `DarkFlare.Tests.EditMode` 程序集
- `Tests/PlayMode/ApplicationLifecyclePlayModeTests.cs`：覆盖冷启动唯一性、取消回滚、并发请求、连续 Session、场景卸载和直接 `Main` 重载
- `Tests/PlayMode/ApplicationHostSceneTransitionPlayModeTests.cs`：覆盖每 generation 一次 `SessionRunning` 通知、回滚中的重载、三次快速请求 latest-wins、挂起回滚 / 作用域超时有界收敛，以及受控停止 / Shutdown / Emergency 终态隔离
- `Tests/PlayMode/SceneSessionComponentBindingPlayModeTests.cs`：覆盖 `Main → Main`、无 Provider 空场景进入 `Main`、延迟绑定三态、绑定异常回滚，以及 poisoned Scene 拒绝重绑 / 重试
- `Tests/PlayMode/SessionObjectRegistryOwnershipPlayModeTests.cs`：覆盖旧对象延迟销毁只注销其原始 Registry，不影响新 Session
- `Tests/PlayMode/SaveCaptureRestorePlayModeTests.cs`：覆盖 Main 完整快照 / Restore、保存 → Continue → NewGame → Continue 旧档保留和菜单操作入口

`UI`、`Utilities` 目前主要是占位，为后续模块扩展预留。

**首版玩法循环和 `alpha 0.2.0–0.2.7` 基础设施已完成**：当前已覆盖 UIToolkit HUD / 背包装备 / 商店 / 打造 / 场景交互，以及唯一应用宿主、Session 重建、稳定身份、内容目录、DTO / 迁移、`auto` 本地存档 / Restore / 删除、Settings V1 / 完整默认恢复、中英运行时本地化、Bootstrap / Main Scene Flow、Game Time、Application Shell、应用级输入 / 重绑定 / Glyph、Audio、Reduce Motion、Platform Lifecycle、结构化日志、玩家错误和 Addressables Resource Service。运行流程见 [`gameplay-loop.md`](gameplay-loop.md)，长期规则见[alpha 0.2 长期运行时契约](infrastructure/alpha-0.2-runtime-contract.md)，最终证据见[alpha 0.2 综合验收记录](infrastructure/alpha-0.2-acceptance.md)，完成过程保存在 [`plan/archive/`](plan/archive/README.md)。

### `Assets/Settings`

项目资源级设置目录。当前可见内容主要用于：

- 输入系统配置（`InputSystem_Actions.inputactions`，含 `Keyboard&Mouse` 与 `Gamepad` 两套控制方案；现已作为唯一输入源并自动生成 C# 包装类）
- UIToolkit 面板与文本配置（`UI/GamePanelSettings.asset`、`UI/GamePanelTextSettings.asset`，参考分辨率 1920×1080）
- UI 字体链（动态 `UI/Fonts/GameCjkFont.asset` → `GameLatinFont.asset`）
- Application Audio 配置（`Audio/AudioServiceConfiguration.asset`、`Audio/DarkFlareAudioMixer.mixer` 与 `Audio/UI/ui_confirm.wav`）
- URP 配置
- 场景相关设置资源

界面结构与样式位于 `Assets/UI/`；`ItemWorkbench.uxml/.uss` 定义共享装备与玩家背包，`GameRoot.uxml` 提供顶层 tooltip / drag overlay。Unity 默认运行时主题位于 `Assets/UI Toolkit/UnityThemes/`。

### `Assets/TextMesh Pro`

TextMesh Pro 默认资源与字体目录。当前 `QiushuiShotai SDF` 回退到 `LiberationSans SDF`；秋水书体的 OFL 与来源说明和字体源同目录保存。

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
- 代码架构入口和唯一生命周期所有者已经就位
- 战斗、掉落、背包、交易、打造、输入、HUD、情境菜单与世界交互入口已有首版可运行内容
- `auto` 本地存档与 Restore、Settings V1、运行时本地化、完整 SceneFlow、Game Time、应用 UI 外壳、重绑定、音频、可访问性、平台生命周期、结构化日志和资源治理均已实现；`alpha 0.2.7` 只处理封板缺口、综合验收与计划归档，手动槽位和云同步不属于本版本

因此，后续工作重点不在“再拆目录”，而在把每一层真正填上首批可运行内容。
