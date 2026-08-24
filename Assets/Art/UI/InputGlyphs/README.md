# Input Glyph 资源合同

本目录是 alpha 0.2.5 首版输入提示资源。视觉语言参考 `Assets/UI/ApplicationShell.uss` 的深石板底色、暖金描边与高对比焦点；Glyph 不使用品牌 ABXY 字母，保持通用手柄语义。

## 家族合同

- 每个 PNG 只包含一个完整 Glyph，禁止 SpriteSheet 与人工切片。
- 生成与交付画布固定为 64×64 RGBA；运行时显示尺寸为 36×36。
- PPU 64、Pivot `(0.5, 0.5)`、Sprite Mode Single、Full Rect、Bilinear、无 Mipmap、无压缩。
- 透明边缘至少 6 px；光学中心保持在 `(32, 32)` 附近。
- 键盘使用动态文字覆盖在统一 Keycap 外壳上；手柄按钮使用方向标记，不写死厂商字母。
- 消费者为 `ApplicationShell.uss` 与 `ApplicationSettingsController`；未知控制路径继续使用可读文本后备。

## 一图一 Sprite 映射

| 文件 | GlyphId | 用途 |
| --- | --- | --- |
| `keyboard_keycap.png` | `keyboard.keycap` | 动态键盘短文本外壳 |
| `gamepad_button_south.png` | `gamepad.button_south` | 手柄南键 |
| `gamepad_button_east.png` | `gamepad.button_east` | 手柄东键 |
| `gamepad_button_west.png` | `gamepad.button_west` | 手柄西键 |
| `gamepad_button_north.png` | `gamepad.button_north` | 手柄北键 |
| `gamepad_start.png` | `gamepad.start` | Start / Menu |
| `gamepad_dpad.png` | `gamepad.dpad` | D-pad 导航 |
| `gamepad_left_stick.png` | `gamepad.left_stick` | 左摇杆 |

资源由 Unity Editor API 使用确定性像素几何生成，生成日期为 2026-08-24；不依赖外部来源或未授权素材。量化合同由 `input-glyphs.audit.json` 验证。
