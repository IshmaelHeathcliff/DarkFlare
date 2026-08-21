# alpha 0.2.5 输入、音频、可访问性与平台生命周期执行计划

> 状态：进行中；切片 0 已完成，切片 1 待执行
> 建立日期：2026-08-21
> 规划基线：`04f6d31`（`alpha 0.2.4` 完成提交）
> 实施前置：`alpha 0.2.4` 已独立提交，工作区干净；后续实现不得回写或混入上一阶段
> 上位计划：[alpha 0.2 基础设施开发计划](./alpha-0.2-plan.md)
> 强制契约：[alpha 0.2 基础设施约束契约](./alpha-0.2-infrastructure-contract.md)
> 前置模块：[应用生命周期与会话作用域](../infrastructure/application-lifecycle.md)、[用户设置与本地化](../infrastructure/user-settings-localization.md)、[游戏状态、场景流与应用 UI 外壳](../infrastructure/game-state-scene-flow-ui-shell.md)、[输入与运行时 UI](../input-ui-system.md)

## 阶段结论

本阶段在 Application 级 Settings、Localization、Scene Flow、Game Time 和 UI Shell 之上，补齐输入、音频、可访问性和平台回调的真实消费者。关键不是增加四组彼此独立的组件，而是让它们遵守同一条应用生命周期：设置最先加载，服务只创建一次，Session 只借用依赖，场景切换不重建应用服务，挂起与退出按固定顺序停止新输入、请求检查点并限时收敛。

现有 `UserSettingsSnapshot` 已冻结 Audio、Input 和 Accessibility 字段，因此本阶段默认保持 Settings Schema 1，不为“开始消费已有字段”创建无意义迁移。只有实际改变序列化形状或字段语义时才允许提升 Schema，并必须同时提供逐级迁移与旧样本。

输入的核心迁移是消除 Bootstrap UI 与 Session `GameInput` 各持有一套不可同步 Action 状态的问题。Application 级输入服务持有唯一运行时 Action Asset、绑定覆盖、活动设备族和 Glyph Resolver；QFramework 中的 `GameInput` 收缩为非所有权适配层。音频同样属于 Application，使用 AudioMixer、受控 Source 池和有所有者的播放句柄。可访问性只开放已有真实消费者的“降低动态效果”，文本缩放、高对比和屏幕震动继续保留为数据预留，不在 UI 中伪装为已支持。

## 实施进度

- [x] 切片 0：特征测试、合同与资产清单冻结
- [ ] 切片 1：Application Input Service 与 Session 适配
- [ ] 切片 2：重绑定、设备族、Glyph 与设置页面
- [ ] 切片 3：AudioMixer、Audio Service 与首个真实 Cue
- [ ] 切片 4：降低动态效果与平台生命周期
- [ ] 切片 5：跨模块故障演练、策略与回归
- [ ] 切片 6：模块文档、综合验收与计划归档

### 切片 0 完成记录（2026-08-21）

- 实施起点为 `04f6d31`；开始时只有本计划与索引文档变更，0.2.4 已独立提交。既有 GameInput、Settings 合同和 Application 生命周期专项基线为 `20/20` 通过。
- 新增 Application Input 纯合同，冻结 Gameplay / UI Context、五类 suspension reason、KeyboardMouse / Gamepad 设备族、七个正式可重绑定 Action、复合 Binding Part、结构化 Rebind Result / Conflict 和 Glyph Token。
- 七个可重绑定 Action 已与正式 `InputSystem_Actions.inputactions` 自动对照；每项均存在 Keyboard&Mouse 与 Gamepad 绑定，尚无真实消费者的 Attack / Look 不进入首版重绑定目录。
- 新增 Audio 纯合同，冻结 Master / Music / SFX / UI 分类、并发策略、结构化结果和首个稳定 CueId `ui.confirm`。项目当前音频资产数量为 0；切片 3 使用 Unity MCP Audio 生成能力创建唯一首批 UI Confirm 文件，并在 `Assets/Audio/UI/README.md` 记录提示、生成日期和来源，不导入来源不明的临时音频。
- 新增 `MotionProfile`，冻结默认时长倍率 1、Reduce Motion 时长倍率 0.2 和禁止持续动态；新增 Platform Lifecycle 状态、重叠 suspension reason、checkpoint urgency 与无 Session 安全结果合同。
- Settings 继续使用 Schema 1；测试确认 Audio、Input 和 Accessibility 三个预留域均已存在且有效，不新增迁移。
- 五份新增脚本通过 Unity 标准校验，均为 0 warning / 0 error；0.2.5 合同专项 `10/10`、项目 EditMode `369/369`、完整 EditMode `370/370` 通过。
- 本切片未创建运行时服务、AudioMixer、Glyph、设置页面或平台回调，也未修改 ApplicationHost、GameArchitecture、场景和现有运行行为。

## 已确认基线

- Unity 版本为 `6000.4.3f1`；规划基线提交为 `04f6d31`，工作区干净。
- `Assets/Settings/InputSystem_Actions.inputactions` 是唯一输入资产，包含 `Player` 与 `UI` Action Map，以及 `Keyboard&Mouse`、`Gamepad` 两个 Control Scheme。
- 当前真实输入包括移动、交互、菜单开关、UI 导航、提交、取消和拿起 / 放置；`Attack` 与 `Look` 仍未成为正式手动战斗消费者。
- `GameInput` 当前由每代 `GameArchitecture` 创建和销毁，每个 Session 都实例化一份 `InputSystem_Actions`；Bootstrap 的 `InputSystemUIInputModule` 直接引用原始输入资产，二者尚无统一绑定覆盖所有者。
- 当前交互提示通过 `InputAction.GetBindingDisplayString` 同时拼接所有非复合绑定；没有活动设备族、输入 Glyph、重绑定、冲突检测或恢复默认。
- `UserSettingsSnapshot` 已包含 Master / Music / SFX / UI 音量、Mute、Binding Overrides JSON、Glyph 偏好、Text Scale、Reduce Motion、Screen Shake 和 High Contrast，并已完成校验与持久化；运行时除 Language 外尚无消费者。
- Settings Service 只提供整份 Snapshot 的原子更新；当前只有 `WithLanguage` 辅助方法。输入、音频和可访问性事务需要补齐不可变更新入口和失败回滚。
- Bootstrap Application Shell 只有 NewGame、Continue、Language、Quit；Main Game Menu 只有 Save、Return FrontEnd、Close。当前没有统一设置页面。
- 项目中没有 `.mixer` 或正式音频文件；Runtime 只有 Main Camera 的 `AudioListener`，没有 `AudioSource`、Cue 配置、并发策略或句柄释放验证。
- 现有动态表现主要由 `ActorVisualFeedbackController`、`LootPickupVisual` 和 `DamageNumberVisual` 直接使用 PrimeTween；尚无统一动态效果偏好入口。
- `ApplicationHost` 已实现 `Application.wantsToQuit` 门禁、最终存档 Flush 与逆序释放，但没有 `OnApplicationFocus`、`OnApplicationPause` 或等价的平台挂起 / 恢复协调。
- `alpha 0.2.4` 验收基线为 Unity 编译 0 error、EditMode `360/360`、项目 PlayMode `52/52`；完整 PlayMode 54 项中 52 项通过、0 失败，2 项因 Input System 上游 issue 1252825 跳过。

## 目标

- 建立 Application 级唯一输入服务，统一 Player / UI Action Map、重绑定覆盖、设备族、Glyph 和输入暂停。
- 支持键鼠与手柄的安全重绑定、冲突反馈、取消、恢复默认和 Settings V1 持久化。
- 让 Bootstrap UI Module、FrontEnd、Application Shell 和每代 Session 消费同一运行时输入状态，不在场景切换或读档后重复注册。
- 建立可测试的 Glyph Resolver，当前提示只显示活动或偏好设备族，不再把键鼠与手柄显示文本拼在一起。
- 在 Application Shell 建立可从 FrontEnd 与 Paused Game Menu 到达的最小设置页面，并保持焦点、暂停和返回路径。
- 建立 AudioMixer、Application Audio Service、Cue 配置、音量 / 静音、并发、淡入淡出和句柄释放。
- 为至少一个现有 Application UI 交互接入真实 Audio Cue，并覆盖场景切换、取消和 Shutdown。
- 让 `ReduceMotion` 成为至少两个现有 PrimeTween 表现的真实消费者；未实现的可访问性字段不对玩家开放。
- 统一失焦、挂起、恢复和退出事件，保证输入暂停、检查点请求、设备恢复、限时 Flush 和服务释放 exactly-once。
- 用策略测试、故障注入和完整键鼠 / 手柄流程冻结唯一入口与资源所有权。

## 非目标

- 不接入完整手动攻击、瞄准、技能键位或新增玩法操作；`Attack`、`Look` 只保留现有预留。
- 不支持任意设备品牌的全部图标包；首版只覆盖当前 Input Actions 实际消费的键鼠和通用手柄控件，并提供文本后备。
- 不实现多玩家、本地合作、热座、多个 `InputUser` 或按玩家拆分设备。
- 不实现完整音乐系统、环境声、3D 衰减、语音、动态混音快照或全项目音效补齐。
- 不提前完成 `alpha 0.2.6` 的全局 Logger、进程级异常捕获、Addressables 分组重构和通用资源服务。
- 不开放没有真实消费者的 Text Scale、High Contrast 和 Screen Shake 设置；保留 Settings V1 字段不等于已支持。
- 不新增平台 SDK、云存档、后台下载、成就或主机认证流程。
- 不改变 Save Schema、Content ID、Scene Flow 状态、玩法规则或现有存档恢复语义。
- 不读取或修改 `Docs/design/`。

## 冻结架构

### 服务所有权与启动顺序

| 服务 | 作用域 | 创建顺序 | 释放顺序 | 主要消费者 |
| --- | --- | --- | --- | --- |
| `SettingsService` | Application | 已有，最先加载 | 最后关闭存储入口 | Input、Audio、Accessibility、Localization |
| `ApplicationInputService` | Application | Settings 之后、Localization 之前 | 停止平台回调后释放 Actions | Bootstrap UI Module、Shell、Session `GameInput` Adapter |
| `AudioService` | Application | Input 之后、Shell 之前 | 停止新播放、淡出 / 取消、释放句柄 | Shell、后续 Session Cue 调用方 |
| `AccessibilityService` | Application | Settings 之后 | 解除订阅并恢复临时状态 | Shell root、Main root、视觉表现 Adapter |
| `PlatformLifecycleService` | Application | 上述服务就绪后注册 | Shutdown 最先停止接收回调 | Input、Time、Save、Audio、Shell |

- `ApplicationHost` 只负责构造、暴露和逆序关闭服务，不吸收重绑定、Glyph、Mixer 或平台状态机实现。
- `ApplicationInputService` 持有唯一运行时 `InputSystem_Actions` 实例。Bootstrap 的 `InputSystemUIInputModule` 在 Shell Bootstrap 中改绑该实例，不继续使用一份独立状态。
- QFramework 注册非所有权 `GameInput` Adapter。ApplicationHost 存在时 Adapter 只借用 Application Input Service；独立 EditMode / PlayMode fixture 可以显式创建 standalone 输入所有者，并由 fixture 释放。
- Session 停止只解除 Adapter 事件和恢复 Session 输入上下文，不销毁 Application Action Asset；Application Shutdown 才执行最终 Dispose。
- 所有服务初始化和 Close 均幂等；重复 Session、场景切换、失焦 / 恢复不得增加订阅、Action、Source 或 Addressables 句柄计数。

### 输入模式、暂停与设备族

输入服务同时维护三个互相独立的状态：

- `InputContext`：`Gameplay` 或 `UI`，决定启用哪个 Action Map。
- `InputSuspensionReason`：FocusLost、PlatformSuspended、SceneTransition、Rebinding、Shutdown 等可组合原因；存在任一原因时两个 Map 都禁用。
- `InputDeviceFamily`：KeyboardMouse 或 Gamepad，只决定 Glyph 与交互提示，不改变绑定覆盖。

恢复输入时先移除对应 suspension lease，再恢复暂停前的 InputContext。重复释放 lease 幂等；失焦与平台挂起重叠时，只有最后一个原因解除后才恢复。Scene Flow Busy 与 Modal 继续负责 UI 交互阻断，不借用设备族状态表达业务页面。

活动设备族以最近一次有效 Action 或指针交互为准；模拟噪声、摇杆死区内变化和设备新增 / 移除本身不得无条件抢占显示。`InputGlyphPreference.Auto` 跟随活动设备，显式 KeyboardMouse / Gamepad 只覆盖显示族，不重写绑定或配对。

### 重绑定合同

首版设置页面只暴露已有真实消费者：

| Action | 键鼠 | 手柄 | 说明 |
| --- | --- | --- | --- |
| `Player/Move` | 四个复合方向 | 左摇杆 / 可选 D-pad | 逐 part 重绑定；不得留下空方向 |
| `Player/Interact` | 单按键 | 单按钮 | 驱动世界交互与提示 Glyph |
| `Player/ToggleMenu` | 单按键 | 单按钮 | 必须始终保留每设备族至少一个入口 |
| `UI/Navigate` | 四个复合方向 | 摇杆 / D-pad | 与 Gameplay Map 可复用相同控制，不判冲突 |
| `UI/Submit` | 单按键 | 单按钮 | 设置页确认路径 |
| `UI/Cancel` | 单按键 | 单按钮 | 重绑定期间保留硬取消安全路径 |
| `UI/Rearrange` | 单按键 | 单按钮 | 物品拿起 / 放置 |

- `Attack`、`Look`、指针、Click、Scroll 和 Tracked Device 不在本阶段的玩家重绑定列表中；它们继续使用资产默认绑定。
- 冲突只在同一 Control Scheme、同一 Action Map、同一时间可启用的玩家可重绑定项中判定。Player 与 UI Map 互斥，因此 Move / Navigate 使用同一键不构成冲突。
- 交互式重绑定先在内存临时应用，执行路径、设备族、必需入口和冲突校验；通过后调用 `SaveBindingOverridesAsJson`，再用 Settings Service 原子提交整份 Snapshot。
- Settings 提交失败时恢复旧 Overrides JSON、Glyph 和 UI 行显示；不得留下“运行时已改但磁盘未改”状态。
- 启动时先校验 JSON 长度和格式，再加载覆盖；无效或引用已删除 Binding 的覆盖返回结构化失败并回退默认，不阻止 FrontEnd 可操作。
- 恢复默认按设备族或全部绑定执行，并走同一原子提交路径。取消、超时、设备拔出和应用失焦都回滚临时覆盖。
- 重绑定期间 Input Service 获取 `Rebinding` suspension lease，只让捕获操作与专用 Cancel 路径工作，防止捕获按键同时触发菜单或场景操作。

### Glyph Resolver 与首版资产范围

`InputGlyphResolver` 输入 ActionId、可选 Binding part 和目标设备族，输出稳定 `InputGlyphToken`：控件路径、短显示名、可选 Sprite、是否后备。业务 UI 不读取 `Keyboard.current`、`Gamepad.current`，也不硬编码 `E`、`Esc` 或 ABXY 文本。

首版必须覆盖当前可重绑定项可能显示的控件：

- 键盘字母、数字、方向键、Enter、Escape、Space、Tab 和常用修饰键使用统一 Keycap 外壳与动态短文本。
- 鼠标左 / 右 / 中键仅提供 Resolver 后备，不加入重绑定页面。
- 通用手柄覆盖南 / 东 / 西 / 北、Start / Menu、D-pad 四向和左右摇杆方向。
- 未知设备或未知路径使用 Input System display string 后备；后备必须显式标记并保持可读，不显示原始 `<Device>/control` 路径。

首版 Glyph 资产按一图一 Sprite、固定画布和统一视觉尺寸生成 / 导入；执行时遵循 `unity-2d-sprite-workflow`。不建立生产 SpriteSheet，不依赖人工切片。Glyph 只属于显示身份，不写入存档或 Content Catalog。

### 设置页面与焦点

- 在 Application Shell 增加独立 Settings Page，可从 FrontEnd 和 Paused Game Menu 打开；两个入口共用同一 Controller 和 Settings Service。
- 页面首版包含 Audio、Input、Accessibility 三组；Language 保持现有 FrontEnd 入口，也可在设置页复用同一 Localization Service。
- Audio 暴露 Master、Music、SFX、UI 和 Mute；Input 暴露上述重绑定项、Glyph 偏好和恢复默认；Accessibility 只暴露 Reduce Motion。
- Text Scale、High Contrast、Screen Shake 和尚未接入的 Display 字段不渲染控件，不标记为已支持。
- 从 FrontEnd 打开时保存 FrontEnd 焦点；从游戏菜单打开时保留菜单 pause lease 和 UI InputContext。关闭后恢复原页面与有效焦点，不直接恢复 Gameplay。
- Rebinding、确认恢复默认和冲突反馈使用专用 Modal；非阻塞保存成功使用 Toast。Settings Page 不自行访问文件、InputActionAsset 或 AudioMixer。
- 所有标题、Action 名、设备名、冲突和失败消息进入现有 `ui` / `system` String Table，并覆盖 zh-Hans、en、qps-ploc。

### AudioMixer 与 Audio Service

建立 `Assets/Settings/Audio/DarkFlareAudioMixer.mixer`，固定分组：Master、Music、SFX、UI。每组只暴露一个稳定参数名，线性设置值 `0..1` 转换为 `20 * log10(max(value, 0.0001))`，最低钳制为 `-80 dB`；Mute 只压低 Master，不覆盖各分类保存值。

`AudioCueDefinition` / `AudioServiceConfiguration` 负责 CueId、类别、Clip 引用、音量、是否循环、并发上限、超限策略和淡入淡出。业务调用只使用 CueId，不直接持有 AudioClip、AudioSource 或 MixerGroup。

首版播放合同：

- Application Audio Service 拥有受控 Source 池与所有加载句柄；Scene / Session 调用必须提供 owner token 或 owner scope。
- 单次播放返回幂等 `AudioPlaybackHandle`。Owner 结束、取消、场景切换或 Shutdown 时停止并释放；自然结束也必须归还 Source。
- 同一 Cue 默认限制并发；超限策略显式选择 Reject New 或 Stop Oldest，不允许无限创建 GameObject / AudioSource。
- Music / Loop 支持基于 unscaled time 的淡入淡出；本阶段不建立复杂播放列表。
- AudioClip 通过专用 `IAudioClipLoader` / Addressables Adapter 获取，业务 Service 不直接调用 Addressables 静态 API。完整分组、Label 和跨 Loader 治理留给 0.2.6。
- 至少新增一个短 UI Confirm Cue，并接入 FrontEnd 的 NewGame / Continue 或 Application Modal 主确认动作；同一交互快速触发可验证并发上限和 Scene Flow 切换后的释放。
- 项目当前没有音频素材；首个 `ui.confirm` 在切片 3 使用 Unity MCP Audio 生成能力创建，并记录生成提示、日期与来源。生产文件必须具备可追踪来源，测试使用程序化或内存 Fake Clip，不把测试音写入正式资产。

### 可访问性消费者

本阶段正式支持 `ReduceMotion`：

- `AccessibilityService` 从 Settings 初始化并监听变化，公开只读 `MotionProfile`，不让视觉 Controller 直接读 Settings。
- `LootPickupVisual` 在 Reduce Motion 开启时禁用持续漂浮 / 旋转，保留静态可见与拾取反馈。
- `DamageNumberVisual` 或 `ActorVisualFeedbackController` 至少再接入一处：移除位移动画或把过渡缩短到冻结的低动态时长，但不移除关键信息。
- 设置修改立即作用于已存在与之后创建的表现；关闭后恢复默认时不会遗留被停止 Tween 或静态缓存。
- 所有 PrimeTween 仍由原表现对象拥有和停止；Accessibility Service 只提供策略，不持有场景 Tween。

`TextScale`、`ScreenShakeIntensity` 和 `HighContrast` 继续保留在 Settings V1 数据中。本阶段不新增屏幕震动来证明震动设置，也不通过整体 Panel 缩放冒充文本缩放。

### 平台生命周期状态机

`PlatformLifecycleService` 维护可组合原因，而不是把 Unity 回调直接映射成多次保存：

```text
Active
  ├─ FocusLost          → suspend input + platform pause lease + coalesced checkpoint
  ├─ PlatformSuspended  → suspend input + platform pause lease + urgent checkpoint
  ├─ Resuming           → refresh devices / glyph + restore prior input context
  └─ ShuttingDown       → reject new work + final bounded Flush + reverse release
```

- `ApplicationHost` 的 MonoBehaviour 回调只转发事实；保存、输入、音频和时间协调在独立服务中执行。
- 失焦与挂起重叠时只触发一次活动检查点；后续事件合并。没有 Running Session 时保存返回 `SessionUnavailable` 但不升级为玩家错误。
- Focus Lost / Suspend 获取 Input suspension lease 和 `GameTimeService` platform pause lease。恢复时先刷新设备与 Glyph，再释放对应输入 suspension；只有全部平台原因解除后才释放 platform pause lease。
- 既有菜单 pause lease 与平台 pause lease独立。恢复平台事件不得关闭菜单、重建 Session 或覆盖玩家原有暂停状态。
- 挂起检查点使用当前 `SessionSaveFacade` / Save Coordinator 的单写者语义；不得从回调直接访问文件。平台不给足时间时允许返回明确 Timeout / Deferred 结果，已原子提交代际仍有效。
- 退出继续使用现有 `wantsToQuit` 门禁：停止新输入和新音频 → 请求最终 Capture / Flush → 停止 Session → 释放 Application 服务 → 只放行一次 Quit。
- 重复 Focus / Pause / Quit 回调、Editor Play / Stop 和连续三次 Session 不得重复订阅或重复释放。

## 迁移清单

| 当前入口 | 0.2.5 目标 |
| --- | --- |
| `GameArchitecture` 每代创建 / Dispose `GameInput` | Application 唯一输入所有者 + Session 非所有权 Adapter |
| Bootstrap `InputSystemUIInputModule` 直接引用资产 | Shell Bootstrap 绑定 Application runtime Action Asset |
| 交互提示拼接所有绑定显示文本 | 活动 / 偏好设备族的 Glyph Token |
| Settings 仅 `WithLanguage` | Audio / Input / Accessibility 不可变更新与原子回滚 |
| 无设置页面 | Application Shell 统一 Settings Page，FrontEnd / Paused 共用 |
| 无 AudioMixer、AudioSource 或 Cue | Mixer + Audio Service + Loader + Source 池 + 首个 UI Cue |
| PrimeTween 表现忽略 Reduce Motion | Accessibility MotionProfile + 两个真实消费者 |
| 只有 `wantsToQuit` / `OnApplicationQuit` | Platform Lifecycle 状态机统一 Focus / Suspend / Resume / Quit |
| 输入策略只限制零散设备访问 | Input / Audio / 平台回调唯一入口扫描和精确例外 |

## 实施切片

### 切片 0：特征测试、合同与资产清单冻结

- 为当前 `GameInput` Action Map、Bootstrap UI Module、Settings V1、Application quit gate 和 PrimeTween 表现补齐特征测试。
- 冻结 InputContext、Suspension lease、DeviceFamily、Rebind Request / Result / Conflict、Glyph Token、Audio Category / Cue / Handle、MotionProfile 和 Platform Lifecycle Result 合同。
- 冻结可重绑定 Action 清单、冲突范围、必需逃生绑定、首批 Glyph 清单和首个 Audio Cue 来源。
- 记录 `04f6d31` 为实施起点；确认 0.2.4 工作区无混入变更。

验收：纯合同和 Fake Adapter 可在 EditMode 验证；没有未决定的服务所有者、绑定冲突语义、音频来源或平台保存入口。

### 切片 1：Application Input Service 与 Session 适配

- 建立 Application Input Service，持有唯一 runtime Action Asset、InputContext、suspension lease 和设备状态。
- 调整 ApplicationHost 启动 / 关闭顺序并暴露只读服务入口。
- 将 QFramework `GameInput` 改为非所有权 Adapter；standalone 测试使用显式 owner fixture。
- 让 Bootstrap `InputSystemUIInputModule` 绑定同一 runtime Action Asset，验证 FrontEnd 与 Main 共用覆盖。
- 保持现有 Gameplay / UI 模式、菜单 Cancel、键盘 / 手柄移动和交互行为不变。

验收：连续三次 Session 只有一个 Action Asset owner 和一组全局设备订阅；Session Dispose 不破坏 FrontEnd UI 导航。

### 切片 2：重绑定、设备族、Glyph 与设置页面

- 实现可重绑定清单、复合绑定 part、冲突检测、必需入口、取消 / 超时 / 拔出回滚和恢复默认。
- 接入 Settings V1 Binding Overrides JSON 与 GlyphPreference；提交失败恢复旧 runtime 覆盖。
- 实现 DeviceFamily 跟踪与 Glyph Resolver，制作 / 导入首批独立 Glyph 资产和文本后备。
- 在 Application Shell 添加 Settings Page；从 FrontEnd 和 Paused Game Menu 接入，完成焦点与 pause lease 恢复。
- 更新本地化表、UXML / USS 和策略测试。

验收：改绑 Interact / ToggleMenu / Submit 后立即生效，重启 Application 后恢复；冲突、取消和恢复默认均保持可操作，Glyph 同步更新。

### 切片 3：AudioMixer、Audio Service 与首个真实 Cue

- 创建四组 AudioMixer、稳定 exposed parameters、Audio 配置和专用 Clip Loader。
- 实现 Audio Service、Source 池、播放句柄、owner 取消、并发策略、自然结束回收和 unscaled fade。
- 接入 Settings V1 音量 / 静音并支持失败回滚；设置页实时预听 UI 分类。
- 引入一个有来源记录的短 UI Confirm Cue，接入现有 Application UI 主确认动作。
- 增加 Addressables Adapter 精确加载 / 释放测试；0.2.6 前不扩张完整资源分组治理。

验收：分类音量和静音立即生效并持久化；快速重复、Scene Flow 取消、返回 FrontEnd 和 Shutdown 后 Source / handle 计数回零。

### 切片 4：降低动态效果与平台生命周期

- 建立 Accessibility Service / MotionProfile，接入 LootPickup 与第二个现有 PrimeTween 表现。
- 设置页只开放 Reduce Motion，并验证运行中即时切换、后创建对象和 Locale 切换。
- 建立 Platform Lifecycle Service，接入 Focus Lost、Pause / Suspend、Resume 与现有 Quit Gate。
- 使用输入 suspension、Time pause lease、Save Coordinator 单写者和 Audio 暂停 / 恢复能力编排生命周期。
- 覆盖重叠 Focus / Suspend、无 Session、保存 Busy、保存失败、超时、重复 Resume 和退出竞态。

验收：失焦 / 挂起不会继续接受玩法输入，不重复保存或注册；恢复后设备 / Glyph / 原输入上下文正确，菜单暂停仍保持；退出 Flush 结果明确。

### 切片 5：跨模块故障演练、策略与回归

- 为无效 Binding JSON、冲突、Settings 提交失败、设备拔出、Glyph 缺失、Clip 加载失败、Mixer 配置缺失和挂起 Flush 超时注入故障。
- 建立唯一入口策略：Runtime 设备状态、AudioSource / AudioMixer、Application Focus / Pause 回调和 binding override API 只允许基础设施实现访问。
- 验证 FrontEnd、NewGame / Continue、Paused Settings、返回前台、Fatal / Recoverable Modal 在键鼠和手柄下完整可达。
- 验证三次 Session 与三次 Focus / Resume 后 Action、订阅、pause lease、Source、Clip handle 和焦点无累积。

验收：全部可恢复故障回到原稳定态并提供本地化反馈；不可恢复配置阻止服务 Ready，不留下半应用设置或失效资源。

### 切片 6：模块文档、综合验收与计划归档

- 新增输入 / 音频 / 可访问性 / 平台生命周期模块文档，更新项目概览、目录结构、应用生命周期、设置、本地化、场景流和输入 UI 文档。
- 运行 Unity 编译、全量 EditMode、项目 PlayMode、完整 PlayMode、三语言 / 三分辨率 Settings Page 和真实 Play / Stop。
- 手动验证键鼠与手柄从 FrontEnd 到 InGame、Paused Settings、重绑定、音量、Reduce Motion、失焦 / 恢复和返回前台。
- 恢复 `EnterPlayModeOptions = 0`；确认 Console Error 为 0、临时音频 / 截图已清理、`git diff --check` 通过。
- 完成后归档本计划，将上位计划推进到 `alpha 0.2.6`。

验收：完成定义全部满足；既有两项 Input System 上游跳过可以保留，不得新增忽略或用宽泛白名单掩盖失败。

## 测试矩阵

| 层级 | 必须覆盖 |
| --- | --- |
| EditMode：输入合同 | Context、suspension lease、设备族、重复恢复、standalone owner、Application owner |
| EditMode：重绑定 | 单键、复合 part、同 Map 冲突、跨 Map 允许、必需入口、取消、超时、拔出、恢复默认 |
| EditMode：持久化 | Overrides JSON round-trip、无效 / 过大 JSON、Settings commit 失败回滚、Schema 1 保持 |
| EditMode：Glyph | 每个暴露 Action × 两设备族、偏好覆盖、未知路径后备、无原始 control path 泄漏 |
| EditMode：音频 | 线性到 dB、Mute、分类路由、并发策略、owner 取消、自然结束、fade、句柄 exactly-once release |
| EditMode：可访问性 | MotionProfile 即时更新、默认恢复、两个真实消费者合同、未支持字段不出现在 UI |
| EditMode：平台 | Focus / Suspend 合并、Resume 顺序、无 Session、Save Busy / Failure / Timeout、Quit exactly-once |
| PlayMode：唯一性 | Bootstrap 与连续 Session 共用一个输入 owner、一个 UI Module、一个 Audio Service、一个平台订阅组 |
| PlayMode：键鼠 | FrontEnd → NewGame / Continue → Interact → Menu → Settings → Save / Return → Error Recovery |
| PlayMode：手柄 | 同一完整路径，覆盖默认焦点、Submit / Cancel、重绑定和 Glyph 更新 |
| PlayMode：音频 | UI Cue 实际播放、音量 / Mute、快速重复、Scene Flow 取消、返回前台与 Shutdown 回收 |
| PlayMode：可访问性 | 运行中切换 Reduce Motion，现有与新建视觉对象均遵守且关键信息保留 |
| PlayMode：生命周期 | Focus Lost / Resume、Pause / Resume、与菜单暂停重叠、三次循环无累积 |
| PlayMode：布局 | zh-Hans / en / qps-ploc × 1280×720 / 1920×1080 / 2560×1440 的 Settings / Rebind / Conflict Modal |
| 策略与回归 | 输入、音频、平台唯一入口；存档、本地化、Scene Flow、UI、背包、商店、打造和退出 Flush |

## 风险与控制

- **Application 与 Session 各自持有 Actions**：切片 1 先迁移唯一 owner，再开发重绑定；用实例和订阅计数测试阻止双轨状态。
- **Bootstrap UI Module 缓存旧 ActionReference**：Shell Bootstrap 显式重绑 runtime asset 和各 UI Action；每次 Application 启动验证引用属于当前 owner。
- **交互式重绑定捕获了打开菜单 / 取消键**：先 suspension 正常 Map，保留专用取消路径，提交前验证每设备族必需入口。
- **复合方向只改一部分导致无法移动**：绑定清单以 part 为单位，整组提交前验证四向完整且 Control Scheme 一致。
- **Settings 提交失败造成内存 / 磁盘分叉**：所有输入、音频和可访问性变更使用旧 Snapshot + runtime rollback 的两阶段应用。
- **设备噪声导致 Glyph 抖动**：只接受超过死区的有效 Action / 指针输入，并对同族重复事件去重。
- **缺少正式音频素材**：切片 0 冻结唯一首批 Cue 与来源；没有来源记录的临时文件不得进入正式资产。
- **AudioSource 或 Clip handle 泄漏**：Service 独占池和 Loader，所有播放绑定 owner；自然结束、取消、抢占和 Shutdown 共用幂等回收路径。
- **Headless 测试不能可靠听见音频**：单元测试验证 Mixer 参数、Source 状态和句柄；真实 Play 只补充可听验收，不替代自动断言。
- **Reduce Motion 破坏可读反馈**：只移除持续 / 大幅位移，不删除伤害数字、死亡状态或拾取可见性。
- **Focus 与 Suspend 回调顺序因平台不同**：使用原因集合与幂等 lease，不依赖固定先后；测试覆盖 Focus→Suspend 和 Suspend→Focus。
- **挂起时文件 IO 时间不足**：复用 Save Coordinator 原子代际和单写者；保存结果可超时但不得生成半文件。
- **恢复误解除菜单暂停**：平台 pause lease 与菜单 lease 分离，只释放自身 owner。
- **ApplicationHost 膨胀**：Host 只转发生命周期事实并持有服务；Input、Audio、Accessibility、Platform 各自独立目录和接口。
- **阶段越界到 0.2.6**：本阶段只新增 Audio 专用 Loader 和精确规则，不重构现有 Prefab / Sprite Loader 或 Addressables Group。

## 预计文件边界

新增或重点修改范围：

- `Assets/Settings/InputSystem_Actions.inputactions`
- `Assets/Settings/Audio/`
- `Assets/Audio/UI/`
- `Assets/Art/UI/InputGlyphs/`
- `Assets/Scenes/Bootstrap.unity`
- `Assets/UI/ApplicationShell.uxml`
- `Assets/UI/ApplicationShell.uss`
- `Assets/UI/GameRoot.uxml`
- `Assets/Localization/Tables/ui*.asset`
- `Assets/Localization/Tables/system*.asset`
- `Assets/Scripts/Runtime/Infrastructure/Input/`
- `Assets/Scripts/Runtime/Infrastructure/Audio/`
- `Assets/Scripts/Runtime/Infrastructure/Accessibility/`
- `Assets/Scripts/Runtime/Infrastructure/Platform/`
- `Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationHost.cs`
- `Assets/Scripts/Runtime/Infrastructure/Settings/SettingsModels.cs`
- `Assets/Scripts/Runtime/Gameplay/Input/GameInput.cs`
- `Assets/Scripts/Runtime/GameArchitecture.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/GameMenuController.cs`
- `Assets/Scripts/Runtime/Gameplay/UI/InteractionPromptController.cs`
- `Assets/Scripts/Runtime/Gameplay/Visuals/LootPickupVisual.cs`
- `Assets/Scripts/Runtime/Gameplay/Visuals/DamageNumberVisual.cs` 或 `ActorVisualFeedbackController.cs`
- `Assets/Scripts/Tests/EditMode/`
- `Assets/Scripts/Tests/PlayMode/`
- `Docs/docs/infrastructure/`
- `Docs/docs/input-ui-system.md`
- `Docs/docs/plan/`

预计不修改：

- `Docs/design/`
- Save / Content Schema 与迁移样本
- 玩法 Model、伤害、物品、装备、商店和打造规则
- Main 世界布局和场景流状态合同
- Prefab / Sprite Addressables Loader 的完整治理

## 完成定义

- Application 运行期只有一个输入 owner、一个 Audio Service、一个 Accessibility Service 和一个 Platform Lifecycle Service；连续 Session 不重复创建或注册。
- Bootstrap UI Module、FrontEnd、Shell 和 Session Adapter 使用同一 runtime Action Asset 与绑定覆盖。
- 可重绑定清单在键鼠 / 手柄下支持提交、冲突、取消、恢复默认和持久化；Settings 失败可完整回滚。
- 活动设备或 Glyph 偏好变化后，交互提示与设置页立即更新，不显示另一设备族或原始 control path。
- Master / Music / SFX / UI 和 Mute 实时应用并持久化；首个真实 Cue 经过 Service 播放，所有 Source / Clip handle 在 owner 结束后归零。
- Reduce Motion 在至少两个现有视觉表现中可测生效；未实现的可访问性字段不出现在玩家 UI。
- Focus Lost、Suspend、Resume 和 Quit 的输入、暂停、检查点、设备恢复、Flush 与释放顺序有结构化结果和故障测试。
- 键鼠与手柄均能完成 FrontEnd、NewGame / Continue、交互、暂停、Settings、背包、商店、打造、错误恢复和返回前台。
- Unity 编译、全量 EditMode、项目 PlayMode 和完整 PlayMode 零失败；只允许保留原有两项 Input System 上游跳过。
- zh-Hans、en、qps-ploc 三语言与三分辨率下 Settings / Rebind UI 可读、可聚焦、无关键裁切或重叠。
- Runtime 设备状态、AudioSource / Mixer、平台回调和 binding override API 的直接访问均受策略保护，例外精确且有移除阶段。
- Console Error 为 0，`EnterPlayModeOptions = 0`，无临时资源，`git diff --check` 通过。
- 完成后新增模块文档、更新相关文档，归档本计划并把上位计划推进到 `alpha 0.2.6`。
