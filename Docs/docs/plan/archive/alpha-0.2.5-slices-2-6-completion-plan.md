# alpha 0.2.5 切片 2–6 收尾执行计划

> 状态：已完成（2026-08-24）
> 建立日期：2026-08-24
> 上位计划：[alpha 0.2.5 输入、音频、可访问性与平台生命周期执行计划](./alpha-0.2.5-input-audio-accessibility-platform-lifecycle-plan.md)
> 已完成前置：[切片 1 Application Input Service](./alpha-0.2.5-slice-1-application-input-plan.md)

## 目标与成功标准

本计划完成 alpha 0.2.5 剩余切片，并保持切片 1 的 Application 输入唯一所有权不回退。完成时应满足：

- 重绑定、设备族与 Glyph 只由 Application Input Service 管理，设置提交失败能恢复运行时覆盖。
- FrontEnd 与暂停菜单共用 Application Settings Page；关闭后恢复来源页面、输入上下文、暂停租约与焦点。
- Application 只创建一个 Audio Service；分类音量、静音、Cue 并发、Owner 释放和 Shutdown 均可测。
- Reduce Motion 由 Application Accessibility Service 提供，并被两个既有 PrimeTween 表现消费。
- Focus、Pause、Resume 和 Quit 由独立 Platform Lifecycle Service 编排，不重复保存或释放其他暂停所有者。
- 全量 EditMode、项目 PlayMode 与完整 PlayMode 零失败，只保留既有两项 Input System 上游 Ignore。
- 模块文档与目录入口同步，父计划归档，上位计划推进到 alpha 0.2.6。

## 执行顺序

### 1. 输入事务与显示身份

- 扩展 Settings Snapshot 的 Audio、Input 与 Accessibility 不可变更新入口。
- 在 Application Input Service 中加载并校验 Binding Overrides，跟踪有效设备族，并提供重绑定、恢复默认和 Glyph 查询。
- 把重绑定分为临时应用、合同校验、Settings 提交和失败回滚四步；交互捕获持有 Rebinding suspension lease。
- 先以动态 Keycap 文本和通用手柄 Glyph 覆盖正式清单；未知路径始终返回可读文本，不暴露原始 control path。

### 2. 共用设置页面

- 在 Application Shell 中增加 Audio、Input、Glyph Preference 与 Reduce Motion 控件。
- FrontEnd 按钮直接打开；Main 暂停菜单通过 Application Shell 请求打开，保留菜单 pause lease 和 UI Context。
- 重绑定、恢复默认确认和冲突复用 Application Modal；保存结果使用 Toast。
- 只渲染已经有真实消费者的设置，不开放 Text Scale、High Contrast 或 Screen Shake。

### 3. Application 音频

- 建立四组 AudioMixer、稳定参数名、Audio Service Configuration、专用 Clip Loader 和 Source 池。
- 播放句柄、Owner、自然结束、并发拒绝或抢占、暂停 / 恢复和关闭共用幂等释放路径。
- 使用 Addressables Adapter 加载首个 `ui.confirm` Cue；正式音频保存来源记录。
- FrontEnd 主确认动作通过 CueId 播放，不直接访问 AudioClip、AudioSource 或 Mixer。

### 4. 可访问性与平台生命周期

- Accessibility Service 订阅 Settings，发布 MotionProfile 变化。
- Loot Pickup 停止持续漂浮 / 旋转；Damage Number 在低动态模式下降低位移并缩短过渡。
- Platform Lifecycle Service 使用原因集合管理输入 suspension、平台 pause lease、音频暂停与合并检查点。
- ApplicationHost 回调只转发事实；服务返回结构化结果并安全处理无 Session、Busy、失败与超时。

### 5. 策略、回归与归档

- 增加 Input、Audio、Accessibility、Platform 的纯服务测试、故障注入和唯一入口策略。
- 增加连续 Session、设置页两入口、Cue 回收、Reduce Motion 与 Focus / Suspend 重叠 PlayMode 验证。
- 执行编译、专项测试、全量 EditMode、项目 PlayMode 和完整 PlayMode；检查 Console、场景脏状态和测试副作用。
- 总结模块文档，将父计划和本计划归档，并更新 `Docs/docs/README.md` 与 alpha 0.2 上位计划。

## 风险控制

- 不修改 Input Actions 默认绑定来代替运行时覆盖；恢复默认只移除 override。
- 不让 Settings Page 直接访问文件、Action Asset、AudioMixer 或场景 Tween。
- 不在 Unity Editor 打开场景时直接编辑场景 YAML；场景、Mixer、Addressables 与本地化资产统一通过 Unity MCP 或 Editor API 修改。
- 所有新资源均保留来源、Meta 和可验证引用；不提交测试输出、临时截图或 Editor Settings 副作用。
- 若现有代码与计划冲突，以代码为准并同步修正文档，不扩大到 alpha 0.2.6 的通用资源治理。

## 完成记录

- Input 完成持久化重绑定、冲突 / 取消 / 超时 / 拔出回滚、设备族、Glyph 与 Settings 双入口；输入资产和 Settings Schema 均未改动。
- Audio 完成四组 Mixer、配置、Addressables Cue、Source 池、并发、owner 取消、淡入淡出、平台暂停与精确句柄释放。
- Accessibility 完成 Reduce Motion Service 与 Loot Pickup / Damage Number 两个消费者；Platform 完成重叠 Focus / Suspend、检查点、恢复和 Shutdown。
- 策略测试冻结 Input、Audio 和 Platform 唯一入口；模块文档、项目概览、目录结构和索引已同步。
- 最终全量 EditMode `409/409`、项目 PlayMode `53/53`；完整 PlayMode 57 项中 55 项通过、0 失败，2 项为 Input System 上游 issue 1252825 的既有 Ignore。
