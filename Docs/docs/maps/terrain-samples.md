# 地表诊断样例

D 阶段另有 **DarkFlare/地图/打开同源几何样例**，源为 `Assets/Data/Development/Maps/terrain_geometry.dfmap`，复用正式四材质拓扑并加入 U 形障碍、Spawn / Safe 区域；可查看原始阻挡、通行节点与足迹 / 线段查询。实现与限制见[地图几何](./map-geometry.md)，[D 验收](../plan/archive/0.5.0/acceptance/maps/d/README.md)包含物理和导出重导入证据。

Alpha 0.5.0 B 的持久技术输入，配合[诊断预览](./terrain-preview.md)使用。样例仅含地表和变体，不是最终城镇 / 野外，也没有碰撞、交互或刷怪对象。

## 入口与文件

`DarkFlare/地图` 菜单提供“打开拓扑诊断样例”“打开边界诊断样例”和“生成并预览 256×256 压力地图”。这些入口自动补齐不存在的诊断样例与定义；已有文件和 GUID 保留。独立“创建诊断样例”菜单已移除，底层 `MapDiagnosticSamples.EnsureSamples()` 保留。占用目标路径的其他类型资产会报错。

定义和两张源地图位于 `Assets/Data/Development/Maps/`。四个地表使用独立身份 `terrain:preview_dirt`、`preview_grass`、`preview_water`、`preview_road`，地表集为 `terrain_set:preview_set`；每种纯材质有 `_a` / `_b` 两个稳定变体。诊断定义未加入 core / Addressables；纹理、Sprite 和 Tile 仍只驻留预览会话。图形标记用于分辨变体，正式美术家族在 C 建立。

## 拓扑样例

`preview_topology.dfmap`：40×24，origin=(0,0)，cellSize=1，默认泥土，1025 个显示格。每个面板 8×8；横向从左到右，纵向按逻辑坐标由下向上：

| 逻辑 y | 五个面板 |
| --- | --- |
| 0–7 | 泥土、草地、水、道路、草水棋盘鞍点 |
| 8–15 | 道路直线、L 转角、T 丁字、十字、孤点 |
| 16–23 | 水孤点、带草岛的湖泊内外角、三材质接点、四材质中心、对角点接触 |

前四个底部面板各在显示坐标 `(面板×8+1,2)` / `(面板×8+4,2)` 固定 A / B 覆盖；其他纯材质格采用稳定选择。鞍点只展示分区归属，不代表可通行连接。

## 边界与压力样例

`preview_boundary.dfmap`：35×33，origin=(-13.25,7.5)，cellSize=0.5，1224 个显示格。源 chunk 保留 (0,0)、(1,0)、(0,1)，省略 (1,1)；省略块和地图外均回落默认泥土。x 或 y ≥30 的混合条带穿过 31/32 接缝，可叠加逻辑 / 显示网格和 chunk 边界检查。真实 Renderer 中心=(-4.5,15.75)，XY 尺寸=(18,17)。

压力菜单以默认 seed=19260817 的固定整数坐标 Hash 生成四材质 256×256 地图，编译后写入 `Temp/DarkFlare/preview_stress.dfmap`，源路径显示在窗口中。该文件可删除、可重建，不纳入版本库。它有 66049 个显示格，适合测试缓存、全量填充和取消；不是 500 / 1000 怪物导航压测。

## 正式材质样例

C4 的 `MapFormalSamples` 复用上述三种布局生成器，映射到 `terrain_set:basic`、正式 dirt / grass / water / road 和 `_00` / `_01` 变体。几何、seed、chunk 布局保持一致，诊断源与既有 GUID 不被覆盖。

`DarkFlare/地图` 提供“打开正式拓扑样例”“打开正式边界样例”“生成并预览正式 256×256 压力地图”。前两张持久源为 `Assets/Data/Development/Maps/terrain_topology.dfmap` 和 `terrain_boundary.dfmap`，打开时自动补齐缺失文件；压力源为 `Temp/DarkFlare/terrain_stress.dfmap`。独立“创建正式验收样例”菜单已移除，底层 `MapFormalSamples.EnsureSamples()` 保留。已有持久源只读取，不自动重置。

正式入口要求 `Assets/Data/Maps/Terrain/terrain_basic_sources.asset` 的编译修订当前有效，打开既有预览窗口的正式模式，不生成诊断贴图或静默补洞。变更正式源后须先按[纹理编译流程](./terrain-texture-compilation.md)更新，再打开样例。样例不是运行时地图，不写入 Addressables 或 core。

正式 Key 画廊、接点 / 跨块渲染像素和大图验收记录见 [C4 验收](../plan/archive/0.5.0/acceptance/maps/c4/README.md)。角色与真实 Main 镜头的尺度证据沿用同一编译修订的 [C3 采集](../plan/archive/0.5.0/acceptance/maps/c3/materials-main-1920.png)。

## 验证

2026-09-18 完成全图、原始像素、取消和三轮压力采集，详见 [B4 验收记录](../plan/archive/0.5.0/acceptance/maps/b4/README.md)。原始像素的 279168 对相邻边像素全部一致，无透明孔洞。重复生成保持已有文件字节不变；诊断资产无直接或隐式 Addressables 条目。项目完整 EditMode 650/650、PlayMode 75/75、内容校验 0 问题、配置文档覆盖通过。

一次性采集与扫描脚本完成后清理；保留可复用样例生成菜单与已有地图 / 地表 / 预览长期行为回归，不增加核对固定样例数量或历史截图的运行测试。
