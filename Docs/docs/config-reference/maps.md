# 地图配置参考

地图源与导入流程见[地图文档模块](../maps/README.md)。本轮只建立数据配置，没有加入正式 core 目录，也未替换 Main 地表。四个顶层配置在 `DarkFlare/Data/Maps/` 菜单创建，测试使用隔离定义；正式资产以后放在 `Assets/Data/Preset/Maps/`。

所有 ID 均为小写 snake_case 的本地 ID，JSON 中使用带命名空间的完整 ContentId。配置属于共享内容定义，不保存地图实例状态。字段变更暂无历史资产迁移；不能静默重命名已经被地图引用的 ID。

## TerrainDefinition

创建菜单 `Terrain Definition`；命名空间 `terrain`，表示泥土、草地、水、道路等逻辑材质。当前消费者为地图编译器；导航和渲染消费留到后续切片。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_id` | string / null | 必填、唯一稳定本地 ID |
| `_walkable` | bool / true | 逻辑步行许可；当前保存定义，未接入导航 |
| `_movementCost` | int / 1 | 1..10000，供后续通行代价编译 |

## TerrainSetDefinition

创建菜单 `Terrain Set Definition`；命名空间 `terrain_set`，引用可共存的有序材质和兼容变体。次序保留，不用 Unity 实例 ID 作为随机种子。当前不要求全量 Wang Key；切片 B 增加解析与缺省变体选择。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_id` | string / null | 必填、唯一稳定本地 ID |
| `_terrains` | TerrainDefinition[] / 空 | 1..64 项，无空项、重复或内容 ID 冲突 |
| `_variants` | TerrainTileVariant[] / 空 | 变体 ID 在此地表集中唯一；可暂为空，显式覆盖必须存在 |

## TerrainTileVariant

地表集拥有的嵌套记录，无独立资产或 CreateAssetMenu。四角顺序固定为左下、右下、左上、右上。当前保存兼容合同，尚无 Sprite 引用或渲染器。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_id` | string / null | 必填；地表集内唯一的 snake_case 变体 ID |
| `_southWest` | TerrainDefinition / null | 必填，左下；必须来自所属地表集 |
| `_southEast` | TerrainDefinition / null | 必填，右下；必须来自所属地表集 |
| `_northWest` | TerrainDefinition / null | 必填，左上；必须来自所属地表集 |
| `_northEast` | TerrainDefinition / null | 必填，右上；必须来自所属地表集 |

## MapObjectDefinition

创建菜单 `Map Object Definition`；命名空间 `map_object`。同一对象可组合障碍、装饰和交互能力，地图只保存一份 objectId。Inspector 使用中文字段，源导入时检查能力与绑定。交易和打造只绑定现有内容定义，尚未生成游戏对象。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_id` | string / null | 必填、唯一稳定本地 ID |
| `_capabilities` | MapObjectCapabilities / Decoration | Obstacle、Decoration、Interaction 位组合；不可为空或包含未知位 |
| `_footprint` | Vector2 / (1,1) | 障碍以对象位置为中心的世界单位矩形尺寸；每轴正有限且 ≤1000000；按 90° 旋转；完整足迹不得越界 |
| `_interactionKind` | MapInteractionKind / None | None、Trader、Crafting；必须与 Interaction 能力一致 |
| `_appearanceVariants` | string[] / 空 | 唯一合法 snake_case ID；源可留 null 使用默认外观，显式 ID 必须在此表 |

## MapRegionDefinition

创建菜单 `Map Region Definition`；命名空间 `map_region`。源区域可为矩形或简单多边形。当前消费者是语义校验器，运行中的安全区与刷怪接入后续地图阶段。

| 字段 | 类型 / 默认 | 合同 |
| --- | --- | --- |
| `_id` | string / null | 必填、唯一稳定本地 ID |
| `_kind` | MapRegionKind / Named | Named、Safe、Spawn；Spawn 要求类型为 spawn 的定义绑定，其余不得携带刷怪参数 |

## 错误与所有权

内容校验及 `.dfmap` 导入均验证定义、引用、能力与布局。源损坏生成错误报告并撤销有效 MapDefinition，配置中心显示原因；标准 Player 构建前执行地图校验。新增缺失定义或删除冲突后会触发重新导入。

MapDefinition 是只读导入产物，不属于可创建配置，也不进入本文件的字段覆盖列表。仅由 `.dfmap` 生成，不能创建第二份 `.asset` 布局。正式内容目录和 Addressables 的交付在后续地图接入时完成。
