# 物品窗口 UI 修正验收

日期：2026-09-12。Unity 6000.6.0f1，1920×1080。

## 结果

- 详情去除图标；按住左 Shift / 手柄左扳机显示兼容槽装备比较，双戒指两栏均显示，松开 / 拖放取消 / 关闭清理。
- 装备槽按指定占格排列，空槽原生灰阶滤镜去色并降低不透明度，已装备物品保持原色。
- 背包、商店、打造统一 560 px 并居中；工作台默认联动背包，隐藏重复来源和摘要；单独关闭背包仍有独立来源。
- 顶部关闭清理全部物品窗口及属性窗口并恢复 Gameplay；单窗关闭保持原作用域。
- 动作菜单导航覆盖整个 overlay 并限制焦点，跨窗拖放支持高度不对齐的目标，普通浏览保持原方向约束。

## 自动验证

- [EditMode](./editmode.json)：434/434，0 失败。
- [PlayMode](./playmode.json)：65/65，0 失败。含中文 / 英文 / Pseudo Locale 的窗口、文本和输入验证。
- 最后一次重绑定超时修正后：[输入所有权 PlayMode](./input-ownership.json) 3/3。
- 编译无错误，`git diff --check` 通过。

复用原 `EquipmentPhase2PlayModeTests` 增加真实 Shift / 左扳机对比、双戒指内容及不重叠、松开和取消检查，更新原空间邻居断言。复用 `Phase1UxPlayModeTests` 覆盖默认联动、同宽、独立来源和顶部关闭；保留既有穿脱、原子交易、拖放、手柄与 Session 覆盖。没有新增一次性测试套件。

最终 EditMode 复验曾暴露既有超时误判：原重绑定代码用 Unity 实时钟推测 Input System 的超时，结果可能为 Cancelled。已统一使用 RebindingOperation.startTime / timeout 与 InputState.currentTime；保留原超时、取消和设备断开测试，全量复验通过。Unity 自动重写的无关 Prefab / 动态字体缓存及临时日志已清理。

## 视觉证据

- [背包与十槽装备](./inventory.png)
- [双戒指并排比较](./comparison.png)
- [商店联动背包](./shop.png)
- [打造联动背包](./crafting.png)

实测双窗边界：背包 x=392、商店 / 打造 x=968，宽均为 560，窗口组中心 x=960。商店滚动视口宽 520，商品网格宽 516，完整十列不会被右侧滚动条裁切。截图只使用临时运行会话，未保存生产场景。
