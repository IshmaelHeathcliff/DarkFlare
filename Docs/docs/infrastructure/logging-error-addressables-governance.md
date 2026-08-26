# 日志、错误处理与 Addressables 资源治理

> 状态：`alpha 0.2.6` 已完成；最近更新：2026-08-25
>
> 参考：[应用生命周期与会话作用域](./application-lifecycle.md) · [游戏状态、场景流与应用 UI 外壳](./game-state-scene-flow-ui-shell.md) · [Application Audio](./application-audio.md) · [长期运行时契约](./alpha-0.2-runtime-contract.md)

## 职责与所有权

`ApplicationHost` 在其他 Application 服务之前安装唯一正式 `ApplicationLogger`、256 条环形缓冲、全局异常监控、失败协调器和 `AddressableAssetService`。Host 创建前及 Shutdown 后由可重置的早期 Logger 承接 Editor / 启动日志；测试与 Subsystem Registration 必须重置静态状态。

Runtime 通过 `ApplicationLog` 写入，生产事件 ID 只能引用集中 `LogEventIds`。`UnityConsoleLogSink` 是 Runtime 唯一允许调用 `Debug.Log*` 的适配器；Editor / Development Console 路径接收完整等级，非 Development Player 的 Console 路径通过 `MinimumLevelLogSink` 从 Warning 起输出，256 条 Ring Buffer 始终保留完整等级。Sink 故障会被隔离，不会递归写回 Logger。技术日志不本地化，玩家消息只保存 String Table key 与参数。

## 日志与致命错误

每条 `ApplicationLogEntry` 包含单调序号、UTC 时间、等级、稳定事件 ID、消息、异常、Unity context 和有限上下文。环形缓冲覆盖最旧条目，不写磁盘、不上传。

`ApplicationExceptionMonitor` 订阅以下来源，并在 Dispose 时精确退订：

- `Application.logMessageReceivedThreaded`：忽略 Logger Adapter 自身输出；外部 Error 记录，Assert / Exception 提交致命候选。
- `AppDomain.CurrentDomain.UnhandledException`：保留原异常与 `IsTerminating`。
- `UniTaskScheduler.UnobservedTaskException`：忽略正常取消，其余提交未观察异步失败。

工作线程报告通过启动时的主线程 `SynchronizationContext` 回送。`ApplicationFailureCoordinator` 使用 first-failure-wins：首个根因驱动 Application `Failed` 与 Fatal Shell，重复报告只计数，不覆盖根因或重复打开界面。

## 玩家错误表现

`PlayerErrorCatalog` 不重写领域错误事实，只把 `SaveErrorCode`、`SceneFlowErrorCode` 和 `ResourceErrorCode` 映射为 `PlayerErrorPresentation`。表现包含技术码、本地化消息、严重度与允许动作；动作只允许 Dismiss、Retry、ReturnFrontEnd、Quit。

- Retry 只在领域结果证明可重试时出现，并重新提交请求。
- ReturnFrontEnd 只调用既有 Scene Flow，不由 UI 自行卸载场景。
- Application 必需资源和未处理异常只提供 Quit。
- 首批未处理异常与资源错误键已加入 `ui` 的 `zh-Hans` / `en` 表；`qps-ploc` 由 Pseudo Locale 基于正式文本生成。

## 资源服务合同

`AddressableAssetService` 是 Application 级资源入口，`AddressableAssetBackend` 是 Runtime 唯一静态 Addressables API 适配器。

| 类型 | 职责 |
| --- | --- |
| `AssetOwnerScope` | 声明 Application 或 Session 资源所有者；Close 幂等并释放全部租约 |
| `AssetLease<T>` | 成功加载的消费票据；Dispose 幂等且不能转移 owner |
| `ResourceLoadResult<T>` | 返回稳定错误码、租约与仅供诊断的原异常 |
| `ResourceDiagnosticsSnapshot` | 报告 active owner、entry、lease 与 in-flight 数量 |

相同稳定键使用单航班加载；同键不同类型返回 `TypeMismatch`。单个等待者取消不会破坏其他 owner；最后一个 waiter / lease 离开后释放后端句柄。Owner 提前关闭或代际失效时，迟到结果不得进入领域缓存。

Application 音频配置与 Cue 使用 Application owner；Prefab 与 Sprite Loader 使用当前 Session owner。Prefab 实例仍由 `Object.Instantiate` 与 `SessionObjectRegistry` 管理，不伪装成 Addressables instance handle。

## Addressables 资产合同

Editor 治理器 `Alpha026AddressablesGovernance` 通过 Addressables Editor API 应用并验证以下 15 个自有条目；`Default Local Group` 保持为空。

| 分组 | 数量 | Label | 地址前缀 |
| --- | ---: | --- | --- |
| `DarkFlare-Application` | 1 | `df.application`、`df.configuration` | `infrastructure/audio/configuration` |
| `DarkFlare-Session-Prefabs` | 6 | `df.session`、`df.prefab` | `gameplay/prefabs/` |
| `DarkFlare-Session-Sprites` | 7 | `df.session`、`df.sprite` | `ui/icons/items/` |
| `DarkFlare-Audio` | 1 | `df.application`、`df.audio` | `audio/ui/confirm` |

验证器检查 GUID、目标组、规范地址、必需 Label、资源类型、缺失资源、重复地址和默认组误放。Localization 自动管理组与 Unity 包组不做结构迁移，只参与通用重复地址检查。地址与 GUID 都不是存档身份；持久化继续只使用 `ContentId` / `InstanceId`。

## 验证与禁止事项

- 源码策略保证 Runtime 除 Console Sink 外零可执行直接 `Debug.Log*`，除 `AddressableAssetBackend` 外零静态 Addressables 加载 / 释放调用。
- EditMode 覆盖事件目录、环形顺序、Sink 隔离、失败去重、玩家动作、资源单飞 / 双 owner / 类型冲突及资产治理。
- 三轮 FrontEnd → NewGame → Main → FrontEnd 验证 Session owner、entry、lease 和 in-flight 全部回到 Application 基线。
- 真实 Bootstrap Play 达到 Application `Ready`；资源基线为 owner `2`、entry `1`、lease `1`、in-flight `0`，停止后 Console Error / Warning 为 `0`。
- 最终 EditMode `424/424`、项目 PlayMode `53/53`；完整 PlayMode 57 项中 55 项通过、0 失败，2 项为 Input System 上游 issue 1252825 的既有 Ignore。

禁止业务代码直接调用 `Debug.Log*`、`Addressables.*`、持有原始异步句柄，或用资源地址 / GUID 作为存档身份。当前不包含远端 Catalog、CDN、热更新、日志文件、遥测、玩家日志导出或通用 Addressables 实例池。
