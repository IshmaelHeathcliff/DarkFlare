# Alpha 0.5：地图编辑器与 AI 编辑

状态：2026-09-18，[0.5.1 执行计划 E1–E5](../archive/alpha-0.5.1-map-editor-plan.md)已完成并归档。当前工具、协议与限制以[地图编辑器模块](../../maps/map-editor.md)为准；下文保留总体规划背景。入口：[总计划](../alpha-0.5-plan.md)，数据规则见[地图数据](./map-data-terrain.md)。

## 工具选择

在 Unity 内提供 Odin 地图工作台和 SceneView 绘制工具，复用 Unity Undo、Selection、AssetDatabase、SceneView Handles、Tilemap 显示及现有配置中心；不另造通用二维画布。Odin 负责配置 / 属性面板，地图预览通过 Editor API 构建。

Tiled 的 Terrain Brush / Corner Wang 概念可用于规则核对，但首期不同时维护第二个完整外部编辑器，也不承诺任意 TMX 地图无损导入。正式交换格式是项目 JSON，工程内源文件使用 `.dfmap` 专用后缀；需要 Tiled 等适配器时通过同一个 DTO / 校验入口转换。

## 手工绘制

| 工具 | 首期行为 |
| --- | --- |
| 地表笔刷 | 选择泥土 / 草地 / 水 / 道路，尺寸、画笔、擦除到默认地表、吸管、矩形 / 连续线、区域填充；绘制逻辑格，自动更新 Dual Grid 显示 |
| 变体 | 稳定自动分配、选定显示格显式指定兼容 VariantId、清除 override；不改变地表语义 |
| 对象 | 障碍 / 装饰 / 交互点目录搜索，吸附放置、移动、合法旋转、复制、批量删除；显示 footprint 和接地点 |
| 区域 | 矩形与简单多边形、顶点修改、优先级与 Named / Safe / Spawn 类型属性；沿用当前 DTO，不新增传送入口 |
| 检查 | 图层显示 / 锁定、逻辑格 / 显示格切换、碰撞 / 通行 / 净空 / 区域叠加、错误定位、显式起终点的有界静态连通诊断；目标流场预览留至 0.5.4 |
| 文档 | 新建、打开、保存、另存为、导入、导出、校验、重建预览、撤销 / 重做、显示未保存状态 |

一次拖拽笔画视作一个撤销事务；重复刷同一值不制造无效 revision。Undo / Redo 恢复语义但生成新的单调修订。地图复制生成新 MapId；普通导出不改 ID。缩小地图尺寸会移除数据时显示具体差异，不静默裁掉对象。对象正式 Sprite / Prefab 表现绑定留至 0.5.2，本阶段先以 footprint、接地点和类别标记完成布局编辑。

地图预览是派生内容，不能直接拖动预览对象后只改 Scene Transform；选中派生对象应定位原记录，编辑操作仍写文档。预览重建采用 staging，失败保留旧预览并标注失配，保存 / 导出错误不覆盖原文件。关闭窗口或重载域释放临时对象、材质、Native 数据和输入注册。

编辑器自身以键鼠为主；键盘可选工具、撤销、保存和导航属性。游戏中的地图出口、交易、打造等入口同时覆盖键鼠与手柄，不要求在 Unity SceneView 用手柄画地图。

## 导入导出与文件合同

- 提供带格式版本的 JSON Schema、枚举 / 内容目录快照、最小有效样例、命令样例和稳定错误码。使用已安装 Newtonsoft JSON，不增加另一套 JSON 库。
- JSON 属性与集合按规范顺序输出；chunk / 对象按坐标 / ID 排序，浮点使用固定规则；同一文档重复导出字节稳定。
- 数据引用使用 ContentId，导入不根据名字近似匹配，不把任意类型名或任意脚本表达式反序列化为可执行对象。
- 执行结构与内容引用校验；可选几何 / 拓扑诊断使用显式起点、目标和半径，不猜测入口。结果给出稳定 code、JSON path、可用的对象 ID / 格坐标及修复建议；复用编译器的首错报告，不复制另一套规则收集所有错误。
- 导入先预检并显示差异，通过后作为一个事务替换工作副本；未知版本、引用缺失、非法地图完整拒绝。Save 显式落盘，单独核对打开时的磁盘字节 Hash，和工作副本 baseHash 区分。
- 唯一同目录临时文件完成写入和校验后原子替换；写失败保留工作副本，文件已替换但导入失败单独报告。本 Editor 写入串行化；外部进程在最终检查与替换间仍有竞争窗口，原子替换不等于跨进程 CAS，细节见 E1。Map 文档格式版本与玩家 Save Schema 分别迁移。
- 首期外部交换包为 JSON + 所需 ContentId 清单；不把贴图与 Prefab 嵌入 JSON。提供目录缺项诊断，不承诺一次返回所有结构错误。
- 文档有效但可选几何超预算时允许保存，显著标记 GeometryUnavailable，不能将旧几何显示为当前可导航结果；正式可玩地图发布的强制几何验收留至后续阶段。

## AI 与手工共用事务

建立不依赖窗口的 `MapEditService`，供 SceneView 工具、Editor 静态入口及 Unity CLI / MCP 调用。AI 读取结构化信息并发出批量操作，不靠模拟鼠标，也不直接编辑开放场景 YAML。

| 接口（计划名） | 输出 / 操作 |
| --- | --- |
| DescribeSchema / ListDefinitions | 格式、坐标、约束、可用地表 / 对象 / 区域定义 |
| ReadMap / ReadRegion | MapId、revision、hash、范围内逻辑格和对象，支持按区域分页 |
| Validate / PreviewPatch | 结构 / 内容预检、diff、影响矩形及可选几何 / 连通诊断；不改工作副本或文件 |
| ApplyPatch / Save | Apply 复核工作 revision + hash 及定义内容后原子修改工作副本；Save 单独复核磁盘基线并写入 |
| Export / Import | 与手工按钮使用同一规范序列化和校验，不另建 AI 专用数据格式 |

操作集首期包含 PaintCells、FillRect、PaintLine、FillRegion、SetVariant、UpsertObject、RemoveObject、UpsertRegion、RemoveRegion、SetMapMetadata。算法只接受显式输入；自然语言如何转成操作由调用方 AI 完成。

批量请求示例；Patch V1 已独立冻结，不修改地图 V1，正式字段与预算见[模块及 Schema](../../maps/map-editor.md#ai--cli-接口)：

```json
{
  "patchVersion": 1,
  "mapId": "map:town",
  "baseRevision": 12,
  "baseHash": "<读取文档得到的哈希>",
  "operationId": "town-main-road-001",
  "clearAffectedOverrides": false,
  "operations": [
    {
      "type": "FillRect",
      "x": 4, "y": 8, "width": 16, "height": 3,
      "terrainId": "terrain:road"
    },
    {
      "type": "UpsertObject",
      "record": {
        "objectId": "town_trader",
        "definitionId": "map_object:trader",
        "position": { "x": 12.5, "y": 12.5 },
        "rotation": 0,
        "appearanceVariant": null,
        "interaction": {
          "kind": "Trader",
          "contentId": "trader:town"
        }
      }
    }
  ]
}
```

示例 ID 为计划中的目录键，不代表已有正式资产。对象 position 为地图局部世界单位，rotation 为 0/90/180/270 度，格操作为逻辑格整数，区域顶点为逻辑格单位；坐标类型必须在 Schema 中区分。FillRegion 指按起点原材质进行四邻接洪水填充，与区域记录无关。

操作在副本模拟，全部验证后一次提交；任何操作失败不保留前半笔。会话最多保存 256 份幂等回执；重复发送同一 operationId + payload 返回原回执及当前工作版本，不重复执行，Undo 不清除回执；不同 payload 复用 ID 报冲突。回执淘汰或重启后通过 revision / hash 拒绝过期请求并要求重新读取，不承诺永久去重。Patch 大小、操作数量与访问格数都设预算，详见 E4。

AI 可导出截图帮助目视检查，但成功判据是结构 / 行为校验和实际预览一致；仅有“已执行”文本不能代表地图生成正确。接口提供 Editor 内同步调用与批处理入口；运行 Editor 已持有工程时使用连接中的 Editor，不启动第二个写入进程。

## 验收

从空图分别用手绘和同等命令创建同一地表 / 对象 / 区域，语义导出一致；撤销 / 重做恢复布局与稳定变体。覆盖重复命令、过期 revision、磁盘冲突、坏 JSON、未知字段 / 版本策略、失效 ContentId、半笔失败、超大范围、负坐标、对象复制及关闭窗口清理。

本阶段交付城镇 / 野外布局草稿及可复现创建 / 修改样例；可游玩的正式双地图留至 0.5.3。验证 AI 可以读取局部地图、增加绕行通道、修改刷怪区、预览、应用并显式保存，再由人工继续编辑，不丢失任何一层数据。
