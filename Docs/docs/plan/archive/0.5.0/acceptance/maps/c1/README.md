# C1 地表尺度与泥土源 Pilot

2026-09-18：C1 完成。批准单张泥土材质源作为 C2 的输入，冻结[家族合同](../../../../../../maps/terrain-art-contract.md)并交付 `Assets/Settings/TerrainSourceImport.preset`。C2 的采样、周期边、Wang 编译与正式预览尚未实现，整个 C 保持进行中。

## 图像与导入

原图为 `Assets/Art/Sprites/Environment/TerrainSources/terrain_dirt_source_00.png`，1254×1254 RGB，全画布等价 alpha=255，视觉中心 (627,627)，四边留白 0；[单图审计](source-audit.json)通过。审计工具报告“触及画布边缘”是全覆盖地表的预期警告，合同明确允许。可用[审计 Manifest](source-manifest.json)重跑技能脚本。

[亮度与源边测量](source-metrics.json)：平均 sRGB 亮度 0.20257，95 百分位 0.24445，均在背景层级护栏内；原始水平 / 垂直对边平均 RGB 字节差分别为 4.90484 / 5.26209，非零，因此不能宣称成品接缝通过。C2 必须使用共同边带并将生产 Tile 的边差验证为 0。

[Unity 导入记录](unity-import.json)：单 Sprite、FullRect、PPU1254、中心 Pivot、Point、Uncompressed、无 mipmap、sRGB、NPOT None、Alpha Is Transparency=true；纹理实际保持 1254×1254，世界 XY bounds 为 1×1，源地表 Transform 为 (1,1,1)。无直接 / 隐式 Addressables 条目，无新的 core 内容；内容校验 0 问题，配置文档覆盖通过。预设供后续同家族源使用，编译 Tile 不使用该 PPU1254 预设。

生成过程、不可变引用、Hash、两次拒绝原因及最终提示词见[生成记录](generation.md)。未使用 Python 修改图像；Python / Pillow 仅执行读取审计，PNG 按生成结果完整保存。

共用预设排除 `m_SpriteSheet`、外部对象映射、userData 和 AssetBundle 身份字段，避免将 Pilot 的 SpriteID / 名称复制到后续资产。[独立源验证](preset-audit.json)确认应用预设后目标 GUID、SpriteID、Sprite 名称和 userData 保持原值，PPU / 世界尺寸正确；探针 PNG 和 `.meta` 随即删除。

## 尺度与可见性

- [原始尺寸 Pilot](../../../../../../../../Assets/Art/Sprites/Environment/TerrainSources/terrain_dirt_source_00.png)：已检查粗块面、压暗灰褐、无草 / 石板 / 其他地表语义；细节密度比第二张拒绝候选明显降低。
- [1920×1080 角色同框](pilot-scale-1920.png)：Main 相同正交 size=5，源 Sprite 每格 1 单位重复；左至右为玩家、普通、迅捷、重装，沿用现有角色 Sprite 与根缩放。背景未压过角色识别色，但纯单变体的重复图案仍可察觉，最终应结合变体与混合地表检查。
- [通道 / 足迹对照](pilot-clearance-1920.png)：四列通道宽为 1、1.5、1、2；青色圆分别为半径 0.32、0.30、0.36、0.30，橙线仅是尺度标记。无 Collider 或导航模拟，不等价于路径验收。
- [运行中 Main 世界镜头](pilot-main-playmode-1920.png)：在隔离测试存档环境启动现有 Main，仅运行期间隐藏旧地表并替换为源图 Tilemap；玩家、商人、打造台和营地物件仍可辨识。截图来自真实 Main Camera 的 1920×1080 渲染，不含屏幕 HUD；结束恢复旧地表并释放临时资源，没有保存场景或 Prefab 改动。

相机与 Prefab 的只读原始数据见 [scale.json](scale.json)。0.75 单位通道对最大半径只剩 0.015 单侧净空，不作为主通道基线；D 再验证净空、转角和导航离散误差。

## 验证与清理

一次性 PlayMode 真实 Main 采集 **1/1** 通过，[结果](playmode-capture.json)已保存；使用现有隔离数据与 Session 清理夹具。采集测试、`.meta` 和临时 Editor / Python 脚本在收尾删除，不增加持续扫描历史截图的测试。没有修改运行时代码、Main、Prefab、玩家存档或版本号，也未构建 Player。

完成后重跑项目 EditMode **650/650**；首次扫描发现 Alpha Is Transparency 未遵守项目通用 Sprite 合同，已修正源图与预设并通过复测，未改测试断言。最近完整 PlayMode **75/75** 为 B4 基线，本轮只补上述真实 Main 专项，不将其称为重跑全部 PlayMode。下一步 C2 应优先使用这张源图实现纯泥土周期边和失败回退，再接正式 Wang 材质配置。
