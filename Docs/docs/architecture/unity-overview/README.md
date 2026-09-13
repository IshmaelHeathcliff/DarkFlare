# Unity 高层架构

基于 2026-09-13 本地代码分析，基线提交 `3cc0ced8b25237d117406ba38ec6f8d933476ce8`。本图描述当前实现，不包含规划中的主动使用物品与消耗品功能。

- [交互架构图](./darkflare.html)：支持缩放、搜索、关系追踪、主题切换与导出。
- [可维护的图源](./darkflare.architecture.json)
- [交付校验与 SHA-256 回执](./delivery-receipt.json)
- [浏览器检查回执](./darkflare.visual-check.json)
- [明暗主题截图](./darkflare.visual-check.html)
- [装备、词条与伤害作用流程](../equipment-affix-damage/README.md)

## 阅读方式

主链从玩家、场景与 UI，经 QFramework 的 Command / Query 接口进入会话内玩法逻辑，读写状态，再通过领域事件刷新表现。图中 Session 调度是组合与接口层的合并表达，并非额外中间件；Query 可直接读取 Model，Command 可访问 System / Model，不要求每次查询都经过 System。

节点是职责分组，不等于独立程序集或服务。图例的“后端”指本地运行时逻辑，“数据库”包含内存状态、配置和本地文件，“消息总线”指 QFramework 进程内事件。外部依赖是引擎与包依赖，不表示远端服务。实线按标签表示调用、生命周期或数据流，虚线指向所依赖的外部包。

## 模块与代码依据

以下路径相对项目根目录。

| 模块 | 现状与主要依据 |
| --- | --- |
| 应用宿主与场景流 | `Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationHost.cs` 创建应用服务、协调 Profile / Session；`Infrastructure/Flow/SceneFlowService.cs` 管理场景事务。Bootstrap 常驻，Main additive 加载。 |
| Session 与框架 | `Assets/Scripts/Runtime/GameArchitecture.cs` 注册 Model、System 和资源 Utility；`Infrastructure/Lifecycle/GameSessionHost.cs` 持有会话架构租约；`Runtime/Core/QFramework.cs` 提供架构接口。 |
| Controller 与 UI | `Assets/Scripts/Runtime/Gameplay/UI/InventoryPanelController.cs` 通过 Query 获取快照、Command 执行装备动作、注册事件刷新；`Gameplay/UI/HudController.cs` 通过 PrimeTween 更新表现。 |
| 战斗、状态与资源恢复 | `Assets/Scripts/Runtime/Gameplay/Combat/CombatSystem.cs` 接入 StatusSystem 并发送伤害、死亡事件；组合根另注册 ResourceRegenerationSystem、PlayerSkillStateRegistry 和 SpawnSystem。 |
| 物品与经济 | `Assets/Scripts/Runtime/Gameplay/Combat/LootSystem.cs` 订阅死亡事件并使用独立随机频道生成掉落；`Gameplay/Trading/TradingSystem.cs` 访问 InventoryModel / EconomyModel；`Gameplay/Crafting/CraftingSystem.cs` 处理材料、背包与随机打造。EquipmentSystem 管理装备逻辑。 |
| 运行时数据 | 组合根注册 CombatModel、EquipmentModel、InventoryModel、EconomyModel、StatusModel。状态同时存在于 Actor、装备、物品等领域对象中，并非所有数据都集中在 Model。 |
| 内容配置 | `Assets/Scripts/Runtime/Infrastructure/Content/ContentCatalog.cs` 提供内容查询；`Infrastructure/Lifecycle/GameSessionHost.cs` 将 ContentCatalog 放入初始化上下文；`Runtime/Data/` 保存配置类型。 |
| 存档与恢复 | `Assets/Scripts/Runtime/Infrastructure/Persistence/SessionSnapshotSource.cs` 捕获背包、装备、商人、角色、世界和随机状态；SaveCoordinator、LocalSaveStorage 与 RestoreGameSessionInitializer 负责写入、回退及恢复。图中快照/恢复是两向过程的概括，**不表示所有会话状态都已持久化，Status 自动计时与存档仍属后续阶段**。 |
| 资源与表现 | `Assets/Scripts/Runtime/Infrastructure/Resources/ApplicationResources.cs` 统一资源后端与租约；`GameArchitecture.cs` 注入 PrefabAssetLoader / SpriteAssetLoader，并注册 VisualEffectPool / WorldSortingSystem。 |
| 外部依赖 | `Packages/manifest.json` 与 `Assets/Scripts/Runtime/DarkFlare.Runtime.asmdef` 确认 UniTask、PrimeTween、InputSystem、Addressables、Localization、Newtonsoft.Json 等；Odin 位于 `Assets/Plugins/Sirenix/`。 |

应用宿主还统一管理设置、本地化、音频、时间、可访问性、平台生命周期与日志。为了保持高层图可读性，这些职责合并在应用节点与说明卡中；开发侧 Odin、Unity MCP / pipeline 和测试工具不属于玩家运行时业务链。

程序集依赖为 `Editor / Tests → Runtime → Core`。Runtime 当前还显式引用 `PrimeTween.Runtime` 和 `Unity.TextMeshPro`，以实际 asmdef 为准。

## 验收

- 类型：`architecture`；Showcase 9/9，0 错误、0 警告。
- `browser_evidence: passed`：1440×900、1600×1000、1920×1080、2048×1320 均无横向或纵向溢出。
- `visual_review: passed`：人工图像复核 2048×1320 浅色、1440×900 深色截图；节点、卡片与主链完整。小屏副标题较细，可用查看器放大。
- `correction_rounds: 0`：交付后的视觉复核未修改图源；生成阶段已按诊断修正布局。
- 规格 SHA-256：`cd99cca800af8aa7f5cab3eb70e1519c7a2130e08561ccae64eeb1a3727590e3`。
- HTML SHA-256：`5a6b6ad31a03bab7d542fe19f46ff5977417e3b3471dc9c179712ba8cd403b79`。

仅生成文档产物，没有改动运行时代码或 Unity 场景；未运行游戏测试。
