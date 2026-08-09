# alpha 0.1 美术资产单图迁移计划

> 状态：已完成
> 建立日期：2026-08-09
> 完成日期：2026-08-09
> 定位：`alpha 0.1` 目标确定前的生产基础设施整理，不占用 `alpha 0.1.0` 阶段编号

## 目标

将 `Assets/Art` 下现有项目自有运行时栅格资产迁移为可重复生成、自动审计、自动导入和自动重绑的单图资产，彻底移除生产 SpriteSheet、`Sprite Mode: Multiple`、人工 Rect 和 sub-sprite fileID 依赖。

投射物和固定 UI 图标统一使用 `96×96` 交付画布。迁移必须保持现有玩法、碰撞体、动画时序、场景位置和 Addressables 所有权不变。

## 范围

- 5 个角色 / NPC 家族，共 52 个动画帧。
- 12 个固定 UI 图标、4 个 UI 面板 / 槽位状态和 12 个世界物件。
- 投射物、地表、7 个装备图标、2 个世界血条纹理和原型回退纹理。
- 21 个 Animation Clip、相关 Prefab、`Main.unity`、USS、测试和文档引用。
- 8 张 Phase5 历史 Sheet 在零依赖验证后退役。

不包含：

- 战斗标签改进计划。
- 玩法数值、Collider、交互范围、场景布局或动画时序调整。
- 第三方插件纹理、文档参考图和历史验收截图。
- 对现有视觉内容进行风格重绘；本阶段只做可测量、可复现的无损或家族统一导出处理。

## 冻结合同

### 通用导入

- 每个 PNG 只导入一个 Sprite，`Sprite Mode: Single`。
- sRGB、Point、无压缩、关闭 Mipmap、Wrap Clamp、Alpha Is Transparency、Mesh Full Rect。
- 导入设置由 Editor 自动化写入，不使用 Sprite Editor。
- 既有运行时 Transform 缩放仅作为本次保持玩法尺度的历史例外；不得新增逐资产 scale-to-fit。

### 家族规格

| 家族 | 画布 | 处理与锚点 | PPU |
| --- | --- | --- | ---: |
| 玩家、三类怪物、商人动画帧 | `128×128` | 每个家族使用同一完整画布缩放因子；把旧对齐信息烘焙到像素，统一 Pivot 为 `(0.5, 12/128)` | 64 |
| 投射物 | `96×96` | 保留现有 64×64 Pilot 的像素尺寸，完整画布居中补透明边；不放大主体 | 64 |
| 固定 UI 图标 | `96×96` | 12 图统一使用 `0.30` 全家族导出因子，再以 Alpha 加权中心放入画布；不得逐图 scale-to-fit | 64 |
| 装备图标 | `96×96` | 保留现有主体尺度；只允许平移修正视觉中心 | 64 |
| 世界物件 | `384×384` | 保留原像素，不缩放；独立对象居中补透明边并统一 Center Pivot | 181 |
| UI 面板 / 槽位状态 | 各组件现有精确画布 | 一图一组件；保留原像素、中心 Pivot 和已验证 Slice，分别冻结合同 | 100 |
| 世界血条 | `64×8` | 背景保持全画布；Fill 居中补边为同画布 | 64 |
| 地表 | `512×512` | Full-bleed、Repeat、无 Alpha 要求 | 64 |
| PrototypeSquare | `64×64` | 保留为非生产回退资产，统一 Point / 无压缩 | 100 |

角色采用 `128×128`，是因为按既有世界尺度统一导出后，玩家、基础怪物和迅捷怪物的 Release 帧宽度约为 103–106 px。继续使用 `96×96` 会造成裁切或迫使单帧缩放；扩大统一画布可以保持主体尺度、动作完整和共享 Pivot。

## 自动化产物

- 一份清单同时记录旧 Sheet GUID / fileID、源 Rect / Pivot、目标单图路径和家族合同。
- 一套确定性图像处理脚本，可从 `Docs/docs/assets/visual-style/source/` 中的不可变源图重复生成生产单图。
- 一套 Unity Editor 迁移工具，负责 Importer、AnimationClip、Prefab、Scene 和 ScriptableObject 引用重绑。
- 每个家族的审计 Manifest；JSON 报告运行时生成到 `Temp/VisualAssetAudits/alpha-0.1/reports/`，不纳入版本控制。
- 集成测试验证单图数量、画布、Importer、引用、旧 GUID 清零和二次运行幂等性。

## 实施顺序

1. 冻结迁移清单和家族合同，完成只读依赖盘点。
2. 生成全部目标 PNG，逐图运行尺寸、Alpha、中心、边距和组间差异审计。
3. 导入目标 Sprite，并验证每张图只有一个 `21300000` 主 Sprite。
4. 重绑 21 个 Animation Clip、8 个相关 Prefab、`Main.unity`、两份 USS 和其他对象引用。
5. 运行旧 GUID / 路径和 `AssetDatabase` 依赖扫描；存在任一旧消费者时禁止删除 Sheet。
6. 删除 8 张运行时 Sheet，再次导入、扫描、编译和测试。
7. 在 `1920×1080` 下复验世界、动画、投射物、掉落、HUD、背包、商店和打造界面。
8. 同步视觉规范、资产清单和 alpha 计划入口；完成后归档本计划。

## 完成结果

- 80 个旧 sub-sprite 已迁移为 80 张独立 PNG；连同原有单图，`Assets/Art` 当前共 92 张 PNG。
- 75 个既有消费者已重绑；5 个未消费资源按语义独立保留。
- 8 张 Phase5 历史 Sheet、对应 `.meta` 与空 `Assets/Art/SpriteSheets/` 目录已删除。
- `Assets/Art` 中 `Sprite Mode: Multiple` 数量为 0，旧 Sheet GUID / fileID 消费者数量为 0。
- 16 份静态合同覆盖 92/92 个 PNG，审计结果为 0 errors / 0 warnings；确定性生成二次运行为 `changed=0`。
- EditMode 104/104 通过，`Phase5VisualIntegrationTests` 17/17 通过；受 Addressables 加载器影响的 PlayMode 冒烟用例 1/1 通过。
- `SpriteAssetLoader` 与 `PrefabAssetLoader` 改由 `AssetReference.ReleaseAsset()` 释放内部 OperationHandle，重复进入场景或复跑测试不再复用失效句柄。
- 全量 PlayMode 的一次串行运行出现既有测试顺序波动，失败用例单独复跑通过；该波动未涉及旧 Sheet 或新 Sprite 引用。
- 已在 16:9 运行时画面下复核角色、怪物、商人、打造台、世界物件与 HUD；验收截图保存在 `Docs/docs/assets/visual-assets/alpha-0.1/acceptance/`。

## 成功标准

- `Assets/Art` 中不存在生产 SpriteSheet 和 `Sprite Mode: Multiple`。
- 80 个旧 sub-sprite 全部有唯一目标单图，75 个既有消费者全部重绑，5 个未使用资产仍按语义名保留。
- 投射物与 12 个固定 UI 图标均为 `96×96`；角色 52 帧均为 `128×128`。
- 所有生产 PNG 的 Importer、画布、Pivot、PPU 和 Slice 与合同一致。
- 旧 Sheet GUID、路径和 fileID 在 Assets 消费者中为零；Animation Clip 帧时间与状态机参数不变。
- 图像审计、程序集编译、EditMode、PlayMode、Console 和 `1920×1080` 实机截图均通过。
- 重跑生成、导入和迁移工具不会产生额外差异。

## 停止条件

- 任一目标内容触边、被裁切或因单图 scale-to-fit 改变相对尺度。
- 任一动画帧遗漏、帧序改变、旧引用残留或目标 GUID 丢失。
- Prefab / Scene 存在未保存用户修改、缺失脚本或无法安全通过 Editor API 保存。
- UI Slice 穿过角件，或固定图标在真实控件尺寸下出现模糊、跳变和视觉中心漂移。
- 运行时 Collider、交互范围、场景位置或玩法事务因美术迁移发生变化。
