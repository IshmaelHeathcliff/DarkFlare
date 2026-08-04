# 视觉资产清单

## 阶段 0 / 0.5 状态

- 状态：阶段 0 和阶段 0.5 已完成并归档，后续进入阶段 1 UX 快速改进
- 目标：用最小资产集替换视觉切片内的占位方块，并验证 Sprite、Animator、UIToolkit 与既有 Addressables Prefab 的协作。
- 视觉规范：[视觉规范](./visual-style.md)
- 执行计划：[阶段 0：视觉垂直切片执行计划](./plan/archive/phase-0-visual-slice-plan.md)

## 资源清单

| 资产 | 规格 | 状态 | 目标路径 | 用途 |
| --- | --- | --- | --- | --- |
| 风格板 | 1254×1254 参考图 | 已完成 | `docs/assets/visual-style/phase-0-style-board.png` | 统一视觉语言，不直接入游戏 |
| 地表切片 | 512×512，64 PPU | 已接入 | `Assets/Art/Sprites/Environment/VisualSlice/ground_slice.png` | `Main.unity` 原点周围 3×3 地表 |
| 玩家 Idle / Move | 96×96 单帧，4+4 帧 | 已接入 | `Assets/Art/Sprites/Characters/Player/` | 玩家运行时 Animator |
| 玩家 Attack / Hit / Death | 96×96 单帧 | 已接入 | `Assets/Art/Sprites/Characters/Player/Preview/` | 玩家运行时战斗 Animator |
| 基础怪物 Idle / Move | 96×96 单帧，4+4 帧 | 已接入 | `Assets/Art/Sprites/Characters/Monsters/Basic/` | 怪物运行时 Animator |
| 怪物 Attack / Hit / Death | 96×96 单帧 | 已接入 | `Assets/Art/Sprites/Characters/Monsters/Basic/Preview/` | 怪物运行时战斗 Animator |
| 青光投射物 | 64×64 | 已接入 | `Assets/Art/Sprites/Effects/projectile_arcane.png` | 投射物 Prefab，并随飞行方向旋转 |
| 大剑图标 | 64×64 | 已接入 | `Assets/Art/Sprites/Items/Equipment/weapon_greatsword.png` | 背包武器图标和世界掉落 |
| 背包纹理 | 256×256 面板、64×64 状态格 | 已接入 | `Assets/Art/Sprites/UI/` | 面板、普通 / 焦点 / 禁用状态 |

## 接入约束

- 保持玩家、基础怪物、投射物和掉落 Prefab 路径及 Addressable 引用不变。
- 不直接修改 `Main.unity` YAML；场景和 Prefab 改动只通过 Unity MCP 或 Editor 脚本执行。
- 阶段 0 不修改装备槽、掉落概率、怪物属性随机、词条系统和商店状态。
- 透明资产必须验证存在 Alpha 通道、四角透明且无明显色键边缘。
- 生成源图保存在 `docs/assets/visual-style/source/`，游戏只引用处理后的最终图。
- 完整提示词见 [阶段 0 视觉生成提示词](./assets/visual-style/generation-prompts.md)。

## Unity 接入记录

- `Player.prefab`、`Monster_Basic.prefab`：替换根 Sprite，增加 Animator 和只读 `ActorAnimatorController`；Idle / Move 由 Rigidbody2D 速度驱动，Attack / Hit / Death / Revive 由领域 Event 驱动，水平方向通过翻转 Sprite 处理。
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
- `LootPickup.prefab`：根节点恢复稳定缩放并保持原世界拾取半径；新增 `Shadow`、`Visual/Halo`、`Visual/Icon`、`Visual/Label`，使用金属焦点框、中文名 / 稀有度文本和 PrimeTween 悬浮呼吸表现。
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

## 生成记录

### 风格板

- 模式：内置 imagegen。
- 文件：`docs/assets/visual-style/phase-0-style-board.png`。
- 提示摘要：2D 俯视 3/4 暗色奇幻垂直切片；冷灰石地、青绿玩家、暖红怪物、金铜交互、青色投射物；低分辨率手绘和像素化硬边；同时展示地图、角色、物品、特效和背包组件；无文字、无标志、无水印。
- 完整提示集：[阶段 0 视觉生成提示词](./assets/visual-style/generation-prompts.md)。
