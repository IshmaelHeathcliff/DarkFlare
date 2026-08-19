# alpha 0.2.3 用户设置与本地化执行计划

> 状态：实施中（切片 0–2 已完成；切片 3 已完成静态 UXML、Game Menu 与 HUD 动态文本迁移）
> 建立日期：2026-08-19
> 最近更新：2026-08-19
> 实施基线：`ebce6b9`（`alpha 0.2.2` 已完成）
> 上位计划：[alpha 0.2 基础设施开发计划](./alpha-0.2-plan.md)
> 强制契约：[alpha 0.2 基础设施约束契约](./alpha-0.2-infrastructure-contract.md)
> 前置模块：[应用生命周期与会话作用域](../infrastructure/application-lifecycle.md)、[稳定身份、内容目录与迁移框架](../infrastructure/content-identity-migration.md)、[本地存档与 Session 恢复](../infrastructure/local-save.md)

## 阶段结论

本阶段从“已安装 Localization 包但未接入”的状态出发，先建立 Application 级 Settings 与 Localization 服务，再迁移当前真实 UI 和内容显示文本。完成后，语言偏好必须在首份玩家 UI 可见前生效，`zh-Hans` 与 `en` 可在运行时往返切换，Pseudo Locale 能用于布局验收，新增玩家可见硬编码文本会被自动检查阻止。

Settings Schema 在本阶段覆盖语言、音频、输入、显示和可访问性域，但只有存在真实消费者的设置才允许出现在玩家 UI 中。`alpha 0.2.3` 只正式开放语言选择；音频、重绑定、输入图标、显示和可访问性设置保留经过验证的持久化合同，待 `alpha 0.2.5` 接入真实消费者后再声明支持。

## 已确认基线

- `alpha 0.2.0–0.2.2` 已完成并归档；本次规划核验开始时工作区干净，三个阶段均有对应运行时代码、专项测试、模块文档和独立提交。
- `com.unity.localization` `1.5.12` 已安装；项目自有 Runtime 中只有 `SettingsSchemaVersion`，没有 Settings Service、Locale、String Table 或本地化运行时服务。
- Unity 6 与当前 Localization 包支持 UI Toolkit Localization Data Binding，可直接绑定 UXML 的 `text`、`tooltip` 等属性并在 Locale 变化时自动刷新。
- 初步扫描发现 74 处 UXML `text` / `tooltip`、77 处运行时 UI 文本 / Tooltip / Feedback 写入，以及 85 个带显示文本字段的正式配置资产候选。该数字只作为审计起点，实施切片 0 必须区分玩家可见文本、Editor 文本、日志和内部诊断。
- 当前只有 `QiushuiShotai.ttf` 与 `LiberationSans.ttf` 两个项目字体源；尚无正式 UI Toolkit 字体 fallback 与中英文 / 数字 / 符号覆盖验收。
- `Main.unity` 仍是唯一运行场景；Game Menu、HUD、背包、商店、打造、物品详情、交互提示和存档反馈是本阶段的真实迁移消费者。
- 2026-08-19 规划核验执行 `dotnet build Assembly-CSharp.csproj --no-restore`，结果为 0 warning、0 error；阶段最终验收仍以 Unity 编译和 EditMode / PlayMode 为准。

## 当前实施进度

- Settings V1 已实现语言、音频、输入、显示和可访问性域的 DTO、范围校验、确定性 JSON、SHA-256、`0 → 1` 迁移、独立路径、原子提交、单份备份、损坏副本与并发互斥；Application Host 已在 Profile / Session 前初始化 Settings。
- Localization 已建立 `zh-Hans`、`en`、`qps-ploc`，以及 `ui`、`system`、`items`、`stats`、`affixes`、`monsters` 六张职责表。Application 只有在首屏表预热完成后才进入 Ready，启动期场景 Session 请求会排队等待。
- Localization Service 已实现显式语言优先、系统语言自动选择、`zh-Hans` 故障回退、latest-wins 切换、失败回滚和结构化结果；现有 Game Menu 已增加可聚焦的语言下拉框，只有语言设置对玩家开放。
- 7 份 UXML 的 74 个初扫候选已完成迁移：45 个静态文本使用 Localization 1.5.12 原生 UI Toolkit Binding，29 个动态文本入口清空字面量并进入显式策略白名单。Game Menu 存档状态和 HUD 动态文本已迁移，切换 Locale 时可按当前状态重绘；`ui` 表当前有 108 个中英双语 Key，缺失、空翻译和孤儿条目检查为零。
- 当前新增专项验证共 20 项通过；主场景真实冷启动、菜单存档入口与 HUD 法力反馈专项通过。背包、商店、打造、物品详情和交互提示等动态 Controller 文本、配置内容、字体 fallback、Pseudo / 三分辨率布局与全量回归仍未完成，不能据此宣布 `alpha 0.2.3` 完成。

## 目标

- Settings 在 Profile、Session 和玩家 UI 之前加载，损坏时安全回退默认值并保留有限诊断副本。
- 设置文件与游戏存档使用独立目录、Schema、迁移链和服务，不使用 `PlayerPrefs`，不写入存档槽位。
- 建立 `zh-Hans`、`en` 和仅测试可用的 Pseudo Locale，以及职责清晰的 String Tables。
- 迁移所有当前玩家可见静态文本、动态反馈、内容名称和格式化结果；显示文本与 ContentId、存档身份保持分离。
- 运行时语言切换即时刷新当前页面，保留页面、选择、输入模式和键鼠 / 手柄焦点。
- 建立字体 fallback、缺失 Key / 空翻译 / 孤儿 Key / 非法引用 / 硬编码文本自动验证。

## 非目标

- 不建立完整 Settings 页面、Page / Modal / Toast 外壳；这些属于 `alpha 0.2.4`。本阶段只在现有 Game Menu 提供最小语言选择入口。
- 不实现输入重绑定、完整 Glyph Resolver、AudioMixer Service 或可访问性消费者；这些属于 `alpha 0.2.5`。
- 不建立统一 Logger、全局异常捕获或完整 Addressables 分组治理；这些属于 `alpha 0.2.6`。
- 不新增玩法内容、不改动存档 Schema、不把本地化 Key 写入游戏存档。
- 不读取或修改 `Docs/design/`。

## 冻结决策

### Application 启动顺序

启动顺序固定为：

```text
ApplicationBootstrap
  → 创建 ApplicationHost 与 Application Scope
  → 加载、校验、迁移 Settings；失败时建立默认设置
  → 初始化 Unity Localization 并选择 Locale
  → 预热首屏所需 String Tables 与字体
  → Application Ready
  → 创建 Profile / Session
  → 场景 UI 绑定并显示
```

- Settings 和 Localization 由 Application Scope 唯一拥有，使用 Application 根取消令牌。
- 场景组件不得自行初始化 Localization 或直接修改 `LocalizationSettings.SelectedLocale`。
- `ApplicationHost` 进入 `Ready` 前，玩家可见 UI 根保持隐藏；完成首轮绑定后一次性显示，禁止先显示默认语言再闪切。
- 初始化取消、未来 Schema、Settings IO 失败和 Localization 初始化失败均返回结构化结果，不把半初始化服务暴露给 Session。

### Settings 数据与存储

- 正式路径固定在 `Application.persistentDataPath/DarkFlare/Settings`，与 `DarkFlare/Saves` 分离。
- V1 使用独立 `SettingsSchemaVersion = 1`、严格 UTF-8、确定性 JSON、文档大小上限和字段范围校验。
- 存储采用同目录临时文件、Flush、读后验证、原子提交和一份有效备份；损坏文件最多保留一份受控诊断副本。
- 必须提供 `0 → 1` 迁移夹具；未来 Schema 安全拒绝并回退默认值，不猜测解析。
- 设置修改以旧值 / 新值事务执行：校验 → 应用真实消费者 → 原子持久化 → 发布变更事件。应用或持久化失败时恢复旧值并返回稳定错误。
- Settings 文件至少包含以下域；未接入消费者的字段不进入玩家 UI，也不标记为已支持：

| 域 | V1 合同 | 0.2.3 玩家入口 / 消费者 |
| --- | --- | --- |
| Language | `auto`、`zh-Hans`、`en` | 正式开放；Localization Service 与 Game Menu 语言选择 |
| Audio | Master / Music / SFX / UI 范围和静音位 | 只冻结、校验、往返；0.2.5 接入 AudioMixer 后开放 |
| Input | Binding Override JSON、Glyph 偏好 | 只冻结、校验、往返；0.2.5 接入重绑定 / Glyph 后开放 |
| Display | 当前平台适用的基础显示偏好 | 只冻结、校验、往返；有真实显示消费者后开放 |
| Accessibility | 文本 / 动效等可访问性偏好 | 只冻结、校验、往返；0.2.5 接入消费者后开放 |

### Locale 选择与切换

- 首次启动优先使用受支持的系统语言；无法匹配时回退 `zh-Hans`。
- 已保存且有效的显式语言优先于系统语言；保存为 `auto` 时重新按系统语言解析。
- Pseudo Locale 只允许 Editor、Development Build 和自动测试选择，不写入正式用户设置，不出现在发行 UI。
- Locale 切换只能经过 Localization Service；服务负责并发仲裁、取消、表预热、失败回滚和 `LocaleChanged` 事件。
- 快速重复切换采用 latest-wins；被替换请求以 `Cancelled` 恰好完成一次，最终只有最新 Locale 可提交并写入 Settings。

### String Tables 与 Key

首批表固定按职责拆分：

| Table | 范围 |
| --- | --- |
| `ui` | HUD、菜单、背包、商店、打造、物品详情、交互提示与按钮状态 |
| `system` | Settings、存档、生命周期和玩家可操作错误语义 |
| `items` | 物品、物品类型、稀有度与装备槽显示 |
| `stats` | 23 项属性名称、说明和数值格式语义 |
| `affixes` | 物品词条与怪物词条名称、说明 |
| `monsters` | 怪物、角色和当前世界交互对象名称 |

- Key 使用小写语义路径，例如 `menu.save.action`、`item.rarity.magic`，不得包含中文、英文或文件路径。
- 内容配置引用 LocalizedString / 明确的 Table + Entry，不再把中文显示名作为运行时唯一来源。
- ContentId 与 Localization Key 必须独立；Key 变化通过本地化资产迁移处理，不修改存档身份。
- 静态 UXML 文本优先使用 Localization 1.5.12 原生 UI Toolkit Data Binding。
- 动态文本使用 Smart String、命名变量和文化感知格式；禁止用多个翻译片段拼接句子。
- 运行时快照只携带业务值和稳定内容引用；Controller 不缓存只对旧 Locale 有效的最终字符串。

### 字体与缺失内容

- 建立 UI Toolkit Text Settings / Font Asset 与 fallback 链，覆盖简体中文、英文、ASCII 数字、标点、百分号、乘号和当前界面符号。
- 现有字体必须先核对字形覆盖和项目可用性，再决定是否进入正式 fallback；不得仅因为文件存在就视为验收通过。
- 缺失 Key 或空翻译必须记录明确诊断，并按冻结的 `当前 Locale → zh-Hans → 可操作占位` 顺序回退。
- 正式验收中缺失、空翻译、重复语义 Key 和非法表引用均必须为零；可操作占位只用于故障路径，不作为正常交付。

## 实施切片

### 切片 0：文本与设置现状清单

- 生成玩家可见文本清单，覆盖 UXML、UI Controller、正式配置和系统错误。
- 将 74 / 77 / 85 的初扫候选分类为“必须迁移、Editor / 日志允许、内部 ID、待确认”。
- 冻结 Settings V1 字段、默认值、范围、平台适用性、消费者和开放阶段。
- 冻结表名、Key 命名、字体候选、fallback 顺序和硬编码扫描例外格式。

验收：每个候选都有唯一分类；没有“先保留以后再看”的未登记玩家文本。

### 切片 1：Settings 合同、迁移与原子存储

- 建立纯 Settings DTO、Validator、Serializer、Migration Pipeline Adapter、Path Provider 和 Storage。
- 从本地存档模块提取真正通用且无存档语义的底层原子文件原语；Settings 不得调用 SaveCoordinator 或存档槽位 API。
- 建立 Application 级 Settings Service、不可变快照、事务修改、变更事件和关闭 Flush。
- 覆盖首次启动、合法文件、损坏、备份回退、未来 Schema、IO 失败、并发修改和退出取消。

验收：设置加载先于 Profile / Session；测试只使用临时目录；真实存档目录不受影响。

### 切片 2：Localization 启动与 Locale 服务

- 创建 Localization Settings、`zh-Hans`、`en`、Pseudo Locale 和首批空表结构。
- 建立 Application 级 Localization Service 与结构化操作结果，接入启动顺序和 Settings 语言选择。
- 实现首屏表预热、缺失 fallback、latest-wins 切换和失败回滚。
- 在现有 Game Menu 增加最小语言选择入口，并保持键鼠 / 手柄导航。

验收：冷启动无语言闪切；显式语言重启恢复；快速切换只提交最后一次请求。

### 切片 3：静态 UI 与动态反馈迁移

- 将 GameRoot、HUD、Inventory、Shop、Crafting、ItemWorkbench、ItemDetail 的静态 `text` / `tooltip` 改为原生 UI Toolkit 本地化绑定。
- 将金币、生命 / 法力、词缀容量、价格、存档状态、打造反馈和交互提示改为 Smart String 或 Localization Service 格式化。
- 输入提示使用当前 Input System 的绑定显示信息；0.2.5 再替换为完整设备 Glyph，不继续硬编码 `E / Y`、`Esc / B`。
- Locale 切换时刷新动态 UI，但不重建领域状态，不改变当前页、物品选择、拖拽事务或焦点所有者。

验收：当前玩家路径中的 C# / UXML 可见中文字面量为零，登记例外除外。

### 切片 4：内容显示与字体迁移

- 为物品、属性、词条、怪物及真实可见角色 / 交互配置建立本地化引用。
- 更新 ItemDetail、当前属性、怪物 / 交互名称、商店和打造快照的解析边界。
- 保留 Editor 所需中文字段标签；运行时显示名改用本地化引用，验证器检查空引用、错表和重复语义。
- 建立字体与 fallback 资产，生成当前正式表字符集并执行缺字检查。

验收：七件装备、23 项属性、25 个物品词条、10 个怪物词条、三种怪物及玩家实际可见交互名称可在中英文间往返切换。

### 切片 5：自动策略、布局与回归

- 扩展基础设施策略测试：扫描 UXML 文本 / Tooltip 绑定、已知 UI 文本写入入口和正式配置本地化引用。
- 验证所有正式表零缺失、零空翻译、零重复 Key、零孤儿 Key、零非法表引用。
- 在 `1280×720`、`1920×1080`、`2560×1440` 下验证 `zh-Hans`、`en` 和 Pseudo Locale；关键按钮和详情不得截断、遮挡或失去可操作性。
- 回归保存、继续、新游戏、背包、装备、商店、打造、交互、菜单关闭和键鼠 / 手柄焦点。
- 更新模块文档、项目概览、目录结构和索引；完成后归档本执行计划，并将上位计划推进到 `alpha 0.2.4`。

验收：Unity 编译、全量 EditMode、项目自有 PlayMode 和完整 PlayMode 零失败；既有两项 Input System 跳过重新核验并记录上游条件。

## 测试矩阵

| 层级 | 必须覆盖 |
| --- | --- |
| EditMode：Settings | 默认值、范围、确定性 Round-trip、`0 → 1` 迁移、未来版本、损坏 / 备份、原子中断、并发与取消 |
| EditMode：Localization | Locale 解析、latest-wins、Key / Table 完整性、Smart String 参数、文化格式、fallback 与 Pseudo 限制 |
| EditMode：策略 | C# / UXML 玩家文本扫描、配置引用扫描、字体字符集、例外清单 Schema 和到期阶段 |
| PlayMode：启动 | 首次系统语言、保存偏好、损坏设置默认回退、无首帧语言闪切、Application / Session 顺序 |
| PlayMode：运行时 | 中英往返、快速切换、当前页 / 选择 / 焦点保持、动态数值与系统反馈同步刷新 |
| PlayMode：布局 | 三档分辨率 × `zh-Hans` / `en` / Pseudo，键鼠与手柄均可完成关键流程 |
| 回归 | `auto` 保存 / 继续 / 新游戏、连续 Session、退出 Flush、Console Error、任务和事件订阅无累积 |

## 风险与控制

- **首帧语言闪切**：Settings / Localization 必须进入 Application 启动门禁；UI 根首轮绑定完成前不可见。
- **UI Toolkit 动态文本未自动刷新**：静态文本使用原生 binding；动态文本统一订阅 LocaleChanged 并从业务快照重算，测试覆盖每个 Controller。
- **配置迁移混淆身份**：ContentId 不变，只新增本地化引用；存档继续只保存 ContentId。
- **硬编码扫描误报**：只扫描明确的玩家文本入口，并使用机器可读例外记录规则、文件、字面量、原因和移除阶段。
- **字体包体或授权风险**：先做字符集和来源审计，再冻结字体；不在未核验时扩充大字体资产。
- **0.2.3 越界实现 0.2.5**：只有语言设置开放；其他域只交付可迁移合同，不添加无消费者的玩家控件。
- **Localization Addressables 生命周期与现有 Loader 冲突**：本阶段只由 Localization Service 持有本地化初始化与表预热句柄；0.2.6 再纳入统一资源治理。

## 完成定义

- Settings V1、迁移、原子存储、损坏恢复、Application 生命周期和关闭行为均有正式实现与测试。
- `zh-Hans`、`en`、Pseudo Locale、职责分表、Smart String、字体 fallback 和最小语言入口全部可运行。
- 冷启动、运行时切换和重启恢复均无闪切、无半初始化、无焦点丢失。
- HUD、菜单、物品、属性、词条、商店、打造、怪物 / 交互名称和系统错误全部从本地化表取得。
- 正式表零缺失、零空翻译、零重复 Key、零孤儿 Key；玩家可见硬编码扫描零未登记违规。
- 三档分辨率与中英 / Pseudo 布局可操作；键鼠 / 手柄路径与 `alpha 0.2.2` 存档闭环回归通过。
- 新增 `Docs/docs/infrastructure/user-settings-localization.md`，同步项目概览、目录结构与索引；本计划归档并将下一阶段切换为 `alpha 0.2.4`。
