# Application Audio

> 状态：`alpha 0.2.6` 资源治理已接入；最近更新：2026-08-25
>
> 参考：[应用生命周期与会话作用域](./application-lifecycle.md) · [用户设置与本地化](./user-settings-localization.md) · [长期运行时契约](./alpha-0.2-runtime-contract.md)

## 职责与所有权

`ApplicationHost` 在 Application 作用域内创建唯一 `AudioService`。场景和 Session 只能借用服务，不得创建 `AudioSource`、直接访问 `AudioMixer` 或释放 Application 资产。服务拥有 AudioListener 仲裁、Source 池、Audio Clip 租约、活动播放和待加载请求，并在 Application Shutdown 统一回收。

启动时通过 Application `AddressableAssetService` 加载地址 `infrastructure/audio/configuration` 的 `AudioServiceConfiguration`，完成 Mixer、分类组、参数和 Cue 校验后才允许播放。配置与 Cue 分别由 Application owner 持有 `AssetLease`，Shutdown 共用 owner Close 路径精确释放。

## 正式资源

| 资源 | 地址 / 合同 | 用途 |
| --- | --- | --- |
| `Assets/Audio/DarkFlareAudioMixer.mixer` | `MasterVolumeDb`、`MusicVolumeDb`、`SfxVolumeDb`、`UiVolumeDb` | Master / Music / SFX / UI 四级路由 |
| `Assets/Audio/AudioServiceConfiguration.asset` | `infrastructure/audio/configuration` | Mixer 组、参数名和 Cue 清单 |
| `Assets/Audio/UI/ui_confirm.wav` | `audio/ui/confirm` | 首个正式 UI 确认 Cue |
| `Assets/Audio/UI/README.md` | 来源、生成日期和方法 | 音频溯源记录 |

`ui.confirm` 由 Application Modal 确认与设置页音频预听消费。Controller 只提交稳定 `AudioCueId`，不持有 Clip 或 Source。

## 播放合同

- `PlayAsync` 必须携带 owner；加载中、播放中、自然结束、显式停止、抢占、取消和关闭共用幂等释放路径。
- `AudioPlaybackHandle` 精确对应一次播放；重复 `Stop` / `Dispose` 返回已完成，不得二次释放 Source 或 Asset Lease。
- Cue 可选择 `RejectNew` 或 `StopOldest` 并发策略。待加载请求也计入 owner 生命周期，`StopOwner` 会同时取消待加载和活动播放。
- Fade 使用 unscaled time；循环 Cue 停止时支持淡出。平台挂起只暂停当前 Source，恢复不会重建或重复播放。
- 服务创建 Application 级 Listener 并仲裁同一宿主下的 Listener 状态，避免无 Listener 的 UI 音频路径。

## 设置集成

`UserSettingsSnapshot.WithAudio` 原子更新 Master、Music、SFX、UI 与 Mute。Service 把线性 `0–1` 转换为 Mixer dB；Mute 写入最低电平。设置页的连续滑动由 Controller 合并提交，提交失败恢复旧 Snapshot 与旧 Mixer 状态，成功后使用 UI Cue 预听。

Settings Schema 仍为 1；本阶段只开始消费既有字段，没有改变序列化形状。

## 验证与边界

- EditMode 覆盖线性到 dB、并发、句柄幂等、待加载 owner 取消、Mixer 参数应用与 Settings 失败回滚。
- 资产测试冻结四个 Exposed Parameter、配置地址、Cue 地址、来源记录和 Addressables 登记。
- PlayMode 使用真实配置和真实 Clip 验证播放、Application 唯一实例、挂起 / 恢复和 Shutdown 回收。
- `alpha 0.2.6` 全量 EditMode `424/424`、项目 PlayMode `53/53`；完整 PlayMode 57 项中 55 项通过、0 失败，2 项为 Input System 上游既有 Ignore。

当前只接入一个 UI Cue，不代表完整音乐或战斗音频内容已经制作。分组、标签、跨加载器所有权与资源诊断见[日志、错误处理与 Addressables 资源治理](./logging-error-addressables-governance.md)。
