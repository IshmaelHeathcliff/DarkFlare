# 首版玩法循环

## 当前状态

`Main.unity` 已串联首版单场景循环：

```text
生成玩家 -> 战斗 -> 怪物死亡 -> 掉落 -> 自动拾取
-> 整理 / 装备 -> 交易 / 打造 -> 返回战斗
```

玩家可在场景内持续战斗和获取物品，通过 Tab / 手柄 Start 打开随身背包，也可接近商人或打造台后使用 E / 手柄北键进入对应功能。菜单打开时暂停玩法模拟，关闭后恢复战斗与 Gameplay 输入。

当前成果用于验证战斗、物品、经济与构筑之间的闭环，仍是单场景、单武器槽的功能原型。阶段 0 已接入一组视觉垂直切片，但商人、打造台、完整地图和其余内容仍未进入批量美术生产。

## 阶段 0 视觉切片

- `Main.unity` 原点周围增加 3×3 冷灰石地切片，不改变碰撞、刷怪和交互点位置。
- 玩家与基础怪物 Prefab 已接入 Idle / Move / Attack / Hit / Death Animator；移动由 Rigidbody2D 驱动，战斗状态由领域 Event 驱动。
- 投射物和世界掉落分别替换为青光飞弹与大剑图标，四个 Prefab 路径及 Addressables 引用保持不变。
- 视觉生产规格与阶段 0 验收记录见 [视觉规范](./visual-style.md) 与 [视觉资产清单](./visual-assets.md)。

## 阶段 0.5 视觉体验修正

- 玩家 `Rigidbody2D` 已启用插值，以物理帧和渲染帧同步为首要方向解决移动模糊；相机继续在 `LateUpdate` 跟随，未引入 Cinemachine 或 Pixel Perfect Camera。
- `Main.unity` 的地表扩展为 5×5，并新增闭合 `WorldBounds`。玩家 / 怪物碰撞、相机中心限制和刷怪合法范围使用同一边界来源。
- `MonsterSpawner` 对范围外候选点重新采样，失败时回退到边界内最近点，生成间隔、半径和最大数量保持不变。
- 世界掉落由独立 `LootPickupVisual` 绑定图标、投影、稀有度框、中文标签和悬浮呼吸动画；拾取成功、背包满保留和 Addressables Prefab 路径不变。

## 启动与运行流程

1. `CombatPrototypeBootstrap` 预热玩家、怪物、投射物和掉落物 Addressable Prefab，初始化商人、打造配置与玩家初始金币。
2. `SpawnSystem` 生成玩家，`MonsterSpawner` 按配置持续生成怪物；相机随后绑定玩家。
3. 玩家和怪物统一注册到 `CombatModel`，攻击通过 Command 进入 `CombatSystem` 和 `DamageCalculator`；投射物生成或怪物接触攻击成功后发送 `ActorAttackedEvent`，伤害、死亡与复活沿用 `ActorDamagedEvent`、`ActorDiedEvent`、`ActorRevivedEvent` 驱动 Animator。
4. 怪物死亡后，`LootSystem` 根据怪物掉落表生成 `ItemInstance`，再实例化世界掉落物。
5. 玩家触碰掉落物时，`PickupLootCommand` 尝试把物品放入 10×6 背包；背包无空间时保留世界掉落物。
6. 玩家可在背包内选择武器并发送 `EquipItemCommand`。换装以原子交换提交，成功后装备词条通过 `CombatActor.SetModifiers` 进入后续伤害计算。
7. 玩家接近商人或打造台后，可在对应菜单上下文中买卖、打造和装备；关闭菜单后继续战斗，验证金币、物品、词条和伤害变化。

## 阶段 1 UX 快速改进

- 背包、商店和打造使用同一物品详情快照与中文格式化，统一展示基础伤害、隐式、前缀和后缀；打造不再显示 `damage` 等内部属性 ID。
- 鼠标悬停 / 手柄焦点用于临时预览，点击 / Submit 固定选择；详情离开临时预览后恢复固定物品。
- 商店交易后保留原来源列，并按原索引选择下一件或上一件；滚动、焦点和交易反馈可跨刷新、页签往返及关闭重开恢复。
- 当前仍保持单武器槽与既有战斗数值语义，多槽装备与武器基础伤害接入留到阶段 2。

## 模块边界

| 模块 | 主要入口 | 当前职责 |
| --- | --- | --- |
| 启动与生成 | `CombatPrototypeBootstrap`、`SpawnSystem`、`MonsterSpawner` | 预热资源、初始化配置、生成玩家 / 怪物 / 投射物 |
| 战斗 | `CombatModel`、`CombatSystem`、`DamageCalculator` | Actor 注册、伤害结算、生死状态与装备词条生效 |
| 掉落与物品 | `LootSystem`、`LootTableDefinition`、`ItemGenerator` | 死亡掉落、物品实例生成和世界掉落物创建 |
| 背包与装备 | `InventoryModel`、`InventoryGrid`、`EquipmentModel` | 10×6 格子占用、拾取、金币和单武器槽原子换装 |
| 交易 | `EconomyModel`、`TradingSystem`、`ItemValueCalculator` | 单商人库存、买卖价格与事务提交 |
| 打造 | `CraftingSystem`、`CraftingOperations` | 四种词条操作、金币成本、失败回滚与完成事件 |
| 输入与 UI | `GameInput`、`GameMenuController`、各面板 Controller | 键鼠 / 手柄输入、HUD、背包、商店和打造交互 |
| 世界交互 | `PlayerInteractionController`、`WorldInteractionTarget`、`GameplayPauseSystem` | 最近目标选择、情境菜单请求、交互提示与菜单暂停 |

各场景 Controller 实现 `IController`，通过 Command 修改 Model / System，通过 Query 获取只读快照，并注册 Event 刷新表现。Controller 不直接修改 Model，也不直接发送领域 Event。

## 场景与配置

- `Assets/Scenes/Main.unity`：唯一构建场景，包含战斗启动器、刷怪器、`WorldBounds`、5×5 地表、`UIRoot`、唯一 `EventSystem`、商人与打造台原型对象。
- `Assets/Data/Preset/Actors/玩家.asset`：玩家属性与 Prefab 引用。
- `Assets/Data/Preset/Skills/基础投射物技能.asset`：首版投射物技能。
- `Assets/Data/Preset/Monsters/基础怪物.asset`、`基础刷怪表.asset`：怪物和生成规则。
- `Assets/Data/Preset/Loot/基础怪物掉落表.asset`：掉落物与词条池。
- `Assets/Data/Preset/Traders/基础商人.asset`：商人库存与买卖倍率。
- `Assets/Data/Preset/Crafting/基础打造配置.asset`：打造成本与可用词条池。

Prefab 通过 Addressables 预热和实例化，首版不使用 `Resources` 或运行时临时拼装业务 Prefab。

## 首版验证记录

- Unity EditMode 全量 48 项通过，Unity 脚本编译无错误。
- 键盘 E 与手柄北键均可触发场景交互；Tab / Start 只能打开随身背包。
- Play Mode 已完成“购买大剑（100→85 金币）→添加词缀（85→65 金币）→装备→返回战斗”的端到端流程。
- 同一轮验证中，单次伤害由基础 12 提升至 14.9，证明装备词条已进入战斗结算。
- 已检查 visual tree、1920×1080 渲染和 Play 控制台。
- 动画专项验收已通过：Player / Monster Animator Controller 结构与死亡显示配置 3 项 EditMode 通过，Main 场景 PlayMode 已验证 `Attack → Hit → Death → Revive/Idle` 实际切换。
- 阶段 0 体验专项 PlayMode 已通过：真实购买大剑后，键盘 Tab / Esc 与手柄 Start / B 均可打开、关闭背包并恢复暂停状态；物品选中、详情显示、装备命令和默认焦点正常。
- 1280×720、1920×1080、2560×1440 三档背包渲染与边界检查通过；架构退出时会释放 Addressables 预热句柄，重复初始化不再重复加载同一引用。
- 阶段 0.5 全量 EditMode 51/51、项目 PlayMode 3/3 通过；Runtime / Editor 编译 0 错误，三档世界截图和边界机位均未露出地表外空白。
- 阶段 1 全量 EditMode 60/60 通过；PlayMode 8 项中 6 项通过、2 项为包内既有忽略测试。商店连续状态恢复和商店 / 打造三档分辨率布局边界通过，Unity Console 0 错误。

以上数据是首版收尾时的验证记录；后续改动仍应重新运行相关测试和 Play 流程。

## 当前边界

- 仅有 `Main.unity` 单场景，没有安全区、撤离、场景切换或存档闭环。
- 背包只实现矩形格子占用，没有拖拽换位、旋转、堆叠和重量。
- 装备只实现单武器槽，没有卸装、多槽位或耐久。
- 交易只维护单个共享商人库存；卖出物品不进入商人库存，也没有回购。
- 打造只消耗金币，尚无配方、材料、锁定词缀或批量操作。
- 视觉垂直切片已替换玩家、基础怪物、投射物、掉落、扩展地表和背包皮肤，并建立首版世界边界；商人、打造台、完整地图分区、完整装备池与怪物池仍使用原型内容或占位视觉。

输入和 UI 结构见 [输入与运行时 UI](./input-ui-system.md)，打造事务规则见 [打造系统](./crafting-system.md)，伤害与词条规则见 [伤害系统与词条系统设计](./damage-affix-system.md)。
