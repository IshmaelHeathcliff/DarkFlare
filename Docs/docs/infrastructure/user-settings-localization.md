# 用户设置与本地化

> 状态：`alpha 0.2.3` 已完成；`alpha 0.2.5` 已接入 Audio、Input 与 Reduce Motion 真实消费者；最近更新：2026-08-24

本模块负责本地用户设置的持久化、语言切换、字符串表预载、内容名称引用，以及 UI Toolkit / TextMesh Pro 的字体回退。运行时入口由 `ApplicationHost` 持有，业务 UI 只消费 `SettingsService` 与 `LocalizationService`，不直接访问文件系统或 `LocalizationSettings`。

## 运行时边界

- `SettingsService` 维护不可变 `UserSettingsSnapshot`，通过 `LocalSettingsStorage` 在应用作用域内读写 JSON；写入使用串行协调，不允许业务 Controller 直接访问路径。
- `LocalizationService` 支持 `Auto`、`SimplifiedChinese`、`English` 三种用户偏好，最终解析为 `zh-Hans` 或 `en`。语言切换采用 latest-wins：新请求取消旧请求，完成字符串表预载后再提交设置并广播 `LocaleChanged`。
- 启动预载表固定为 `ui`、`system`、`items`、`stats`、`affixes`、`monsters`。当前语言缺失条目时回退 `zh-Hans`，仍缺失则显示 `[table.key]`，不静默返回旧语言文本。
- `qps-ploc` 只用于 Editor 回归，不写入用户设置。其字符替换与扩展配置必须保持在字体链覆盖范围内。

Settings Schema 仍为 1。`UserSettingsSnapshot` 使用 `WithLanguage`、`WithAudio`、`WithInput` 和 `WithReduceMotion` 生成不可变候选值；输入、音频和可访问性服务先尝试原子提交，失败时恢复旧运行时状态，不允许磁盘与内存设置分叉。

## Application Settings Page

`ApplicationSettingsController` 绑定常驻 Shell 中的共享设置页，可从 FrontEnd 或暂停菜单打开。页面关闭后恢复来源页面与焦点；从 Main 打开时继续保留菜单的 UI Context 和 pause lease。

当前玩家可操作项：

- Master、Music、SFX、UI 音量与全局静音；
- Keyboard & Mouse / Gamepad 的正式 Action 重绑定、冲突反馈、取消、超时和恢复默认；
- 自动 / 键鼠 / 手柄 Glyph 偏好；
- 降低动态效果。

Text Scale、High Contrast、Screen Shake 与 Display Mode 尚无完整运行时消费者，因此不显示在页面中。输入重绑定与 Glyph 合同见[输入与运行时 UI](../input-ui-system.md)，Mixer 应用与失败回滚见 [Application Audio](./application-audio.md)，Reduce Motion 见[可访问性与平台生命周期](./accessibility-platform-lifecycle.md)。

## LocalizedContentReference

用途：作为正式配置资产中的嵌套序列化值，只保存字符串表和条目键。它不缓存最终字符串，也不参与稳定 ID、随机或存档身份计算。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_tableName` | `string` / 空 | 正式内容必填；只能使用已登记的表名 | 配置资产拥有；`LocalizationService` 在最终显示边界消费 |
| `_entryKey` | `string` / 空 | 正式内容必填；表内唯一且发布后稳定 | 物品、属性、词缀、怪物、角色、商人、标签和世界交互名称共同使用；改名必须同步所有引用与中英表 |

正式键约定：

- 物品与标签：`items/item.<id>.name`、`items/tag.<id>.name`；
- 属性：`stats/<stat_id>`；
- 物品/怪物词缀：`affixes/item_affix.<id>.name`、`affixes/monster_affix.<id>.name`；
- 怪物、角色、商人和场景交互：`monsters/monster.<id>.name`、`actor.<id>.name`、`trader.<id>.name`、`interaction.<id>.name`。

旧 `_displayName` 当前保留为 Inspector 作者识别和日志兼容字段。玩家可见 UI 不得把它作为最终字符串真值。

## 语义快照与显示边界

- 查询和快照只传 `LocalizedMessage`、稳定属性 ID、数值、枚举与结构化标签，不提前生成最终中英文句子。
- `ItemDetailSnapshotFactory` 输出内容名称、词缀名称和标签的本地化引用；`ItemDetailView`、背包、商店与打造 Controller 在刷新时解析。
- `LootPickupVisual`、`MonsterAffixVisual`、`WorldInteractionVisual` 与 `InteractionPromptController` 订阅 `LocaleChanged`，无需重建玩法对象即可刷新文本。
- 动态模板参数由调用方以 `LocalizedMessage.Arguments` 传入；最终格式化仍由字符串表条目拥有。

## 字体合同

- UI Toolkit 的 `GamePanelSettings` 绑定 `GamePanelTextSettings`；主字体为动态 `GameCjkFont`（QiushuiShotai），回退为动态 `GameLatinFont`（Liberation Sans）。
- TextMesh Pro 的 `QiushuiShotai SDF` 回退到 `LiberationSans SDF`。字体材质必须使用包含 `_TextureWidth` / `_TextureHeight` 的 Distance Field Shader。
- 秋水书体按 SIL Open Font License 1.1 使用；授权和来源说明位于 `Assets/TextMesh Pro/Fonts/QiushuiShotai - OFL.txt` 与 `QiushuiShotai - ATTRIBUTION.txt`。
- `LocalizationPolicyTests` 从六张表收集中英字符，并加入实际 `qps-ploc` 结果，验证整条字体链零缺字。

## 验证与维护

- 正式内容表必须与正式资产引用精确一致，不允许缺键、空翻译、孤儿条目或错误表名。
- UXML 玩家文本必须通过 `LocalizedString` 绑定，或登记为由 Controller 负责的动态文本；迁移过的运行时路径禁止重新加入玩家可见中文字符串字面量。
- `Phase1UxPlayModeTests` 在 `1280×720`、`1920×1080`、`2560×1440` 下验证 `zh-Hans`、`en` 与 `qps-ploc`，检查关键区域边界、重叠和可见文本裁切。
- `Alpha025ProductionAssetTests` 验证 Settings Page 控件、38 个新增中英条目、Glyph 资产与 Audio 配置；输入、音频和可访问性测试覆盖提交失败回滚。
- 新增正式内容类型或字段时，先更新对应配置参考与 `coverage-manifest.json`，再添加字符串表键和正式资产引用。
