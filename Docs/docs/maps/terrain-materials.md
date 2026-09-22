# 四材质与运行时地表资源

正式材质包配置位于 `Assets/Data/Maps/Terrain/`，地表集为 `terrain_set:basic`。泥土、草地、水、道路各有 `00` / `01` 两个纯地表变体；`00` 复用基准源，`01` 使用独立纹理。混合四角 Key 使用基准源。整套产物为 256 个基准 Key 加 8 个候选，共 264 个独立 PNG / Sprite / Tile。

源图统一位于 `Assets/Art/Sprites/Environment/TerrainSources/`，遵守[家族合同](./terrain-art-contract.md)。水的逻辑定义不可步行，其余可步行、代价为 1；这些字段通过[同源几何](./map-geometry.md)驱动碰撞和[导航网格](./navigation.md)。G 已将正式城镇 / 野外及其依赖登记到 core v6，Main 由地图作用域按当前 `.dfmap` 装配地表，资源生命周期见[双地图模块](./world-map-lifecycle.md)。

## 编辑、编译和发布

1. 编辑 `terrain_basic_sources.asset` 的材质源和变体绑定。源配置属于 Editor，不作为运行时加载入口。
2. 通过 Inspector 编译完整 Tile。输出目录与失败回退规则见[纹理编译模块](./terrain-texture-compilation.md)。成功编译只更新 `_compiled`，不会自动改变已发布目录。
3. 核对 `terrain_basic_presentation.asset` 的 Addressables 条目后，点击源配置的“发布已编译地表”。发布验证当前源摘要、完整规则覆盖、目录、地址和递归依赖；通过才更新 `_tiles`，失败恢复原引用。
4. 从 `DarkFlare/地图/打开正式四材质样例` 打开现有技术样例 `Assets/Data/Development/Maps/terrain_materials.dfmap`。地图菜单不自动覆盖源或重编译。

`TerrainPresentationDefinition` 在 `DarkFlare/Maps/Terrain Presentation` 创建，字段见[配置参考](../config-reference/maps.md)。它使用所引用地表集的身份，不另建 `IContentDefinition` 或重复内容 ID。`Validate()` 校验 RuleHash、全部 Key / 候选及 Tile 引用，拒绝缺项和多余项。

| 项目 | 固定值 |
| --- | --- |
| Addressables 地址 | `maps/terrain/basic`，由 `AddressFor(terrain_set:basic)` 得出 |
| 组 | `DarkFlare-Map-Terrain` |
| 标签 | `df.session`、`df.terrain` |
| 直接加载资产 | `terrain_basic_presentation.asset` |
| 运行时依赖 | 地表集、逻辑材质、编译目录、Tile、64×64 PNG / Sprite |
| 禁止依赖 | Editor 源配置、1254×1254 材质源、Development 样例 |

布局仍只保存 ContentId / VariantId，不保存图片地址或 Unity GUID。发布源与显示配置必须引用同一地表集，且一个显示配置只能有一个发布源。重复地址、源变化后未编译、编译后未发布、错误标签和开发源混入运行时依赖均由 `TerrainPresentationPublisher.Scan()` 报错。

扫描接入配置中心内容校验和标准 Player 构建前检查；独立执行 Addressables 构建时，应先调用同一扫描并在出现问题时停止。没有自动修改 AssetBundle 构建流程或发布网络资源。

## 加载与生命周期

消费者通过既有 `AddressableAssetService`，在自身 `AssetOwnerScope` 下 `AcquireAsync<TerrainPresentationDefinition>`，传入地址和取消令牌。成功后调用 `Validate()`，再使用 `Tiles.GetTile(key, variantId)` 精确取 Tile；失败不得回退为诊断色。

资源随 owner / lease 释放。多个 owner 可共享同一条目，关闭其中一个不能销毁另一方仍使用的资产；最后一个 owner 关闭后由资源服务释放加载句柄。不要直接销毁持久 Tile、Sprite 或 Texture。未来 MapScope 将持有对应 owner，目前仅完成资源合同和集成验证。

## 验证边界

[C3 验收](./acceptance/c3/README.md)记录完整源图、真实编译 PNG、原生像素、Main 临时采集和资源包验证。长期保留发布修订 / 失败回退 / 地址冲突，以及共享资源反复加载释放回归。截图采集脚本在验证后删除。

[C4 验收](./acceptance/c4/README.md)已完成水岸、道路、孤点、鞍点、三 / 四材质交点、跨块、大图压力和取消 / 生命周期。长期回归补充正式填充中的取消 / 关闭；技术样例仍不等于最终城镇或野外地图。
