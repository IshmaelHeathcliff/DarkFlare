# alpha 0.2 基础设施约束契约

> 状态：规划期强制契约
> 建立日期：2026-08-17
> 适用范围：`alpha 0.2` 全部实现、迁移、测试和后续新增运行时代码
> 上位计划：[alpha 0.2 基础设施开发计划](./alpha-0.2-plan.md)

## 使用方式

本文使用“必须”“禁止”“允许”表达强制级别。阶段实现可以在独立执行计划中补充细节，但不能静默绕过本文。确需例外时，必须提交机器可读白名单，记录规则、文件、原因和移除阶段，并在版本封板前复核。

本文是规划期契约，不代表当前代码已经全部符合。现有技术债在对应阶段迁移；新增代码从 `alpha 0.2.0` 起不得扩大债务面。

## 基础设施总清单

| 类别 | 能力 | alpha 0.2 交付级别 |
| --- | --- | --- |
| 应用基础 | Composition Root、服务注册、作用域、启动 / 关闭、统一取消 | 完整交付 |
| 游戏流 | 应用状态、Session、场景加载、Loading、失败回滚 | 完整交付 |
| 数据身份 | 内容 ID、实例 ID、内容注册表、Schema 版本 | 完整交付 |
| 数据演进 | 配置、设置、存档迁移与旧版本夹具 | 完整交付 |
| 存档 | DTO、快照、槽位、原子写入、备份、恢复、自动保存 | 完整交付 |
| 本地化 | Locale、String Table、格式化、字体、伪本地化、扫描 | 完整交付 |
| 用户设置 | 语言、音频、输入、显示、可访问性与迁移 | 完整交付 |
| UI 基础 | Page、Modal、Toast、Busy、焦点栈、错误反馈 | 最小可用闭环 |
| 输入 | Action Map、重绑定、冲突、Glyph、设备切换 | 最小可用闭环 |
| 时间 | 暂停、缩放、逻辑时间与不受缩放时间的统一入口 | 完整交付 |
| 随机 | 登记随机通道、可复现状态、存档恢复 | 完整交付 |
| 音频 | AudioMixer、分类音量、播放、并发和释放 | 最小可用闭环 |
| 可访问性 | 文本 / 动效等真实可消费设置 | 最小可用闭环 |
| 平台生命周期 | 失焦、挂起、恢复、退出、限时 Flush | 最小可用闭环 |
| 资源 | Addressables 地址、分组、加载、释放、验证、构建 | 最小可用闭环 |
| 可观测性 | 结构化日志、异常、诊断摘要、故障上下文 | 完整交付 |
| 性能 | Marker、耗时 / 分配 / 句柄预算与回归 | 最小可用闭环 |
| 构建发布 | PlayerSettings、Build Profile、版本、产物、报告 | 完整交付 |
| 工程质量 | 本地 CI、测试门禁、策略扫描、例外清单 | 完整交付 |
| 开发工具 | 存档查看 / 校验、Locale 预览、状态诊断 | 最小可用闭环 |
| 环境管理 | Development / Release 配置和功能开关 | 最小可用闭环 |
| 用户数据 | 删除 / 重置本地数据、诊断导出边界 | 完整交付 |
| 延期能力 | 云存档、账号、联网、远程配置、热更新、分析、Mod | 仅登记扩展点 |

## 作用域与所有权

| 作用域 | 典型状态 | 创建 | 释放 |
| --- | --- | --- | --- |
| Application | Logger、Settings、Localization、BuildInfo、SceneFlow、根 Cancellation | 进程启动一次 | 应用关闭 |
| Profile | 当前槽位元数据、Profile 状态、Save Coordinator | 选择新游戏 / 继续游戏 | 返回前台或切换槽位 |
| Session | QFramework 玩法架构、随机状态、玩家 / 背包 / 装备 / 经济、Session Cancellation | 创建新游戏或成功恢复存档 | 离开当前游戏 |
| Scene | 场景对象、摄像机、场景 UI、刷怪器、场景 Addressables 句柄 | 场景加载事务 | 场景卸载事务 |

- 每个服务、任务、事件订阅、资源句柄和运行时对象必须有唯一所有者。
- 长生命周期对象禁止反向持有短生命周期对象；需要通信时使用 ID、只读快照、事件或受控句柄。
- 初始化和释放必须幂等。部分初始化失败时，只释放已经成功创建的部分。
- 静态可变状态必须有明确重置入口并通过重复 Session 测试；优先避免静态业务状态。
- `DontDestroyOnLoad` 只允许应用宿主管理的对象使用，且必须纳入唯一性测试。

## QFramework 接入

- Controller 只注册事件、读取 Query 和发送 Command，不直接写 Model、不直接保存文件、不切换场景、不操作全局时间。
- Model 持有领域状态；System 负责跨 Model 规则和长期运行行为；Utility 只封装无领域状态或明确外部能力。
- 应用级服务不应依赖场景 Controller。需要接入 QFramework 时，由 Composition Root 注入 Adapter。
- 领域事件表达已经完成的事实，不作为要求外部服务执行事务的唯一可靠队列。
- 存档恢复通过专用 Restore Transaction 写入 Model，禁止 UI Controller 逐项重放玩家操作。

## 异步与线程

- 所有 UniTask 必须接收或取得所属作用域的 `CancellationToken`。
- 长任务必须定义超时、取消、异常和重复请求策略；取消与失败不得混为成功。
- 禁止无人观察的 `UniTaskVoid` / `Forget()`。事件桥接如无法返回 Task，必须通过统一的受监控异步运行器记录异常。
- Unity 对象、QFramework Model 和场景状态只能在主线程读取或修改，除非 API 明确线程安全。
- 后台线程只处理冻结的纯 DTO、序列化、校验和文件 IO；回到主线程后必须再次检查作用域是否仍有效。
- 关闭顺序固定为：停止接收新请求 → 取消任务 → 限时等待 / Flush → 释放资源 → 反初始化服务。

## 稳定身份与版本

- 内容身份必须使用业务稳定 ID；禁止使用 Asset GUID、资源地址、文件路径、显示名称和 Unity Instance ID 作为存档身份。
- 运行时实例必须有独立实例 ID。内容 ID 回答“是什么”，实例 ID 回答“是哪一个”。
- ID 规则必须集中验证，禁止各模块自行改变大小写、前缀或空白处理。
- 显示文本和本地化 Key 均不得作为业务身份。
- `GameVersion`、`BuildVersion`、`ContentVersion`、`SaveSchemaVersion`、`SettingsSchemaVersion` 分开维护，禁止用一个整数替代全部版本。
- 迁移只允许逐级、确定性执行。业务代码不得长期保留散落的旧版本兼容分支。
- 未知未来 Schema 必须拒绝加载，不得尝试按当前格式猜测解析。

## 持久化数据分类

| 数据 | 归属 | 是否存档 | 说明 |
| --- | --- | --- | --- |
| 语言、音量、输入、显示、可访问性 | UserSettings | 是 | 独立于槽位，最早加载 |
| 背包、物品实例、装备、金币 | Profile | 是 | 使用稳定 ID 和纯 DTO |
| 玩家位置 / 资源、怪物、掉落、商人库存 | Run | 是 | 用于继续当前 Session |
| 随机通道、刷怪 / 冷却剩余逻辑时间 | Run | 是 | 不能只保存根种子 |
| UI 焦点、Hover、展开页、动画 | Transient | 否 | 恢复后按当前状态重建 |
| 投射物、命中特效、音频播放实例 | Transient | 否 | 读档后不恢复在途表现 |
| 配置 ScriptableObject | Content | 不复制 | 通过 ContentId 解析 |

## 存档规范

- 游戏存档禁止使用 `PlayerPrefs`；项目运行时代码默认不得调用 `PlayerPrefs`。
- 正式存档只写入 `Application.persistentDataPath` 下由 Storage 实现管理的目录。
- `Assets/Data/Saves` 只允许存放 Editor 测试夹具或样例，且不得被运行时当作可写目录。
- 领域模块只提供 Snapshot / Restore Adapter，不直接访问 `File`、`Directory` 或 Serializer。
- DTO 必须是纯数据，不包含 `UnityEngine.Object`、委托、Task、CancellationToken、场景引用或运行时服务。
- 存档必须带 Header、Schema、版本、槽位、时间、完整性信息和状态摘要。
- 写入必须使用临时文件、校验、原子提交和备份；禁止直接截断唯一有效主文件后写入。
- 同一槽位同时最多一个写事务；后续请求必须合并、排队或被明确拒绝。
- 快照必须在逻辑一致点取得。跨多个 Model 的状态由 Save Coordinator 在同一事务中冻结。
- 读档先完整解析、校验、迁移和解析 ContentId，再提交到运行时；任一步失败都不得留下半恢复状态。
- 存档错误面向玩家返回本地化错误码和恢复动作，内部日志保留异常链但不泄漏完整用户数据。
- 自动测试必须使用注入的临时 Storage Root，禁止触碰真实 `persistentDataPath`。

## 本地化规范

- 所有玩家可见文本必须来自本地化表，包括按钮、标题、Tooltip、物品 / 属性 / 词条 / 怪物名称、系统错误和输入提示。
- C#、UXML 和正式配置中禁止新增未登记的玩家可见硬编码文本。
- Debug 文本、日志、内部 ID、测试名和仅 Editor 使用的技术字段允许不本地化。
- Table 按职责拆分；Key 使用稳定语义路径，不包含具体语言文本。
- 内容 ID 与本地化 Key 分离；配置可引用 Key，但存档只保存内容 ID。
- 句子禁止通过多个翻译片段拼接；使用参数化条目和文化感知格式。
- Runtime Locale 切换必须通过 Localization Service，业务模块不散落直接修改全局 Locale。
- 缺失翻译在开发构建中必须显著记录，Release 使用冻结 fallback，并仍保留诊断事件。
- 字体和 fallback 是本地化交付的一部分；新增 Locale 前必须验证字形覆盖与布局预算。
- Pseudo Locale 只用于测试，不作为用户正式选项或存入 Release 默认设置。

## 用户设置规范

- Settings 与游戏槽位分离，且必须在第一份玩家 UI 创建前加载。
- 每个设置项必须定义类型、默认值、范围、平台适用性、是否需要重启和至少一个消费者。
- 没有运行时消费者的设置不得显示为已支持。
- 设置修改先校验并应用，再原子持久化；应用失败必须回滚 UI 表示或给出明确错误。
- Settings Schema 使用独立迁移链；损坏时回退默认值并保留有限诊断副本。
- Locale、输入绑定、音量和可访问性均使用同一 Settings Service，不各自创建私有文件。

## 场景、状态与 UI 规范

- 只有 Scene Flow Service 可以调用运行时场景加载 / 卸载 API。
- 场景名称和路径不得散落在业务代码中；使用集中注册的 SceneId。
- 场景切换必须是可取消事务，包含前置检查、加载、激活、Session / Scene 绑定、失败回滚和旧场景释放。
- Loading UI 显示阶段与真实进度，不承诺无法测量的伪百分比。
- Page、Modal、Toast 和 Busy Overlay 职责分离；Modal 使用焦点栈并阻止底层输入，Toast 不抢焦点。
- 玩家可操作错误必须提供本地化说明和恢复动作；不可恢复错误进入 FatalError 状态。
- 暂停与场景状态分离：暂停不等于 Session 销毁，也不能让不受缩放的 UI / 保存任务停止。

## 输入规范

- `InputSystem_Actions.inputactions` 保持唯一输入资产，`GameInput` / Input Service 保持唯一运行时入口。
- Controller 不直接查键盘、鼠标或手柄设备状态。
- UI 文本不硬编码 `E`、`Esc`、手柄按钮名等按键；通过绑定显示名或 Glyph Resolver 获取。
- 重绑定覆盖必须可序列化、校验、恢复默认并处理冲突。
- 设备切换只改变输入上下文和 Glyph，不隐式覆盖玩家绑定。
- 新增玩法操作时必须同时定义键鼠、手柄、UI 导航和本地化输入说明。

## 时间与随机规范

- 只有 Time / Pause Service 可以修改 `Time.timeScale`。
- 每个计时器必须声明使用游戏时间、未缩放时间还是持久化逻辑时间。
- 保存的剩余时间不得直接依赖进程内绝对 `Time.time`。
- 玩法随机只通过 `GameplayRandomSystem` 的登记通道；禁止在业务逻辑直接使用 `UnityEngine.Random` 或临时 `System.Random`。
- 新增随机通道必须有稳定名称、派生规则、消费者和固定种子测试。
- 需要读档继续的随机通道必须保存完整状态或等价可证明的继续位置。

## 音频与可访问性规范

- 运行时音频只通过 Audio Service 播放，业务代码不直接创建无所有者 AudioSource。
- 音频实例必须声明类别、并发策略、作用域和释放路径。
- Master、Music、SFX、UI 音量使用 AudioMixer 参数并由 Settings 驱动。
- 可访问性项必须记录实际消费者、目标范围和测试方式；禁止只保存但不应用。
- 降低动态效果等设置必须能够覆盖 PrimeTween、特效或镜头表现的统一入口，而不是在各 Controller 写分支。

## 日志、异常与诊断规范

- 生产 Runtime 只通过 Logger 记录；除 Logger Adapter 外禁止直接 `Debug.Log*`。
- 日志必须使用稳定事件 ID 和类别，不依赖只适合人工搜索的自由文本。
- 关键事务必须携带必要上下文，例如状态、Session、槽位、ContentId 或 InstanceId，但不得记录完整存档和敏感系统路径。
- 玩家错误码与内部异常分离。玩家看到本地化消息，诊断记录原始异常链。
- 全局异常捕获必须防递归，且不能把进程级致命错误错误标记为已恢复。
- 诊断导出默认只包含版本、平台、设置摘要、存档 Header、最近日志和资源计数；导出完整用户数据必须另行确认。

## Addressables 与资源规范

- 正式运行时资源只通过资源服务 / Loader 获取；业务模块不直接调用 Addressables 静态 API。
- 地址使用稳定命名规范，但地址不是存档身份。
- 每次加载必须有与所有者绑定的释放路径；Asset Handle 与 Instance Handle 不得混用。
- 预热列表由内容或场景依赖声明，不允许 Bootstrap 手工无限追加隐式依赖。
- Addressables 分组、Label、重复地址、缺失引用和构建结果必须自动验证。
- 远程 Catalog、热更新和下载缓存不在 alpha 0.2 启用；相关配置保持关闭并登记未来扩展点。

## 构建、环境与功能开关规范

- 构建必须来自统一入口，并输出机器可读结果、日志、版本信息和失败码。
- Development 与 Release 配置显式分离；不得通过检查 `Application.isEditor` 代替全部环境策略。
- 功能开关必须集中定义、可查询、可记录来源；正式构建不允许未登记调试开关。
- 版本信息必须能在运行时诊断中读取，至少包含游戏版本、构建号、Git SHA、内容版本和 Schema。
- 任何测试、迁移、配置、本地化、Addressables 或 Player 构建失败都必须使质量门禁失败。

## 禁止 API 与唯一入口

| 禁止直接使用 | 唯一入口 | 允许例外 |
| --- | --- | --- |
| `File` / `Directory` / 原始存档路径 | Save / Settings Storage | Editor 工具、构建脚本、Logger 文件 Sink；必须在白名单目录 |
| `PlayerPrefs` | Settings / Save Service | 无默认例外 |
| `SceneManager.Load*` / `Unload*` | Scene Flow Service | Scene Flow 实现与测试 Adapter |
| 玩家可见字符串字面量 | Localization Service / String Table | 日志、测试、Editor 技术 UI |
| `Debug.Log*` | Logger | Logger Adapter、第三方包 |
| `Addressables.*` | Asset Service / Loader | Loader 实现与 Editor 验证工具 |
| `Keyboard.current` / `Gamepad.current` 等 | GameInput / Input Service | 输入实现与输入测试 |
| `Time.timeScale =` | Time / Pause Service | Time Service 实现 |
| `UnityEngine.Random` / 临时 `System.Random` | GameplayRandomSystem | 纯视觉随机须登记独立非玩法入口 |
| 保存 `UnityEngine.Object` / GUID / 路径 | ContentId + InstanceId DTO | 无 |
| 无所有者 `UniTaskVoid` / `.Forget()` | 受监控异步运行器 | 框架要求的事件桥接，仍必须被监控 |

## 自动执行矩阵

| 规则 | 主要门禁 |
| --- | --- |
| 只有 Storage 访问运行时文件 | Runtime 源码策略扫描 + Storage 单元测试 |
| 禁止 gameplay PlayerPrefs | Runtime 源码策略扫描 |
| 场景只经 Scene Flow | 调用点扫描 + PlayMode 快速重复切换测试 |
| 可见文本必须本地化 | C# / UXML / 配置扫描 + String Table 完整性测试 |
| Runtime 只经 Logger | 调用点扫描 + Logger Sink 测试 |
| Addressables 只经 Loader | 调用点扫描 + 句柄生命周期测试 |
| 输入只经 GameInput | 调用点扫描 + 键鼠 / 手柄 PlayMode 测试 |
| 时间与随机使用唯一入口 | 调用点扫描 + 固定时间 / 固定种子测试 |
| DTO 不含 Unity 引用 | 反射结构测试 + Round-trip 测试 |
| Schema 变更必须有迁移 | 版本登记测试 + 历史夹具迁移测试 |
| 服务可重复创建 / 释放 | 多 Session 生命周期 PlayMode 测试 |
| 资源和任务无累积 | 句柄 / 对象 / Cancellation 计数回归 |
| 规范例外不可无限期 | 白名单 Schema 校验 + 到期阶段检查 |

## 文档要求

每个基础设施模块完成时，模块文档必须至少包含：

- 模块目标、非目标和职责边界。
- 公共接口、QFramework 接入点和依赖方向。
- 所有权、作用域、初始化 / 释放顺序和取消策略。
- 数据结构、稳定 ID、Schema、默认值和迁移规则。
- 主线程 / 后台线程边界。
- 错误分类、玩家反馈、日志事件和恢复动作。
- 配置、Editor 工具、开发调试入口和 Release 行为。
- 自动测试、性能预算、已知限制和扩展方式。
- 禁止事项、唯一入口和策略扫描覆盖。

## 契约完成定义

- 本文全部“完整交付”能力已转化为正式模块文档和运行时代码。
- “最小可用闭环”能力均有真实消费者、设置 / 生命周期接入和自动测试。
- 禁止 API 扫描零未登记违规；临时白名单均有可追踪去向。
- 后续新增模块无需自行发明存档、本地化、场景、输入、时间、随机、日志和资源规则。

