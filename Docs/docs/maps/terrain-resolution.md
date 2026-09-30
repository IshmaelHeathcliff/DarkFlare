# Dual Grid 坐标与地表规则

本文描述 Alpha 0.5.0 B1：共享坐标、四角身份、不可变候选快照与确定性变体选择，已提交为 `0c866f7`。B2 的[分区、边签名与增量解析](./terrain-partition.md)、B3 的[诊断预览](./terrain-preview.md)及 B4 的[样例与压力记录](./terrain-samples.md)均已完成，见 [B 计划归档](../plan/archive/0.5.0/plans/alpha-0.5.0-wang-preview-plan.md)。本模块不创建纹理、Tile、碰撞或导航数据。

## 数据入口

`MapDocumentCompiler.Compile` 仍是地图结构 / 语义校验入口；地图 JSON V1 与导入资产身份不变。`CompiledMapDocument` 新增只读 OriginX、OriginY、CellSize、Seed、Revision、TerrainSetId、DefaultTerrainId，以及 GetTerrainId 和 TryGetVariantOverride。坐标保留 double 精度，覆盖查询返回不可变字符串，修改源 DTO 或 CreateEditableCopy 的结果不会修改快照。

地表规则从 `TerrainRuleSnapshot.Compile(terrainSet, lookup)` 建立。它先验证定义及引用身份，再复制 ContentId、四角和候选 ID，不持有 ScriptableObject。修改 / 销毁原定义不会改变已建立的规则快照；定义更新后由调用方重建规则。

## 坐标合同

`DualGridCoordinates` 同时供地图变体校验和规则解析使用，避免两套角顺序漂移。

| 操作 | 规则 |
| --- | --- |
| ContainsLogical | 0 ≤ x < width，0 ≤ y < height |
| ContainsDisplay | −1 ≤ x < width，−1 ≤ y < height |
| GetLogicalCenter | origin + (x+0.5,y+0.5) × cellSize |
| GetDisplayCenter | origin + (x+1,y+1) × cellSize |
| WorldToLogical | floor((world−origin)/cellSize)，地图外返回真实格坐标，不夹到边界；非有限值与 Int32 溢出拒绝 |
| GetDisplayChunk | 每轴 floor(coordinate/32)，例如 −1→−1、−32→−1、−33→−2；不改变源 chunk 编号 |
| GetAffectedDisplayCells | 合法逻辑格 (x,y) 返回四个显示坐标 (x−1,y−1)、(x,y−1)、(x−1,y)、(x,y)；本接口仅计算邻接，不代表已实现增量重建 |

采样 NW=`T(x,y+1)`、NE=`T(x+1,y+1)`、SE=`T(x+1,y)`、SW=`T(x,y)`。T 直接读取整张编译网格，跨 source chunk 不回退；只有地图外返回 defaultTerrainId。显示采样和中心查询拒绝无效显示格。

## 四角身份与变体

`TerrainWangKey` 保存 NW、NE、SE、SW 四个完整 terrain ContentId，支持相等比较及稳定字典 Hash，不把四种材质编号硬编码为 byte。单材质 GetMask 的对应位为 1、2、4、8；未出现在四角中的合法材质返回 0。无效或非 terrain 的 ID 不可构造 Key；规则查询还要求材质属于该地表集。

`TerrainRuleSnapshot.Resolve(map,x,y)` 校验地表集身份并读取共享四角。返回 TerrainVariantSelection：Key、VariantId、HasVariant、IsOverride。

- 有 override 时必须在当前规则中存在且四角一致，否则拒绝；定义变更不会静默重选覆盖。
- 无 override 时，将该 Key 的候选按 VariantId 的 Ordinal 顺序排列，再按稳定 Hash 取模选择。
- 没有候选时明确返回 HasVariant=false / VariantId=null，并保留原 Key；没有虚构 ID、材质回退或源文件写入。B2/B3 的诊断显示需显式处理此状态。
- 候选表是只读集合，候选列表重排不改变规则 Hash 或选择。候选新增 / 删除、角绑定改变属于规则变化。
- 无关坐标、对象或区域修改以及文档 revision 变化不会重新选择当前格的变体。

## Hash 边界

变体选择采用 FNV-1a 64：offset basis=`14695981039346656037`，prime=`1099511628211`，乘法对 2^64 取模。输入顺序为带长度的域字符串 `DarkFlare.TerrainVariant.v1`、seed、x、y、terrainSetId、NW、NE、SE、SW。整数为 32 位小端补码；字符串前缀为 32 位小端 UTF-8 字节数。ContentId 限定 ASCII，可逐字节处理，不为每格分配临时字符串 / byte[]。

固定参考向量：seed=−123、x=−1、y=32，地表集 `terrain_set:test_set`，四角依次为 `terrain:test_dirt`、`terrain:test_grass`、`terrain:test_water`、`terrain:test_road`，共 153 字节，结果 `03a5643bdc5a01e8`。该值由独立 BigInt 实现核对。

RuleHash 是 SHA256，输入包括规则域 `DarkFlare.TerrainRules.v1`、地表集 ID、有序地表 ID、按 ID 排序的候选及其四角，字段带长度 / 数量。它描述本模块的候选与 Key 配置，不包含分区、像素尺寸、颜色、Sprite 资源，也不包含步行 / 移动代价。B2 独立记录分区修订；后续显示和导航缓存必须补充各自依赖，不能把 RuleHash 或地图 SourceHash 单独当作所有派生数据的版本。

## 验证

2026-09-18 B1 提交前：TerrainResolverTests **20/20**、完整 EditMode **623/623**、PlayMode **75/75** 通过。内容校验 0 项问题，配置文档字段覆盖通过。后续最新结果见[B2 验证](./terrain-partition.md#验证)。本轮未构建 Player，尚未进行新地表的视觉验收。

新增长期测试覆盖实际地图四角采样的 256 组合、二元 16 模式、64 材质地表集、负原点 / 显示 chunk、跨 31/32 源 chunk、坐标往返和越界、候选顺序、覆盖冲突、稳定 Hash 及定义快照隔离。MapDocumentTests / MapDocumentImporterTests 保留导入数据完整性和失败恢复回归，没有删除旧 Main 地表测试。本模块测试不代表已通过可见接缝或寻路验收。
