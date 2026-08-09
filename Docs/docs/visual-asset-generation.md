# 美术资产生成规范

## 状态与适用范围

- 状态：alpha 阶段当前生产规范
- 生效日期：2026-08-09
- 适用对象：新生成或重新生成的角色帧、怪物帧、NPC 帧、装备图标、固定 UI 图标、UI 状态、九宫格面板、投射物、特效和世界物件
- 对应 Skill：[Unity 2D 单图生产流程](../../.agents/skills/unity-2d-sprite-workflow/SKILL.md)、[Game UI Kit](../../.agents/skills/artifact-template-game-ui-kit/SKILL.md)

初版阶段接入的 SpriteSheet 已按 [alpha 0.1 美术资产单图迁移计划](./plan/archive/alpha-0.1-art-asset-migration-plan.md)完成退役。项目自有运行时栅格资产不再保留 `Sprite Mode: Multiple`、人工 Sprite Editor Rect 或 sub-sprite fileID 依赖。

## 目标

1. 在生成前确定统一尺寸、主体占比、视觉中心和锚点，而不是在导入后人工修图。
2. 每次只生成和验收一张图，降低多对象整图导致的尺寸漂移、误切和内容不连贯。
3. 用自动审计发现画布、主体尺寸、中心、边距和帧间差异，失败时尽早停止。
4. 让 Unity 导入、Pivot、PPU、九宫格 Border 和引用绑定可自动执行，不依赖 Sprite Editor 手工调整。
5. 保持同一角色、物品或 UI 家族的身份、材质、光向、轮廓与视觉重量连续。

## 硬性规则

- 一个生产 PNG 只包含一个运行时 Sprite、一个动画帧、一个 UI 组件或一个交互状态。
- 禁止生成或合并生产 SpriteSheet、图集、网格图、状态板和 contact sheet。
- 新资产统一使用 `Sprite Mode: Single`；不得把 Sprite Editor 人工切片或 Pivot 微调列为生产步骤。
- 生成前必须冻结家族合同；合同未确认时不得开始批量生成。
- 先生成一个 Pilot，通过数值与视觉验收后再派生后续图片。
- 每张后续图片生成后立即单独审计；前一张未通过时不得继续下一张。
- 禁止按每张图的 Alpha 包围盒单独缩放到目标尺寸，禁止非等比缩放、紧边界裁切和运行时 Transform 补偿。
- 主体尺寸或视觉中心不合格时，应重新生成或基于 Pilot 编辑，不得依靠手工裁切、缩放或 Pivot 修补。
- 生产资产不得烘焙文字、数量、示例物品或与组件职责无关的装饰。

## 家族资产合同

每个家族在生成前必须记录：

| 类别 | 必填内容 |
| --- | --- |
| 身份 | 家族 ID、资产角色、命名、目标路径、所有消费者 |
| 参考 | 不可变风格参考、角色或组件身份参考、允许变化的内容 |
| 画布 | 精确生成画布、精确交付画布、是否存在统一全画布导出变换 |
| 主体 | Alpha 主体宽高范围、覆盖率、最小边距、视觉中心范围 |
| 锚点 | 角色脚底线、图标光学中心、世界物件接地点或 UI Slice 区域 |
| Unity | PPU、Pivot、Filter、Compression、Mesh、Mipmap、色彩空间、Transform Scale |
| 连贯性 | 状态、方向、帧序、帧间尺寸 / 中心 / 基线容差、不可漂移特征 |
| 验收 | 自动审计、原生像素对比、运行时场景、UI 尺寸和 1920×1080 截图 |

生成画布与交付画布应优先保持一致。如果生成工具不能直接输出交付分辨率，可以在合同中预先声明一个家族共用的“完整画布等比导出”步骤；同一家族所有图片必须使用相同缩放因子、采样方式和透明补边，不得按单张主体大小动态计算。

## 当前默认交付规格

以下规格沿用初版已验证的视觉体量。新家族可以有意不同，但必须在生成前另立合同并说明原因。

| 家族 | 交付画布 | Alpha 主体目标 | 中心 / 锚点 |
| --- | --- | --- | --- |
| 玩家 / 人形 NPC 单帧 | 128×128 | 宽 52–66、高 86–92 px | 固定 Pivot `(0.5, 12/128)`；稳定帧可见包围盒中心 `x = 64 ± 2 px` |
| 普通人形怪物单帧 | 128×128 | 宽 76–84、高 86–94 px | 固定 Pivot `(0.5, 12/128)`；稳定帧可见包围盒中心 `x = 64 ± 2 px` |
| 迅捷四足怪物单帧 | 128×128 | 宽 68–78、高 48–58 px | 固定 Pivot `(0.5, 12/128)`；共享接地线 |
| 重装怪物单帧 | 128×128 | 宽 68–82、高 82–90 px | 固定 Pivot `(0.5, 12/128)`；稳定帧可见包围盒中心 `x = 64 ± 2 px` |
| 装备图标 | 96×96 | 最长边 72–80 px | Alpha 加权中心偏差不超过 3 px，任一边距不少于 5 px |
| 投射物特效 | 96×96 | 由具体家族 Pilot 冻结，禁止逐图放大填满画布 | 默认画布中心；方向性拖尾必须保留安全边距 |
| 固定 UI 图标 | 96×96 | 当前家族最长边约 64–78 px | Alpha 加权中心偏差不超过 3 px，并复核光学中心 |
| 地表纹理 | 512×512 | 允许全画布覆盖 | Center，四边无接缝 |
| UI 面板 / 状态 | 每个组件单独冻结 | 不使用通用缩放到框 | Slice 保护区和独立装饰区必须预先定义 |
| 当前世界物件家族 | 384×384 | 保留 Pilot 像素尺度，禁止逐件 fit-to-canvas | Center；接地点、占地和遮挡关系另做场景验收 |

同一稳定动画组的躯干高度差默认不超过 4 px、总宽度差不超过 12 px、横向视觉中心差不超过 2 px、底部边距差不超过 2 px。Attack、Hit、Death 的大动作或特效需要单独分组，不能通过压缩角色本体满足稳定帧容差。

## 单图生成流程

### 1. 定义 Pilot

1. 选择真实风格参考和身份参考，不能只使用文字描述“相同风格”。
2. 冻结家族合同与审计 Manifest。
3. 只生成一个中性 Pilot：角色使用 Idle 0，装备使用一个代表图标，UI 使用 Default 状态或基础面板。
4. 提示词明确“单一主体、固定画布、完整可见、无网格、无标签、无其他状态、无额外对象”。
5. 运行单图审计并进行原生像素视觉复核。任一项失败即重新生成或编辑 Pilot。

### 2. 逐张派生

1. 每张动画帧或状态都以不可变 Pilot 作为身份与风格参考。
2. 为保证动作连续，可额外引用上一张已通过帧，但不能只沿上一帧连续编辑，以免身份逐步漂移。
3. 固定角色解剖、服装、装备、镜头、朝向、光向、色板、轮廓线宽、身体尺度和画布；每次只改变当前动作或状态。
4. 生成一张、审计一张、视觉确认一张，再继续下一张。
5. 通过的文件直接使用最终确定性命名保存，不再合并为 Sheet。

### 3. 自动审计

运行：

```powershell
python .agents/skills/unity-2d-sprite-workflow/scripts/audit_sprite_assets.py path/to/manifest.json
```

审计内容包括：

- 精确画布尺寸和 PNG 格式；
- Alpha 通道与可见包围盒；
- Alpha 覆盖率和四边透明安全边距；
- Alpha 加权视觉中心；
- 角色底部边距或接地线；
- 家族内宽高、中心、覆盖率和基线最大差值；
- 机器可读 JSON 报告和非零失败退出码。

脚本通过不等于视觉通过。身份、解剖、材质、像素密度、光向、动作连贯和 UI 状态可读性仍需逐张视觉复核。自动审计失败时禁止用后处理把主体单独缩放到范围内。

## Unity 导入规范

- 每个 PNG 使用 `Texture Type: Sprite (2D and UI)`、`Sprite Mode: Single`。
- Point、无压缩、关闭 Mipmap、Alpha、Mesh、色彩空间、PPU 和 Pivot 通过 Preset、Editor 工具或 Unity MCP 自动设置。
- 角色与站立物件使用合同约定的统一 Pivot；图标默认 Center。例外必须在合同中声明并由导入自动化处理。
- 运行时 Transform 默认 `(1, 1, 1)`。如果旧系统存在固定等比 Scale，必须在迁移计划中统一消除或明确保留，不能按资产临时调整。
- Animation Clip 通过文件名中的状态与序号绑定独立 Sprite，例如 `actor_player_idle_down_00.png`。
- Prefab、ScriptableObject、Addressables、USS/UXML 和场景只引用独立 Sprite GUID，不新增 sub-sprite fileID 依赖。
- 打开的场景与 Prefab 必须通过 Unity MCP 或 Editor API 修改，不直接编辑场景 YAML。

## UI 与 Game UI Kit

- Game UI Kit 组件板只作为风格、材质、层级、边框和焦点语言参考，不能直接切割为生产资产。
- 概念稿或界面 Mockup 可以是组合画面，但必须明确标记为不可导入 Unity。
- 生产按钮、槽位、图标、面板和 Default / Hover / Focused / Pressed / Disabled 状态分别生成独立 PNG。
- UI 位图不烘焙文字、数量和示例内容；文字、布局、图标组合与输入提示继续由 UXML / USS 管理。
- 九宫格面板每张图独立定义四向 Border；Slice 线不得穿过角件、铆钉、斜切缺口、徽记或不可重复纹理。
- 无法安全九宫格拉伸时，拆为“可拉伸底板 + 独立装饰”，各自仍是一图一资产。
- 接入前预览最小、正常和最大实际控件尺寸，并验证键鼠 Hover 与手柄 Focused 状态均清晰。

## 目录与命名

```text
Assets/Art/Sprites/
  Characters/{subject}/
  Monsters/{subject}/
  NPCs/{subject}/
  Items/Equipment/
  Effects/
  Environment/{family}/
  UI/Icons/
  UI/Frames/
```

- 动画帧：`{domain}_{subject}_{state}_{direction}_{index}.png`
- 图标：`{domain}_{subject}_{variant}.png`
- UI 状态：`ui_{component}_{state}.png`
- 九宫格：`ui_{component}_base.png`，独立装饰使用 `ui_{component}_{decoration}.png`
- 同一状态的序号固定补零，不依赖文件系统自然排序。

## 完成门槛

- 家族合同、Manifest、Pilot 和所有逐张审计均通过。
- 原生像素并排检查不存在身份、尺度、中心、基线、材质、光向或状态漂移。
- Unity 导入不需要打开 Sprite Editor，不存在 `Sprite Mode: Multiple` 或新 sub-sprite 引用。
- 动画、Prefab、Addressables、ScriptableObject、USS/UXML 和场景引用全部有效。
- 1920×1080 下运行时世界、动画和 UI 状态通过；键鼠与手柄路径均可辨识。
- Console、相关测试、程序集编译和资源释放检查通过。
- 交付记录包含合同、参考图、提示词、单图路径、审计报告、Unity 设置、消费者和验收截图。
