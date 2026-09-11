# ComputeBuffer 退出提示定位

2026-09-11，Unity 6000.4.3f1 / 2D Animation 14.0.3，Windows Player。

## 对照结果

| 场景 | 回退缓冲区 | 变形管理器 | 退出结果 |
| --- | --- | --- | --- |
| 只有默认 Camera / Light 的空场景；排除项目 ApplicationBootstrap | 有效，64 元素 × 64 字节 | 未创建 | 同样出现 ComputeBuffer GC 释放提示 |
| 同一个构建，仅增加 `-clear-fallback` | 调用包内 ClearFallbackBuffer 后失效 | 未创建 | 正常退出，提示消失 |

对应[基线读数](./gpu-empty-baseline.txt)、[基线日志](./gpu-empty-baseline.log)、[释放后读数](./gpu-empty-cleared.txt)、[释放后日志](./gpu-empty-cleared.log)。两个进程均自然退出，没有加载 Bootstrap / Main、装备、UI、Session 或玩家存档。

包源码 `Runtime/BatchedDeformation/GpuDeformationSystem.cs` 中，`CreateFallbackBuffer` 带 AfterSceneLoad 初始化属性，无条件创建静态 `s_FallbackBuffer` 并设置 `_SpriteBoneTransforms`。`ClearFallbackBuffer` 则由变形系统的 `Cleanup` 调用；空场景未创建 DeformationManager，无法进入这条释放路径。诊断通过 `Unity.2D.Animation.Runtime` 程序集反射读取该字段，并在第二次运行中仅调用 ClearFallbackBuffer，构成直接对照。

结论：这是既有 2D Animation 包的退出清理问题，不是测试正常 Log，也不是本次十槽 UI / 装备创建的缓冲区。它在进程退出后出现；运行中完整 Player 验收 Error / Warning 均为零。作为依赖已知问题记录，不据此声称完整进程日志零 Warning。

本轮未修改 PackageCache、升级依赖或加入反射访问内部成员的正式兼容层。后续处理应优先验证包的官方修复版本。诊断脚本、诊断场景与编译定义已清理，ApplicationBootstrap 恢复原文件；保留此证据，不增加版本专用长期测试。
