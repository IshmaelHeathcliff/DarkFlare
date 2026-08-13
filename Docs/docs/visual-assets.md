# 视觉资产清单

## 当前状态

- 状态：alpha 单图迁移、双层地表 Tile、掉落稀有度环与 alpha 0.1.7 多帧战斗特效已完成；当前共 121 个独立 PNG，旧 SpriteSheet 与旧静态投射物已清理。
- 生产规范：[美术资产生成规范](./visual-asset-generation.md)
- 视觉规范：[视觉规范](./visual-style.md)
- 已归档计划：[alpha 0.1 美术资产单图迁移计划](./plan/archive/alpha-0.1-art-asset-migration-plan.md)
- 地表模块：[双层地表 Tilemap](./ground-tilemap.md)
- 初版返工历史：[阶段 5 美术资产规范收缩与返工](./plan/archive/initial-experience-optimization/phase-5-visual-asset-correction-plan.md)

项目自有运行时栅格资产统一为“一张 PNG、一个主 Sprite”。生产资产使用 `Sprite Mode: Single`，不再维护 SpriteSheet、人工 Rect、人工 Pivot 或 sub-sprite fileID。

## 生产资产

| 家族 | 数量 | 画布 / PPU | 路径 | 运行时用途 |
| --- | ---: | --- | --- | --- |
| 玩家动画帧 | 12 | 128×128 / 64 | `Assets/Art/Sprites/Characters/Player/` | Player Idle / Move / Attack / Hit / Death |
| 荒原游魂动画帧 | 12 | 128×128 / 64 | `Assets/Art/Sprites/Monsters/Basic/` | Monster Basic Animator |
| 裂爪猎犬动画帧 | 12 | 128×128 / 64 | `Assets/Art/Sprites/Monsters/Swift/` | Monster Swift Animator |
| 铁壳尸傀动画帧 | 12 | 128×128 / 64 | `Assets/Art/Sprites/Monsters/Heavy/` | Monster Heavy Animator |
| 商人动画帧 | 4 | 128×128 / 64 | `Assets/Art/Sprites/NPCs/Merchant/` | Merchant Idle Animator |
| 投射物与受击特效 | 23 | 96×96 / 64 | `Assets/Art/Sprites/Effects/Projectile/Arcane/`、`Assets/Art/Sprites/Effects/Hit/` | 飞行循环、独立命中、普通受击与暴击受击 |
| 掉落稀有度环 | 1 | 96×96 / 100 | `Assets/Art/Sprites/Effects/effect_loot_rarity_ring.png` | 世界掉落旋转圆环，由运行时按稀有度着色 |
| 装备图标 | 7 | 96×96 / 64 | `Assets/Art/Sprites/Items/Equipment/` | Addressables、世界掉落和全部物品 UI |
| 固定 UI 图标 | 12 | 96×96 / 64 | `Assets/Art/Sprites/UI/Icons/` | HUD、菜单、装备空槽、缺失图标 |
| UI 面板 / 槽位状态 | 4 | 独立精确画布 / 100 | `Assets/Art/Sprites/UI/Frames/` | 背包面板、默认、焦点、禁用状态 |
| 世界物件 | 12 | 384×384 / 181 | `Assets/Art/Sprites/Environment/WorldProps/` | 营地、道路、岩石、树木与打造区装饰 |
| 基础地表 Tile | 3 | 512×512 / 64 | `Assets/Art/Sprites/Environment/GroundTiles/Base/` | `GroundBaseTilemap` 5×5 全覆盖 |
| 地表细节 Tile | 3 | 512×512 / 64 | `Assets/Art/Sprites/Environment/GroundTiles/Details/` | `GroundDetailTilemap` 稀疏裂纹、碎石与灰烬 |
| 旧地表风格参考 | 1 | 512×512 / 64 | `Assets/Art/Sprites/Environment/VisualSlice/ground_slice.png` | 无运行时消费者，仅保留为风格参考 |
| 世界血条 | 2 | 64×8 / 64 | `Assets/Art/Sprites/UI/Phase5/` | 三种怪物的世界空间血条 |

`Assets/Art/Textures/Prototype/PrototypeSquare.png` 保留为 64×64、100 PPU 的非生产回退纹理；正式场景和 Prefab 不得依赖它。

## 家族合同

### 角色与 NPC

- 52 帧共用 128×128、64 PPU、Pivot `(0.5, 12/128)`。
- 迁移使用每个旧家族唯一的完整画布等比因子，并把旧对齐信息烘焙进像素；不得逐帧 fit-to-canvas。
- 128×128 是为容纳玩家、荒原游魂和裂爪猎犬约 103–106 px 宽的 Attack Release 帧，避免 96×96 裁切。
- Player、Basic、Swift、Heavy、Merchant 根 Scale 继续保留 `0.80 / 0.75 / 0.90 / 0.75 / 0.80`，因为根节点同时承载 Collider 和子节点。该例外不能用于新资产。
- 文件名中的 `_se_` 统一表示运行时默认朝右。当前荒原游魂的既有 12 帧源像素实际朝左，因此 `Monster_Basic.prefab` 显式配置 `ActorAnimatorController._sourceFacesRight = false` 并在默认预览中水平翻转；移动时按该家族合同反向解析 `flipX`。该例外只属于整个 Basic 家族，禁止逐帧修改 Pivot、Transform 或单独翻转。未来若重制为原生朝右，必须一次性更新全族与 Prefab 合同。

### 投射物、图标与 UI

- 投射物交付画布固定 96×96；保留原 56×22 主体像素尺寸并居中补边，不做非整数放大。Projectile Prefab 根 Scale 0.25 保持不变。
- 掉落稀有度环交付画布固定 96×96、100 PPU、Center Pivot；可见圆环为 80×80 px，四边透明留白 8 px。运行时保持 Transform Scale 1，以 55°/s 匀速顺时针旋转，不再复用 UI 槽位框或缩放脉冲。
- 稀有度环使用同一张中性灰白 Sprite 着色：普通 `rgb(153,158,168)`、魔法 `rgb(92,138,220)`、稀有 `rgb(220,176,63)`、独特 `rgb(211,105,48)`，与 UI 稀有度语义一致。
- 12 个固定 UI 图标交付画布固定 96×96；全家族统一使用 0.30 导出因子，再按 Alpha 加权中心放置，不按单图缩放。
- 七件装备保持 96×96；只对战斧做整数平移以修正视觉中心，原路径、GUID 和 Addressables 所有权不变。
- 四个 UI Frame 保留原生像素与独立画布：面板 `534×527 / Border 98`，默认槽 `436×465 / 78`，焦点槽 `479×469 / 157`，禁用槽 `436×469 / 157`。
- `Theme.uss` 与 `Inventory.uss` 只引用独立主 Sprite；九宫格 Slice 继续由已冻结的组件 Border 驱动。

### 世界资产

- 12 个世界物件统一使用 384×384、181 PPU、Center Pivot；保留原主体像素，不按物件单独放大。
- 既有场景实例 Scale 属于布局意图，不在资产迁移中归一；场景位置、Collider、交互范围与排序保持不变。
- 三张基础地表 Tile 保持 512×512 Full-bleed，并通过共享边缘和对边一致性保证任意变体相邻；三张细节 Tile 使用同画布透明叠加。两层均不包含 Collider。
- 世界血条背景与 Fill 统一为 64×8。

## 自动化与审计

- 确定性生成脚本：`tools/visual_slice/prepare_assets.py`
- 旧 GUID / fileID 与目标路径清单：`tools/visual_slice/asset_pipeline_manifest.json`
- 审计入口：`tools/visual_slice/run_asset_audits.ps1`
- 静态合同：`Docs/docs/assets/visual-assets/alpha-0.1/manifests/`
- 机器报告：运行审计时生成到 `Temp/VisualAssetAudits/alpha-0.1/reports/`，不纳入版本控制。

当前 20 份正式静态 family 合同覆盖 115/115 个非地表 Tile PNG，无重复、无遗漏；六张地表 Tile 使用独立 Pilot / Family 合同和边缘审计。alpha 0.1.7 另保留四份已确认 Pilot 合同作为逐帧生成依据，不重复计入正式覆盖；所有审计均未通过放宽阈值或逐图缩放资产。

关键量化结果：

- 固定 UI 图标视觉中心：x=48.116–48.283、y=48.110–48.397。
- 奥术投射物飞行帧可见范围为 52–60×18–26 px，视觉中心合同为 x=50–54、y=46–50，边缘留白不低于 12 px。
- 掉落稀有度环主体：80×80，视觉中心 `(48.016, 48.178)`，四边留白均为 8 px。
- 角色帧底部边距 3–5 px；Idle / Move 稳定组差异全部在冻结上限内。
- 世界物件最小边缘留白 12 px。
- 三张基础 Tile 的水平、垂直对边差值均为 0，跨变体共享边缘差值为 0；三张细节 Tile 的 Alpha 覆盖率为 13.7%–22.8%，四边留白均不低于 56 px。

## Unity 消费者

- 21 个 Animation Clip 引用 52 张独立角色 / NPC 帧；Animator Controller 的状态、参数和帧时间不变。
- Player、三类 Monster、Merchant Prefab 的根 Sprite 引用各自 Idle 00。
- `Projectile_Default.prefab` 原路径、Addressables key、物理根和 `0.25` 根 Scale 保持不变；飞行 Sprite 位于单位 Scale 的 `Visual` 子节点，由 6 帧循环 Controller 驱动。
- 投射物命中、普通受击和暴击受击使用三个独立特效 Prefab；`VisualEffectPool` 对每类预热 4 个、最多保留 24 个活动实例，0.25 秒后自动回收并在架构退出时统一释放。池根保持默认 `HideFlags.None`，由架构释放与场景卸载双重兜底；禁止使用会在 PlayMode 退出后进入无效场景的 `DontSave*` 标志。
- 玩家与三种怪物均通过独立 `HitEffectAnchor` 放置受击表现；五类主伤害 Tint 由统一色板解析，未命中、闪避、无伤害与治疗不会误播角色受击动画。
- 七个 Item ScriptableObject 与 Addressables 继续引用原装备图标 GUID。
- `SpriteAssetLoader` 与 `PrefabAssetLoader` 通过 `AssetReference.ReleaseAsset()` 释放并清空内部 OperationHandle，重复进入场景或复跑测试不会复用失效句柄。
- `LootPickup.prefab`、`CraftingStation.prefab`、三种怪物血条和 `Main.unity` 通过独立 Sprite 绑定。
- `LootPickup.prefab` 的 Halo 独立绑定掉落稀有度环，并由 `LootPickupVisual` 驱动持续旋转及四档稀有度着色。
- `Main.unity` 通过六个独立 `Tile` 资产构成 `GroundBaseTilemap` 与 `GroundDetailTilemap`；旧 `ground_slice.png` 不再参与运行时绑定。
- `Theme.uss` 使用 11 个已消费固定图标；`ui_icon_back.png` 作为当前未消费的语义资产保留。
- 12 个世界物件中的 ground cracked、ground dirt、stone path 和 ruined wall 当前未消费，仍按语义名保留。

## 迁移验收结果

- `Assets/Art` 当前有 121 张 PNG、0 个 `Sprite Mode: Multiple`，`Assets/Art/SpriteSheets/` 已删除。
- 80 个旧 sub-sprite 的 75 个消费者已全部重绑，5 个未消费语义资产仍保留。
- 全部 PNG Importer 与画布、PPU、Pivot、Border、Point、None、Full Rect、Mipmap Off、Wrap 合同一致。
- 对 Animation、Prefab、Scene、USS、ScriptableObject 和 Addressables 的精准扫描未发现旧 Sheet GUID、路径或 fileID。
- 确定性生成检查为 `generated=80 changed=0`；16 份合同覆盖 92/92 个 PNG，结果为 0 errors / 0 warnings。
- 五个项目程序集均编译成功，Unity Console 清理后为 0 errors / 0 warnings；EditMode 104/104 通过。
- `Phase5VisualIntegrationTests` 17/17 通过；受 Addressables 加载器影响的 PlayMode 冒烟用例 1/1 通过。
- 全量 PlayMode 的一次串行运行仍可触发既有测试顺序波动，失败用例单独复跑通过，不涉及旧 Sheet 引用。
- 运行时世界、角色、怪物、商人、打造台和 HUD 已在 16:9 画面复核；截图见 [`main-runtime-2560x1440.png`](./assets/visual-assets/alpha-0.1/acceptance/main-runtime-2560x1440.png)。
- 双层地表专项的 19 项 EditMode 与 1 项 PlayMode 验收通过；5×5 基础层、11 格细节层、无 Collider、排序和 40×40 视觉覆盖均由自动化校验。
- alpha 0.1.7 四个特效家族共 23 张 Sprite 的量化审计为 23 assets / 0 errors / 0 warnings；运行时定向合同验证飞行绑定、三个特效 Prefab、四个受击锚点、五类 Tint、命中语义和有界对象池均通过。正式 Main 中三类池预热 12 个，战斗后 18 个实例全部回收，退出后池根和实例均为 0；旧静态投射物与动态缩放冲击实现零依赖后已删除。
- 正式渲染已拆分为 `Ground`、`WorldObject`、`WorldEffect`、`WorldInfo`；七个世界 Prefab 与 Main 八个场景物件使用唯一 SortingGroup 和显式接地点，投射物 / 命中受击 / 战斗文字进入专用层，精准验证为 18 targets / 0 issues。完整合同见[世界渲染与稳定层级](./world-rendering.md)。
- alpha 0.1 综合验收为 EditMode 208/208、PlayMode 21 通过 / 0 失败 / 2 个上游忽略项；五个项目程序集均为 0 errors。三档 16:9 分辨率的 HUD、物品工作台、商店、打造与物品浮窗人工 / 自动边界验收通过。

## 掉落稀有度环生成记录

- 生成工具：内置 imagegen。
- Pilot 提示：单个深色奇幻掉落特效；中性灰白的细金属 / 奥术圆环，中心完全留空，四个方位带短尖角与少量不对称符文刻度，使旋转清晰可读；正交视图，无物品、无文字、无方框、无阴影；纯绿色色键背景。
- 处理：统一移除色键后，以完整源画布中心为基准使用 `0.875` 等比因子，再一次性导出 96×96；没有紧边裁切、逐图 fit、手工 Sprite Editor 调整或运行时比例补偿。
- 验收：`loot-rarity-effects.json` 为静态合同，结果为 1 asset / 0 errors / 0 warnings。
