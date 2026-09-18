# 地图配置参考

地图源与导入流程见[地图文档模块](../maps/README.md)。正式双地图及其依赖已加入 core v6，Main 按 WorldMapConfiguration 装配当前图。四个内容定义在 `DarkFlare/Data/Maps/` 菜单创建；C3 正式四材质位于 `Assets/Data/Maps/Terrain/`，通过独立的[地表资源包](../maps/terrain-materials.md)发布。显示配置与 Editor 源配置使用 `DarkFlare/Maps/` 菜单，不另建内容身份。

所有 ID 均为小写 snake_case 的本地 ID，JSON 中使用带命名空间的完整 ContentId。配置属于共享内容定义，不保存地图实例状态。字段变更暂无历史资产迁移；不能静默重命名已经被地图引用的 ID。

## TerrainDefinition

创建菜单 `Terrain Definition`；命名空间 `terrain`，表示泥土、草地、水、道路等逻辑材质。当前消费者为地图文档编译、Wang 规则与正式纹理编译 / 预览；导航和运行时地图渲染留到后续切片。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_id` | string / null | 必填、唯一稳定本地 ID |
| `_walkable` | bool / true | 逻辑步行许可；当前保存定义，未接入导航 |
| `_movementCost` | int / 1 | 1..10000，供后续通行代价编译 |

## TerrainSetDefinition

创建菜单 `Terrain Set Definition`；命名空间 `terrain_set`，引用可共存的有序材质和兼容变体。次序保留，不用 Unity 实例 ID 作为随机种子。B 已提供解析和缺省变体选择；C2 正式纹理编译为所选材质生成完整 Key 基准和全部候选。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_id` | string / null | 必填、唯一稳定本地 ID |
| `_terrains` | TerrainDefinition[] / 空 | 1..64 项，无空项、重复或内容 ID 冲突 |
| `_variants` | TerrainTileVariant[] / 空 | 变体 ID 在此地表集中唯一；可暂为空，显式覆盖必须存在 |

## TerrainTileVariant

地表集拥有的嵌套记录，无独立资产或 CreateAssetMenu。四角顺序固定为左下、右下、左上、右上。该记录只保存兼容合同；Sprite / Tile 与源 PNG 分离，见[正式纹理配置](../maps/terrain-texture-compilation.md)。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_id` | string / null | 必填；地表集内唯一的 snake_case 变体 ID |
| `_southWest` | TerrainDefinition / null | 必填，左下；必须来自所属地表集 |
| `_southEast` | TerrainDefinition / null | 必填，右下；必须来自所属地表集 |
| `_northWest` | TerrainDefinition / null | 必填，左上；必须来自所属地表集 |
| `_northEast` | TerrainDefinition / null | 必填，右上；必须来自所属地表集 |

## TerrainPresentationDefinition

创建菜单 `DarkFlare/Maps/Terrain Presentation`。稳定 Addressables 入口为 `maps/terrain/<地表集本地 ID>`，由发布器更新编译目录引用；它不是内容定义，不写入 core 或存档。当前真实示例为 `Assets/Data/Maps/Terrain/terrain_basic_presentation.asset`，消费者通过既有 AddressableAssetService 的 owner / lease 加载并调用 Validate。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_terrainSet` | TerrainSetDefinition / null | 必填、与源配置相同；首期 1–4 材质；不应在发布后静默更改身份 |
| `_tiles` | TerrainTileCatalog / null | 发布器拥有的只读字段，必须覆盖全部 Key 和候选，与当前规则摘要一致；不引用高分辨率源 |

Editor 发布校验源修订、输出内容、地址唯一性、标签与运行时依赖；失败恢复旧引用。首次创建没有历史迁移。发布后的资源由 MapScope 加载用于正式 Main。

## MapObjectDefinition

创建菜单 `Map Object Definition`；命名空间 `map_object`。同一对象可组合障碍、装饰和交互能力，地图只保存一份 objectId。Inspector 使用中文字段，源导入时检查能力与绑定。交易和打造绑定现有内容定义，Portal 绑定目标地图与入口；MapScope 创建对应交互对象。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_id` | string / null | 必填、唯一稳定本地 ID |
| `_capabilities` | MapObjectCapabilities / Decoration | Obstacle、Decoration、Interaction 位组合；不可为空或包含未知位 |
| `_footprint` | Vector2 / (1,1) | 障碍以对象位置为中心的世界单位矩形尺寸；每轴正有限且 ≤1000000；按 90° 旋转；完整足迹不得越界 |
| `_interactionKind` | MapInteractionKind / None | None、Trader、Crafting；必须与 Interaction 能力一致 |
| `_appearanceVariants` | string[] / 空 | 唯一合法 snake_case ID；源可留 null 使用默认外观，显式 ID 必须在此表 |

## MapRegionDefinition

创建菜单 `Map Region Definition`；命名空间 `map_region`。源区域可为矩形或简单多边形。当前消费者包含语义校验器和[静态几何查询](../maps/map-geometry.md)：Safe 排除刷怪足迹，不阻挡行走；Spawn 落点必须完整位于指定区域。运行中的刷怪接入后续地图阶段。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_id` | string / null | 必填、唯一稳定本地 ID |
| `_kind` | MapRegionKind / Named | Named、Safe、Spawn；Spawn 要求类型为 spawn 的定义绑定，其余不得携带刷怪参数 |

## MapObjectPresentationCatalog

创建菜单 `Object Presentation Catalog`，在配置中心按类型编辑。目录只保存表现绑定，不保存 `.dfmap` 布局、对象 footprint 或交互数据。运行时由 Addressables owner 持有，编辑器使用相同引用定位纯表现 Prefab。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_entries` | MapObjectPresentationEntry[] / 空 | 对象定义不可重复；缺失绑定只阻止正式表现装配，不阻止纯布局编辑 |

## MapObjectPresentationEntry

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_definition` | MapObjectDefinition / null | 明确对象定义，不使用名称近似匹配 |
| `_appearances` | MapObjectAppearance[] / 空 | 必须有唯一默认项；所有定义中的外观 ID 必须覆盖，未知 ID 不可登记 |

## MapObjectAppearance

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_variantId` | string / 空 | 空表示默认外观，非空必须属于对应对象定义 |
| `_prefab` | AssetReferenceGameObject / null | 必填、Addressables 登记；资源只包含 Sprite 表现、显式排序单元和分段组件，不允许物理或玩法脚本 |

## WorldMapConfiguration

创建菜单 `World Map Configuration`，定义 Session 的默认入口及共享表现资源。正式布局由 `.dfmap` 导入；各图的交互和刷怪能力从对象绑定与区域派生。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_startingMap` | MapDefinition / null | 新游戏地图，须登记在正式内容目录 |
| `_startingEntryId` | string / new_game | 默认地图的有效入口 ID |
| `_terrainPresentation` | AssetReferenceT / null | 地表发布资源，每次 MapScope 持有独立 lease |
| `_objects` | MapObjectPresentationCatalog / null | 地图对象的共享表现绑定 |

## 校验与资源释放

内容校验及 `.dfmap` 导入均验证定义、引用、能力与布局。源损坏生成错误报告并撤销有效 MapDefinition，配置中心显示原因；标准 Player 构建前执行地图校验。新增缺失定义或删除冲突后会触发重新导入。

MapDefinition 是只读导入产物，不属于可创建配置，也不进入本文件的字段覆盖列表。仅由 `.dfmap` 生成，不能创建第二份 `.asset` 布局。地表显示目录已单独登记 Addressables；地图内容目录及运行时场景接入在后续阶段完成。

静态几何通过 `MapGeometryInput.Capture` / `MapGeometryCompiler.Compile` 按需派生，不增加可手工编辑的 SO。默认 4×4 导航子格与 0.0125 物理余量，容量和坐标精度限制见[模块合同](../maps/map-geometry.md)。预览中勾选“构建碰撞与通行”即可检查；导入成功不代表任意派生预算都可接受该地图。
