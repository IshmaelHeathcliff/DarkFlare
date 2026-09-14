# 状态图标家族合同

2026-09-14：八枚已生成、导入并绑定；逐图及整组审计通过。

生成工具多次将透明背景输出为棋盘格，透明试图不进入正式资源。整组重新采用不透明深色底图标；原燃烧试图仅作为符号与像素材质参考。该选择适用于全部八枚，不逐图修改尺寸或抠图。

- 风格依据：正式 `Assets/Art/Sprites/UI/Icons/ui_icon_health.png` 的暗色奇幻、硬边像素和左上高光。背景目标冷黑色 `#111821`（生成结果允许每通道 ±8 的近黑色偏差），无边框、文字、徽章和额外装饰，状态符号居中。
- 生成画布固定 1254×1254，交付 96×96。全家族使用完整画布 nearest-neighbor 等比导出 16/209，不裁剪、不按主体包围盒缩放。
- 每 PNG 一个完整状态图标。画布不透明，符号最长边目标 64–76px、中心落在 (48,48)±8px；符号短边允许由形状决定。四角必须保持背景色，符号离画布边至少8px。符号可读性以原像素及40px实显人工复核，alpha审计仅验证不透明完整画布。
- 首枚深色底燃烧通过审计和视觉检查后冻结为唯一家族参考，后续七枚均使用它。
- 导入：Sprite Single、Full Rect、PPU64、Pivot(0.5,0.5)、Point、无压缩、无Mipmap、sRGB；无九宫格边界，Transform(1,1,1)。
- 文件：`Assets/Art/Sprites/UI/Statuses/status_{burning,bleeding,poison,stun,chill,shock,weakness,equipment_guard}.png`。
- 消费者：StatusDefinition.Icon、Addressables / SpriteAssetLoader、HUD与菜单状态查看，常规40px展示。
- 验证：每枚及整组执行sprite audit；1920×1080中英/Pseudo、焦点、悬停与Session资源释放；保存全部最终提示与生成路径。

自动 alpha 审计的八条边缘警告来自不透明满画布背景，属于此合同预期；不表示前景符号被裁切。前景符号边距和可读性另经逐图视觉检查。审计文件为 [sprite-audit.json](./sprite-audit.json)，固定提示见 [generation-prompts.md](./generation-prompts.md)。
