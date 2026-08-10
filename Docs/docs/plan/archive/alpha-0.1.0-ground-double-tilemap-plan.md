# alpha 0.1.0 地表双网格 Tilemap 计划

> 状态：已完成
> 完成日期：2026-08-10

## 目标

用双层 Tilemap 替换 `Main.unity` 中 25 个重复的地表 `SpriteRenderer`，保持现有 40×40 世界单位
视觉覆盖和 32×32 `WorldBounds` 不变，并生成一套符合单图生产规范的新地表 Tile。

## 场景结构

```text
GroundGrid                    Grid，位置 (-20, -20, 0)，Cell Size (8, 8, 0)
├── GroundBaseTilemap         5×5 全覆盖，Sorting Order -100
└── GroundDetailTilemap       稀疏装饰，Sorting Order -99
```

- 基础层负责连续可行走地表，不包含碰撞。
- 细节层只放置透明裂纹、碎石与灰烬，不改变玩法规则。
- 旧 `VisualSliceGround` 仅在新 Tilemap 创建、填充和校验成功后从场景移除。

## 冻结资产合同

### 基础地表家族

| 项目 | 合同 |
| --- | --- |
| 资产 | `ground_base_stone_00..02.png`，每张一个完整 Tile |
| 路径 | `Assets/Art/Sprites/Environment/GroundTiles/Base/` |
| 风格参考 | `Assets/Art/Sprites/Environment/VisualSlice/ground_slice.png` |
| 生成 / 交付 | 内置生成器固定输出 1254×1254；全画布统一 Lanczos 导出为 512×512 |
| 视觉 | 俯视暗黑荒原石地，石板、泥土与少量枯草；低饱和石墨灰、焦褐和暗铜色 |
| 接缝 | 全画布不透明；对边像素一致；三个变体共享 Pilot 外 16 px 边缘，64 px 内自动羽化 |
| Unity | Single、PPU 64、Pivot Center、Point、Compression None、MipMap Off、Clamp、Full Rect、Scale 1 |
| 运行时 | 每格 8×8 世界单位；任意基础变体可相邻 |

### 地表细节家族

| 项目 | 合同 |
| --- | --- |
| 资产 | `ground_detail_cracks_00.png`、`ground_detail_rubble_00.png`、`ground_detail_ash_00.png` |
| 路径 | `Assets/Art/Sprites/Environment/GroundTiles/Details/` |
| 风格参考 | 基础 Pilot 与现有 `ground_slice.png` |
| 生成 / 交付 | 内置生成器固定输出 1254×1254 纯色抠图源；统一去背景后全画布 Lanczos 导出为 512×512 |
| 主体 | Alpha 覆盖率 3%–35%，可见宽高 160–416 px，四边透明留白至少 48 px |
| 中心 | Alpha 加权中心 x/y 均在 200–312 px |
| Unity | Single、PPU 64、Pivot Center、Point、Compression None、MipMap Off、Clamp、Full Rect、Scale 1 |

所有生产 PNG 均保持一图一 Sprite，不创建 SpriteSheet，不使用 Sprite Editor 手工切割或 Pivot 调整。

## 自动化

1. `tools/ground_tiles/process_ground_tiles.py` 统一完成全画布导出、基础层周期边缘处理和细节层缩放。
2. 复用 `audit_sprite_assets.py` 检查画布、Alpha、覆盖率、中心和留白。
3. `audit_ground_tiles.py` 额外检查基础层对边与跨变体共享边缘。
4. Unity Editor 工具自动配置 Importer、创建 `Tile` 资产、建立双层 Tilemap、确定性填充并保存场景。

## 验收

- 六张独立 PNG 和六个 Tile 资产全部存在，Importer 合同一致。
- 基础层 25 格全覆盖，细节层稀疏且不遮挡主要营地对象。
- 场景中只保留新的 `GroundGrid`，不存在旧 `VisualSliceGround`。
- 地表覆盖仍为 -20..20，`WorldBounds` 保持 -16..16。
- 原生像素审计、EditMode、PlayMode、程序集构建和 Unity Console 通过。
- 1920×1080 运行截图无明显接缝、空洞或规律性大块重复。

## 完成结果

- 六张独立生产 PNG 与六个 Unity `Tile` 资产已生成、审计和导入。
- `Main.unity` 已建立 25 格基础层与 11 格细节层，旧 `VisualSliceGround` 已退出运行时。
- 基础 Tile 的对边与跨变体共享边缘差值均为 0；细节透明留白和视觉中心全部通过合同。
- 地表专项 EditMode 19/19、PlayMode 1/1 通过；双层均无 Collider，`WorldBounds` 未改变。
- 当前模块说明见[双层地表 Tilemap](../../ground-tilemap.md)。
