# C2 正式纹理编译验收

2026-09-18，Unity 6000.6.0f1 / URP 17.6.0。C2 已完成，模块说明见[正式地表纹理编译](../../terrain-texture-compilation.md)。只消费 C1 批准的泥土源；其他三材质与第二变体待 C3，C 计划仍未整体完成。

## 真实产物

- 源：`Assets/Art/Sprites/Environment/TerrainSources/terrain_dirt_source_00.png`，SHA256 `29f81593da1906ebbb85038ea286ce261b432557504dfbae40852b818e114a05`，原始字节保持不变。
- 配置：`Assets/Data/Development/Maps/terrain_pilot_sources.asset`；样例地图 `terrain_pilot.dfmap` 为 24×16、origin=(-12,-8)、cellSize=1，仅使用 `terrain:pilot_dirt`，425 个显示格。
- 编译修订：`0364ce05b507980aaf3d215fea70f1e29ab154e198e38566542b97f2349299b3`；一个基准 Tile，PNG SHA256 `980377ec228206b22e7f45b934c59f976161c4e123131de80711ca68db8beb34`。目录路径、规则摘要和 Sprite 规格见 [pilot-build.json](./pilot-build.json)。
- [单图合同审计](./compiled-audit.json)：64×64，Alpha 覆盖率 1、中心 (32,32)、无留白，0 错误。唯一“像素接触画布边缘”提示符合地表全铺满合同。
- [独立 PNG 像素审计](./compiled-metrics.json)：两轴对边及第二像素带最大 RGBA 差均为 **0**；平均 sRGB 亮度 0.2030，P95 0.2338。

## 视觉证据

- [正式样例窗口](./formal-window.png)：明确选择正式模式、源配置和地图，状态为有效正式材质，工具栏与预览区域完整可见。
- [512×512 原生像素平铺](./pilot-native-repeat.png)：每 Tile 64 屏幕像素，8×8 Tile，未缩放单张 PNG。
- [1920×1080 正式隔离预览](./pilot-repeat-1920.png)：正交 size=5，与 Main 镜头尺度一致。
- [真实 Main 临时替换采集](./pilot-main-playmode-1920.png)：读取已编译 Tile，临时单位网格，玩家 / 商人 / 打造台同框；独立测试数据环境，采集后释放临时网格、材质与 RT，恢复旧 Renderer。镜头 size=5、角色在视口内经断言；没有保存场景或修改正式旧地表。

目视未见透明缝或错位边，人物与交互物保持前景层级。单一纹理的重复仍可辨认，本步未将它宣称为最终四材质地图；C3/C4 继续验证变体混用、多材质接点与整体观感。采集的是 Camera 世界画面，不包含覆盖式 HUD。

## 回归与清理

- [项目 EditMode](./editmode.json)：**659/659**。其中新增 9 项覆盖整数面积预期、周期化与内部保留、全部 256 Key / 变体共享边、非法 / 取消输入、修订复用与源修改失效、写 Tile 后取消、输出异常回退、损坏既有修订拒绝、缺变体 / 错画布拒绝和正式预览生命周期。
- [项目 PlayMode](./playmode.json)：**75/75**。
- [一次性真实 Main 采集](./playmode-capture.json)：**1/1**，采集测试及辅助脚本在完成后清理，不长期保留历史截图测试。
- [配置校验](./validation.json)：内容 **0 问题**，配置文档覆盖通过；正式 Pilot 的 RequireCurrent 验证通过。测试隔离源、输出目录和预览资源均清理；交付时另行打开可见的正式样例窗口。

本轮未构建 Windows Player，运行版本维持 `0.4.2-alpha` / core v5 / SaveSchema3。未接入正式地图 Addressables、运行时新地图、碰撞或导航。下一步 C3 逐张生成并审计其余材质与变体。
