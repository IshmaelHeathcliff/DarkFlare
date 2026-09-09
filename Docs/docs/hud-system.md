# 战斗 HUD

0.3.3 的入口按钮、交互提示和中区分隔接入公共 `Components.uss`，复用已验证的资源底板与 Glyph；按下不缩放，Toast 位于资源仪表上方并支持长文本换行。资源、冷却和输入所有权仍遵循下列合同，见[统一组件验收](./assets/acceptance/alpha-0.3.3-ui-style/README.md)。

## 布局与内容

Main 的 `GameRoot.uxml` 组合 `Hud.uxml`，共享原有 UIDocument、PanelSettings 与 Application EventSystem。1920×1080 下仪表位于 y=932–1068：左右各 300×136 的生命 / 法力底板，中间 840×136 的技能与窗口入口区，间距 12。既有物品窗口宽度与网格保持原值。

生命和法力显示当前 / 上限及即时比例；中区显示当前自动攻击名称、耗蓝、间隔、剩余间隔、冷却条和金币。就绪只代表资源与冷却条件满足，不保证有攻击目标。世界交互提示位于仪表上方。

正式底板为 `ui_hud_resource_base.png`，生命和法力共享中性材质。填充、文字与状态由 UI Toolkit 绘制；不以拉伸图片模拟资源比例。规格与证据见[HUD 资产合同](./assets/acceptance/alpha-0.3.2-hud/asset-contract.md)。

## 数据与生命周期

`HudController` 通过共享 `GetAttributeRuntimeQuery` 读取资源及 `PlayerSkillStateRegistry` 的只读技能状态，和[属性详情](./attribute-details.md)采用同一状态优先级：等待角色、禁用、死亡、无来源、法力不足、暂停、冷却、就绪。不存在时显示原因，不保留上次拒绝施法的历史提示。

资源、装备、金币和角色事件立即刷新。100 ms 的 UniTask 仅查询轻量运行快照，不反复生成完整属性解释；计时不推进玩法。旧 `LastSnapshot` 保留事件刷新兼容。刷新任务归属 Session SceneScope 的 `combat-hud` 子作用域，禁用、离场和 Session 结束取消并解绑，重新启用读取新角色状态。

有效生命在 (0,25%] 时出现危险文字与轮廓；持续脉冲只改变独立轮廓透明度，不延迟真实数字或填充。Reduce Motion、暂停、输入挂起和解绑会停止 PrimeTween 并复位透明度。低生命静态文字仍保留。资源与技能时钟遵从玩法暂停，不用现实时间假装恢复。

## 输入所有权

背包、属性和暂停按钮调用原有菜单入口。窗口打开或 Shell 接管时隐藏 HUD 入口，资源和技能仍可读；装饰与根容器不参与命中或导航。Gameplay 仅额外启用 UI Point / Click 以接收鼠标按钮操作，不启用 UI Navigate / Submit。`IsUiEnabled` 表示导航动作启用，避免仅启用指针便被误认为完整 UI 模式。

键盘 / 手柄沿用已有 ToggleMenu 与 Pause Actions，属性通过窗口入口到达，没有额外快捷键或 Gameplay 方向导航。HUD 与 Shell 共用 `InputGlyphs.uss`，设备、偏好和重绑定变化刷新实际 Glyph；未知控件保留可读文本，键盘文本边框随内容伸展。

## 回归维护

长期覆盖合并到 `GameplayUiFoundationTests`、`ApplicationInputServiceTests`、`Alpha014ResourceSystemPlayModeTests` 和 `Phase1UxPlayModeTests`：查询边界、挂起禁用、原子耗蓝与恢复提示、真实鼠标入口 / 手柄返回、低生命与降低动态、禁用再绑定。截图采集和临时角色只用于当次验收，完成后删除脚本，证据归档到[验收目录](./assets/acceptance/alpha-0.3.2-hud/README.md)。
