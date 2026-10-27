# 战斗 HUD

Main 的 `GameRoot.uxml` 组合 `Hud.uxml`，沿用原有 PanelRenderer、PanelSettings 与 Application EventSystem。`HudController` 通过 QFramework 查询和事件读取真实资源、技能及绑定，不推进冷却或主动发起攻击。

## 布局与信息

0.7.2-alpha.4 将普通技能与六火焰槽合成一排，生命在左、法力在右，四个图标入口紧邻技能组。1920×1080 默认中英约1043×126px，资源仪表宽160px、技能图标槽56×56px；长绑定和伪本地化可自然增宽、换行。物品窗口宽度与格子保持原值。

生命和法力直接显示当前 / 上限及即时比例，金币为次级读数。七个技能位置常驻真实键位、空槽 / 无来源 / 缺蓝 / 暂停 / 死亡等短提示，以及剩余冷却遮罩和秒数。遮罩面积直接使用权威剩余比例，冷却结束清空；不可用不只靠颜色区分。

普通技能使用当前武器图标，火焰读取真实实例 `IconGuid`；有来源但缺图使用明确占位，空槽不显示假技能图标。背包、属性复用已有背包 / 护甲图标，天赋和暂停使用独立生成 PNG。入口图形28px、命中区域至少40px，按下不改变大小，名称与真实绑定可悬停查看。

完整技能名称、耗蓝、间隔、剩余冷却与原因在鼠标悬停说明显示，说明本身也可停留阅读。手柄沿既有菜单、属性、天赋和状态页查看同源详情，Gameplay 不启用 HUD 方向导航。详见[手动技能](skill-board/manual-skills.md)与[属性解释](attribute-details.md)。

资源底板复用 `ui_hud_resource_base.png` 九宫格，槽位复用 `ui_slot_base.png`，填充与数字由 UI Toolkit 绘制。合同、图标审计和三语言画面见[紧凑 HUD 验收](plan/archive/0.7.2/acceptance/compact-hud/README.md)。默认空 / 满槽背景矩形并集面积比旧版减少52.32%–62.46%；提示层不混入主 HUD 面积比较。

## 提示与状态

世界交互提示在主 HUD 上方，技能说明再排在交互提示上方并限制在视口内。游戏中的 Toast 位于上方中央，避免盖住交互提示和技能说明；前台 Toast 保持原位置。右上状态条及其显示开关继续由 `StatusPanelController` 管理，详见[状态生命周期与查看](status-lifecycle-ui.md)。

有效生命在 (0,25%] 时出现危险文字与轮廓；持续脉冲只改变独立轮廓透明度，不延迟真实数字或填充。Reduce Motion、暂停、输入挂起和解绑停止 PrimeTween 并复位，静态警告仍保留。

## 数据与生命周期

`GetAttributeRuntimeQuery` 与 `GetPlayerSkillRuntimeQuery` 提供即时快照，状态优先级与属性详情一致：等待角色、禁用、死亡、无来源、法力不足、暂停、冷却、就绪。普通技能快照只补齐已有图标字段，没有新增保存字段、配置或冷却时钟。

资源、装备、金币和角色事件立即刷新。100ms 可取消 UniTask 只读取轻量快照，归属 Session SceneScope 的 `combat-hud` 子作用域；旧 `LastSnapshot` 保留事件兼容。动态图标使用 Session 的 `SpriteAssetLoader` 已预热缓存，固定入口沿 USS 依赖加载，不在刷新中发起新加载。

技能表现拆在 `HudController.Skills.cs`，与资源控制器共用绑定和作用域。悬停由 Pointer 事件驱动；离开卡片 / 说明后延迟120ms关闭，方便跨过间隙。换来源、开窗、Shell、禁用、脱离 Panel 和重载关闭旧说明，语言与运行数值刷新当前内容。解绑取消延迟，移除回调、动态图标和源快照引用，重新绑定读取当前 Session。

## 输入所有权

四入口沿用原有菜单路由。窗口打开或 Shell 接管时隐藏入口、火焰条和说明，资源与普通技能仍可读。按钮、技能热点和可见说明使用同一指针命中判断与帧末屏蔽；点击技能槽不会施法。鼠标保持移入热点也终止旧保持，移开后须释放再按。键盘 / 手柄技能不受鼠标悬停影响。

装饰与根容器不参与命中或导航。Gameplay 仅启用既有 UI Point / Click，不启用 Navigate / Submit；键盘 / 手柄通过 ToggleMenu、Pause 和窗口循环进入详情。Glyph 使用真实重绑定与设备偏好，没有图形时保留可读文本。

## 回归维护

长期覆盖保留在 `GameplayUiFoundationTests`、`PlayerSkillsTests`、`ApplicationInputServiceTests`、现有资源、保存恢复与 `Phase1UxPlayModeTests`：来源 / 冷却、鼠标保持中断、四入口、手柄移动与施法、低生命、重载和保存继续。模拟移动按夹具的物理时钟注入，结束恢复采样模式；不屏蔽错误日志。截图采集、面积计算及测试桥接在验收完成后清理，证据保留在归档目录。
