# alpha 0.4 状态核心验收

日期：2026-09-13。Unity 6000.6.0f1，Windows Editor，通过 unity-pipeline 在当前项目运行。基线提交 `2e6ab0f`，本次结果对应其上的状态核心工作区改动；运行版本保持 `0.4.0-alpha`。

| 检查 | 当次结果 | 记录 |
| --- | --- | --- |
| Unity 重编译 | completed，failed=false，errors=[] | CLI recompile_status |
| 项目 EditMode | 486/486，失败 / 跳过 / 不确定均为 0 | [editmode.json](./editmode.json) |
| 状态核心 | 44/44，包含在上述 EditMode 中 | editmode.json 的 statusTests |
| 项目 PlayMode | 67/67，失败 / 跳过 / 不确定均为 0 | [playmode.json](./playmode.json) |

状态测试保护：统一与独立层、共享 / 逐层到期、满层默认与覆盖、最强候选接替、周期相位与边界、大步 / 小步 / 预算续处理、足额消费与驱散、来源绑定 / 更新 / 释放、不可变快照、准备 / 提交 / 精确回退、重入队列、注册代次及 Session 清理。完整 EditMode 同时覆盖配置发现、字段文档映射、内容身份及既有玩法规则；PlayMode 验证现有生命周期和玩法集成未回归。

本次测试维护将 Alpha017BaselineTests 中“固定十二个顶层类型”的历史断言调整为内容登记覆盖；保留原有代表性嵌套结构要求，增加资产引用与非序列化运行时数据不被误递归的负例。配置字段覆盖继续由 ConfigurationDocumentationCoverageTests 承担，没有通过忽略失败或删除独有覆盖放宽规则。

收尾已确认 Editor 停止播放，Reload Scene Only 基线保持启用且选项为 1。PlayMode 产生的 GameCjkFont 动态字形缓存已通过 Editor 清理，并恢复、重新导入测试前版本；字体资产不属于本次改动。

阶段 1 的交付边界见[状态模块](../../../status-system.md)。本次没有接入实际属性效果、伤害 / 控制、正式七异常资产、存档或 UI，因此没有相应新视觉验收，也不将状态系统总计划标为完成。未新增临时测试或截图脚本，核心回归按模块命名长期保留。
