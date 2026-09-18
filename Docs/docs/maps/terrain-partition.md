# 地表分区与增量解析

本文描述 Alpha 0.5.0 B2，基于[共享坐标与 Wang 规则](./terrain-resolution.md)提供纯技术分区、边采样签名和完整 / 局部解析。输入输出不包含 Unity Tile、纹理或场景对象；后续 B3 已提供[独立可见预览](./terrain-preview.md)。

## 分区采样

`TerrainPartition.Sample(key,u,v,denominator)` 使用有理数坐标，要求分母为正、u/v 位于 0..denominator。四角整数权重依次为 `(d-u)v`、`uv`、`u(d-v)`、`(d-u)(d-v)`，按相同 ContentId 合并后取最大者；平局按完整 ContentId 的 Ordinal 顺序决定。乘积与累加使用 Int64，Int32 最大分母也不溢出。

`Rasterize(key,size,cancellationToken)` 在参考采样之上生成不可变 `TerrainPartitionRaster`，边长 2..1024，像素 `(x,y)` 使用分母 `size-1`。最外行列精确位于 u/v=0 或 1，每个像素只归属一个原始角材质，无透明孔洞。它不依赖四材质编号、候选顺序或变体 ID；同一 Key 的所有变体共用相同分区和边。

`GetEdge` 返回只读材质采样序列，作为逐采样可比对的边签名：North / South 均左→右，East / West 均下→上。序列包含两个端点，比较时还须使用相同尺寸。共享边只受其两端材质影响，另一对角不会改变边。

分区修订为 `TerrainPartition.Revision`，需要改变权重或平局规则时必须更新。它属于显示依赖，不能单用地表候选 RuleHash 代替。鞍点的中心像素归属不代表两个区域可通行；足迹几何与导航仍未实现。

## 解析快照

`TerrainResolver.Resolve(map,rules,previous?,cancellationToken)` 接受已经完整校验的 `CompiledMapDocument` 和不可变规则。首次不传 previous，后续传上一次成功结果。返回 `TerrainResolution`：

- `Map`：本次输入快照，保留 SourceHash、地图头与最新 revision。
- `RuleHash` / `PartitionRevision`：本次规则与分区修订。
- `GetCell(x,y)`：显示坐标与 `TerrainVariantSelection`，后者包含 Key、VariantId、HasVariant、IsOverride。
- `IsFullRebuild` / `RebuiltCells`：是否全量以及按 y、x 排序的去重重算坐标。
- `ComparisonMilliseconds` / `ResolutionMilliseconds`：源比较及待重算列表生成、结果准备及规则求解的独立耗时。

无候选时继续保留 `HasVariant=false`，不会虚构或写回 VariantId。只有显式调用技术分区的诊断显示才可呈现该 Key；正式资源缺失处理仍由后续显示层负责。规则中已删除或不兼容的 override 会使整个调用失败。

## 失效与局部更新

| 输入变化 | 处理 |
| --- | --- |
| 首次，地图 ID、尺寸、origin、cellSize、seed、地表集、palette 顺序、默认边界、RuleHash 或分区修订变化 | 全量重建 `(width+1)×(height+1)` 个显示格 |
| 逻辑地表改变 | 重算四邻显示格，去重；包括地图外圈 -1 和真实源 chunk 两侧 |
| override 新增、修改、删除 | 重算其显示格，与地表影响合并 |
| 仅对象、区域、文档 revision 变化 | 不重算地表，但发布新 Map / SourceHash |
| 同一源 Hash 且所有依赖不变 | 跳过逐格比较，共享不可变选择数组 |
| 候选列表重排，同一候选 ID 与绑定集合 | RuleHash 不变，不触发刷新 |

当前比较为 O(逻辑格数 + 显示格数)。有局部变化时先复制整个选择数组 O(显示格数)，随后仅对 RebuiltCells 重新求解；零地表变化共享数组。耗时不包含 DTO 校验、JSON 编译、纹理生成或渲染。本模块未实现块级持久数组或可信 dirty 输入，不承诺端到端 O(1)。

函数没有全局缓存；上次成功结果由调用方持有。新数组先准备，成功后才返回完整快照；取消或异常不会修改 previous。比较 / 栅格化每行检查取消，求解每 128 格及发布前检查取消。B3 将通过 UniTask、请求代次和资源会话管理异步消费，处理迟到结果与过期画面；本模块不把同步计算伪装成异步操作。

后续纹理缓存必须额外纳入诊断颜色、尺寸、变体内部标记或正式 Sprite 资源修订，并按 Key / 变体共享资源，不能每个显示坐标分配纹理。

## 验证

2026-09-18 B2 收尾：TerrainResolverTests 专项 **40/40**、完整 EditMode **643/643**、PlayMode **75/75** 通过。内容校验 0 项问题，配置文档字段覆盖通过，该轮未构建 Player。最新结果见[B3 预览验证](./terrain-preview.md#验证与边界)；256×256 的耗时 / 内存记录与可见取消验收留在 B4。

B2 在既有地表模块套件增加 20 项行为覆盖，分文件维护分区与增量场景：独立 decimal 权重参考遍历全部 256 Key；所有兼容 Key 对的水平 / 垂直边与独立两端预期一致；大分母、平局、只读签名与非法输入；跨 31/32 源 chunk / 负显示边、连续编辑与缺省块恢复；override 增改删；头 / 规则失效、对象 / 区域零重算；取消和末格失效保持已发布快照。B1 的 20 项及既有导入、旧地表测试保留，没有新增版本专属套件。
