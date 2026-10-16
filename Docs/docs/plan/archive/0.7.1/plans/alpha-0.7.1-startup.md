# Alpha 0.7.1 启动准备

状态：2026-10-06，**启动准备已完成并归档**。下文保留 2026-09-30 / 10-06 实施前基线与当时未执行项；首次实际阶段已交付 `0.7.1-alpha.1`，当前证据见[几何验收](../acceptance/geometry/README.md)。[技能面板执行计划](alpha-0.7.1-skill-board-plan.md)接管功能拆分与验收入口，里程碑顺序由[0.7 总计划](../../../alpha-0.7-plan.md)管理。

## 首次启动核对基线（2026-09-30）

| 项目 | 核对结果 | 依据 |
| --- | --- | --- |
| 分支与提交 | `main` / `e2b97d2223cb39cffc5dbac0cb6b1ca01c9f8b8d` | 2026-09-29 SemVer 对象与应用接入交付提交 |
| 运行版本 | `0.7.0-alpha.2` | `ProjectSettings/ProjectSettings.asset` 的 `bundleVersion` |
| Unity / URP | `6000.6.0f1` / `17.6.0` | `ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json` |
| 正式内容目录 | core v10 | `Assets/Data/Preset/Content/正式内容目录.asset` |
| 存档 / 设置 Schema | 9 / 1 | `Assets/Scripts/Runtime/Infrastructure/Persistence/VersionContracts.cs` |
| 地图交换格式 | 当前 DTO 与规范输出 V4 | `Assets/Scripts/Runtime/Maps/MapDocument.cs`、[地图合同](../../../../maps/README.md) |
| 正式地图源 | 城镇 V2 / r1、64×64；野外 V4 / r9、128×128 | `Assets/Data/Maps/World/town.dfmap`、`wilderness.dfmap`；历史格式读取时在内存升级 |
| 野外名额 | 入图 20、10 处范围触发各 8，共 100 个普通怪物；1 个独立 Boss 点 | 野外源文件的 11 个 `spawnBatches` 与 `bossSpawns` |
| Editor 运行选项 | Reload Scene Only，关闭 Domain Reload | `ProjectSettings/EditorSettings.asset`：选项启用且数值为 1 |

核对开始时唯一已有未提交改动为 `Assets/Settings/UI/Fonts/GameCjkFont.asset`，本次保留原状；代码、Packages 与 ProjectSettings 相对 HEAD 无未提交变更。本次只更新开发文档，不读取 `Docs/design/`。

## 规划时基线更新（2026-10-06）

当前为 `main` / `bd01a4c`；`8052a6c` 已完成版本化文档归档，随后 `bd01a4c` 升级 Unity 与包。Unity 当前为 `6000.6.4f1`（`12bfff696524`），URP `17.6.0`、Input System `1.20.1`、Addressables `2.11.2`、Localization `1.5.13`。运行版本仍为 `0.7.0-alpha.2`，core v10、存档 / 设置 Schema 9 / 1 与地图 V4 未推进。

本轮规划开始时工作区干净。以下 2026-09-29 测试结果属于升级前验收，本轮没有重跑；不能用它们宣称 `6000.6.4f1` 环境或技能面板功能已通过。

## 项目进度与验收边界

- Alpha 0.1–0.4：最小玩法循环、基础设施、十槽装备与物品交互、状态、消耗品、随机商店已完成，当前成果见[项目概览](../../../../project.md)和[玩法循环](../../../../gameplay-loop.md)。
- Alpha 0.5：四地表、地图编辑器、稳定世界排序、城镇 / 野外生命周期及 Flow Field / A* 批量导航已完成，见[地图模块](../../../../maps/README.md)。
- Alpha 0.6：独立 HFSM / 行为树、普通怪物、守卫 Boss、有限刷怪、手动重置及 Editor 综合交付已完成，见[行为模块](../../../../behavior/README.md)。
- Alpha 0.7.0：不可变 `SemanticVersion` 与 `GameVersion.TryGetSemanticVersion` 接入已完成；历史版本字符串继续可用，内容目录、Schema 与地图兼容域独立管理，见[版本管理与 API](../../../../version-management.md)。

最近一次项目完整回归是 2026-09-29 的 EditMode **882/882**、PlayMode **102/102**，均为 0 失败 / 跳过；核心定向为 52/52，版本域与存档定向为 32/32。见[0.7.0 验收](../../0.7.0/plans/alpha-0.7.0-semver-plan.md)与[原始结果](../../0.7.0/acceptance/alpha-0.7.0-semver-tests.json)。原记录中的验收身份是 `751225c` 加当时未提交实现，随后已纳入 `e2b97d2`；本次核对记录中的 6 个代码、测试及 ProjectSettings 文件 SHA-256，全部匹配当前文件。

本次没有重跑 Unity 测试，以上数字保留上一轮验收含义。当前版本 Player、真实设备、跨进程中断 / 重启与 300 / 600 活动单位性能矩阵仍未执行；历史 Player 数据不替代本版结果。依照[测试维护规范](../../../../testing/test-maintenance.md)，只有用户针对当前任务明确要求 Player 验收时才构建，这些未执行项不阻塞下一小版本规划。

## 进入实施前的准备

0.7.1 主功能已确定为六边形技能面板与柴薪、火焰、余烬道具，同时重构技能运行时、取消默认自动施放并提供六个技能键；新增能量瓶专栏、打怪积蓄、中心充能、属性分配与柴薪 / 天赋独立点亮，见[执行计划](alpha-0.7.1-skill-board-plan.md)、[几何合同](geometry-contract.md)、[六键施放合同](manual-skills-contract.md)及[成长合同](energy-progression-contract.md)。手动存档 / Profile、背包旋转 / 重量、其他装备扩展、交易回购 / 多商人、打造配方与设置预留项仍属于[现有边界](../../../../project.md#当前边界)，不自动纳入本版。

1. 明确本版主要功能或修复目标、必须交付项及暂缓范围，再阅读对应模块文档并核对现有实现。
2. 建立本小版本执行计划，按可独立验证的交付边界拆分阶段，列出正常 / 失败路径、成功标准及受影响的键鼠 / 手柄和本地化行为。
3. 分别评估内容目录、存档 / 设置 Schema 与地图格式 / Revision；需要迁移时复用真实历史夹具，测试使用隔离数据根。
4. 复用现有模块测试完成阶段验证；首次阶段交付使用 `0.7.1-alpha.1`，后续阶段依次递增。仅文档准备不更新 `bundleVersion`，不为无阶段号的 `alpha` 生成空交付。
5. 更新模块文档和当前进度；本启动记录在准备 / 未实施期间保持活动，首次实际阶段交付后随启动核对材料归档；小版本完成时再归档执行计划与对应验收。

启动核对的成功标准已满足：基线有代码 / 配置依据，历史验收与未执行项分开记录，下一实施入口可访问。2026-10-06 已补齐功能范围、五阶段成功标准及[验收清单](../acceptance/checklist.md)；进入各阶段前解决执行计划列出的对应待明确合同。
