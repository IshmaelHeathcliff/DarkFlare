# Alpha 0.5.0 B：Dual Grid / Wang 解析与预览

状态：2026-09-18，B1–B4 已完成并归档。B1 提交 `0c866f7`，B2/B3 提交 `dc33aaa`；B4 交付持久拓扑 / 边界样例、可生成压力源、1920×1080 与原始像素证据。当前行为见[诊断预览模块](../../maps/terrain-preview.md)与[样例模块](../../maps/terrain-samples.md)，实际性能、取消和回归结果见 [B4 验收](../../maps/acceptance/b4/README.md)。项目 EditMode 650/650、PlayMode 75/75、内容和文档覆盖通过。下一步为 C 正式地表家族合同与 Pilot；0.5.0 尚未整体交付。

入口：[0.5.0 阶段计划](alpha-0.5.0-map-foundation-plan.md)；现状：[地图文档模块](../../maps/README.md)；后续总体规则：[地图数据与地表](../alpha-0.5/map-data-terrain.md)。

## 交付范围

让有效 `.dfmap` 在独立 Editor 预览中显示四材质 Dual Grid 地表，四角解析、稳定变体、边界与局部更新可检查。交付完整 256 Key 的诊断调色板、真实源地图预览、接缝与生命周期验证，以及后续正式地表可以复用的纯解析接口。

B 只使用算法生成的诊断色、覆盖区和内部变体标记，不生产正式材质纹理，不替换 Main，不创建商人 / 怪物玩法对象，也不实现画笔、AI Patch、碰撞或寻路。C 才按 [unity-2d-sprite-workflow](../../../../.agents/skills/unity-2d-sprite-workflow/SKILL.md) 固定正式参考、Pilot 和单 PNG / Single Sprite 家族；诊断调色板是查看界面，不是生产 SpriteSheet。

## 代码基线与改动入口

下表保留 A 提交时的基线与 B 的改动方向；B1 的实际接口以地表规则模块为准。

| 现状 | B 的处理 |
| --- | --- |
| `CompiledMapDocument` 只直接暴露尺寸、GetTerrain 和源 Hash，其他头字段须重新解析 JSON | 增加消费者需要的只读 origin、cellSize、seed、terrainSetId 与覆盖查询；保留快照隔离，不在每个显示格中反序列化 |
| `MapDocumentCompiler` 已内联四角变体兼容判断 | 抽出共享坐标 / 四角采样工具，编译校验与显示解析共用，保留 A 的负坐标与边界合同 |
| `TerrainSetDefinition` 的变体有稳定 ID 和四角，没有 Sprite / 权重字段 | B 编译为只读候选表；显示资源由独立诊断工厂提供，正式 Sprite 配置在 C 增加，不往布局 JSON 写资源路径 |
| 地表集最多 64 种，四种是本轮验证材料 | 四角身份使用完整 ContentId 元组；紧凑索引只在同一规则快照内使用，不能把任意 Key 当作 8 位四材质数值 |
| `SourceHash` 仅是规范 JSON 的 SHA256 | 规则和显示资源独立计算修订摘要；地图头、规则、候选和显示配置共同参与缓存有效性判定 |
| 旧 `GroundTilemapSetup` 会重建并保存 Main | 只参考其批量 Tilemap 用法；新预览不调用旧重建工具，不复用固定 5×5、8 单位的地表假设 |

计划增加 `Runtime/Maps` 下的 DualGrid 坐标、Wang Key / 规则快照、地表解析结果与增量比较；`Editor/Maps` 增加诊断 Tile 资源所有者和预览窗口。名字可随具体职责调整，不提前增加无消费者的 MapModel / MapSystem。Editor 预览不启动 Session；未来运行时显示 Controller 再接入 QFramework。

## 坐标与 Key 合同

- 逻辑格 `(x,y)` 中心：`origin + (x+0.5,y+0.5) × cellSize`；世界转逻辑格必须 floor，先减 origin。
- 显示格范围：`x=-1..width-1`、`y=-1..height-1`，总量 `(width+1)×(height+1)`；显示中心：`origin + (x+1,y+1) × cellSize`。
- 四角：NW=`T(x,y+1)`、NE=`T(x+1,y+1)`、SE=`T(x+1,y)`、SW=`T(x,y)`。地图外返回 defaultTerrainId，chunk 外仍须读取真实邻居。
- `WangKey` 语义顺序固定 NW、NE、SE、SW；单材质二元 mask 位为 1、2、4、8。四材质画廊枚举 `4^4=256`，但算法不限制为四种。
- 变更逻辑格 `(x,y)` 只影响显示格 `(x-1,y-1)`、`(x,y-1)`、`(x-1,y)`、`(x,y)`；集合去重并裁剪到有效显示范围。
- 显示 chunk 归属使用数学 floor 除法，明确 `-1` 属于负 chunk，不能用 C# 整数截断。它只是显示缓存划分，不改变源文件的 32×32 非负 chunk 编号。
- Tilemap 的 Grid、anchor、Sprite Pivot 必须联合验证真实 Renderer bounds；选用单位 Grid 加统一 cellSize 换算，默认中心 anchor 对应根平移 `origin + (0.5,0.5)×cellSize`。禁止把 origin 或半格偏移加两次。

## 分区、边和变体

技术分区采用既有计划的四角权重 `NW=(1-u)v`、`NE=uv`、`SE=u(1-v)`、`SW=(1-u)(1-v)`，按相同 TerrainId 汇总。最大者拥有该采样点，平局按完整 TerrainId 的 Ordinal 顺序决定。参考函数与栅格化分开，不能把诊断 PNG 反过来当作未来通行规则。

采样须确定性：诊断栅格使用固定整数分子比较权重，外边界采样明确落在 u/v=0 或 1；每个像素恰属一个材质，四角一致且无透明孔洞。边签名按统一全局方向定义：水平左→右、垂直下→上，匹配边逐采样相等。另一对角、块创建次序和变体不得改变共享边。鞍点只允许零宽接触的视觉归属，不能由单个中心像素宣称两块可通行区域连通；D 再验证足迹几何。

| 情况 | 结果 |
| --- | --- |
| 有合法 override | 使用其稳定 VariantId，不重新随机；四角不兼容继续拒绝 |
| 有多个兼容候选 | 按 VariantId 的 Ordinal 顺序整理，以固定 Hash 选择；输入包含 seed、带符号显示坐标、完整 Key、terrainSetId，不能使用枚举序号或 Unity InstanceID |
| 候选列表重排 | 同一 ID 集合下结果不变；新增 / 删除候选属于规则变化，允许该 Key 重新选择并记录失效 |
| 无配置候选 | 仅在明确的诊断模式中由完整分区算法提供该 Key 的默认显示；不替换成其他材质，不往源文件补写虚构 VariantId |
| 正式显示资源未就绪 | 显示“诊断预览”，不能标为正式美术完成，也不能将诊断资产加入 core / Addressables 发布组 |

Hash 实现采用明确的字节编码、长度前缀和固定算法（首选 FNV-1a 64），附固定测试向量；不使用进程相关的 string.GetHashCode。四种纯材质各准备两个可区分的诊断变体，变化仅限内部；外边至少保留两像素不受变体标记影响。它们不改变分区、通行或代价。

## 全量与局部重建

解析结果包含显示坐标、四角 Key、选定变体、规则修订，独立于 Unity Tile / Texture。首次或尺寸、原点、格宽、seed、地表集 / palette、默认边界或规则变化时全量构建。局部地表变化按四邻显示格重建；仅 override 改变时重建其显示格；对象 / 区域变化不无故刷新地表。

必须区分“绘制派生增量”和“源校验增量”：B 仍可完整校验并编译下一份 MapDocument，再比较新旧不可变快照。未实现块级输入事务前，不承诺单次源导入端到端为 O(1)。比较与求解耗时分别测量；后续 MapEditService 可以提供可信 dirty 集合，但本轮不实现绘制命令或 Patch。

Tile 资源按规则摘要、Key、VariantId 和显示配置共享，禁止按每个坐标生成纹理或 MonoBehaviour。缓存使用完整摘要而非单独源 Hash。所有新结果先准备，再整体发布；取消、源非法或资源构建失败不显示混合版本。保留旧画面时必须标记过期且禁用有效快照操作。

## 独立预览与样例

配置中心“地图文档”增加打开预览入口，独立 Odin EditorWindow 提供地图 / Key 画廊切换、缩放、平移、适配全图、逻辑 / 显示网格、四角 / VariantId、chunk 边界和错误状态。调试数值只在 Editor 中出现，完整手绘工作台仍属于 0.5.1。

优先使用 Editor Preview Scene、Camera、RenderTexture 与一个 Chunk 模式 Tilemap。Tilemap 使用 Ground 层；诊断显示使用明确的 URP Unlit 资源，避免当前场景光源影响分区判读。64×64、PPU64、Center Pivot、Point、无 mipmap / 压缩、全不透明只作为诊断显示规格，不冻结正式材质画布。

Preview Scene / URP 渲染是否按预期隔离，必须在 B3 用现有 Unity 6000.6 Editor 实测。可使用 PreviewRenderUtility 等 Unity 预览设施解决适配问题，但不能静默退回修改活动场景。相关 API：[Preview Scene](https://docs.unity3d.com/ja/current/ScriptReference/SceneManagement.EditorSceneManager.NewPreviewScene.html)、[Tilemap anchor 与格中心](https://docs.unity3d.com/cn/6000.0/ScriptReference/Tilemaps.Tilemap.html)。

资源由窗口会话集中持有。源切换、关闭、脚本重载和退出 Play 前统一取消 UniTask，注销订阅，关闭 Preview Scene，释放 RenderTexture、临时 Texture / Sprite / Tile / Material；新请求递增 generation，迟到结果不能覆盖当前地图。不得引入每格 Update 或 Application / Session 常驻对象。

| 样例 | 用途 |
| --- | --- |
| 256 Key 画廊 | 每个 Key 独立展示并带四角标签；这是规则查看器，不伪装成一张满足全局邻接的源地图 |
| 小型 `.dfmap` 技术样例 | 纯地表、孤点、内外角、道路直线 / 转角 / 丁字 / 十字、三材质接点、四材质中心及鞍点 |
| 35×33、非零 / 负 origin 样例 | 跨 31/32 chunk 接缝、地图边缘、缺省块、负显示格与半格偏移 |
| 256×256 压力输入 | 66049 个显示格，记录冷 / 热构建、局部变化、源比较、内存峰值与取消响应；可重复生成，不保存巨型手工夹具 |

持久技术定义和源如有需要放在独立 `Assets/Data/Development/Maps/`，使用 `terrain:preview_dirt` 等独立稳定 ID，不与未来正式定义冲突；诊断纹理优先只驻留预览会话。截图与一次性性能记录归档 `Docs/docs/maps/`，不导入为生产资产。需要创建 Unity UI / 场景对象时遵守 Unity MCP / Editor API 流程，禁止手改开放场景 YAML。

## 执行与验收顺序

| 顺序 | 工作 | 完成证据 |
| --- | --- | --- |
| B1（完成） | 共享坐标、四角 Key、不可变规则与确定性选择 | 20 项专项及完整 EditMode 623/623 通过，覆盖负 origin、负显示格、边界、二元 16 模式、四材质 256 Key、64 材质输入及固定 Hash 向量 |
| B2（完成） | 技术分区、边签名、变体选择结果、全量 / 局部解析 | 专项 40/40；256 Key 独立权重预期、全部兼容边、跨 chunk 增量等价、缓存失效与失败隔离通过；变体内部诊断标记由 B3 实现 |
| B3（完成） | 隔离 Tilemap 预览、资源缓存与取消 | 6 项资源 / 会话及 1 项真实导入窗口专项通过；当前 Unity / URP 可见，负 origin / cellSize 的 bounds、鼠标检查 / 缩放 / 平移已实测 |
| B4（完成） | 样例、压力记录、完整回归与文档 | 两张持久源图、256 Key 画廊、1920×1080 与原始像素证据；279168 对边像素无差异；256×256 三轮冷 / 热 / 局部中位数 942 / 211 / 222 ms；取消释放与完整回归通过 |

测试优先扩展 MapDocumentTests 的兼容校验，新增 TerrainResolver / MapTerrainPreview 等按模块命名的行为覆盖；保留旧 GroundTilemapTests。接缝检查使用解析结果及独立边界预期，不只比较同一函数的两次输出。稳定快照、取消后释放、导入后自动刷新需真实生命周期测试；一次性采集脚本完成后清理。

B1/B2 先测纯逻辑，B3 再做 Editor 可见性核对；不要仅凭编译成功或 256 个编号就通过视觉验收。未测量前不承诺帧率或把导航的 500 / 1000 怪物目标当成本切片性能结果。完整回归完成后更新地图 / 地表模块说明并归档本计划；运行版本保持 `0.4.2-alpha`，C/D 未完时不宣布整个 0.5.0 完成。
