# 阶段 0.5：视觉体验修正执行计划

## 状态

- 状态：已完成并归档
- 建立日期：2026-08-04
- 完成日期：2026-08-04
- 所属计划：[初版体验优化计划](./initial-experience-optimization/README.md)
- 关联子计划：[视觉填充](./initial-experience-optimization/visual-plan.md)
- 前置阶段：[阶段 0：视觉垂直切片](./phase-0-visual-slice-plan.md)
- 目标场景：`Assets/Scenes/Main.unity`

## 完成摘要

- 以玩家已经验证有效的 `Rigidbody2D.Interpolate` 为主方案解决移动模糊，保留 `LateUpdate + SmoothDamp`，未引入 Cinemachine 或 Pixel Perfect Camera。
- 地表由 3×3 扩展为 5×5，新增闭合 `WorldBounds`；相机、物理边界和怪物生成共用同一范围。
- 掉落 Prefab 增加投影、金属稀有度框、图标、中文标签和 PrimeTween 悬浮 / 呼吸动画，拾取事务未变。
- 1280×720、1920×1080、2560×1440 世界截图及 1920×1080 边界机位通过；Console 0 Error。
- 专项 EditMode 4/4、全量 EditMode 51/51、项目 PlayMode 3/3 通过；Runtime / Editor 编译 0 错误。

## 阶段目标

在不扩张玩法、内容池和 UI 功能的前提下，修正阶段 0 实机验收暴露的三项视觉可读性问题：

1. 相机跟随时人物移动模糊。
2. 地图切片边界明显。
3. 世界掉落物不够清晰。

完成后，后续 UX、装备和内容工作应建立在稳定的运动画面、地图覆盖和掉落可读性上，不再反复返工视觉基线。

## 当前实现基线

### 相机与角色移动

- `CameraFollowTarget` 在 `LateUpdate` 中用 `Vector3.SmoothDamp` 跟随玩家，当前平滑时间为 `0.08s`。
- 玩家移动由 `PlayerController.FixedUpdate` 设置 `Rigidbody2D.linearVelocity`。
- 玩家 Prefab 的 `Rigidbody2D` 当前未启用插值。
- Main Camera 为正交相机，`Orthographic Size = 5`；没有 Cinemachine，也没有挂载 Pixel Perfect Camera。
- URP Render Scale 为 `1`，相机后处理和抗锯齿均未开启；角色与地表纹理使用 `64 PPU + Point` 采样。
- 阶段 0 的 1920×1080 Editor 截图中，Game View 以 `0.61x` 缩放显示。必须先区分编辑器视口缩放与实际运行时采样，不能直接把模糊归因于动画资源。

### 地图与边界

- `VisualSliceGround` 当前由 3×3 个 8×8 世界单位的地表 Sprite 组成，总覆盖约 24×24 世界单位。
- 场景当前没有地图边界 Collider、Camera Confiner 或等价的世界范围约束。
- 怪物以玩家为中心、半径 `8` 生成；玩家和怪物都可能继续移动到地表覆盖范围之外。
- 当前硬边问题同时包含视觉覆盖不足和实际玩法边界缺失，不能只靠在边缘增加装饰解决。

### 世界掉落

- `LootPickup.prefab` 只有根节点，没有独立图标、光圈、投影或标签子节点。
- 根节点缩放为 `0.4`，Sprite 排序值为 `5`；实际图标明显小于角色轮廓。
- `LootPickupController.Init` 当前只按稀有度修改 Sprite 颜色。
- 自动拾取、背包满保留和销毁逻辑已经可用，本阶段不得改变其事务语义。

## 范围决策

- 按“相机 → 地图 → 掉落 → 综合验收”执行。相机不稳定时不开始校准地图边缘与掉落尺寸。
- 先验证 Game View `1x` / 原生分辨率与独立运行画面，再决定是否修改运行时代码。
- 不为本阶段引入 Cinemachine。现有跟随组件足以承载首版修正。
- URP Pixel Perfect Camera 已在当前工程可用，但只在原生画面仍存在非整数采样问题时接入；不得为了像素对齐明显改变可视范围或制造抖动。
- 地图的“可玩边界”和“视觉覆盖”分开处理：碰撞保证规则稳定，地表与边缘装饰负责隐藏硬切片。
- 掉落表现与拾取事务分离。视觉层只读取 `ItemInstance` 的名称、类别和稀有度，不发送伤害、物品或背包领域事件。
- 阶段 0.5 只校准当前大剑掉落；按物品基底配置完整图标映射留到批量视觉阶段。

## 实施顺序

### 0.5.0 建立可复现基线

1. 使用固定玩家出生点、固定移动方向和固定掉落位置建立复现路径。
2. 分别记录以下画面：
   - Game View `1x` 或尽可能接近整数缩放。
   - 1280×720、1920×1080、2560×1440 原生输出。
   - 水平、垂直、斜向移动中的 Idle / Move / Attack 切换。
   - 地图四个方向的最远可达位置。
   - 单个掉落和多个相邻掉落。
3. 将修正前截图放入 `docs/assets/visual-style/phase-0.5/before/`，固定相同位置用于前后对比。
4. 记录以下诊断数据：Game View 缩放、相机位置小数、玩家位置小数、相机与玩家误差、当前帧率和 `Fixed Timestep`。

决策门槛：

| 结果 | 后续动作 |
| --- | --- |
| 只有 Editor 非整数缩放时发糊 | 不修改运行时采样；固化验收必须使用 `1x` 或原生输出 |
| 原生输出仍有物理帧与渲染帧不同步 | 对玩家 Rigidbody 插值和相机更新时序做最小调整 |
| 原生输出存在非整数像素采样或纹理跳动 | 小范围试接 URP Pixel Perfect Camera，并核对 PPU、参考分辨率与可视范围 |
| `SmoothDamp` 本身造成拖尾或跟随迟滞 | 降低 / 移除阻尼，或增加可配置的直接跟随与平滑跟随模式后选择首版方案 |

完成门槛：能够稳定复现问题，并明确是 Editor 显示问题、物理插值问题、相机跟随问题还是像素采样问题。

### 0.5.1 修正相机跟随清晰度

1. 保留 `CombatPrototypeBootstrap.SetupCamera` 的目标绑定入口，不在玩家 Controller 中直接控制相机。
2. 根据 0.5.0 结果对 `CameraFollowTarget` 做最小修改：
   - 保留 `LateUpdate` 读取最终目标位置。
   - 将实际采用的跟随方式和参数序列化，避免运行时代码硬编码试验值。
   - 清除目标变化或复活后的旧速度状态，避免瞬时拖尾。
   - 为地图边缘预留可选跟随范围约束。
3. 仅在原生画面验证有效时，把 Player 的 Rigidbody 插值改为 `Interpolate`；同时复查碰撞、复活和暂停恢复。
4. 仅在仍存在采样问题时挂载 URP Pixel Perfect Camera：
   - `Assets PPU` 与现有 `64 PPU` 保持一致。
   - 参考分辨率以不改变主要可视范围为约束反推。
   - 分别检查 Grid Snapping、Upscale Render Texture 和裁切策略，不同时开启未经验证的选项。
5. 删除诊断阶段未采用的临时代码与组件组合，只保留最终一种默认方案和必要配置。

验证：

- 键鼠与手柄分别完成水平、垂直、斜向移动。
- Idle、Move、Attack、Hit、Death、复活切换正常。
- 菜单暂停时相机不继续漂移，关闭后无突跳。
- 1280×720、1920×1080、2560×1440 原生画面中的角色轮廓和动画帧清晰。

### 0.5.2 扩展地图覆盖并建立真实边界

1. 在场景中建立独立的 `WorldBounds` 根节点，通过闭合 `EdgeCollider2D` 或等价静态边界限制玩家与怪物。
2. `CameraFollowTarget` 使用边界的世界范围计算相机中心限制，按正交尺寸和实际宽高比扣除半屏范围。
3. `MonsterSpawner` 使用同一世界范围约束生成点：
   - 优先重新采样落在范围内的位置。
   - 有限次尝试失败后再回退到范围内最近点。
   - 不改变现有生成间隔、最大数量和半径配置。
4. 使用 unityMCP 扩展 `VisualSliceGround`。地表覆盖范围至少满足：
   - 可玩边界。
   - 最大验收宽高比下的半屏镜头范围。
   - 一格过渡 / 安全余量。
5. 在可玩边界附近加入有限的暗边、碎石、岩壁或植被过渡，打散连续直线；装饰不承担核心碰撞职责。
6. 核对商人、打造台、玩家出生点和刷怪区域，避免被边界、装饰或镜头限制遮挡。

建议场景层级：

```text
World
├── VisualSliceGround
├── BoundaryVisuals
└── WorldBounds
    └── EdgeCollider2D
```

验证：

- 玩家和怪物无法离开有效区域。
- 怪物不会生成在边界外、墙体内或不可达区域。
- 相机到达边缘时不显示空白背景，也不会突然跳动。
- 三档分辨率巡查四边和四角，视觉硬边与碰撞穿帮均不可见。

### 0.5.3 强化掉落物表现

1. 重构 `LootPickup.prefab` 的表现层级，根节点继续负责 Collider 和拾取 Controller：

```text
LootPickup
├── Shadow
├── Halo
├── Icon
└── Label
```

2. 将当前大剑图标移到 `Icon` 子节点，根节点恢复稳定缩放；图标初始目标高度以角色视觉高度的约 60%–80% 为调试区间，最终以实机场景为准。
3. 增加独立的投影和稀有度光圈：
   - 投影只提供地面锚点，不使用稀有度颜色。
   - 光圈、角标或形状与颜色共同表达稀有度。
   - 排序保持“地表 < 投影 / 光圈 < 图标 < 标签 < 角色关键反馈”。
4. 使用 PrimeTween 增加克制的悬浮 / 呼吸表现：
   - 只改变视觉子节点，不移动根 Collider。
   - 禁用或销毁时取消 Tween。
   - 菜单暂停行为与世界时间约定一致。
5. 标签读取 `ItemInstance.BaseDefinition.DisplayName`，缺少数据时使用安全占位，不输出原始 ID。
6. 将视觉绑定封装在表现组件或明确的绑定方法中；`LootPickupController.Init` 只负责传入物品数据，拾取 Command 与背包事务不变。
7. 校准单个掉落、多个相邻掉落、角色覆盖和战斗特效同时出现时的可读性。

验证：

- 常用镜头缩放下，无需贴近即可发现掉落。
- 不只依赖颜色即可判断物品与稀有度。
- 多个相邻掉落仍能看出数量和大致位置。
- 自动拾取成功后根对象与视觉一并销毁。
- 背包已满时掉落保留，Tween、标签和光圈继续正常显示且不重复初始化。

### 0.5.4 综合回归与归档

1. 增加 EditMode / PlayMode 覆盖：
   - 相机设置目标、清理平滑速度和边界约束。
   - 怪物生成点始终位于世界范围内。
   - 掉落初始化、稀有度映射、背包满保留与销毁路径。
2. 运行全量 EditMode、相关 PlayMode 和脚本编译。
3. 使用 unityMCP 完成三档分辨率验收，留存相同机位的 Before / After 截图。
4. 键鼠和手柄各跑一轮“移动 → 战斗 → 掉落 → 自动拾取 → 背包满后保留”。
5. 检查 Console、Tween 取消、场景退出、暂停恢复和 Addressables 释放。
6. 同步更新 `visual-style.md`、`visual-assets.md` 和 `gameplay-loop.md`。
7. 完成后总结模块文档，将本计划移动到 `docs/plan/archive/`，再把总计划状态切换为阶段 1。

## 预计影响文件

### 必然检查或修改

- `Assets/Scripts/Runtime/Gameplay/Bootstrap/CameraFollowTarget.cs`
- `Assets/Scripts/Runtime/Gameplay/Spawning/MonsterSpawner.cs`
- `Assets/Scripts/Runtime/Gameplay/Loot/LootPickupController.cs`
- `Assets/Prefabs/Combat/Player.prefab`
- `Assets/Prefabs/Loot/LootPickup.prefab`
- `Assets/Scenes/Main.unity`
- `Assets/Scripts/Tests/EditMode/`
- `Assets/Scripts/Tests/PlayMode/`

### 按诊断结果新增

- 掉落表现组件，例如 `Assets/Scripts/Runtime/Gameplay/Visuals/LootPickupVisual.cs`
- 掉落光圈、投影等小型资源，放入 `Assets/Art/Sprites/Effects/Loot/`
- 阶段 0.5 验收截图，放入 `docs/assets/visual-style/phase-0.5/`

## 总验收矩阵

| 场景 | 通过标准 |
| --- | --- |
| Game View 非整数缩放 | 能明确说明它与原生输出的差异，不把 Editor 显示伪影当成运行时缺陷 |
| 键鼠 / 手柄移动 | 水平、垂直、斜向运动中人物轮廓清晰，无明显抖动、拖影、突跳或跟随迟滞 |
| 动画切换 | Move、Attack、Hit、Death、复活期间相机稳定，动画状态不因修正失效 |
| 地图四边与四角 | 三档分辨率下不显示空白背景或突兀切片硬边，玩家与怪物不能越界 |
| 边缘刷怪 | 怪物生成点始终合法，生成节奏与数量不变 |
| 单个掉落 | 图标、锚点、名称和稀有度在战斗背景中立即可辨识 |
| 多个掉落 | 数量与位置仍可读，不因标签、光圈或排序互相完全遮挡 |
| 自动拾取 | 成功时正常入包并销毁世界对象，事务只执行一次 |
| 背包已满 | 拾取失败后对象和表现保留，无重复 Tween、空引用或报错 |
| 暂停与退出 | 相机、动画和 Tween 按约定暂停 / 取消，Console 无新增错误或泄漏警告 |

## 风险与控制

- **把 Game View 缩放误判为运行时模糊**：所有结论必须由 `1x` 或原生输出复核。
- **插值与像素对齐互相冲突**：逐项 A/B，不同时修改 Rigidbody、相机阻尼和 Pixel Perfect 参数。
- **Pixel Perfect 改变可视范围**：先记录当前相机覆盖，再反推参考分辨率；范围变化超出容差则回退。
- **地图扩展后重复纹理更明显**：通过有限翻转、过渡装饰和边缘遮挡降低重复感，不在本阶段制作完整 Tilemap 内容。
- **相机边界与玩法边界不一致**：二者使用同一世界范围来源，并在四角做碰撞与画面联合测试。
- **怪物集中生成在边缘**：范围外位置优先重新采样，避免大量简单 Clamp 到同一点。
- **掉落视觉改变拾取半径**：根 Collider 尺寸保持独立，不随 Icon Tween 或子节点缩放变化。
- **标签造成画面噪声**：限制字号、长度和排序；多个掉落时优先保证图标与位置，不堆叠完整详情。

## 本阶段不做

- 商店状态保留、统一装备详情和装备栏扩展。
- 随机血量、随机伤害、掉落概率、词条池、装备池和怪物池。
- 完整 Tilemap、完整地图分区、新场景或新地形玩法。
- Cinemachine 包引入、镜头震动、动态缩放或复杂转场。
- 全部装备类别与稀有度的正式世界掉落资产。
- 修改自动拾取为按键拾取。
