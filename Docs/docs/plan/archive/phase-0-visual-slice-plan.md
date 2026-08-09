# 阶段 0：视觉垂直切片执行计划

## 状态

- 状态：已完成并归档（2026-08-04）
- 所属计划：[初版体验优化计划](./initial-experience-optimization/README.md)
- 目标场景：`Assets/Scenes/Main.unity`
- 后续阶段：阶段 1 UX 快速改进

## 目标

用一组小而完整的资产验证首版视觉方向和生产链，形成后续批量制作可以复用的规格。阶段 0 只做一个代表性切片，不追求一次替换全部占位资源。

完成后应能在 `Main.unity` 中看到一小块风格统一的地图、玩家、基础怪物、投射物、掉落和背包面板，并确认 Sprite、Animator、UIToolkit 和 Addressables 能稳定协作。

## 成功标准

- 形成可执行的视觉规范：色板、尺寸、PPU、Pivot、Sorting Layer、动画帧率、UI 状态和资源命名。
- 视觉切片中的世界对象不再使用 `PrototypeSquare.png`，但原占位资源仍保留作回退。
- 玩家和基础怪物完成 Idle / Move / Attack、Hit、Death 的运行时接入与状态切换验证。
- 背包面板完成一版静态视觉皮肤，键鼠和手柄焦点仍清晰可用。
- `Main.unity` 原有战斗、掉落、商人、打造和暂停循环不被破坏。
- Unity 编译、现有 EditMode、Play 控制台和三档分辨率检查通过。

## 切片内容

| 内容 | 本阶段交付 | 暂不展开 |
| --- | --- | --- |
| 地图 | 出生点到营地 / 战斗区的一小块地表、路径、边界和装饰 | 完整地图和大量装饰变体 |
| 玩家 | 1 套角色形象；Idle、Move、Cast、Hit、Death 动画样张 | 装备纸娃娃和多套外观 |
| 怪物 | 现有基础怪物的新形象和动画样张 | 迅捷型、重装型怪物 |
| 投射物 | 飞行 Sprite 和命中特效样张 | 多技能特效 |
| 装备 | 大剑图标、稀有度框和槽位预览 | 7 件装备全量图标 |
| 掉落 | 大剑对应世界掉落图标、光圈和标签样式 | 所有类别和稀有度组合 |
| UI | 背包面板底图、格子、按钮、焦点、选中和禁用状态 | 商店、打造和 HUD 全量换肤 |

## 暂定视觉规格

批量生成前允许根据切片结果调整，阶段 0 先按以下规格验证：

- 风格：俯视 3/4、暗色奇幻、低分辨率手绘 / 像素化边缘。
- 色彩：冷灰与低饱和环境为主，玩家使用青绿识别色，敌人使用暖红，交互物使用金铜色。
- 地图 Tile：64×64 像素，64 PPU，以 1 Unity Unit 对应 1 格为基线。
- 角色单帧：64×64 或 96×96 像素，统一脚底 Pivot，避免动画时角色漂移。
- 物品图标：64×64 像素透明底；世界掉落可复用图标并增加独立光圈。
- UI 图标：32×32 / 64×64 两档；面板纹理提供可九宫格缩放的安全边缘。
- 动画：8–12 FPS；玩家移动优先验证四方向，怪物允许左右翻转减少资源量。
- 稀有度：颜色之外必须同时使用边框或角标，不依赖单一色彩表达。

## 资源目录

执行时先核对引用，再建立或调整以下目录：

```text
Assets/Art/
  Animations/
    Clips/
    Controllers/
  Sprites/
    Environment/VisualSlice/
    Characters/Player/
    Characters/Monsters/Basic/
    Items/Equipment/
    Effects/
    UI/
```

- 当前空目录 `Assets/Art/Animaitons` 只有在确认无引用后才更名为 `Animations`。
- 不删除 `Assets/Art/Textures/Prototype/PrototypeSquare.png`。
- 美术源文件和 Unity 导入文件使用稳定英文名，中文显示名留在配置资产中。

## 执行步骤

### 0.1 记录当前基线

1. 使用 unityMCP 检查 Editor、当前场景、Game 视图和控制台状态。
2. 在 1920×1080 留存当前地图、战斗、背包与掉落画面。
3. 记录当前 Prefab、Addressable key、Sorting Layer、PanelSettings 和资源引用，作为回归基线。

### 0.2 建立视觉规范与资产清单

1. 新建 `Docs/docs/visual-style.md`，记录暂定规格、色板、尺寸、命名和交互状态。
2. 为切片资产建立清单，逐项标注尺寸、透明背景、方向、帧数、用途和目标路径。
3. 明确玩家、怪物、投射物、物品和 UI 的轮廓差异，避免只靠颜色区分。

### 0.3 生成与筛选视觉资产

1. 使用 image gen 生成一张整体风格板，先确认地图、角色、怪物、物品和 UI 是否属于同一视觉语言。
2. 依据风格板分别生成透明背景角色 / 物品素材、无缝或可拼接地图素材，以及 UI 九宫格 / 图标素材。
3. 对动画帧做一致性检查，淘汰轮廓、比例、光照或武器位置明显跳变的结果。
4. 保留生成提示和关键参数，便于后续三种怪物与七件装备复用风格。

### 0.4 导入与技术验证

1. 配置 Texture Type、Sprite Mode、PPU、Filter Mode、Compression、Pivot 和切片边界。
2. 创建 Animator Controller 和动画 Clip，先在 Prefab / Inspector 预览全部状态。
3. 保持现有玩家、怪物、投射物和掉落 Prefab 的 Addressable key 不变，只替换或增加表现子节点。
4. 静态 UI 纹理通过 UXML / USS 接入；阶段 0 不新增物品图标异步加载服务。
5. 若需要表现桥接脚本，保持其只读取 Rigidbody / 领域 Event 驱动 Animator，不修改战斗 Model。

### 0.5 接入 Main 场景

1. 使用 unityMCP 在出生点、营地入口和最近战斗区域铺设切片地图与碰撞边界。
2. 接入玩家、基础怪物、投射物和掉落的切片表现。
3. 只为背包页接入新的面板、格子和状态视觉，保留原有结构和操作逻辑。
4. 检查商人、打造台、刷怪范围和交互触发器没有因地图布局被遮挡或移位。

### 0.6 验证与风格门槛

1. 运行 Unity 脚本编译和全量 EditMode。
2. 使用键鼠和手柄完成移动、战斗、拾取、打开背包、选择装备和关闭菜单。
3. 检查 Idle / Move、Attack、Hit、Death 运行时切换，确认 Pivot、朝向、帧间一致性和死亡可见时长。
4. 验证 1280×720、1920×1080、2560×1440 的 Game 视图和 UI 焦点。
5. 检查控制台、Addressables、场景保存、Prefab 引用和 `git diff --check`。
6. 留存切片截图，并在批量生产前确认或修订视觉规范。

## 允许的代码范围

- 允许增加小型表现组件、Animator 参数桥接和只读视觉配置。
- 允许为 UI 静态样式增加 USS class 和图片引用。
- 不修改装备槽、商店状态、掉落概率、怪物随机属性和词条系统；这些属于后续阶段。
- 不为切片引入通用资源框架；现有 Addressables 与 Prefab 结构能够满足时直接复用。

## 完成后的下一步

视觉切片通过后：

1. 固化 `Docs/docs/visual-style.md` 和资产清单。
2. 主计划状态切换到“阶段 1：UX 快速改进”。
3. 开始共用物品详情快照、商店 ViewState 和背包 / 商店 / 打造词条显示。
4. 暂不批量生成三种怪物和七件装备，等装备与内容数据结构稳定后再进入阶段 5。

## 本轮执行记录

已完成：

- 视觉规范、资产清单、风格板和完整生成提示词。
- 31 个最终位图的生成、色键移除、裁帧、尺寸归一和 Alpha 验证。
- 玩家 / 基础怪物 Idle、Move、Attack、Hit、Death Animator；战斗 Trigger、Any State 转换和 Idle / Move 返回链已接入。
- 玩家投射物、怪物接触攻击、受伤、死亡与复活事件已接入 `ActorAnimatorController`；怪物死亡延迟 0.65 秒销毁。
- 动画专项 3 项 EditMode 与 Main 场景 1 项 PlayMode 通过，确认 `Attack → Hit → Death → Revive/Idle` 实际切换。
- 投射物、大剑掉落、背包面板 / 格子 / 焦点视觉接入。
- `Main.unity` 3×3 地表切片、4 个既有 Prefab 表现替换和 Addressables 路径保留。
- Runtime / Editor 编译、Unity 安装后日志、1920×1080 Play 世界画面检查。

最终验收：

- Unity EditMode 48/48 通过；PlayMode 共执行 6 项、0 失败，其中 4 项通过，2 项 Input System 包内不稳定用例按原标记跳过。
- 阶段 0 体验专项 PlayMode 通过真实购买大剑验证背包物品选择、详情、装备、Tab / Esc、Start / B、默认焦点和暂停恢复。
- 1280×720、1920×1080、2560×1440 三档 Game View 完成实际切换、主要元素边界断言和截图。
- `GameArchitecture.Deinit()` 释放 Addressables 预热句柄；重复架构初始化的全量 PlayMode 回归通过。
- 控制台无项目运行时错误；C#、USS 与 Markdown 范围的 `git diff --check` 通过。Unity 序列化器在 `Main.unity` 与角色 Prefab 的空字符串字段后保留空格，未直接修改已打开场景的 YAML。
- 视觉规范与资产清单已固化。
