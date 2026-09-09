# alpha 0.3.3 统一 UI 组件验收

2026-09-09；实现基线 `dc4dc2e`；Unity 6000.4.3f1。Main 与 Shell 统一按钮、槽位、面板、提示及表单材质，保留既有独立窗口、输入所有权和领域事务。模块合同见[组件规范](../../../ui-component-style.md)。十槽装备与最终 Player 封板仍在 0.3.4。

## 当次验证

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 项目 EditMode | 432/432，0 失败 / 跳过 | [NUnit XML](./editmode-results.xml) |
| 项目 PlayMode | 63/63，0 失败 / 跳过 | [NUnit XML](./playmode-results.xml) |
| 运行布局与状态采集 | 3/3，57 张三语言截图、2 张状态板 | [采集 XML](./visual-results.xml) |
| Shell 最终布局复核 | 1/1，更新 24 张 Shell 截图 | [采集 XML](./shell-results.xml) |
| 两张正式 PNG 自动审计 | 每张 0 错误；各 1 项合同允许的全出血边缘提示 | [按钮](./control-audit.json)、[槽位](./slot-audit.json) |

测试复用项目隔离 Session / 存储。伪本地化扩展真实可见文本；六词条使用既有压力快照，不修改正式配置。状态板通过临时控件注入伪状态 / 选择类，验证渲染与优先级，不将其算作真实鼠标或手柄操作；实际拖放、右键、交易、打造、取消回退、导航和输入接管由项目长期回归覆盖。

## 视觉证据

每种运行画面都有 `zh-Hans`、`en`、`qps-ploc` 三张 1920×1080 原始截图。

| 画面 | 中文 | 英文 | 伪本地化 |
| --- | --- | --- | --- |
| HUD | [图](./hud-zh-Hans.png) | [图](./hud-en.png) | [图](./hud-qps-ploc.png) |
| 背包 | [图](./inventory-zh-Hans.png) | [图](./inventory-en.png) | [图](./inventory-qps-ploc.png) |
| 商店单窗 | [图](./shop-zh-Hans.png) | [图](./shop-en.png) | [图](./shop-qps-ploc.png) |
| 打造单窗 | [图](./crafting-zh-Hans.png) | [图](./crafting-en.png) | [图](./crafting-qps-ploc.png) |
| 商店 + 背包 | [图](./shop-inventory-zh-Hans.png) | [图](./shop-inventory-en.png) | [图](./shop-inventory-qps-ploc.png) |
| 打造 + 背包 | [图](./crafting-inventory-zh-Hans.png) | [图](./crafting-inventory-en.png) | [图](./crafting-inventory-qps-ploc.png) |
| 商店 + 背包 + 属性 | [图](./shop-inventory-attributes-zh-Hans.png) | [图](./shop-inventory-attributes-en.png) | [图](./shop-inventory-attributes-qps-ploc.png) |
| 打造 + 背包 + 属性 | [图](./crafting-inventory-attributes-zh-Hans.png) | [图](./crafting-inventory-attributes-en.png) | [图](./crafting-inventory-attributes-qps-ploc.png) |
| 六词条 | [图](./six-affixes-zh-Hans.png) | [图](./six-affixes-en.png) | [图](./six-affixes-qps-ploc.png) |
| 暂停 | [图](./pause-zh-Hans.png) | [图](./pause-en.png) | [图](./pause-qps-ploc.png) |
| 返回确认 | [图](./return-confirmation-zh-Hans.png) | [图](./return-confirmation-en.png) | [图](./return-confirmation-qps-ploc.png) |
| FrontEnd | [图](./front-end-zh-Hans.png) | [图](./front-end-en.png) | [图](./front-end-qps-ploc.png) |
| 下拉菜单 | [图](./language-dropdown-zh-Hans.png) | [图](./language-dropdown-en.png) | [图](./language-dropdown-qps-ploc.png) |
| 设置表单 | [图](./settings-zh-Hans.png) | [图](./settings-en.png) | [图](./settings-qps-ploc.png) |
| 重绑定列表 | [图](./settings-bindings-zh-Hans.png) | [图](./settings-bindings-en.png) | [图](./settings-bindings-qps-ploc.png) |
| 错误弹窗 | [图](./error-modal-zh-Hans.png) | [图](./error-modal-en.png) | [图](./error-modal-qps-ploc.png) |
| Busy | [图](./busy-zh-Hans.png) | [图](./busy-en.png) | [图](./busy-qps-ploc.png) |
| Toast | [图](./toast-zh-Hans.png) | [图](./toast-en.png) | [图](./toast-qps-ploc.png) |
| Fatal | [图](./fatal-zh-Hans.png) | [图](./fatal-en.png) | [图](./fatal-qps-ploc.png) |

[按钮 Pilot](./button-pilot.png)覆盖 Default、Hover、Pressed、Focus、Disabled 及 24×24 至 434×52 消费者；[槽位状态板](./slot-states.png)覆盖普通、焦点、选中及同时叠加选择 / 悬停 / 焦点的有效和无效投放。材质不改变控件几何，物品焦点没有第二条金色选择边。

## 修复、资产与维护

- 公共皮肤集中到 `Components.uss`，清理旧全局 Button 动效与无消费者 HUD 规则；布局继续由模块 USS 管理。
- 下拉菜单使用共享 Panel 的运行主题，解决弹层不继承 Shell 内容样式的问题；设置滚动箭头采用相同材质。
- Toast 长文本换行，前台删除 / 退出按钮在内容区内分配宽度；三语言可见文字和按钮边界纳入已有 Shell 回归。
- 装备槽投放状态在 Hover / Focus 时仍优先；真实 HUD 鼠标按下用例增加尺寸、固定边框与无缩放检查。
- 新图加入已有 `Phase5VisualIntegrationTests` 资产合同；不新建版本套件，长期测试数量不变。临时截图 / 状态板代码及 `.meta` 已清理，历史 XML 和 PNG 不成为运行测试输入。
- 两张新图经 Unity MCP / Editor API 直接导入，Main / Shell 的依赖扫描均包含其正式路径；复用原 Addressables 加载链，没有新增句柄。详见[合同](./asset-contract.md)、[消费者测量](./consumer-measurements.txt)与[生成记录](./generation-record.md)。

部分 MCP job 在 Domain Reload 后丢失结束回调，以含结束时间和逐项结果的 Unity NUnit XML 为准；仅在 Editor 已停止测试后清理孤立 job。

完成版本为 `0.3.3-alpha`，前台版本改为读取 `Application.version`；截图采集于升级前，版本标注为当时的 `0.3.2-alpha`。内容 / 存档 Schema 不变，没有新增字体、着色器或平台专属渲染依赖，本阶段未运行发布构建。测试引入的字体缓存恢复为执行前备份，保留用户既有字体修改；EnterPlayModeOptions 恢复关闭 / None，Editor 留在 Bootstrap 非 Play 状态。
