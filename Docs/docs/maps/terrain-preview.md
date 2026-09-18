# 地表诊断预览

Alpha 0.5.0 B3 提供独立 Odin EditorWindow，消费[分区与增量解析](./terrain-partition.md)。入口为 `DarkFlare/地图/地表诊断预览`，或配置中心 → 地图文档 → **打开地表诊断预览**。仅在 Editor 的 Edit 模式工作，不启动游戏 Session，也不修改 Main。

## 使用

- **Key 画廊**：展示四材质的全部 256 Key，以及泥土 / 草地 / 水 / 道路各 A、B 两个诊断变体。各格独立，不能当作一张满足全局邻接的地图。标签上行为 NW / NE，下行为 SW / SE；D、G、W、R 的中文说明位于窗口上方。缩得太小时隐藏标签，放大后恢复。
- **地图源**：关闭画廊模式，选择 `.dfmap`。窗口重新读取和校验真实源，包含导入失败时的 TextAsset；保留源路径与 GUID，移动源后跟随路径。地图尚无正式候选时明确显示诊断分区，检查信息显示“无候选”。
- **浏览与检查**：滚轮缩放，中键 / 右键拖动，适配全图；可叠加逻辑格、显示格和显示 chunk 边界。左键读取显示坐标、chunk、四角 ContentId 与 VariantId。
- **更新和错误**：地图重导入（含定义依赖重导入）、移动或删除触发延迟合并刷新；也可手动重新加载。构建、取消或失败期间旧画面标记过期，格子检查禁用。成功后整体替换。修复源并重新导入后恢复。

显示 chunk 仍采用数学 floor：负显示格在负 chunk。世界渲染使用单位 Grid、中心 anchor、中心 Sprite Pivot，根平移为 `origin + 0.5×cellSize`，XY 缩放为 cellSize。显示范围比逻辑地图每边多半格；不把外围默认材质误当作源数据扩展。

## 资源与异步生命周期

`DiagnosticTileLibrary` 以完整规则摘要、分区修订、显示修订、四角 Key 和 VariantId 为缓存键。显示配置当前固定为 `diagnostic-rgba32-point-64-v1`；改变配色、尺寸或标记算法必须递增。每份共享资源含一个 Texture2D、Sprite 和 Tile，租约引用计数归零立即释放。

诊断规格为 64×64、RGBA32、Point、Clamp、PPU64、Center / FullRect、无 mipmap、全不透明、无碰撞。四种已知材质使用固定色，其余合法材质用稳定 ID 的色相；变体只改变内部点状亮度，至少外沿两像素完全不变。诊断 Variant 名只用于画廊示例，不写入布局 JSON；正式地图选择仍遵守 B1 的候选 / override 合同。

每个 `MapTerrainPreviewFrame` 使用 Unity 的 [PreviewRenderUtility](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/PreviewRenderUtility.cs) 管理独立 Preview Scene、Camera 和 RenderTexture；一个 Ground / Chunk 模式 Tilemap 使用明确的 URP 2D Sprite Unlit Material。临时资源均为 HideAndDontSave，不进入 core / Addressables。渲染使用允许 SRP 的预览路径，并成对结束 BeginPreview / EndPreview。

`MapTerrainPreviewSession` 管理请求代次、CancellationTokenSource、当前帧和构建帧。源校验 / 定义快照在主线程完成，纯解析通过 UniTask 切到线程池，随后明确回主线程。Tile 创建 / 批量填充分段让出；每批最多 1024 格或 8 份新资源。新请求先取消旧请求并释放其未发布帧，迟到结果不能回写。

已有规则结果与 Tile 租约可复用；当前为保证整体发布，仍创建新的预览帧并完整填充 Tilemap，没有实现渲染场景的局部块交换。因此“重算格数”只代表规则求解量，不能当作渲染提交量。旧帧保留到新帧成功，期间相同资源由租约共享；失败不会出现混合修订。

关闭窗口、脚本重载、Editor 退出以及离开 Edit / Play 模式时取消任务，释放构建帧、当前帧、材质和 Tile 缓存；PreviewRenderUtility 清理 Camera / RenderTexture / Preview Scene。窗口关闭时注销项目、地图导入、Play 状态、重载与退出订阅。回到 Edit 模式后再按保存的窗口选择重建。

## 验证与边界

2026-09-18：6 项资源 / 会话专项、1 项真实导入与窗口恢复专项通过；完整 EditMode **650/650**、PlayMode **75/75** 通过，内容校验 0 项问题、配置文档覆盖通过。没有新增生产地表、手绘工作台、对象排序、碰撞或导航，未构建 Player；正式样例、1920×1080 系列验收、256×256 冷 / 热构建与内存压力留在 B4。

长期回归包括 Tile 共享和最后租约释放、纯地表变体内部差异与不透明边、真实 Renderer bounds / Preview Scene 隔离、跨请求取消和关闭时无迟到发布、失败保留过期帧与恢复、零地表变更复用 Tile，以及 `.dfmap` 内容重导入 → 损坏 → 修复 → 关闭后的资源释放。导入通知使用地图 AssetPostprocessor；实际测试发现 `EditorApplication.projectChanged` 单独不能保证内容重导入刷新，已保留回归防止退化。

当前 Unity 6000.6.0f1 / URP 已实测可见窗口。临时 35×33 地图 origin=(-13.25,7.5)、cellSize=0.5，Renderer XY 中心=(-4.5,15.75)、尺寸=(18,17)，与显示外圈边界一致；1224 个显示格。通过窗口鼠标事件核对格子四角、缩放和平移。截图对应的小图单次比较约 1.07 ms、求解约 3.44 ms，仅作功能检查，不作为压力预算。

窗口打开期间经历脚本重载后恢复画廊。PlayMode 全回归期间额外读取 Editor 状态：进入 Play 后预览 Session=null、自有诊断对象=0、Preview Scene=0；返回 Edit 后 Session 有效、画廊自动恢复。此处检查的是会话生命周期，不是大地图压力取消响应。

- [Key 画廊窗口截图](./acceptance/b3-window.png)
- [真实地图源窗口截图](./acceptance/b3-map-window.png)

截图中的地图仅为一次性技术输入；源和定义由 Editor API 创建、验证后删除，采集脚本清理。地图示例资产仍由 B4 交付。
