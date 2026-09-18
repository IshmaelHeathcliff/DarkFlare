# C3 四材质双变体与资源发布验收

日期：2026-09-18。范围为泥土、草地、水、道路各两个纯地表变体、完整 Wang 产物与 Addressables 发布。版本仍为 `0.4.2-alpha` / core v5 / SaveSchema 3。Main 场景资产保持原地表；C4 的完整样例观感和大图压力验收尚未完成。

## 资产与拼接

- 保留 C1 泥土 Pilot 原始字节，SHA256 为 `29f81593da1906ebbb85038ea286ce261b432557504dfbae40852b818e114a05`。
- 8 张源图全部为 1254×1254、不透明；使用统一导入预设，无逐图缩放、裁切或 Pivot 修正。7 张新源的全部生成提示词、实际参考及 Hash 见 [prompts.json](./prompts.json)。
- 每材质两个纯地表候选，混合 Key 使用基准源。共 256 个基准 Key + 8 个候选，每项独立 64×64 PNG / Single Sprite / Tile，PPU64。
- 产物修订：`6f78c7c26772ac7918ae3f20680b0df7837b7c0e444dc6dceea40ce3288ffad0`；全部引用与 PNG Hash 见 [compiled-catalog.json](./compiled-catalog.json)。
- 实际 PNG 的 16 种水平 / 16 种垂直边签名、1024 次兼容边比较，最大 RGBA 差为 **0**；两变体外侧 2 px 同样为 0，内部各有超过 2300 个不同像素。见 [compiled-metrics.json](./compiled-metrics.json)。
- [源图审计](./source-family-audit.json) 8/8、[编译图审计](./compiled-family-audit.json) 264/264，均无错误；触边警告是满画布地表合同的预期结果。

道路首个第二变体均值 0.2322，与基准 0.2770 混用时形成明显暗块，见 [拒绝画面](./rejected/road-dark-blocks.png)。后续偏亮 / 偏暗候选均未放宽标准接收。最终源均值 0.2727，两变体相差约 0.0042；其余材质也满足差值 ≤0.015，详见 [source-metrics.json](./source-metrics.json)。生成工具的亮度调整并不精确，因此以实际像素和编译平铺结果为准。

## 可见证据

- [四材质总览 1920×1080](./materials-overview-1920.png)：纯材质、混合边、道路与水域技术布局。
- [原生像素 2112×1600](./materials-native.png)：64 px / 世界单位，覆盖完整显示边界。
- [变体对照 512×256](./variants-native.png)：由左至右泥土、草地、水、道路，上行为 01，下行为 00。
- [真实 Main 镜头 1920×1080](./materials-main-1920.png)：临时通过实际资源包加载地表，保留现有角色与正交 size=5 镜头；采集后恢复，不保存场景修改。

已复核材质可辨识、无缺图和透明裂缝、道路暗块消除。第一版材质轮廓继续使用 B 的整数分区，没有额外水岸渐变或道路描边；C4 将补齐孤点、鞍点、三 / 四材质接点、跨块和最终观感检查。

## 发布与回归

运行时入口 `maps/terrain/basic`，独立组 `DarkFlare-Map-Terrain`，标签 `df.session` / `df.terrain`。源与逻辑定义、显示配置位于 `Assets/Data/Maps/Terrain/`，样例位于 Development。正式包暂不加入 `Preset` / core，避免提前改变当前存档所用内容目录。依赖树不包含高分辨率源、Editor 源配置或开发样例。

发布器在编译后显式更新显示目录，并验证当前源摘要、规则、全部 Tile、地址唯一性与依赖；失败恢复旧目录。依赖检查显式包含待发布目录，避免 AssetDatabase 尚未保存引用时漏查。测试使用内存 Addressables 配置，避免临时分组污染项目全局设置。

验证记录见 [EditMode](./editmode-results.json)、[PlayMode](./playmode-results.json)和 [AssetBundle / Main 采集](./packed-and-capture-results.json)：完整 EditMode 661/661，完整永久 PlayMode 76/76；使用最终 AssetBundle 的共享加载 / 释放专项 1/1，真实 Main 临时采集 1/1。最终内容校验无问题，默认 Play Mode Script 恢复 Use Asset Database。只构建了本地 Addressables 内容，未构建 Windows Player。

本地资源构建后，禁用 Domain Reload 的测试环境曾返回 0 项测试；该结果不计为通过。请求脚本域重载后，实际执行上述 2 项专项并全部通过。Main 截图只检查显示与现有角色尺度，商人等旧对象的位置未按临时技术地表重新布局，不代表最终城镇布局或可达性。

长期新增 2 项发布事务回归、1 项共享资源生命周期回归；一次性截图测试与脚本完成后删除。未引用的失败道路编译修订仅保留上述拒绝证据，清理其本轮临时产物；C2 Pilot 和本次正式修订保留。

C3 完成；下一步为 [C4 正式样例与压力验收](../../../plan/archive/alpha-0.5.0-terrain-art-plan.md)，C 计划尚不归档。
