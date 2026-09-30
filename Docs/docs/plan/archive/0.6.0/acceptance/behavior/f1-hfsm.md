# F1：独立 HFSM 验收

日期：2026-09-22。范围仅为 Alpha 0.6.0 的 F1，模块说明见[HFSM](../../../../../behavior/hfsm.md)。

## 结果

- Unity `6000.6.0f1` 实际重编译完成：`compilationFailed=false`，错误列表为空。
- EditMode `DarkFlare.Tests.HfsmTests`：**35 / 35 通过**，失败 / 跳过 / 不确定均为 0；工具报告耗时 0.07 秒，耗时不作为性能门禁。
- 核心引用审查：四个源文件仅使用 System 类型，归已有 `DarkFlare.Core`，没有新增或修改 asmdef，没有 Unity / Runtime / Editor / 行为树依赖。

## 行为覆盖

测试覆盖兄弟及跨层转移、父级和显式优先级、声明顺序、初始子链、忽略与重入、每步仅一次提交、零时间与非法时间、停止 / 重启 / Reset、错误图拒绝、不可变定义与实例隔离。

故障覆盖 Guard、Tick、Start Enter、转移中部分 Enter、Stop Exit、转移 Exit，以及回调重入。验证已进入状态恰好清理一次，退出失败后不宣称可安全重用，部分进入不报告成功提交。诊断覆盖多租约、环形覆盖、条件结果的时间戳、读快照不重新执行条件。

## 分配测量

同一纯 C# 上下文在两个叶状态间切换，先预热 1,000 次，再测量 10,000 次 Tick 和切换；分别开启与关闭诊断。使用现有项目方式：启用 Unity Profiler，`ProfilerRecorder` 采集当前线程 `GC.Alloc`，关闭帧末汇总选项，读取记录事件。

两种模式均先用 512 字节数组确认采集器实际捕获分配，然后清空记录再测量。两种模式的测量区间均为 **0 个 GC.Alloc 事件**，成功转移计数均为 11,000。测试结束恢复原 Profiler 开关并释放记录器及诊断租约。

本机 Unity 的 `GC.GetAllocatedBytesForCurrentThread()` 对正向探针返回 0，最初两项测试因此失败；已替换为上述有效方式，没有将失效计数器当作零分配证据。

结果仅证明预热后的纯 HFSM Tick / 状态切换及其诊断记录。构建、实例创建、Reset、租约、异常、业务回调和 UI 格式化不在零分配结论内。500 / 1000 实例的规模采集留到 F6，不外推为整场游戏性能。

## 复现与边界

```powershell
unity command recompile --format json
unity command recompile_status --format json
unity command run_tests --mode editor --filter DarkFlare.Tests.HfsmTests --async_tests true --format json
unity command test_status --format json
```

每个异步步骤等待完成再执行下一步。本次仅执行 HFSM 定向 EditMode 测试，没有重跑全项目 EditMode、PlayMode 或构建 Player。F2 行为树、F3 组合样例、F4 Animator、F5 可视化和 F6 综合验收尚未完成，版本保持 `0.5.5-alpha`，内容 / 存档 / 地图格式不变。
