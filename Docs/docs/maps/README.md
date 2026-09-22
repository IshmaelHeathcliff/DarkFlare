# 地图文档与导入

地图运行时导航见[批量导航与怪物移动](./navigation.md)，切图与冻结见[双地图生命周期](./world-map-lifecycle.md)。

Alpha 0.5.0 地图基础已完成，当前包含文档服务、SO 定义、严格校验、规范导出、只读导入、地表资源发布与[静态几何 / 通行查询](./map-geometry.md)。已增加[手绘 / AI 地图编辑器](./map-editor.md)，Main 已接入[城镇 / 野外及地图生命周期](./world-map-lifecycle.md)，正式怪物使用[批量导航](./navigation.md)绕障。`0.5.5-alpha` 的 [I 综合交付](./acceptance/i/README.md)已完成：当前地图制作、双地图 Player、历史存档和组合压力通过，执行计划已归档。

B 阶段已完成：[共享坐标、四角 Key 与稳定变体规则](./terrain-resolution.md)、[分区、边签名与增量解析](./terrain-partition.md)、[独立地表诊断预览](./terrain-preview.md)及[持久样例和压力记录](./terrain-samples.md)。地图校验与解析共用四角采样。C1 的[正式地表家族合同](./terrain-art-contract.md)和[泥土源 Pilot](./acceptance/c1/README.md)已完成；C2 的[确定性纹理编译与正式预览](./terrain-texture-compilation.md)已完成，真实泥土样例可从地图菜单打开；C3 的[四材质双变体与资源发布](./terrain-materials.md)已完成，[C4 正式样例与压力验收](./acceptance/c4/README.md)也已完成，C 已提交 `9a3f7a9`，[D 同源碰撞与静态导航几何](./map-geometry.md)也已完成：默认 4×4 子格、圆足迹 / 线段 / 落点查询、碰撞投影及预览。D 已提交 `e396485`。[地图编辑器 E1–E5](./map-editor.md)已完成，[世界对象表现与分段排序](./object-presentation.md)阶段 F 也已完成，正式双地图与作用域已接入并完成 [G 验收](./acceptance/g/README.md)。

## 唯一源与内容身份

- 布局源：`Assets/Data/Maps/<name>.dfmap`，UTF-8 JSON；交换导出使用 `.json`。
- ScriptedImporter 固定主资产键 `map_definition`，生成 MapDefinition；源文件 `.meta` 决定 GUID，重新导入保持 local file ID。
- `MapDefinition.Id` 是本地 ID，完整身份为 `map:<id>`。地表、地表集、对象、区域分别使用 `terrain`、`terrain_set`、`map_object`、`map_region`。
- 定义资产按[地图配置参考](../config-reference/maps.md)创建。正式双地图及其依赖进入 core v6 内容目录。

## V2 交换合同

[最小样例](./sample.dfmap)使用四种地表，须先建立对应定义。[JSON Schema](./map-document.schema.json)由 `MapDocumentJson.CreateSchema()` 从 DTO 生成，与读取器共享属性、枚举和集合上限；它检查结构，引用、坐标、拓扑和几何必须继续运行 `MapDocumentCompiler.Compile`。

字段全部必填，允许空值的字段仍须显式写 `null`。枚举采用精确名称字符串，不接受数字或大小写转换；拒绝未知字段、重复属性、缺字段、类型强制转换、非有限数值和尾部第二份文档。当前输出 formatVersion=2，V1 读取时在内存补齐入口与空出口字段，不修改源文件；revision 非负，seed 为 Int32。

| 数据 | 规则 |
| --- | --- |
| origin / cellSize | origin 是世界平移，可为负；世界范围限制 ±1000000，格宽 (0,1024] |
| width / height | 每轴 1..1024，总格数 ≤1048576；先验容量再分配网格 |
| terrainPalette | 1..64 个不重复完整 TerrainId，必须属于选定地表集；顺序是 chunk 索引含义 |
| defaultTerrainId | 必须在 palette 中；省略块和地图边界虚拟样本使用它 |
| chunks | 固定 32×32，1024 槽，索引 `x+y*32`；块坐标从零起，不可重复；末端地图外填充必须为默认地表 |
| variantOverrides | 显示格范围 x=−1..width−1、y=−1..height−1；左下、右下、左上、右上取逻辑样本 (x,y)、(x+1,y)、(x,y+1)、(x+1,y+1)；显示中心为 origin+((x+1),(y+1))×cellSize；兼容四角的稳定变体 ID，不接受重复覆盖 |
| objects | 地图内唯一 objectId，definitionId、局部世界 position、rotation=0/90/180/270、可空 appearanceVariant、可空 interaction；障碍足迹不可越界 |
| interaction | Trader / Crafting 的 contentId 指向对应定义，目标字段为 null；Portal 的 contentId 为 null，targetMapId / targetEntryId 必填；不接受任意参数包 |
| entries | 最多 256 个唯一 entryId，kind 为 Arrival / NewGame / Respawn，position 为局部世界坐标；正式地图入口 / 出口跨图校验见生命周期模块 |
| regions | 唯一 regionId、definitionId、Int32 priority、Rectangle/Polygon、points、可空 spawnDefinitionId；坐标为逻辑格，可含小数 |
| Rectangle | points 为左下和右上两个点，面积为正 |
| Polygon | 3..64 点，顺时针或逆时针均可；不重复首点；不允许自交、自接触、相邻共线或零面积 |
| Spawn 区域 | spawnDefinitionId 必填并解析现有 MonsterSpawnDefinition；其他区域必须为 null |

对象和入口 position 尚未叠加 origin，消费者只能在局部转世界时加一次。源布局不保存对象运行时状态；离场冻结使用独立 [MapScope / 快照合同](./world-map-lifecycle.md)。

单文件读取上限 8 MiB、嵌套深度 16、字符串长度 256；对象 ≤16384、区域 ≤1024、块 ≤1024。规范输出也受 8 MiB 限制，大图应保留默认块的稀疏表示；256×256 全块样例属于已覆盖范围。

## 调用与失败行为

`MapDocumentJson.Read` → `MapDocumentCompiler.Compile(dto, lookup)` → `CompiledMapDocument`。核心服务不调用 AssetDatabase；调用方显式提供 `IMapDefinitionLookup`。MapDocumentException 提供字段路径和原因，首个错误即停止，无半成品网格对外发布。

快照拥有独立网格、规范 JSON 和 SHA256，`GetTerrain` / `GetTerrainId` 提供只读地表查询，头字段与覆盖查询可直接读取，`CreateEditableCopy` 返回脱离快照的 DTO。规范导出按名称排序属性，按坐标排序块 / 覆盖，按稳定 ID 排序对象 / 区域；palette 和多边形顶点保留语义次序。SHA256 表示规范源内容，不是定义版本或导航缓存键；候选规则版本另见地表规则模块。

Editor 定义索引只查询地表 / 对象 / 区域及交互、刷怪定义，不读取地图或 ContentCatalog，避免包含地图的目录形成导入环。索引读取过身份的定义登记源依赖，成功解析的定义额外登记产物依赖；因此缺引用导入仍能观察已有 SO 的 ID 恢复。新增 / 删除定义通过索引 Hash 失效，在导入回调外注册并受控刷新；当前改动任一索引定义可能重导入多张地图，尚未实现按 ID 分片索引。遵循 [Unity 依赖 API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetImporters.AssetImportContext.DependsOnCustomDependency.html)。

坏源替换为 TextAsset 错误报告，旧 MapDefinition 不继续有效；引用修复后自动恢复。配置中心内容校验检查所有地图源和产物，标准 Player 构建前阻断错误地图。后续 Addressables 地图发布流程必须复用同一校验。

## 编辑入口

打开 `DarkFlare/配置中心` → `地图文档`：选择地表集、输入新地图 ID 和尺寸以创建 `.dfmap`；选择地图源可以读取摘要、打开文本源、导入 / 导出交换 JSON，或打开独立地表诊断预览。写入前完整校验，同目录临时文件替换后再导入，失败不覆盖旧源。

SO 字段仍在“按类型”页编辑；地图没有独立 CreateAssetMenu，不允许直接修改只读布局产物。完整画笔、SceneView 舞台、Undo 和 AI Patch 事务已由[地图工作台](./map-editor.md)提供；配置中心可跳转进入。有编辑会话的地图必须在工作台导入 / 保存，旧入口拒绝旁路覆盖。

## 验证

2026-09-18 F 收尾：EditMode **707/707**、PlayMode **78/78**，内容与配置覆盖通过，版本 `0.5.2-alpha`。对象表现、独立 Windows Player 与性能边界见 [F 验收](./acceptance/f/README.md)。


2026-09-18 E 收尾：EditMode **697/697**、PlayMode **77/77**，内容校验 0 项问题。当前版本 `0.5.1-alpha`；地图编辑器、输入与性能见 [E 验收](./acceptance/e/README.md)。以下保留 A–D 历史验证记录。

2026-09-18 D 收尾：EditMode **676/676**、PlayMode **77/77**，内容校验 0 项问题。物理余量、离散窄路误拒绝、几何容量及未构建 Player 的边界见 [D 验收](./acceptance/d/README.md)。下段为 B 阶段历史记录。

2026-09-18：B4 完成后项目完整 EditMode **650/650**、PlayMode **75/75** 通过；A 的 35 项地图回归及 40 项地表规则回归、B3 的 7 项[预览与导入刷新回归](./terrain-preview.md#验证与边界)保留。内容校验 0 项问题，配置文档字段覆盖通过。1920×1080、原始像素与 256×256 压力记录见 [B4 验收](./acceptance/b4/README.md)，其中单独记录工具附带运行的包测试。本轮未构建 Player。

长期测试：MapDocumentTests、MapDocumentImporterTests，以及内容 ID、配置文档覆盖和现有地图回归。新增测试覆盖数据完整性、导入稳定身份、缺引用 / ID 改名 / 冲突恢复、失败不覆盖旧源，以及不同区域设置的规范交换。导入器测试使用唯一临时目录，结束删除源、配置和 `.meta`，不向正式内容池写入测试资产。原配置发现测试修正“所有登记类型均可手动创建”的假设，明确验证导入地图不能另建布局资产；旧 GroundTilemapTests 保留。

2026-09-18 G 收尾：版本 `0.5.3-alpha`、core v6、Schema 4、地图文档 V2。EditMode **713/713**、PlayMode **83/83**，正式双地图、冻结恢复、独立进程继续与 20 轮往返通过，见 [G 验收](./acceptance/g/README.md)。

2026-09-19 H 收尾：`0.5.4-alpha`、core v6、Schema 5；EditMode **722/722**、PlayMode **85/85**。批量导航、动态阻挡、500 / 1000 单位 Player 矩阵与 20 轮大图往返通过，见 [H 验收](./acceptance/h/README.md)。
