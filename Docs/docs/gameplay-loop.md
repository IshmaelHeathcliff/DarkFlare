# 首版玩法循环

## 当前状态

`Main.unity` 已串联首版单场景循环：

```text
生成玩家 -> 战斗 -> 怪物死亡 -> 掉落 -> 自动拾取
-> 整理 / 装备 -> 交易 / 打造 -> 返回战斗
```

玩家可在场景内持续战斗和获取物品，通过 Tab / 手柄 Start 打开随身背包，也可接近商人或打造台后使用 E / 手柄北键进入对应功能。菜单打开时暂停玩法模拟，关闭后恢复战斗与 Gameplay 输入。

当前成果用于验证战斗、物品、经济与构筑之间的闭环，仍是单场景功能原型。装备已扩展为武器、护甲、左戒指和右戒指四槽，正式内容池包含七件装备、25 个物品词条、10 个怪物词条和三种怪物；地图、商人、打造台、三种怪物、七件装备与运行时 UI 已完成首版视觉接入。

## 阶段 0 视觉切片

- `Main.unity` 原点周围增加 3×3 冷灰石地切片，不改变碰撞、刷怪和交互点位置。
- 玩家与基础怪物 Prefab 已接入 Idle / Move / Attack / Hit / Death Animator；移动由 Rigidbody2D 驱动，战斗状态由领域 Event 驱动。
- 投射物和世界掉落分别替换为青光飞弹与大剑图标，四个 Prefab 路径及 Addressables 引用保持不变。
- 视觉生产规格与阶段 0 验收记录见 [视觉规范](./visual-style.md) 与 [视觉资产清单](./visual-assets.md)。

## 阶段 0.5 视觉体验修正

- 玩家 `Rigidbody2D` 已启用插值，以物理帧和渲染帧同步为首要方向解决移动模糊；相机继续在 `LateUpdate` 跟随，未引入 Cinemachine 或 Pixel Perfect Camera。
- `Main.unity` 的视觉地表扩展为 5×5，并新增闭合 `WorldBounds`。当前已在保持覆盖范围不变的前提下迁移为基础 / 细节双层 Tilemap；玩家 / 怪物碰撞、相机中心限制和刷怪合法范围仍使用同一边界来源。
- `MonsterSpawner` 对范围外候选点重新采样，失败时回退到边界内最近点，生成间隔、半径和最大数量保持不变。
- 世界掉落由独立 `LootPickupVisual` 绑定图标、投影、稀有度框、中文标签和悬浮呼吸动画；拾取成功、背包满保留和 Addressables Prefab 路径不变。

## 启动与运行流程

1. `CombatPrototypeBootstrap` 先配置并记录随机根种子，再预热玩家、怪物、投射物、掉落物 Addressable Prefab 和七件装备图标，初始化商人、打造配置与玩家初始金币。
2. `SpawnSystem` 生成玩家，`MonsterSpawner` 使用独立位置与实例种子持续生成怪物；相机随后绑定玩家。
3. 玩家和怪物统一注册到 `CombatModel`，攻击通过 Command 进入 `CombatSystem` 和 `DamageCalculator`；投射物生成或怪物接触攻击成功后发送 `ActorAttackedEvent`，伤害、死亡与复活沿用 `ActorDamagedEvent`、`ActorDiedEvent`、`ActorRevivedEvent` 驱动 Animator。生命 / 法力变化另由统一资源事件驱动 HUD 与调试。
4. 怪物死亡后，`LootSystem` 先执行表级掉落概率，成功后才按条目权重生成 `ItemInstance` 并实例化世界掉落物。
5. 玩家触碰掉落物时，`PickupLootCommand` 尝试把物品放入 10×6 背包；背包无空间时保留世界掉落物。
6. 玩家可在背包内选择物品和目标槽并发送 `EquipItemCommand`，也可通过 `UnequipItemCommand` 卸下。`EquipmentSystem` 原子提交背包与四槽状态，并从完整 Loadout 重建角色装备效果。
7. 玩家接近商人或打造台后，可在对应菜单上下文中买卖、打造和装备；关闭菜单后继续战斗，验证金币、物品、词条和伤害变化。

## 阶段 1 UX 快速改进

- 背包、商店和打造使用同一物品详情快照与中文格式化，统一展示基础伤害、隐式、前缀和后缀；打造不再显示 `damage` 等内部属性 ID。
- 鼠标悬停 / 手柄焦点用于临时预览，点击 / Submit 固定选择；详情离开临时预览后恢复固定物品。
- 商店交易后保留原来源列，并按原索引选择下一件或上一件；固定 10×6 商人背包不使用滚动条，焦点和交易反馈可跨刷新、页签往返及关闭重开恢复。
- 阶段 1 当时仍保持单武器槽与既有战斗数值语义；这些边界已在阶段 2 被四槽装备与武器伤害来源替换。

## 阶段 2 装备槽与伤害来源

- `EquipmentModel` 已改为每个 Actor 持有完整四槽 Loadout；物品通过序列化槽位掩码声明可装备位置。
- 装备、替换和卸下均由 `EquipmentSystem` 先预检背包空间，再一次性提交背包、槽位和 Actor 效果，失败时不改变任何状态或发送成功事件。
- 多件装备效果从四槽集中重建；武器 `LocalItem` 只处理本地基础伤害，护甲、抗性和其他角色属性通过统一聚合进入战斗快照。
- 最大生命装备通过 `max_health` 进入 Actor 有效属性；穿脱时保持当前生命比例，HUD 随装备事件刷新当前值与上限。
- HUD 不再显示装备摘要；当前属性窗口直接展示 Actor 聚合后的护甲、闪避、移动速度、暴击率与四类抗性，
  换装后与生命数据一并刷新。
- 基础投射物已配置为武器伤害来源。开局在刷怪前原子装备 20–40 物理基础伤害的大剑；空手时不生成投射物，也不消费玩家攻击种子。
- 投射物在发射时生成 `AttackSnapshot`，冻结来源物品、基础伤害、命中 / 暴击 Roll、标签、攻击者属性和修改器；命中时换装不会追溯改变在途伤害。
- 命中率由攻击者命中与目标闪避计算并裁剪到 `5–95%`；失败区分闪避与自然未命中。暴击使用独立 Roll，并在攻击方缩放后统一作用于全部伤害类型。
- `DamageResolvedEvent` 覆盖命中、未命中、闪避与无伤害；只有实际正数生命损失才发送 `ActorDamagedEvent` 并触发受击表现。
- 背包页已接入四槽按钮、显式左右戒指选槽、安全比较、替换和卸下，并通过键鼠、手柄与 1920×1080 布局自动化验收。

## 阶段 3 随机化与掉落规则

- `GameplayRandomSystem` 统一管理根种子，并把生成位置、怪物实例、玩家攻击、怪物攻击和掉落拆为独立序列。
- `CombatPrototypeBootstrap` 提供固定种子调试开关，默认关闭；根种子在启动时写入日志。
- 基础怪物每个实例按基础生命的 `0.85–1.15` 生成最大生命，实例属性不修改共享 `MonsterDefinition`。
- 玩家初始大剑伤害为 `20–40`，荒原游魂接触伤害为 `6–10`；同一攻击根种子派生独立基础伤害、命中与暴击子流。
- 基础怪物掉落表概率为 `35%`。失败时不创建世界物体，成功后才执行条目权重和物品词条生成。
- 生命、伤害和掉落配置增加倒置范围、负值、概率越界、空池和全零权重校验。

完整规则见 [随机化与掉落规则](./randomization-system.md)。

## 阶段 4 标签、词条与内容池

- 稳定 ID 已统一为小写 `snake_case`，词条目录从 `Afflixes` 迁移为 `Affixes`。
- 正式内容达到 14 标签、25 个可观察生效的物品词条、10 个独立怪物词条、2 武器、2 护甲、3 戒指。
- 荒原游魂、裂爪猎犬和铁壳尸傀使用独立定义、掉落表与 Addressable Prefab，并按 `55 / 30 / 15` 进入现有刷怪池。
- 商人库存覆盖七件普通装备，打造与三张掉落表覆盖 25 个物品词条；掉落装备生成一前缀一后缀。三份正式怪物定义各自显式引用完整 10 词条池。
- 配置中心新增内容校验页，检查 ID、范围、兼容、池覆盖、Prefab 和 Addressables。

完整内容清单见 [首批内容池](./content-system.md)。

## 阶段 5 批量视觉接入与表现反馈

- 裂爪猎犬和铁壳尸傀已接入独立 Sprite、Animation Clip、Animator Controller 与 Addressable Prefab；三种怪物可直接依靠轮廓和体量区分。
- 七件正式装备使用独立 Addressable 图标，世界掉落、背包、四个装备槽、商店、打造和详情从同一物品基底解析视觉；HUD 不再消费装备图标。
- `SpriteAssetLoader` 统一负责图标去重预热、缓存、取消清理与释放，`GameArchitecture.Deinit()` 与 Prefab Loader 一并释放资源。
- 地图在 5×5 基础 Tilemap、稀疏细节 Tilemap 和 `WorldBounds` 上布置营地、路径、边界装饰与火盆，不改变生成、碰撞、交互或 AI 规则。
- 商人与打造台改为独立 Prefab，通过 `WorldInteractionVisual` 响应既有焦点消息；战斗表现包含伤害数字、怪物血条、死亡淡出，以及独立的投射物飞行、命中、普通受击和暴击受击多帧动画，旧命中闪白与静态 Sprite 缩放冲击已移除。
- `Theme.uss` 统一 HUD 与三个菜单的图标、稀有度、按钮和焦点状态，继续复用唯一 `UIDocument` 和 `EventSystem`。

完整规格、资产与验收截图见 [视觉规范](./visual-style.md) 和 [视觉资产清单](./visual-assets.md)。

## alpha 0.1.3 碰撞与怪群安全

- 玩家、怪物和世界阻挡分别使用 `PlayerActor`、`MonsterActor`、`WorldObstacle` Layer。玩家—怪物、怪物—怪物和玩家—玩家不产生 2D 实体接触；两类 Actor 仍与世界阻挡及 `Default` 层交互。
- `Player.prefab` 与三个怪物 Prefab 的整个层级已迁移到对应 Actor Layer；`Main.unity/WorldBounds` 是 `WorldObstacle` 上的闭合非 Trigger `EdgeCollider2D`。
- 怪物移动在 `0.6` 距离内停止追逐，并使用半径 `0.8`、权重 `0.65` 的同阵营软分离。完全重叠时由怪物实例种子产生稳定退让方向，合成速度不会超过配置移速。
- 接触伤害仍由中心距离与每只怪物自己的攻击间隔驱动，不依赖碰撞回调。三种怪物分别以 `0.75 / 0.55 / 1.0` 秒独立尝试；没有受击无敌帧、玩家全局伤害冷却或同帧合并。
- 永久配置校验覆盖 Layer、Physics2D Matrix、Actor Prefab、`WorldBounds` 和怪物移动参数；精准迁移复跑后目标资产哈希保持不变。

## alpha 0.1.4 法力、耗蓝与恢复

- `CombatActor` 正式保存当前生命和当前法力，上限分别读取 `max_health` 与 `mana`；出生和复活恢复满值，上限变化时保持当前比例。
- 玩家基线为 `100` 最大法力、每秒 `1` 生命恢复和每秒 `5` 法力恢复。基础投射物每次成功生成消耗 `8` 点法力；伤害来源无效或法力不足时不生成投射物、不发送攻击事件，也不消费玩家攻击随机种子。
- `ResourceRegenerationSystem` 以单一可取消 UniTask 每 `0.25` 秒推进存活且启用的 Actor。菜单暂停、死亡、禁用和注销期间不恢复，恢复游戏后不补算暂停时间。
- 伤害、主动治疗、被动恢复、法力消耗 / 恢复、上限变化和复活都发送带原因的统一资源事件。被动生命恢复不复用主动治疗事件，避免持续产生治疗飘字。
- HUD 在生命条下显示当前 / 最大法力；法力不足时给出所需数值，下次成功释放时清除。背包属性详情按稳定顺序显示全部 23 项属性。

## alpha 0.1.5 全属性与怪物词条

- 25 个正式物品词条覆盖 `StatIds.All` 的 23 项公开属性；力量、敏捷和智力统一在直接修改器聚合后派生生命、命中 / 闪避和法力。
- 三种怪物按实例根种子从 10 项独立怪物词条中生成 `0–2` 条。生命、数量、选择和逐词条数值使用互不扰动的确定性子流。
- 防御、生命和移速词条由 `CombatActor.Stats` 消费；元素附伤由接触攻击快照携带，不在 `MonsterController` 中添加专用伤害分支。
- 带词条怪物默认显示最多两行配置颜色的世界名称；零词条、死亡或禁用时隐藏，受伤后生命条仍独立工作。
- `SpawnSystem` 记录实例根种子、词条 ID / 掷值和最终关键属性，便于固定种子复现。

## 模块边界

| 模块 | 主要入口 | 当前职责 |
| --- | --- | --- |
| 启动与生成 | `CombatPrototypeBootstrap`、`SpawnSystem`、`MonsterSpawner` | 预热资源、初始化配置、生成玩家 / 怪物 / 投射物 |
| 战斗 | `CombatModel`、`CombatSystem`、`DamageCalculator`、`AttackSnapshotFactory` | Actor 注册、攻击快照、伤害结算与生死状态 |
| 资源 | `CombatActor`、`CombatSystem`、`ResourceRegenerationSystem` | 当前生命 / 法力、资源变更事件、技能耗蓝与统一恢复节拍 |
| 怪物移动与碰撞 | `MonsterController`、`MonsterSteeringCalculator`、`GameplayPhysicsLayers` | 接近停止、同阵营软分离、角色与世界的 Layer 碰撞合同 |
| 随机化 | `GameplayRandomSystem`、`MonsterInstanceRandomSeeds`、`MonsterAffixGenerator` | 根种子、独立通道、怪物实例生命 / 词条与可复现调试 |
| 掉落与物品 | `LootSystem`、`LootTableDefinition`、`ItemGenerator` | 死亡掉落、物品实例生成和世界掉落物创建 |
| 背包与装备 | `InventoryModel`、`InventoryGrid`、`EquipmentModel`、`EquipmentSystem` | 10×6 格子占用、四槽穿戴、原子替换 / 卸下和装备效果聚合 |
| 交易 | `EconomyModel`、`TradingSystem`、`ItemValueCalculator` | 单商人库存、买卖价格与事务提交 |
| 打造 | `CraftingSystem`、`CraftingOperations` | 四种词条操作、金币成本、失败回滚与完成事件 |
| 输入与 UI | `GameInput`、`GameMenuController`、各面板 Controller | 键鼠 / 手柄输入、HUD、背包、商店和打造交互 |
| 世界交互 | `PlayerInteractionController`、`WorldInteractionTarget`、`GameplayPauseSystem` | 最近目标选择、情境菜单请求、交互提示与菜单暂停 |
| 视觉表现 | `SpriteAssetLoader`、`ItemVisualPresenter`、`ActorVisualFeedbackController`、`VisualEffectPool`、`MonsterAffixVisual`、`WorldInteractionVisual` | 动态图标、多帧战斗反馈、有界特效回收、怪物词条辨识、交互高亮和临时表现清理 |

各场景 Controller 实现 `IController`，通过 Command 修改 Model / System，通过 Query 获取只读快照，并注册 Event 刷新表现。Controller 不直接修改 Model，也不直接发送领域 Event。

## 场景与配置

- `Assets/Scenes/Main.unity`：唯一构建场景，包含战斗启动器、刷怪器、`WorldObstacle` 层的 `WorldBounds`、`GroundGrid` 下的 5×5 基础 Tilemap 与稀疏细节 Tilemap、营地与边界装饰、`UIRoot`、唯一 `EventSystem`、商人与打造台 Prefab。
- `Assets/Data/Preset/Actors/玩家.asset`：玩家属性与 Prefab 引用。
- `Assets/Data/Preset/Skills/基础投射物技能.asset`：首版投射物技能。
- `Assets/Data/Preset/Stats/`：与 `StatIds.All` 一一对应的 23 份属性定义。
- `Assets/Data/Preset/Monsters/`：三种怪物定义与 `基础刷怪表.asset`。
- `Assets/Data/Preset/MonsterAffixes/`：10 个独立怪物词条定义。
- `Assets/Data/Preset/Loot/`：三张怪物掉落表、七件装备条目与完整词条池。
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
- 阶段 2 后续修正验证为 EditMode 75/75 通过；PlayMode 9 项中 7 项通过、2 项为包内既有忽略测试。四槽键鼠 / 手柄操作、最大生命与 HUD 同步、属性资产一致性、三档分辨率和在途投射物换装快照通过，Runtime / Editor 与测试程序集编译无错误。
- 阶段 3 全量 EditMode 82/82 通过；PlayMode 11 项中 9 项通过、2 项为项目既有忽略测试。固定种子 `24681357` 连续运行两次 Main 时，前三只怪物生命、玩家 / 怪物攻击和掉落序列完全一致；关闭固定种子后两次启动根种子正常变化。Runtime / Editor 与测试程序集编译 0 警告、0 错误。
- 阶段 4 全量 EditMode 87/87 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游用例按原标记忽略、0 失败。正式装备交易 / 打造 / 四槽流程、固定种子前 12 只怪物双次重放和三个怪物 Addressables 预热通过，四个项目程序集编译无警告和错误。
- 阶段 5 全量 EditMode 92/92 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游用例按原标记跳过、0 失败。Core、Runtime、Editor、EditMode 与 PlayMode 五个项目程序集编译通过；七件装备图标预热、三种怪物 Animator、战斗反馈与三档 UI 渲染通过，最终 Play Console 为 0 错误、0 警告。
- 阶段 6 全量 EditMode 98/98 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游既有用例按标记跳过、0 失败。五个项目程序集顺序编译均为 0 警告、0 错误，Unity Console 为 0 错误、0 警告。Main 重复进入前后 Addressables 缓存稳定为 6 个 Prefab 与 7 个 Sprite，停止战斗发射源后临时伤害数字、冲击和投射物均清零。
- 双层地表专项验收为 EditMode 19/19、PlayMode 1/1 通过；基础层 25 格、细节层 11 格、两层无 Collider，视觉覆盖继续保持 `-20..20`，`WorldBounds` 保持 `-16..16`。
- alpha 0.1.7 阶段 C 已将 23 张独立特效帧接入正式投射物与四类战斗 Actor；EditMode 运行时合同 15/15、对象池 PlayMode 合同 2/2 通过，每类特效预热 4 个且活动上限 24。Main 战斗冒烟确认实例可回收，退出后池根与实例均为 0，Console 为 0 错误。
- alpha 0.1.7 阶段 D 已将地表、世界对象、世界特效与战斗信息拆入四个正式 Sorting Layer；七个 Prefab 和 Main 八个场景物件使用单一整体边界与接地点排序。Main 冒烟中 14 个活动对象的稳定 ID / Order 均唯一，同点与上下交叉顺序稳定，详见[世界渲染与稳定层级](./world-rendering.md)。
- alpha 0.1.7 综合验收为 EditMode 208/208；PlayMode 21 项通过、0 失败，另有 2 项 Input System 上游已知忽略测试。正式 Main 中 27 个活动对象的 StableSortId / Order 均唯一，排序稳态 1024 次 Tick 为 0 B managed allocation；特效池峰值后全部回收，退出 PlayMode 不再残留池根。
- alpha 0.1.3 全量 EditMode 148/148 通过；PlayMode 17 项中 15 项通过、2 项 Input System 上游既有用例按标记跳过、0 失败。12 只怪物四方向脱困、WorldObstacle 阻挡、Default Trigger 与迁移幂等性专项均通过。
- alpha 0.1.4 专项 EditMode 6/6、Main PlayMode 1/1 通过；全量 EditMode 156/156 通过，PlayMode 18 项中 16 项通过、2 项 Input System 上游既有用例按标记跳过、0 失败。四个相关程序集构建零错误，三档 UI 布局回归通过，真实 Main 停止后的 Console 为零错误。
- alpha 0.1.5 主属性专项 6/6、怪物词条纯领域专项 6/6、正式内容专项 13/13、怪物词条表现 PlayMode 2/2 通过；全量 EditMode 176/176 通过，PlayMode 20 项中 18 项通过、2 项 Input System 上游既有用例按标记跳过、0 失败。Main 固定种子 `24681357` 的前 12 个怪物、词条、攻击与掉落双次重放一致，四个相关程序集构建零错误。

以上数据是首版收尾时的验证记录；后续改动仍应重新运行相关测试和 Play 流程。

## 当前边界

- 仅有 `Main.unity` 单场景，没有安全区、撤离、场景切换或存档闭环。
- 背包已实现矩形格子占用、物品与装备栏之间的拖拽整理；当前仍没有物品旋转、堆叠和重量。
- 装备已实现武器、护甲和双戒指槽，以及替换和卸下；首批七件装备和 25 个物品词条已接入，但尚无耐久、套装、纸娃娃和词条等级段。
- 交易只维护单个共享商人库存；卖出物品不进入商人库存，也没有回购。
- 打造只消耗金币，尚无配方、材料、锁定词缀或批量操作。
- 首版视觉已覆盖地图、玩家、三种怪物、投射物、掉落、七件装备、商人、打造台、HUD 与三个菜单；`PrototypeSquare` 仅保留为调试回退，不再表达主要可玩对象。当前仍不包含音效、音乐、纸娃娃或镜头震动。

输入和 UI 结构见 [输入与运行时 UI](./input-ui-system.md)，装备事务见 [装备系统](./equipment-system.md)，打造事务规则见 [打造系统](./crafting-system.md)，伤害与词条规则见 [伤害系统与词条系统设计](./damage-affix-system.md)。
