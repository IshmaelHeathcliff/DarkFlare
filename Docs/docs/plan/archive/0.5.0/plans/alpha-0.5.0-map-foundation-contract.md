# Alpha 0.5.0：首轮数据与导入合同

状态：A 数据与导入切片已完成，2026-09-18 归档。当前实现以[地图文档模块](../../../../maps/README.md)为准。[0.5.0 执行计划](alpha-0.5.0-map-foundation-plan.md)继续推进 B/C/D，本归档不代表地表显示或整个 0.5.0 已验收。以下保留 2026-09-17 首轮工程核对与实施合同。

## 已核对的工程事实

- `ContentDefinitionMetadata.cs` 通过 `ContentDefinitionAttribute` 登记类型命名空间，资产实现 `IContentDefinition.Id`；`ContentCatalogDefinition.CatalogId = core` 是目录身份，不能代替类型命名空间。
- `ConfigurationTypeDiscovery` 只发现带 `DarkFlare/Data/` CreateAssetMenu 的可创建 SO；当前配置中心直接编辑资产，不能原样用于只读的导入地图。
- 连接中的 Unity `6000.6.0f1` Editor 在 Bootstrap、非 Play；通过 AssetDatabase 只读读取了四个 Prefab，未保存资产或切换场景。

| Prefab | 本地 Circle 半径 | 世界缩放 XY | 有效半径 |
| --- | --- | --- | --- |
| Player | 0.4 | 0.8 | 0.32 |
| Monster_Basic | 0.4 | 0.75 | 0.30 |
| Monster_Swift | 0.4 | 0.9 | 0.36 |
| Monster_Heavy | 0.4 | 0.75 | 0.30 |

四者主 Sprite 画布均为 128×128、PPU64；画布不是 Alpha 可见尺寸。Main 场景文件中的正交相机 size 为 5，本次未打开 Main 实测画面。角色根缩放保持原样，不因新地表顺手调整美术或碰撞。

## 首轮尺度

逻辑格先采用 1 世界单位；导航子格优先比较 0.25 与原建议的 0.5。对于 0..1 的直通道，居中采样的 0.5 子格中心为 0.25 / 0.75，距最近墙只有 0.25，可能把半径 0.30–0.36 的单位全部判阻挡。0.25 子格具有 0.375 / 0.625 的内部采样点，但仍需把实际安全边距纳入验证，不能只凭更细就声称可达。

首轮建立 0.75、1、1.5、2 单位宽通道，以及转角 / 水岸 / 原点偏移对照。用连续 footprint 净空作为参考，导航节点检查中心处足迹，边检查完整移动线段；不要同时膨胀障碍又重复加一次单位半径。正式水岸几何完成后才冻结导航分辨率。地图 JSON 不保存导航子格数组，A* 与 Flow Field 以后读取相同编译结果。

地表技术调色板可先按 64×64 / PPU64 验证 1 单位显示块；正式生成画布、视觉密度、共享边采样带和导出规格在 Pilot 阶段测量后冻结。本轮没有生成或批准正式地表图。

## 文件与身份

工程布局源使用 `Assets/Data/Maps/<name>.dfmap`，内容为规范 JSON。专用后缀供 ScriptedImporter 注册；交换导出可以是 `.json`，导入按钮验证后写回 `.dfmap`。不注册通用 `.json` 导入器，也不依赖 `.dfmap.json` 被当作独立类型。Unity 对原生格式与自定义格式采用不同导入路径，见[官方导入器说明](https://docs.unity3d.com/6000.0/Documentation/Manual/ScriptedImporters.html)。

| 定义（计划类型） | 命名空间 / 示例 | 创建方式 |
| --- | --- | --- |
| MapDefinition | `map:foundation_sample` | `.dfmap` 导入生成，不提供创建独立可编辑 SO 的菜单 |
| TerrainDefinition | `terrain:dirt`、`terrain:grass`、`terrain:water`、`terrain:road` | Odin SO；本地 ID 不重复包含命名空间 |
| TerrainSetDefinition | `terrain_set:basic` | SO，保存有序地表定义与 Wang / 变体引用 |
| MapObjectDefinition | `map_object:trader` | SO，以类型化能力组合障碍 / 装饰 / 交互 |
| MapRegionDefinition | `map_region:spawn` | SO，保存区域语义及合法参数 |

正式目录是否加入新内容由内容交付阶段处理。第一切片使用隔离样例和测试目录，不提前提升 core 版本或污染正式目录；但必须验证这些类型能够通过现有 ContentCatalog 构建与稳定 ID 校验。

## 文档结构 V1

首期使用固定类型 DTO，属性 / 集合均有上限；不引入自由对象图或多态运行时代码反序列化。

V1 拒绝未知字段、重复 JSON 属性、缺少必填集合、非有限数值与未知格式版本；禁止 TypeNameHandling 自动创建类型。先限制输入字节和深度，再反序列化和分配地图数组。容量参数集中配置，第一切片至少接受总计划的 256×256 逻辑格压力样例，并为实际采用的边界补充超限拒绝用例。

| 字段 | 合同 |
| --- | --- |
| formatVersion / revision / seed | 格式 V1；非负文档修订；明确范围的整数种子。格式升级和内容修订分别管理 |
| mapId / terrainSetId | 完整类型化 ContentId，与目录和定义类型匹配 |
| origin / cellSize / width / height | 有限原点与正格宽，正整数尺寸；结构层先检查容量和乘法溢出再分配 |
| terrainPalette / defaultTerrainId | 有序 TerrainId 表；默认值必须在表内，目录不匹配或重复项拒绝 |
| chunks | 固定边长 32 的逻辑块；chunk 坐标不得重复。每块 cells 为 1024 个 palette 索引，行序 `x + y*32`；地图末端外侧槽固定为默认地表，禁止藏越界内容 |
| variantOverrides | 显示格坐标、兼容 VariantId；无效 Key / 材质组合拒绝；缺省由稳定 Hash 决定 |
| objects | 唯一 objectId、definitionId、地图局部世界位置、合法旋转、可选外观变体、类型化交互绑定；地图头 origin 只加一次 |
| regions | 唯一 regionId、definitionId、优先级、矩形 / 简单多边形及类型参数；区域坐标用逻辑格空间，可包含有限小数 |

障碍、装饰、交互为 objects 的不同能力，不拆成可重复身份的三套对象列表；编辑器仍展示三个语义图层。运行期可变开闭 / 已交互状态属于地图运行快照，源布局只记录初始配置。

最小样例（定义需由测试目录提供）：

```json
{
  "formatVersion": 1,
  "mapId": "map:foundation_sample",
  "revision": 0,
  "seed": 1,
  "origin": { "x": 0, "y": 0 },
  "cellSize": 1,
  "width": 4,
  "height": 4,
  "terrainSetId": "terrain_set:basic",
  "terrainPalette": ["terrain:dirt", "terrain:grass", "terrain:water", "terrain:road"],
  "defaultTerrainId": "terrain:dirt",
  "chunks": [],
  "variantOverrides": [],
  "objects": [],
  "regions": []
}
```

小图缺省为空对象 / 区域是结构合法状态，不代表城镇 / 野外玩法验收合格；实际地图交付另检查出生点、出口和必达交互。JSON Schema 与 C# 语义校验应共享枚举 / 字段来源，或保留一致性用例，避免两套规则漂移。

## 第一个实现切片

1. 在既有 Runtime 程序集下增加 `Data/Maps` 和纯文档服务，先完成 DTO、错误定位、规范序列化、不可变编译快照；不提前建立尚无消费者的 MapSystem / MapScope。
2. 在 `Editor/Maps` 增加 `.dfmap` 导入器及所需定义查询适配。核心服务接受显式定义查询接口，不能在 Runtime 使用 AssetDatabase。
3. Importer 固定主资产键，如 `map_definition`；子资产如有则按语义 ID 命名，不使用遍历序号。重复导入保持 GUID / local file ID，引用变更有可观察重建。
4. 将内容解析放在独立 Editor 定义索引中，避免地图导入依赖“包含这张地图的正式 ContentCatalog”造成循环。按实际读取的源 / 导入结果登记依赖；新增、删除、改名 / ID 冲突通过受控索引修订使相关地图失效。Importer 不扫描并重写全项目，不在 OnImportAsset 内调用 SaveAssets / Refresh 形成重入。
5. 配置中心保留现有 SO 创建行为，增加地图专用入口：新建源文件、打开源、只读导入摘要、错误列表；不把导入 MapDefinition 伪装成可直接改字段的配置。完整绘制工作台留到 0.5.1。
6. 接入内容校验和隔离测试，验证导出 / 重导入、失败及资源释放。源无效时明确标记导入失败并阻止该地图发布；旧预览可显示为过期，但不能把缓存成功结果当当前有效产物。

首轮不生成全套 256 Tile，也不替换 Main 地表。完成数据切片后，继续 0.5.0 的 Wang 解析、可见预览、正式资产和几何切片。

## 切片完成条件

纯规则测试覆盖规范 JSON 往返、palette / chunk 解析、对象能力组合、区域、错误路径、未知字段 / 版本、缺引用、容量和非有限数值。Importer 测试验证稳定身份、依赖重导入、缺资产恢复、源损坏和不产生第二份布局资产。结构结果以语义验证，不通过固定文件 / 字段数量断言代替行为。

测试复用 ContentIdentityTests、配置覆盖及相关工具设施；新地图纯规则按 MapDocument 等模块命名。保留正式 Main 尚在使用的旧 GroundTilemapTests，等实际消费者迁移后才修改其旧布局断言。全部测试使用隔离临时资产 / 数据目录，最终清理新增的临时文件与 `.meta`。

上述通过只标记“数据与导入切片完成”，不标记整个 0.5.0 完成或提前升级运行版本。

## 完成记录

2026-09-18：完成 `.dfmap` 源、固定类型 DTO、不可变快照、规范 JSON 与 SHA256、五种内容命名空间、Odin 定义与地图专用入口、严格校验、稳定导入资产、失败报告、定义依赖恢复和 Player 构建前校验。规范 Schema 从 DTO 属性和枚举生成，语义校验仍由 C# 编译器执行。

35 项地图长期测试全部通过；完整 EditMode 603/603、PlayMode 75/75，内容校验与配置字段文档覆盖通过。包括 256×256 全块压力文档、对象复合能力、区域几何、变体兼容、源损坏、事务失败不覆盖、GUID/localID 稳定及新增 / ID 修改 / 冲突删除恢复。既有配置发现回归明确排除不可手工创建的 MapDefinition；未删除旧 Main 地表覆盖。

实际 V1 的交互绑定仅实现 Trader/Crafting，区域参数实现 Spawn 定义绑定；出口、出生点与运行状态在双地图阶段扩展。当前定义索引读取的源均登记依赖，尚未按 ID 分片优化。没有生成正式美术、替换 Main、改变运行版本或加入正式 core 内容目录。PlayMode 与后续验证记录见模块文档。
