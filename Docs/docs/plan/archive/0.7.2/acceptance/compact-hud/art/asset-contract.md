# HUD 入口图标生产合同

家族 `compact-hud-entries`，2026-10-27 冻结。消费者为 `Hud.uxml/.uss` 的 `game-hud-board` 与 `game-hud-pause`；背包、属性复用当前 `ui_icon_inventory.png` / `ui_icon_armor.png`。无可用 CodeGraph 工具，已通过源码检索与 Unity AssetDatabase 核对消费者。

- 不可变材质参考：`Assets/Art/Sprites/UI/Icons/ui_icon_inventory.png`，旧铜、深铁、像素硬边、左上光，无烘焙文字。天赋 Pilot 通过后亦作暂停的不可变参考。
- 每张只含一个独立图标，透明背景；生成画布 1254×1254，交付画布 96×96。所有成员统一完整画布等比导出，固定 Pillow LANCZOS，比例 16/209；禁止按 Alpha 主体裁切、缩放、移动或修 Pivot。首张工具试产未遵循请求的1024画布，实际1254，尚未接受或扩展；按工具原生画布重新冻结并重新生成 Pilot，后续不逐张改变合同。
- 交付主体最长边 64–78 px，四边至少 5 px；Alpha 阈值 8，加权中心 x/y 均 45–51 px。覆盖率 0.12–0.60。天赋主体宽高各 64–78；暂停宽 48–72、高 64–78。
- 天赋：简明六边形构筑纹章，中心单一火焰、粗线 / 节点，24–28px 可辨。暂停：两个独立粗金属竖条，整体对称；无外框、额外符号或细碎粒子。
- 生产路径 `Assets/Art/Sprites/UI/Icons/ui_icon_talent.png`、`ui_icon_pause.png`；各自 Single / FullRect / PPU64 / Center / Point / sRGB / Alpha / 无压缩与 Mipmap，Border0，Scale1。
- 固定 USS 资源依赖，沿现有 Panel 生命周期加载。无 Addressables 动态重复加载。按钮至少40×40，图标28×28，按下不改变大小。
- 每张生成后立即单图审计，Pilot 量化与视觉通过再扩展；最终核对24/28/40px与Main 1920×1080，归档提示词、原始生成图、审计结果及实际截图。

主体或中心不符时重生成 / 编辑，不采用逐图补偿。
