# 双层地表 Tilemap

## 当前状态

`Main.unity` 已用基础 / 细节双层 Tilemap 替换旧 `VisualSliceGround` 的 25 个独立 `SpriteRenderer`。视觉覆盖继续保持 40×40 世界单位，玩法边界仍由原 `WorldBounds` 提供，不新增碰撞、寻路或交互规则。

```text
GroundGrid                    Grid，位置 (-20, -20, 0)，Cell Size (8, 8, 0)
├── GroundBaseTilemap         5×5 全覆盖，Sorting Order -100
└── GroundDetailTilemap       11 格稀疏细节，Sorting Order -99
```

两层都使用 Default Sorting Layer、Sprite Lit 材质和 Chunk 渲染，不包含 `TilemapCollider2D`。基础层覆盖 `-20..20`，`WorldBounds` 继续保持 `-16..16`。

## 资产家族

生产资源遵守“一张 PNG、一个 Sprite”：

- `Assets/Art/Sprites/Environment/GroundTiles/Base/ground_base_stone_00..02.png`
- `Assets/Art/Sprites/Environment/GroundTiles/Details/ground_detail_{cracks,rubble,ash}_00.png`
- `Assets/Art/Tiles/Environment/Ground/` 下的六个同名 Unity `Tile` 资产

所有源图均为 512×512、64 PPU、Center Pivot、Single、Full Rect、Point、Compression None、MipMap Off、Clamp。基础图为不透明 RGB，细节图为透明 RGBA；旧 `ground_slice.png` 只保留为风格参考，不再有运行时消费者。

## 生成与审计

生成使用内置图像生成器，并同时引用旧地表与已通过 Pilot：

- 基础 Pilot：单张无边框、正俯视、可无缝平铺的暗黑荒原石地；冷灰石板、焦褐泥土和极少量枯草，不包含道具、光源、文字或界面元素。
- 基础变体：保持 Pilot 的材质、像素密度、光向和共享边缘，只改变内部石板裂缝、泥土与磨损分布。
- 细节 Pilot / 变体：单个正俯视裂纹、碎石或灰烬装饰簇，放在统一纯色抠图背景上，不生成地表底色、边框、文字或多个组件。

生成器固定输出 1254×1254，家族统一使用全画布 Lanczos 导出到 512×512；禁止按单张主体包围盒缩放。`tools/ground_tiles/process_ground_tiles.py` 负责确定性导出、基础 Tile 对边周期化、跨变体共享外 16 px 边缘和细节透明清理；`audit_ground_tiles.py` 负责额外的接缝检查。

审计结果：

- 三张基础 Tile 的水平与垂直对边差值均为 0，跨变体共享边缘差值为 0。
- 三张细节 Tile 的 Alpha 覆盖率为 13.7%–22.8%，四边透明留白均不低于 56 px。
- 六张资产均通过固定画布、PPU、视觉中心、留白、导入和单 Sprite 合同。

## 场景填充

基础层使用冻结的 5×5 确定性图案，三个变体均被消费：

```text
0 1 0 2 0
2 0 1 0 2
0 2 0 1 0
1 0 2 0 1
0 1 0 2 0
```

细节层固定放置 11 格，三个细节均被消费；只允许 0/90/180/270 度旋转与 0.38–0.62 Alpha。布局避开主要营地对象，并保持细节只承担材质变化，不承担玩法语义。

## 自动化入口

- Unity 菜单：`DarkFlare/地表/重建双层 Tilemap`
- Editor 工具：`Assets/Scripts/Editor/GroundTilemapSetup.cs`
- 资产处理：`tools/ground_tiles/process_ground_tiles.py`
- 资产审计：`tools/ground_tiles/audit_ground_tiles.py`
- 场景 / 导入测试：`Assets/Scripts/Tests/EditMode/GroundTilemapTests.cs`

重建工具先在 `GroundGrid_Staging` 完成创建和数量校验，成功后才替换现有 `GroundGrid` 或旧 `VisualSliceGround`，并以同名路径更新六个 Tile 资产。重复执行得到相同层级、填充和绑定。

## 验收结果

- 地表专项 EditMode：19/19 通过。
- Main 场景专项 PlayMode：1/1 通过。
- 基础层 25 格、细节层 11 格；无旧地表根对象、无 Tilemap Collider。
- 1920×1080 的 EditMode 与 PlayMode 截图均无空洞、明显接缝或大块机械重复。
