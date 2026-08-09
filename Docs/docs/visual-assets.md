# 视觉资产清单

## 当前状态

- 状态：阶段 5 美术返工与阶段 6 最终视觉复验均已完成；alpha 单图生产规范已生效
- 目标：以统一的暗色奇幻视觉覆盖世界、三种怪物、七件装备、交互物、战斗反馈与运行时 UI，同时保持既有玩法事务不变。
- 视觉规范：[视觉规范](./visual-style.md)
- 生产规范：[美术资产生成规范](./visual-asset-generation.md)
- 执行计划：[阶段 5 美术资产规范收缩与返工（已归档）](./plan/archive/initial-experience-optimization/phase-5-visual-asset-correction-plan.md)

下表中的 Sheet、人工 Rect 和 sub-sprite 引用是初版已接入资产的现状记录，不再作为后续生产模板。alpha 阶段新增或重新生成的资产统一使用一图一 Sprite、逐图审计和 `Sprite Mode: Single`；既有 Sheet 只在独立迁移计划中替换。

## 资源清单

| 资产 | 规格 | 状态 | 目标路径 | 用途 |
| --- | --- | --- | --- | --- |
| 风格板 | 1254×1254 参考图 | 已完成 | `Docs/docs/assets/visual-style/phase-0-style-board.png` | 统一视觉语言，不直接入游戏 |
| 地表切片 | 512×512，64 PPU | 已接入 | `Assets/Art/Sprites/Environment/VisualSlice/ground_slice.png` | `Main.unity` 原点周围 3×3 地表 |
| 玩家动画 | 1448×1086 Sheet；12 个子 Sprite | 已迁移 | `Assets/Art/SpriteSheets/Phase5/player_sheet.png` | 玩家 Idle / Move / Attack / Hit / Death Animator |
| 基础怪物动画 | 1448×1086 Sheet；12 个子 Sprite | 已迁移 | `Assets/Art/SpriteSheets/Phase5/monster_basic_sheet.png` | 荒原游魂运行时 Animator |
| 青光投射物 | 64×64 | 已接入 | `Assets/Art/Sprites/Effects/projectile_arcane.png` | 投射物 Prefab，并随飞行方向旋转 |
| 大剑图标 | 96×96，主体最长边 78 px | 已返工 | `Assets/Art/Sprites/Items/Equipment/weapon_greatsword.png` | 与其余六件装备统一画布和主体尺寸 |
| 背包纹理 | 1254×1254 Sheet；4 个紧边界子 Sprite | 已重绑 | `Assets/Art/SpriteSheets/Phase5/ui_frames_sheet.png` | 面板、普通槽和状态槽分别使用安全 Slice |

## 阶段 5 资源

| 资产 | 规格 | 状态 | 目标路径 | 用途 |
| --- | --- | --- | --- | --- |
| 裂爪猎犬 | 1448×1086 Sheet；12 个子 Sprite | 已返工 | `Assets/Art/SpriteSheets/Phase5/monster_swift_sheet.png` | 整图导入、Sprite Editor 自定义 Pivot，Prefab 等比缩放 0.90 |
| 铁壳尸傀 | 1254×1254 Sheet；12 个子 Sprite | 已返工 | `Assets/Art/SpriteSheets/Phase5/monster_heavy_sheet.png` | 整图导入、Sprite Editor 自定义 Pivot，Prefab 等比缩放 0.75 |
| 七件装备图标 | 96×96，主体最长边 72–80 px | 已返工 | `Assets/Art/Sprites/Items/Equipment/` | 共用旧大剑视觉语言并统一视觉重量 |
| 商人 | 1983×793 Sheet；4 个子 Sprite | 已返工 | `Assets/Art/SpriteSheets/Phase5/merchant_idle_sheet.png` | 整图导入、Sprite Editor 自定义 Pivot，Prefab 等比缩放 0.80 |
| 地图与打造台装饰 | 1448×1086 Sheet；12 个紧边界子 Sprite | 已重绑 | `Assets/Art/SpriteSheets/Phase5/world_props_sheet.png` | 火盆与工作台使用人工修正后的完整图形边界 |
| 固定 UI 图标 | 1448×1086 Sheet；12 个紧边界子 Sprite | 已重绑 | `Assets/Art/SpriteSheets/Phase5/ui_icons_sheet.png` | USS 静态引用当前子 Sprite，不再生成散图 |

## 接入约束

- 保持玩家、基础怪物、投射物和掉落 Prefab 路径及 Addressable 引用不变。
- 不直接修改 `Main.unity` YAML；场景和 Prefab 改动只通过 Unity MCP 或 Editor 脚本执行。
- 阶段 0 不修改装备槽、掉落概率、怪物属性随机、词条系统和商店状态。
- 透明资产必须验证存在 Alpha 通道、四角透明且无明显色键边缘。
- 新生成源图、家族合同、Manifest 和审计报告保存在对应版本的视觉资产目录；一个生产 PNG 只能包含一个 Sprite、动画帧、UI 状态或世界物件。
- 新资产不得生成或合并 SpriteSheet，不依赖 Sprite Editor 人工切片、Pivot 微调或逐资产缩放；Unity 导入设置通过 Preset、Editor 工具或 Unity MCP 自动完成。
- [阶段 0 视觉生成提示词](./assets/visual-style/generation-prompts.md)仅作为初版历史记录，其中的 Sheet 提示不得复用于 alpha 生产。

## Unity 接入记录

- `Player.prefab`、`Monster_Basic.prefab`：根 Sprite 和全部 Animation Clip 已迁移到各自 4×3 Sheet；Idle / Move 由 Rigidbody2D 速度驱动，Attack / Hit / Death / Revive 由领域 Event 驱动，水平方向通过翻转 Sprite 处理。
- `Player.controller`、`Monster_Basic.controller`：增加 `Attack`、`Hit`、`Death` Trigger 与可达的 Any State 转换；Attack / Hit 播放后按 `Moving` 返回 Idle / Move，Death 保持到销毁或复活。
- 玩家和怪物死亡时立即停止运动并关闭碰撞，但保留 Sprite 播放 Death；怪物延迟 0.65 秒销毁，玩家复活后重置 Animator 并回到 Idle。
- `Projectile_Default.prefab`：替换青光投射物 Sprite；`ProjectileController` 根据飞行方向设置 Transform 朝向。
- `LootPickup.prefab`：替换为大剑 Sprite，保留既有拾取、碰撞和稀有度染色逻辑。
- `Main.unity`：增加 `VisualSliceGround` 和 3×3 地表 Sprite，不增加 Collider，不移动商人、打造台、刷怪器和 UI。
- 背包：`Inventory.uss` 使用面板与三种格子状态纹理；武器按钮增加大剑图标子元素，原选择和装备命令不变。
- 既有四个 Prefab 路径和 Addressables 引用未改变；`PrototypeSquare.png` 仍保留作回退。

## 阶段 0.5 接入记录

- `Player.prefab`：`Rigidbody2D` 插值设为 `Interpolate`。该最小改动已由玩家实机确认显著缓解相机跟随时的人物移动模糊。
- `CameraFollowTarget`：保留 `LateUpdate + SmoothDamp`，增加目标变化时的速度清理和可选世界范围约束；相机中心会扣除当前正交半屏范围。
- `Main.unity`：地表从 3×3 扩展为 5×5；新增约 `(-16, -16)` 到 `(16, 16)` 的闭合 `WorldBounds`，同时绑定相机和 `MonsterSpawner`。
- `MonsterSpawner`：范围内优先重新采样，有限次失败后回退到边界内最近点，不改变生成半径、间隔和最大数量。
- `LootPickup.prefab`：根节点保持稳定缩放与原世界拾取半径；正式视觉由 `Visual/Halo`、`Visual/Icon`、`Visual/Label` 组成，使用金属焦点框、中文名 / 稀有度文本和 PrimeTween 悬浮呼吸表现，不再引用 `PrototypeSquare` 阴影。
- `LootPickupVisual` 只绑定 `ItemInstance` 到表现，不发送物品或背包事件；原自动拾取、背包满保留和销毁事务不变。

## 当前验证

- 31 个最终位图全部通过 RGBA、透明四角和主体覆盖率检查。
- Unity 已完成导入、动画生成、Prefab 保存和 `Main.unity` 保存；安装成功标记之后没有新的编译或引用异常。
- `DarkFlare.Runtime.csproj`、`DarkFlare.Editor.csproj` 编译通过；存在项目既有的 `System.Threading.Tasks.Extensions` 版本冲突警告，无错误。
- 1920×1080 Play 截图已确认玩家、基础怪物、投射物、掉落、地表和 HUD 能共同运行；Play 后最新 900 行 Editor 日志无运行时异常。
- 动画专项 3 项 EditMode 通过：两个 Animator Controller 参数 / 状态 / 转换、战斗 Clip 时长、玩家与怪物死亡显示及怪物延迟销毁配置均满足约束。
- Main 场景动画 PlayMode 通过：实际验证 `Attack → Hit → Death → Revive/Idle` 状态切换，死亡期间 Sprite 保持可见、Collider 关闭，复活后恢复。
- Unity 全量 EditMode 48/48 通过；PlayMode 共执行 6 项、0 失败，其中 4 项通过，Input System 包内 2 项上游不稳定用例按原标记跳过。
- 阶段 0 体验专项 PlayMode 已验证真实购买大剑、背包选中与装备、Tab / Esc、Start / B、默认焦点和暂停恢复。
- 1280×720、1920×1080、2560×1440 三档 Game View 均完成实际切换、边界断言和截图，面板、物品图标、详情、按钮与焦点未越界。

## 阶段 0.5 验证

- 专项 EditMode 4/4 通过：玩家插值、相机正交范围约束、刷怪点范围和掉落表现绑定。
- Unity 全量 EditMode 51/51 通过；项目 PlayMode 3/3 通过，其中阶段 0.5 场景测试验证 5×5 地表、共享边界和三档宽高比下镜头不会越出地表。
- `DarkFlare.Runtime.csproj`、`DarkFlare.Editor.csproj` 编译通过，0 错误；仍只有工程既有的 `System.Threading.Tasks.Extensions` 版本冲突警告。
- 1280×720、1920×1080、2560×1440 原生截图和 1920×1080 边界机位截图均未显示空白背景；多掉落场景中图标和框体仍可分辨。
- Play 运行及截图后 Console 为 0 条 Error。
- `GameArchitecture.Deinit()` 已接入 `PrefabAssetLoader.ReleaseAll()`；全量 PlayMode 复跑未再出现同一 `AssetReference` 重复加载。

## 阶段 5 技术接入与美术复验

- 七件正式装备通过 `ItemBaseDefinition.Icon` 引用独立 Addressable Sprite；`SpriteAssetLoader` 负责去重预热、缓存查询、取消清理和统一释放。
- 世界掉落、背包、四个装备槽、商店、打造、物品详情和 HUD 均从同一物品基底解析图标；缺失图标显示统一剪影，不再固定回退到大剑。
- 裂爪猎犬和铁壳尸傀使用独立 Sprite、Animation Clip、Animator Controller 与 Addressable Prefab，继续遵守 `Moving`、`Attack`、`Hit`、`Death` 参数契约。
- `ActorVisualFeedbackController`、`MonsterHealthBarVisual`、`DamageNumberVisual` 与 `ProjectileImpactVisual` 消费既有战斗事件，提供命中闪白、伤害数字、血条、死亡淡出和投射物冲击，不改变伤害或掉落时序。
- 商人与打造台已改为独立 Prefab，并由 `WorldInteractionVisual` 表达焦点轮廓、提示和名称牌；地图保持 5×5 地表与既有 `WorldBounds`，仅增加营地、路径和边界装饰。
- UI 以 `Theme.uss` 统一深色背景、金铜强调、稀有度、按钮和焦点状态，继续复用唯一 `UIDocument` 与 `EventSystem`。
- 全量 EditMode 92/92 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游用例按原标记跳过、0 失败。Core、Runtime、Editor、EditMode 与 PlayMode 五个项目程序集编译通过。
- 1280×720、1920×1080、2560×1440 三档原生渲染已检查 HUD、背包、商店和打造；最终 Play Console 为 0 错误、0 警告。
- 上述结果证明资源加载、Animator、UI 和反馈链路成立，但不代表最终美术通过。复验发现裂爪猎犬主体仅 56×42 px、铁壳尸傀 75×65 px、商人 67×69 px；非方形源单元被直接压成 96×96，造成比例失真。
- 大剑仍为 64×64，而六件新装备为 96×96 且主体最长边达到 81–84 px，世界与 UI 中的相对尺寸不一致。
- 地表装饰已有层级和坐标，但缺少营地功能组、路径净空、战斗簇与边界语义约束，当前摆放不作为最终验收结果。
- 固定 64×64 UI 图标本体没有触边，但面板和槽位的九宫格切线未按装饰结构分别定义：`ui_inventory_panel` 的 28 px 切线穿过金色角件，焦点 / 禁用槽与普通槽共用 10 px 切线会分割角件或斜切缺口，需按资产重设或拆分装饰层。
- 首轮返工曾将裂爪猎犬、铁壳尸傀和商人按 Alpha 边界等比重切到 96×96；该逐帧输出方式已被后续“整图导入 + Sprite Editor 切片”替代，但对应的等效可见尺寸与 0.80 / 0.90 等比缩放基线继续保留。
- 七件装备已使用旧大剑作为风格参考重新统一为 96×96，主体最长边统一为 78 px；原路径与 GUID 保持不变，Addressables、物品配置和运行时图标引用无需迁移。
- `Main.unity` 的地表覆盖与装饰已按营地核心、南北路径、东西战斗簇和边界角落重排；商人与打造台坐标及全部玩法碰撞保持不变。
- 背包面板、普通槽、焦点槽和禁用槽已改为引用人工修正后的紧边界子 Sprite；运行时继续使用 98、78、157 px 三档安全 Slice。
- 返工专项 EditMode 7/7、全量 EditMode 93/93、全量 PlayMode 8/8 通过；五个项目程序集顺序编译通过，Unity Console 无错误或警告。
- 二次返修将裂爪猎犬、铁壳尸傀、商人和世界物件的透明生成整图直接导入 `Assets/Art/SpriteSheets/Phase5/`，全部使用 `Sprite Mode: Multiple`；Animation Clip、Prefab 和 Main 场景均改为引用可在 Sprite Editor 手动调整的子 Sprite，旧派生逐帧 / 单物件 PNG 已移除。
- 世界物件 Sheet 使用人工修正的紧边界切片；火盆 Rect 为 `(404,72,207,225)`，工作台 Rect 为 `(678,45,359,296)`，完整覆盖实际图形且不再按等分网格截断工作台左侧。
- `WorldInteractionVisual` 已移除焦点缩放 Tween，工作台火光与独立火盆上的 `AmbientPulseVisual` 组件已移除；运行时两次采样确认商人、工作台、火盆和火光缩放保持不变。
- 二次返修专项 EditMode 9/9、全量 EditMode 95/95、全量 PlayMode 8/8 通过。
- 三次返修将玩家、荒原游魂、12 个固定 UI 图标及 4 个面板 / 槽位也迁移为 SpriteSheet；10 个角色 Clip、3 个相关 Prefab 和两份 USS 均已迁移，40 个旧派生散图在零依赖审计后移除。
- 三次返修全量 EditMode 97/97 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游用例按原标记跳过、0 失败；五个项目程序集编译均为 0 警告、0 错误，Unity Console 为 0 错误、0 警告。
- 后续人工调整 UI 与世界物件 Sheet Rect 后，`Theme.uss`、`Inventory.uss`、`CraftingStation.prefab`、`LootPickup.prefab` 和 Main 场景装饰已按新 fileID 重绑；打造台轮廓 / 火焰与掉落光环 / 缺失图标的 5 个失效引用已清零。
- 人工切片重绑后全量 EditMode 98/98 通过；PlayMode 12 项执行完成且 0 失败（其中 2 项 Input System 上游用例沿用跳过标记）；Core、Runtime、Editor、EditMode 与 PlayMode 五个项目程序集均为 0 警告、0 错误，Unity Console 为 0 错误、0 警告。

## 验收截图

- [Editor 场景视图](./assets/visual-style/phase-0-editor-window.png)
- [1920×1080 Play 视图](./assets/visual-style/phase-0-play-game.png)
- [1280×720 背包验收](./assets/visual-style/phase-0-acceptance-inventory-1280x720.png)
- [1920×1080 背包验收](./assets/visual-style/phase-0-acceptance-inventory-1920x1080.png)
- [2560×1440 背包验收](./assets/visual-style/phase-0-acceptance-inventory-2560x1440.png)
- [阶段 0.5 1280×720 世界验收](./assets/visual-style/phase-0.5/after/phase-0.5-world-1280x720.png)
- [阶段 0.5 1920×1080 世界验收](./assets/visual-style/phase-0.5/after/phase-0.5-world-1920x1080.png)
- [阶段 0.5 2560×1440 世界验收](./assets/visual-style/phase-0.5/after/phase-0.5-world-2560x1440.png)
- [阶段 0.5 1920×1080 边界验收](./assets/visual-style/phase-0.5/after/phase-0.5-boundary-1920x1080.png)
- [阶段 5 人工切片重绑后 1280×720 世界 / HUD 验收](./assets/acceptance/phase-5-correction/rebound-world-ui.png)
- [阶段 5 人工切片重绑后 1280×720 背包验收](./assets/acceptance/phase-5-correction/rebound-inventory-ui.png)
- [阶段 6 1920×1080 世界 / HUD 终验](./assets/acceptance/phase-6/world-hud-1920x1080.png)
- [阶段 6 1920×1080 背包终验](./assets/acceptance/phase-6/inventory-1920x1080.png)
- [阶段 6 1920×1080 商店终验](./assets/acceptance/phase-6/shop-1920x1080.png)
- [阶段 6 1920×1080 打造终验](./assets/acceptance/phase-6/crafting-1920x1080.png)

阶段 5 首轮及中间返工截图已在人工切片重绑完成后清理，避免旧切片和旧引用画面继续作为当前验收依据。自阶段 6 起，当前及后续视觉验收只保证 1920×1080；上述四张阶段 6 截图是当前正式验收基线。

## 生成记录

### 风格板

- 模式：内置 imagegen。
- 文件：`Docs/docs/assets/visual-style/phase-0-style-board.png`。
- 提示摘要：2D 俯视 3/4 暗色奇幻垂直切片；冷灰石地、青绿玩家、暖红怪物、金铜交互、青色投射物；低分辨率手绘和像素化硬边；同时展示地图、角色、物品、特效和背包组件；无文字、无标志、无水印。
- 完整提示集：[阶段 0 视觉生成提示词](./assets/visual-style/generation-prompts.md)。
