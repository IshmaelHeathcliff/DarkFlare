# 统一 UI 组件生成记录

2026-09-09；工具为 imagegen。以项目已批准的窗口和 HUD 原图为视觉参考，遵循单图生产规范；未生成组件拼图或手工切片。

| 交付 | 生成输出 | Unity GUID |
| --- | --- | --- |
| `ui_control_base.png` | `exec-7877b139-9448-43b2-99a5-650d5f485cc6.png` | `228abfc658c026947a8a34cdcfdc7ef1` |
| `ui_slot_base.png` | `exec-6a310dcc-13e6-4a0e-aeb7-037aa3f10f57.png` | `009d445a5b8dc944685392b6c277dc59` |

按钮提示要求：单个 1254 正方形、完整不透明炭黑底、细旧铁方形斜角轮廓，中性颜色供运行时着色；32 源像素九宫格保护区内只留规则边缘，中心近均匀，不含文字、图标、金属铆钉或复杂角饰。首张 `exec-3aa6551b-c561-4c6f-b961-06cb936f66db.png` 的角饰超出保护区，拒绝导入；重新生成简洁薄框后通过按钮 Pilot。

槽位在按钮 Pilot 通过后生成，以按钮原图为同家族参考，保持固定画布、薄规则边缘与空白中心，改为更暗的内凹表面。两个交付均直接复制完整生成输出，没有裁切、重采样或逐图缩放。

自动审计见 [control-audit.json](./control-audit.json) 与 [slot-audit.json](./slot-audit.json)：每张 0 错误，1 个允许的全出血接边提示。原生检查、最小 / 最大消费者和状态板共同验证装饰保护及拉伸表现；Import 设置见[资产合同](./asset-contract.md)。

通过 Unity MCP / Editor API 导入和绑定。Main / Shell 的 `Components.uss` 显式引用新 Sprite，随原 UI Addressables 依赖链加载；没有额外资源句柄。大窗口复用原 96 Border 底板，未修改其规格；不为只改变颜色的状态生成重复资产。
