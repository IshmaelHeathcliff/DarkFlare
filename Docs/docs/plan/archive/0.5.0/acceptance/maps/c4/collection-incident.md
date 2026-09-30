# 一次性采集故障记录

2026-09-18 首次大图采集过程中 Unity 6000.6.0f1 发生原生崩溃。已完成的拓扑 / 边界像素审计不受影响；该次没有完整性能记录，不计为压力验收通过。

Editor.log 只报告 `Native Crash Reporting`、`Got a UNKNOWN while executing native code`，Managed Stacktrace 为空，无法据此确认引擎或项目的具体故障点。首张压力截图已产生，崩溃发生在后续采集流程。

恢复 Editor 后，一次性采集脚本的调度统一使用 UniTask，Unity API 调用前增加主线程检查和分步骤落盘记录。该修正用于排除采集器越线程的风险，不表示已证明它是崩溃根因。项目运行时代码未因这次采集故障改变。最终验收只采用恢复后的完整运行记录。
