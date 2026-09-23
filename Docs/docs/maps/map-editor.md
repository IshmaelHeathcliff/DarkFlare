# 地图编辑器与 AI 编辑

当前 `0.5.5-alpha` 已通过从空白图开始的实际 SceneView 手绘、AI 原子编辑、导入导出与正式预览联验，保留[样图与操作证据](./acceptance/i/workflow.md)。

地图编辑器模块。入口：`DarkFlare/地图/地图编辑器`，或配置中心“地图文档”→“打开地图绘制工作台”。阶段 G 扩展入口及跨图出口；0.6.2 增加[V3 Boss 点位](./README.md)，运行时装配见[双地图生命周期](./world-map-lifecycle.md)。

## 手工操作

1. 在“文档”选择 `.dfmap` 并打开，或选择地表集、ID 与尺寸新建空图。正式四材质资源可自动使用 `terrain_basic_sources`。
2. “绘制”→“进入 / 适配绘制舞台”，在独立 SceneView 舞台编辑 XY 平面；退出舞台回到原场景。临时 Tilemap、材质和标记不写入 Main。
3. B 画笔、E 擦除、I 吸管、R 矩形、L 直线、F 四邻接同材质填充、V 显示格变体、Q 选择、O 放置对象、Z 矩形区域。尺寸控制方形笔刷；快速拖动插值补格，整笔只有一次 Undo。大笔画超过 4096 格时以范围框反馈，不逐格画轮廓；超出事务预算整笔拒绝。
4. Ctrl/Cmd+S 保存；Ctrl/Cmd+Z 撤销，Ctrl/Cmd+Shift+Z 或 Ctrl/Cmd+Y 重做。Esc、失焦到非 SceneView、切舞台和进入 Play 取消未完成笔画。Alt 和中键保留 SceneView 浏览，文本输入不触发绘图快捷键。
5. 对象目录按 ID 与能力筛选；选择足迹后可移动、编辑直角旋转、外观和 Trader / Crafting / Portal 绑定。Shift 多选对象，支持复制与批量删除；复制分配新 ID。属性编辑通过“应用对象属性”提交。
6. 区域支持矩形、多边形和顶点列表。选中区域可拖顶点，在“应用区域 / 顶点属性”时完整校验并形成一次撤销事务；未应用的候选形状允许暂时无效。区域类型来自 Named / Safe / Spawn 定义，刷怪区绑定现有 Spawn 定义。

各层可以显示 / 隐藏、锁定。“对象与区域”页可选择正式表现目录并切换正式对象 / 诊断轮廓；布局选择仍以足迹、接地点与 ObjectId 为准。对象标签默认只显示选中项，可手动显示全部标签。表现目录、分段排序和资源生命周期见[对象表现](./object-presentation.md)。显示格变体必须与四角地表兼容；默认拒绝重绘引起的失配，也可显式启用“清除受影响变体”。

新建 / 另存为不得覆盖已有源或重复 MapId；另存为输入新的本地 ID，普通导出保留身份。导入先显示差异，再替换工作副本；必须保持当前 MapId。元数据仅支持 seed、origin、cellSize、width、height，提交前显示尺度和差异；不提供静默裁剪、palette 重排或地表集迁移。

## 地图入口与出口

入口页维护 EntryId、Arrival / NewGame / Respawn 用途和局部世界坐标，SceneView P 工具放置入口。Portal 对象填写目标 MapId 与 EntryId；Trader / Crafting 仍绑定内容 ID。AI 的 `UpsertEntry` 使用完整 `record`，`RemoveEntry` 使用 `id`，与手工操作共享校验、Undo 和修订。正式资源位于 `Assets/Data/Maps/World/`，编辑样例保持独立。

Boss 点位页维护稳定 ID、Boss 怪物定义、局部世界位置及单位朝向。SceneView 显示点位圆圈和朝向线，选择后可移动并提交。`UpsertBossSpawn` 使用完整 `record`，`RemoveBossSpawn` 使用 `id`；它们与手工编辑共享地图事务、预览、Undo / Redo 和修订。Boss 点不能作为玩家入口或普通刷怪区域。

## 事务、撤销与恢复

- `MapEditService`：批量操作、预算、结构 / 内容校验和候选快照，不访问窗口、文件或 Undo。沿用 `MapDocumentJson` 的严格 DTO 规则，单条记录验证不重复序列化整图。
- `MapEditSession` / `MapEditSessions`：同一源 GUID 共用一个工作副本。Apply 修改内存，Save 显式落盘。提交同步在 Editor 主线程重新捕获定义并完成校验，不跨帧发布半成品。
- `MapEditFiles`：文件字节 Hash 与工作副本 Hash 分开。写唯一同目录临时文件，紧邻替换前复核磁盘基线，再原子替换及 ImportAsset。替换成功但导入失败返回 ImportFailed；不把它描述为“未写入”。原子替换不保证外部进程之间的 CAS，最终复核到替换仍有竞争窗口。
- Unity Undo 只拥有临时语义载体。Undo / Redo 恢复内容但生成新的单调 revision；无变化不产生新修订，脏状态忽略修订号。保存后仍可撤销，回到已保存语义时恢复干净状态。
- 每个载体最多累计 64 笔、估算 64 MiB 语义历史，字节估算取 UTF-8 和 UTF-16 较大值，不等于 Unity 原生堆总量。达到预算时只调用 `Undo.ClearUndo(本载体)` 并提示；Unity 可能留下空撤销组，其他工具的历史仍保留。
- `Library/DarkFlare/MapDrafts` 保存有效事务检查点：单记录 ≤8 MiB、全部记录 ≤64 MiB。保存 / 放弃后去掉完整草稿，只保留轻量修订记录，以阻止旧请求在重开后再次生效。重载恢复内容但不恢复 Unity Undo 栈或永久请求回执；尚未结束的笔画不在恢复范围。
- 恢复时遇源身份或磁盘变化，将旧记录保留为 `.conflict-<id>.json`，显示位置供手工合并；不会自动覆盖源，也不会在关闭新会话时抹掉旧草稿。冲突备份计入总预算；检查点写入失败保留工作副本并提示立即保存。

## 预览与静态诊断

对象移动、旋转、外观、Undo / Redo 和 AI Patch 会刷新正式对象；仅对象或区域变化时复用当前地表帧。更换地表资源、地表内容或依赖时才失效地表。正式对象候选失败会明确标记旧画面过期。工作台复用 `MapTerrainPreviewSession` / `Frame`：笔画完成后合并刷新请求，后台求解、主线程分批填充完整帧，再移入 `MapEditingStage`。编辑中显示待提交范围，重建中旧画面明确标为过期。切图、关闭、Play 或重载释放预览；多个窗口共享数据，仅一个工作台拥有活动绘制舞台。

“诊断与 AI”页显式选择起终点（世界坐标）与半径，复用 [D 静态几何](./map-geometry.md)及 CheckEdge 做有界连通检查。结果区分 Connected、Disconnected、NoLegalEndpoint、BudgetExceeded；预算耗尽不等于断路。选中交互对象后，可检查四侧合法站位，不要求走进阻挡中心。障碍轮廓最多绘制 8192 个矩形，查询仍使用完整几何。

几何手动刷新，不在 MouseDrag 上完整编译或创建 Physics2D。编辑 / 定义变化立即使旧诊断失效。合法文档即使可选几何超容量也可保存，但返回 GeometryUnavailable，不能算可导航。此处不包含运行时路径、流场、A*、Job 或怪物行为。

## AI / CLI 接口

通过连接中的 Unity Editor 调用 `DarkFlare.Editor.MapEditApi` 静态方法，不需要窗口、鼠标模拟或第二个写入进程。返回规范 JSON；失败给出 code、path、operationIndex、message 与 suggestion。最终候选的跨操作语义错误以文档 path 定位，operationIndex 为 -1，避免误归因到最后一项操作。`MapEditSessions.Close(path)` 可结束会话并保留有效恢复检查点。

| 方法 | 行为 |
| --- | --- |
| `DescribeSchema()` | 独立 Patch V1 JSON Schema、坐标、操作、记录结构与预算 |
| `ListDefinitions(filter, offset, count)` | 按 ID / 名称搜索目录，返回完整 ContentId、类型、配置与变体；每页 ≤256 条 |
| `ReadMap(path)` | 元数据、当前 revision / hash、dirty 与源路径；大集合通过分页读取 |
| `ReadRegion(path, x, y, width, height, offset, count)` | 指定窗口地表最多 4096 格；对象、区域、入口、override 分别按整图稳定顺序分页，每类最多 256 条，返回总数与下一 offset；记录不按矩形中心裁掉跨界对象 |
| `PreviewPatch(path, json)` | 校验候选并输出改格数量、受影响显示范围、对象 / 区域 / override 的增删改；不修改内存或磁盘 |
| `ApplyPatch(path, json)` | 重新校验基线、定义与全部操作，原子修改工作副本 |
| `Save(path)` | 按独立磁盘基线保存 |
| `Import(path, json, baseRevision, baseHash)` / `Export(path, destination)` | 导入到工作副本；AI 导出只接受未占用的 `.json` 路径 |
| `Diagnose(path, startX, startY, endX, endY, radius, maxVisits)` | 对当前工作副本运行可选静态诊断，最大 262144 次节点访问 |

Patch ≤1 MiB、≤256 操作、≤262144 次格子 / 候选访问。洪水填充和重叠区域的重复访问同样计预算。使用 MapEditService.Patch 创建基线封套，或按 Schema 构建 JSON：

```json
{
  "patchVersion": 1,
  "mapId": "map:town_draft",
  "baseRevision": 1,
  "baseHash": "替换为 ReadMap 返回的 hash",
  "operationId": "widen_road_001",
  "clearAffectedOverrides": false,
  "operations": [
    { "type": "FillRect", "x": 29, "y": 10, "width": 6, "height": 8, "terrainId": "terrain:road" }
  ]
}
```

支持 PaintCells、FillRect、PaintLine、FillRegion、SetVariant、UpsertObject / RemoveObject、UpsertRegion / RemoveRegion、UpsertEntry / RemoveEntry、UpsertBossSpawn / RemoveBossSpawn、SetMapMetadata。Upsert 使用完整 `record`；对象和 Boss position 为局部世界单位，rotation 是角度，区域顶点为逻辑格单位。未知字段 / 类型 / 版本拒绝，不执行任意脚本或按名称近似匹配内容。

同一会话保留最近 256 份 operationId + 规范 payload 回执；相同请求返回原回执及当前版本，不再次执行，Undo 不清除回执。ID 对应不同 payload 报 Conflict；淘汰 / 重启后依靠 revision + hash 拒绝旧请求，不承诺永久去重。分页结果带版本，跨页期间变化应重新读取。

## 样例与验证

`DarkFlare/地图/编辑样例` 可打开城镇 / 野外草稿。源位于 `Assets/Data/Maps/EditorSamples/`，生成器只补缺失资产，不覆盖用户修改。两图包含四地表、障碍 / 装饰、交互绑定或刷怪 / 安全区域；它们是编辑布局，不是 0.5.3 的可玩双地图。

协议快照、可复现 Patch、性能和回归证据见 [E 验收](./acceptance/e/README.md)。不把编辑器的测试结果计为 500 活动怪物 / 1000 压测或 Windows Player 的导航验收。
