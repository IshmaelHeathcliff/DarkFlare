# 可访问性与平台生命周期

> 状态：`alpha 0.2.5` 已完成；最近更新：2026-08-24
>
> 参考：[应用生命周期与会话作用域](./application-lifecycle.md) · [本地存档与 Session 恢复](./local-save.md) · [Application Audio](./application-audio.md)

## 可访问性服务

`ApplicationHost` 在 Application 作用域内创建唯一 `AccessibilityService`。服务从 Settings Schema 1 的 `ReduceMotion` 生成不可变 `MotionProfile`，提交成功后广播变化；设置写入失败时保持旧 Profile。

设置页只开放已有真实消费者的“降低动态效果”：

- `LootPickupVisual` 在降低动态时停止并复位持续漂浮和旋转；关闭设置后恢复表现。
- `DamageNumberVisual` 保留数字与淡出可读性，移除位移并把持续时间缩短到 0.13 秒。

文本缩放、高对比和屏幕震动仍只是数据预留，不显示在玩家设置页，也不声明为已支持。

## 平台生命周期服务

`PlatformLifecycleService` 独占 Focus / Pause / Resume 的编排，`ApplicationHost.OnApplicationFocus` 与 `OnApplicationPause` 只转发平台事实。Focus Lost 与 Suspend 是可重叠原因；第一个原因进入挂起，最后一个原因移除后才恢复。

挂起顺序：

1. 获取独立的 Platform 输入 suspension lease。
2. 获取独立的 `GameTimeService` pause lease。
3. 暂停 `AudioService`。
4. 当前 Session 可用时，请求本次挂起 episode 唯一的检查点。

恢复顺序：

1. 刷新 Input System 设备与活动设备族。
2. 恢复 Audio。
3. 释放 Platform 自己的 time pause lease。
4. 最后释放 Platform 输入 suspension。

菜单暂停 lease 与 Platform lease 彼此独立；恢复平台状态不会解除玩家仍打开的暂停菜单。无 Session、保存 Busy、保存失败、超时和取消都返回结构化 `PlatformLifecycleResult`，不制造半提交文件，也不提前恢复输入。

## 退出与关闭

`BeginShutdown` 先关闭新的平台事务入口；Quit Gate 继续执行最终 Capture / Flush 和 Session 收敛。`Close` 幂等释放平台持有的输入与时间 lease，恢复 Audio 状态，然后 Application 按反向创建顺序销毁其余服务。

平台转发任务由 Application 作用域跟踪，关闭期间使用已捕获的服务实例，避免宿主清空字段后迟到 continuation 访问已释放对象。

## 验证与边界

- EditMode 覆盖 MotionProfile 成功 / 回滚、两个表现消费者规则、Focus / Suspend 重叠、无 Session、Busy、保存失败、超时、取消与关闭。
- PlayMode 验证 Application 级 Audio / Accessibility / Platform 唯一实例、真实 Cue、挂起 / 恢复以及 Shutdown 无残留。
- 策略测试限制平台回调只能由 `ApplicationHost` 声明，Input lease、AudioSource / AudioListener / Mixer 与绑定覆盖 API 只能由登记 owner 使用。
- `alpha 0.2.5` 全量 EditMode `409/409`、项目 PlayMode `53/53`；完整 PlayMode 57 项中 55 项通过、0 失败，2 项为 Input System 上游既有 Ignore。

移动端后台时限、主机平台认证回调和系统级音频中断尚未接入；新增平台适配必须继续转发到本服务，不得在业务组件重复编排保存、输入或时间。
