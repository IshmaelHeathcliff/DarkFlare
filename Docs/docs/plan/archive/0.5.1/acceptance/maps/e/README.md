# E：地图编辑器验收

日期：2026-09-18。环境见 [environment.json](environment.json)，Unity 6000.6.0f1，Ryzen 7 PRO 8845HS / Radeon 780M。操作说明见[地图编辑器](../../../../../../maps/map-editor.md)。

## 行为证据

- [完整 EditMode](editmode.json)：697/697，地图文档、地表、几何、编辑事务及已有项目规则。
- [地图编辑最终定向回归](map-edit-final.json)：21/21，原子失败 / 取消、no-op、单调 Undo / Redo、重复请求、变体失配、定义改变、文件冲突、恢复冲突、坐标与连通性、舞台资源、真实鼠标 / 快捷键、AI 继续编辑和保存。
- [PlayMode](playmode.json)：77/77，无失败 / 跳过；本阶段不改变运行时主地图与存档格式。
- 测试临时源 / 定义使用独立 Assets 目录，完成后删除；不写正式玩家存档。原活动场景保持干净，恢复 Reload Scene Only。

撤销预算只清理地图载体；测试另外登记一个编辑器对象，越过本地图的有效 / 空撤销组后仍能恢复它。磁盘变化时冲突草稿独立保留，关闭新会话不覆盖旧草稿。

界面实拍：[地图工作台](workbench.png)、[SceneView 绘制舞台](scene-tools.png)、[城镇地表预览](town-draft.png)。最终编辑器状态与配置检查见 [final-validation.json](final-validation.json)。

## 性能与边界

[performance.json](performance.json)：64² / 128² / 256² 连续区域地图，每个尺寸三轮大面积 FillRect、完整正式地表预览、Undo / Redo 和静态几何编译。128² 大面积提交加预览约 0.36–0.53 秒；256² 提交约 0.24–0.81 秒，主线程仍有可感停顿。完整几何单列，不参与画笔逐帧预算。

[input-performance.json](input-performance.json)：在真实 SceneView 发出 MouseDown / MouseDrag / MouseUp 与 Repaint，逐格核对结果；每尺寸三轮，每轮十次反馈采样。128² 各轮反馈 P95 为 3.10 / 5.73 / 6.41 ms，笔画结束到完整预览为 0.48 / 0.63 / 0.61 秒，达到首期 ≤50 ms / ≤1 秒目标。256² 最慢完整笔画预览约 0.71 秒。画笔测量使用稀疏布局，后两轮含 64 个对象 / 16 个区域；不外推为任意密集 256² 的逐帧保证。

批次中的 64 个对象 + 16 个区域更新约 0.26–0.42 秒。测量中发现逐条 Upsert 重复序列化整图，已改为仅校验单条记录结构，再统一编译最终候选；修正前约 1.3–2.2 秒的数据不作为最终结果。

Mono 的 `GC.GetAllocatedBytesForCurrentThread` 此环境返回 0，不能解读为零分配。补采的 [memory.json](memory.json) 使用 Unity Profiler 帧级计数，包含整个 Editor 的工具调用、后台和绘制开销，不等同于操作自身精确分配。撤销采用有界语义载体估算，Unity 原生历史管理开销不包含在 JSON 文件大小内。

静态几何继续遵守 D 容量：有效源的可选几何超限不阻止保存，必须标 GeometryUnavailable；不保留旧几何充当当前结果。世界排序、跨图暂停恢复、Flow Field / A* 与 500 / 1000 怪物规模均留给后续阶段。本轮没有构建 Windows Player。

## 协议与样例

- [Patch V1 Schema](patch.schema.json)：从 MapEditService.DescribeSchema 导出，与操作字段表及现有 DTO 结构共用来源。
- [城镇生成 Patch](town.patch.json)、[野外生成 Patch](wilderness.patch.json)：对 MapEditorSamples.Blank 生成的 64² 空文档应用。对已存在 / 编辑过的图应重新读取 revision / hash，不能复用样例的旧基线。
- 两份 `.dfmap` 草稿和六份对象 / 区域定义保存在 `Assets/Data/Maps/EditorSamples/`；不加入正式 core / 玩家地图流程。

临时采样脚本位于 Library，完成本次验证后清理；长期保留 MapEditTests 的规则、文件 / 恢复及真实输入回归，不保留读取历史验收文件来证明当前成功的测试。
