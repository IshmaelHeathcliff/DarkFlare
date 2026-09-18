# 正式地表纹理编译

Alpha 0.5.0 C2 消费[家族合同](./terrain-art-contract.md)中的完整材质源，生成独立 PNG / Single Sprite / Tile，再由既有[隔离预览](./terrain-preview.md)显示。C3 增加四材质各两变体与[运行时资源发布](./terrain-materials.md)。Main 场景资产尚未替换，地图生命周期与导航仍由后续阶段接入。

## 使用入口

1. 在 Project 中创建 `DarkFlare/Maps/Terrain Texture Sources`，选择地表集、完整基准材质与规则变体源。每张源图必须为原生 1254×1254 不透明 PNG。
2. 点击 Inspector 的“编译正式 Tile（整批成功后发布）”。进度窗可取消。必须先将配置保存为资产；不要修改编译目录内文件。
3. 打开 `DarkFlare/地图/地表诊断预览`，关闭 Key 画廊，选择 `.dfmap`，勾选“正式材质模式”并指定源配置。源过期、缺 Tile、导入设置或产物内容被改动都会拒绝本次发布，旧帧标记过期。
4. 已交付样例可直接从 `DarkFlare/地图/打开正式泥土样例` 打开。源、地表集、材质配置和布局位于 `Assets/Data/Development/Maps/terrain_pilot*`，不进入 core。菜单只打开已有配置，不自动覆盖或重新编译。

布局仍只保存 ContentId、Wang 变体 ID 与坐标；不写图片路径或 Unity GUID。选择地表集必须与正式目录相同，RuleHash 必须一致。诊断画廊继续使用诊断色；正式模式没有诊断色补洞。

## 配置与所有权

### TerrainTextureSources

Editor 专用 ScriptableObject，无内容 ID，不是运行时内容定义。Inspector 使用 Odin。

| 字段 | 含义 |
| --- | --- |
| `_terrainSet` | 既有 TerrainSetDefinition；首期正式编译支持 1–4 种地表，B 语义解析的 64 材质上限保持不变 |
| `_materials` | TerrainMaterialSource 数组；完整且恰好覆盖地表集，每种地表只能绑定一次 |
| `_variants` | TerrainVariantSource 数组；每个规则候选都必须显式绑定，不能缺失或重复 |
| `_compiled` | 最近成功编译的 TerrainTileCatalog，只读，由整批成功后的发布步骤更新 |
| `_presentation` | 可空的运行时显示配置；“发布已编译地表”通过校验后才更新该配置的目录引用 |

### TerrainMaterialSource

| 字段 | 含义 |
| --- | --- |
| `_terrain` | 对应 TerrainDefinition |
| `_source` | 独立 PNG 的 Texture2D；编译读取原始 PNG 字节，不使用平台缩放 / 压缩后的 GPU 像素 |

### TerrainVariantSource

| 字段 | 含义 |
| --- | --- |
| `_variantId` | 地表集内唯一的既有规则候选 ID |
| `_materials` | 此 Key 中至少一种材质的覆盖源；未覆盖部分继续使用基准，不能引用 Key 外材质 |

每个 Key 编译基准项和全部规则候选。变体边带来自所属材质的基准；即使四角均相同，随机选择 / 显式覆盖仍使用既有 B 规则。

`TerrainTileCatalog` 是只读编译产物，仅持有地表集 ID、RuleHash、内容修订、Key / Variant → Tile 和 PNG 摘要；不引用高分辨率源。它没有创建菜单，也不放入通用内容目录。正式预览借用其中的持久 Tile，不加入诊断资源租约，释放场景时不销毁这些资产。

## 确定性采样与共享边

`TerrainTextureCompiler` 只消费 Color32 数组，不读取 AssetDatabase。全画布面积采样到 64×64，每个源像素与目标格的重叠面积使用整数计算，在 sRGB 字节域分别累积 RGB，除法四舍五入；Alpha 必须全为 255。无裁切、按内容定位或按单图修正。

基准材质先处理 X 对边，再处理 Y 对边。距对应边 `d < 8` 时，以两侧像素四舍五入均值为目标，混合权重为 `min(6, 8-d)/6`；距所有边至少 8 px 的内部保持采样结果。外侧两像素保证同步，过渡起点 d=2 同样为全权重，d=7 为 1/6。固定次序保证两轴及四角同时一致。

变体用距最近边的距离和相同权重混合基准像素，内部来自自己的源。随后调用 `TerrainPartition.Rasterize(key,64)`，根据每个样本归属从对应材质的相同采样相位取色。没有改变水岸分区、鞍点或通行语义。

算法修订为 `area-srgb-integer-64-periodic-2-8-xy-v1`。SHA256 编译修订包含算法、分区修订、RuleHash、按稳定 ID 排序的原始源字节摘要及变体绑定。导入器改动不改变源 PNG 字节；编译产物的 Sprite 导入设置另行严格验证。

## 事务、取消与更新

编译先捕获输入并校验、采样全部源，再创建 `Assets/Art/Tiles/Environment/WangCompiled/<源配置GUID>/<修订>/`。每项是独立 PNG / Sprite / Tile，最后创建 `catalog.asset`。输出为 64×64、PPU64、Center、FullRect、Point、无压缩 / mipmap、sRGB、Clamp、无 Tile 碰撞、白色 Tint、单位 Tile 矩阵。

所有 Tile 成功并通过校验，且发布前再次捕获的源修订未改变，才持久化配置的 `_compiled` 引用。取消或失败删除本次新修订目录，不改变旧目录和旧引用。每个 Tile 完成后可通过 `IProgress<int>` 通知调用方，CancellationToken 在采样、组合和发布前检查。同步 Editor 构建用进度窗取消，不创建后台 Unity 对象。

已有同修订目录只允许验证和复用；损坏目录报告错误，不在原路径覆盖。旧修订保留，以免破坏其他引用；本轮没有自动垃圾回收。目录提供 `GetTile` 精确匹配，不存在的 Key / 变体直接报错。

地图、PNG、资产内容导入通知与 projectChanged 合并刷新预览；所有刷新仍走会话代次和取消机制。正式显示修订随内容变化，即使地图语义没有变化也会创建新帧。错误时保留的画面是过期证据，不代表最新源有效。

## 验证与边界

[C2 验收](./acceptance/c2/README.md)包括全部 256 Key 与变体的兼容边 RGBA 一致、整数面积预期、内部保留、输入拒绝、真实资产编译 / 摘要失效、写 Tile 后取消与异常回退、损坏修订拒绝、正式帧原子切换及资源释放。新增 9 项长期回归；一次性截图脚本和 PlayMode 采集测试在采集后清理。

泥土 Pilot 保留为编译链路基线。四材质技术样例与发布证据见 [C3 验收](./acceptance/c3/README.md)；多材质接点、跨块、大图与构建中取消验收见 [C4 记录](./acceptance/c4/README.md)。
