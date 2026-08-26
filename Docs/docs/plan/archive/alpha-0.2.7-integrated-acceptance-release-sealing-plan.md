# alpha 0.2.7 综合验收与封板执行计划

> 状态：已完成并归档
> 建立日期：2026-08-25
> 执行基线：`be85dd4`（`alpha 0.2.6` 已提交）
> Unity：`6000.4.3f1`
> 上位计划：[alpha 0.2 基础设施开发计划](./alpha-0.2-plan.md)
> 长期契约：[alpha 0.2 长期运行时契约](../../infrastructure/alpha-0.2-runtime-contract.md)
> 前置阶段：[alpha 0.2.6 日志、错误处理与 Addressables 资源治理](./alpha-0.2.6-logging-error-addressables-plan.md)
> 最终记录：[alpha 0.2 综合验收记录](../../infrastructure/alpha-0.2-acceptance.md)

## 阶段定位

`alpha 0.2.7` 是 `alpha 0.2` 的最后一个阶段，不新增玩法内容，而是证明 `alpha 0.2.0–0.2.6` 建立的基础设施能够在同一条真实玩家路径、故障路径和发布构建中协同工作，并把规划期约束转化为长期维护入口。

本阶段默认优先复用既有测试和模块，不为“看起来完整”复制同义抽象。现状审计发现的契约缺口允许在本阶段精准修复；除此之外只做验收、证据固化、文档封板和计划归档。

## 执行基线

- `ApplicationHost` 已拥有 Application / Profile / Session / Scene 生命周期、Settings、Localization、Scene Flow、Time、Input、Audio、Accessibility、Platform、Logger、Save Coordinator 与 Resource Service。
- `Bootstrap.unity` 为 build index `0`，`Main.unity` 为 build index `1`；应用从 FrontEnd 新建或继续游戏，以 additive Main 运行 Session。
- `alpha 0.2.6` 验收后 EditMode `424/424`、项目 PlayMode `53/53`；完整 PlayMode 57 项中 55 项通过、0 失败，2 项为 Input System 上游 issue 1252825 的既有 Ignore。
- 真实 Bootstrap 已达到 Application `Ready`；FrontEnd 资源基线为 owner `2`、entry `1`、lease `1`、in-flight `0`，正常路径 Console Error / Warning 为 `0`。
- 15 个 DarkFlare Addressables 条目位于四个生命周期分组，`Default Local Group` 为空；Runtime 直接 `Debug.Log*` 和 Addressables 静态 API 已收口到唯一 Adapter。
- 基础设施例外清单当前有 8 项：2 项 QFramework 外部框架例外、2 项 Localization Service 所有权例外、4 项 PlayMode 底层场景测试 Adapter 例外；均已有精确路径、原因和移除条件。
- 执行前 `PlayerSettings.bundleVersion` 为 Unity 默认值 `1.0`；切片 0 已更新为 `0.2.7-alpha` 并加入合同测试。

## 完成结果

- 覆盖清单已切换为 `complete`，`Application.version = 0.2.7-alpha`；删除自动档、恢复全部默认设置、端到端路径、故障矩阵、UI 矩阵与构建均有可解析证据。
- PlayMode 每次测试运行都重新安装单所有者隔离根；Subsystem 静态重置保留仍被夹具持有的测试覆盖，TearDown 后恢复正式 provider 并清空测试运行目录。
- 隔离根内完成语言、重绑定、NewGame、两次保存、Continue、删除、恢复默认和 Host 重启；设置消费者、Continue 状态、资源与对象基线均收敛。
- Save V0 / Settings V0 代表夹具、故障矩阵、三语言 × 三分辨率 Shell 检查、Addressables clean build 与 Windows Development Player build 已完成。
- 最终全量 EditMode `443/443`、项目 PlayMode `56/56`；完整 PlayMode 60 项中 58 项通过、0 失败，2 项为 Input System issue 1252825 的既有 Ignore。
- 最终项目 PlayMode 全量运行前后，真实 Settings / Saves 四个文件的路径、长度与 SHA-256 完全一致，测试运行目录子项为 `0`。

## 封板前必须解决的缺口

### 测试数据隔离

当前部分 PlayMode 场景流测试会启动正式 `ApplicationHost`，Host 默认从 `Application.persistentDataPath` 创建 Settings 和 Save 根目录。测试必须证明自己不会读取、覆盖或遗留开发者真实用户数据。

本阶段建立仅测试可安装的启动依赖覆盖：

- 正式 Player 继续使用 `PersistentSettingsPathProvider` 与 `PersistentSavePathProvider`，默认行为不变。
- PlayMode 验收在 Host Boot 前为 Settings / Save 安装同一次测试运行拥有的隔离根目录；根目录位于临时目录，不得是 `persistentDataPath` 的子目录。
- 覆盖必须有所有者、可重复安装保护和静态重置入口；测试结束后逆序关闭 Host、释放覆盖并清理临时目录。
- 所有启动真实 Host 的项目 PlayMode 夹具必须断言实际根目录属于本次测试运行；禁止继续用硬编码 `C:/...` 伪装临时存储。
- 测试覆盖只在 `UNITY_INCLUDE_TESTS` 下可见，不进入正式 Player API。

### 用户数据删除与设置重置

[长期运行时契约](../../infrastructure/alpha-0.2-runtime-contract.md) 把“删除和重置本地设置 / 存档”列为完整交付，本阶段已补齐最小闭环：

- FrontEnd 提供“删除自动存档”动作，必须二次确认；只允许在 FrontEnd、无活动 Session、Save Coordinator 空闲时执行。
- 删除范围仅限 `auto` 槽位根目录下的正式代际、临时文件和有限损坏诊断副本；路径继续经过 `SaveSlotId` 与 Storage 边界验证。
- 删除不存在的槽位按幂等成功处理；IO 失败返回结构化错误，重新 Probe 后由真实磁盘状态决定 Continue 是否可用，不能显示虚假成功。
- Settings Page 提供“恢复全部默认设置”动作，必须二次确认；使用 `UserSettingsSnapshot.Default` 经 Settings Service 正常提交，不绕过校验和原子存储。
- 默认设置提交成功后，Locale、Input binding / Glyph、Audio 和 Reduce Motion 消费者必须在当前 Application 内同步收敛；失败时保留旧快照和 UI 表示。
- 删除存档不修改 Settings；恢复设置不修改存档。两项操作不组合成不可证明的跨文件伪原子事务。
- 新增玩家文本进入 `ui` String Table，并覆盖 `zh-Hans`、`en` 与 Pseudo Locale。

## 目标

- 用隔离数据根完成冷启动 → 设置修改 → NewGame → 保存 → FrontEnd → Continue → 再保存 → 安全退出的端到端回归。
- 完成旧版本迁移、存档损坏、内容缺失、存储失败、场景取消、资源失败和未观察异步异常的证据闭环。
- 验证删除自动档、恢复默认设置、重启后持久状态和所有运行时消费者一致。
- 在多 Session、多 Locale、多分辨率和键鼠 / 手柄路径中验证对象、任务、事件、输入、音频、时间与资源不累积。
- 完成 Addressables 内容构建与 Windows Development Player 构建冒烟，证明测试专用代码和 Editor 依赖未进入 Player。
- 设置准确的 alpha 版本标识，建立统一验收记录，修正文档陈旧项并归档全部 `alpha 0.2` 计划。

## 非目标

- 不新增地图、怪物、装备、技能、掉落、打造或经济内容。
- 不实现手动存档槽位、Profile 选择、存档重命名、云同步或跨设备迁移。
- 不引入远端 Catalog、CDN、热更新、遥测、日志文件或玩家日志导出。
- 不实现 Addressables 实例池、Addressables Scene 或通用依赖注入容器。
- 不移除仍有正式兼容职责的旧标签 / 命令入口；只审计并记录去向。
- 不修改 `companyName`。该值会改变现有平台持久化根目录，必须在未来分发准备阶段配套数据迁移后单独处理。
- 不读取、引用或修改 `Docs/design/`。

## 冻结决策

### 版本与构建身份

- `PlayerSettings.bundleVersion` 在本阶段更新为 `0.2.7-alpha`，并由 EditMode 资产合同测试冻结。
- `GameVersion.Current` 继续读取 `Application.version`；Save Schema、Settings Schema 和 Content Version 均不因封板自动递增。
- Windows Development Player 只作为本地验收产物，输出到仓库外临时目录，不提交构建文件。
- Addressables 验收先执行配置验证，再执行 clean content build；构建失败不得以 Fast Mode Play 成功替代。

### 验收证据

- 新增机器可读验收覆盖清单，为每个 `alpha 0.2` 完成条件登记 requirement ID、自动测试证据、人工 / 构建证据和已知限制。
- 已有测试能够完整证明的合同只登记证据，不复制测试；只有跨模块断点或当前缺口新增测试。
- 覆盖清单由 EditMode 测试校验：ID 唯一、证据非空、测试名可解析、已知 Ignore 精确为 Input System issue 1252825 两项。
- 故障测试允许产生预期 Error，但必须使用 `LogAssert` 或隔离 Sink 明确消费；正常 Bootstrap、玩家主路径和构建冒烟必须为零 Error / Warning。

### 迁移样本

- 将当前内嵌在测试字符串中的 Save V0、Settings V0 代表样本固化到测试夹具目录，禁止放入正式运行时存档目录。
- 夹具必须是 UTF-8、无机器绝对路径、无真实用户数据，并带来源版本与预期迁移摘要。
- 迁移测试必须证明逐级迁移、确定性结果、输入不被原地修改、未来 Schema 拒绝和 ContentId 解析失败不提交 Session。

### 文档封板

- 新增 `alpha 0.2` 综合验收记录，作为当前测试计数、构建结果、故障矩阵、截图矩阵、例外审计和已知限制的唯一最新入口。
- 阶段完成时把规划期基础设施契约迁移为 `Docs/docs/infrastructure/` 下的长期运行时契约，并更新全部引用。
- 将 `alpha-0.2-plan.md` 与本执行计划移入 `Docs/docs/plan/archive/`；历史阶段文档中的当时测试计数保持历史事实，不机械改写。
- 修正仍把重绑定、音频、可访问性或平台生命周期写成“后续阶段”的当前文档；项目概览、目录结构和索引必须与实现一致。

## 执行切片

### 切片 0：封板清单与基线冻结

- 建立验收覆盖清单，逐条映射上位计划完成定义和基础设施契约。
- 冻结基线提交、Unity / 包版本、build scenes、Addressables 分组、正式内容数量、测试计数和资源快照。
- 审计 8 项策略例外、所有 `[Ignore]` / `[Explicit]`、兼容入口和当前文档陈旧项。
- 更新 `bundleVersion` 并增加版本、场景、Addressables 和覆盖清单合同测试。

成功标准：每项完成条件都有可执行证据或明确缺口；不存在“验收时再决定”的无主项目。

### 切片 1：PlayMode 隔离存储环境

- 为 Host 的 Settings / Save Path Provider 建立最小可注入创建边界及测试专用安装器。
- 让所有启动真实 Host 的项目 PlayMode 夹具复用同一隔离环境与清理流程。
- 增加保护测试：实际根目录不在 `persistentDataPath` 下、跨测试无残留、异常 TearDown 后仍可再次启动。
- 保持正式 Player 默认依赖和启动顺序不变。

成功标准：项目 PlayMode 全量运行前后，开发者真实 Settings / Save 目录的文件清单和内容哈希完全不变。

### 切片 2：用户数据闭环补齐

- 为 Save Storage / Coordinator 增加 `auto` 槽位删除的结构化合同、门禁、取消和错误映射。
- 为 Settings Service 增加恢复完整默认快照的正式操作，并验证所有消费者收敛。
- 在 FrontEnd / Settings Page 接入确认 Modal、Busy、Toast、焦点恢复和键鼠 / 手柄导航。
- 补齐三语言文本、源码策略、存储故障和 PlayMode UI 测试。

成功标准：删除后 Continue 立即不可用且重启后仍不可用；恢复默认后所有设置域与消费者一致且重启后保持；两项失败都不破坏另一类用户数据。

### 切片 3：端到端玩家路径与迁移样本

- 使用隔离根执行：冷启动 → 英文 → 非冲突重绑定 → NewGame → 修改可持久状态 → 保存 → FrontEnd → Continue → 校验状态 → 再保存 → 返回 → 删除存档 → 恢复默认设置 → 重启 Host。
- 验证重启后语言、绑定、音量 / 可访问性默认值、Continue 状态、唯一 Host / EventSystem 和资源基线。
- 固化 Save V0 / Settings V0 夹具并接入迁移、校验和拒绝未来版本测试。
- 连续三轮 NewGame / Continue / Return 后比较对象、任务、暂停 lease、输入 owner、音频实例和资源诊断基线。

成功标准：整条路径可重复执行，不读取真实用户数据、不重复新游戏发放、不丢失已提交状态、不遗留 Session 资源。

### 切片 4：综合故障演练与规范审计

- 复用或补齐以下故障证据：截断 / 校验失败 / 双代损坏、旧版本迁移失败、ContentId 缺失、磁盘写入 / 删除失败、场景取消 / 替代、Addressables 失败 / 取消 / owner 提前关闭、未观察 UniTask 异常、重叠挂起 / 退出和 Abandoned 迟到 continuation。
- 验证上一份有效存档不被故障覆盖，失败 Session 完整回滚，玩家错误动作不越权，首个致命根因不被覆盖。
- 复核基础设施例外清单：删除已失效项；保留项必须有当前原因和未来移除条件；禁止新增无到期条件例外。
- 执行全部源码策略、Localization、内容目录、Addressables 治理和配置覆盖验证。

成功标准：故障矩阵全部有自动证据；零未登记策略违规；正常路径 Console 为零 Error / Warning。

### 切片 5：界面矩阵、构建与真实运行

- 自动执行 `zh-Hans`、`en`、Pseudo Locale × `1280×720`、`1920×1080`、`2560×1440` 的 FrontEnd、Settings、HUD、暂停菜单和错误 Modal 布局检查。
- 在 `1920×1080` 分别完成键鼠与手柄的 FrontEnd → Settings → NewGame → Pause → Save → Return 导航；不得依赖硬编码按键文本。
- 执行 Addressables clean content build、Windows Development Player build，并从 Bootstrap 冷启动完成 NewGame、保存、Continue 与安全退出。
- 验证 Player 中不存在测试菜单、测试存储覆盖或 Editor-only 依赖；收集启动日志、资源快照和必要截图。

成功标准：构建成功、真实 Player 主路径成功、布局无截断 / 重叠 / 焦点丢失，退出无未处理异常。

### 切片 6：全量回归、文档封板与归档

- 运行 Unity 编译、全量 EditMode、项目 PlayMode 和完整 PlayMode；不新增 Ignore。
- 清理测试生成的字体图集、Enter Play Mode 设置、临时存档、构建输出和场景序列化噪音。
- 完成综合验收记录和长期基础设施契约，更新模块文档、项目概览、目录结构、文档索引与配置参考覆盖。
- 记录已知限制和下一版本候选；不把手动槽位、云同步、远端资源等非目标伪装为遗漏实现。
- 归档总计划和阶段计划，更新归档索引；此时才把 `alpha 0.2` 标记为完成。

成功标准：仓库只剩预期改动，文档链接有效，`git diff --check` 通过，计划状态与实现一致。

## 验收矩阵

| 层级 | 必测内容 |
| --- | --- |
| EditMode | 版本合同、覆盖清单、路径隔离、存档删除、设置默认恢复、迁移夹具、策略例外、Localization 与 Addressables 治理 |
| 项目 PlayMode | 隔离 Host、完整玩家路径、删除 / 重置 UI、三轮 Session、唯一输入 / 音频 / 资源所有权、故障回滚 |
| 完整 PlayMode | 项目测试零失败；仅允许 Input System issue 1252825 的既有两项 Ignore |
| UI 矩阵 | 3 Locale × 3 分辨率；键鼠与手柄各完成一次完整导航 |
| 构建 | Addressables clean build、Windows Development Player build、无 Editor / Test 依赖泄漏 |
| 真实运行 | Bootstrap → NewGame → Save → FrontEnd → Continue → Safe Quit，Console 正常路径零 Error / Warning |
| 数据安全 | 测试前后真实用户目录哈希不变；失败不覆盖上一有效存档；测试临时目录最终清空 |

## 风险与控制

| 风险 | 控制 |
| --- | --- |
| 测试覆盖安装晚于自动 Host Boot | 覆盖安装与 Host 重建形成统一夹具协议；每次测试先断言实际 Root，再允许写入 |
| 删除槽位时部分文件失败导致 UI 状态虚假 | 删除后重新 Probe，以磁盘事实刷新 Continue；失败返回结构化结果并允许重试 |
| 恢复默认设置触发多个消费者形成部分应用 | 先原子提交 Settings，再由同一快照广播；消费者失败进入日志 / 玩家错误，不伪造旧 UI 状态 |
| 综合测试过长、失败难定位 | 单元 / 领域故障继续分层；端到端只验证跨模块边界，并记录阶段与稳定事件 ID |
| 故障注入产生预期 Error 污染全量验收 | 每项故障显式消费预期日志；正常路径单独清空并验证 Console |
| Player 构建暴露测试钩子或 Editor 引用 | 测试入口受 `UNITY_INCLUDE_TESTS` 约束，Player 构建作为封板强制门禁 |
| 修改公司标识导致用户数据根迁移 | 本阶段不修改 `companyName`，在未来分发准备阶段单独规划路径迁移 |
| 文档归档后链接失效 | 归档切片执行全仓 Markdown 相对链接扫描，并更新所有指向规划期契约的引用 |

## 预计文件边界

- 修改 `ProjectSettings/ProjectSettings.asset`：只更新 `bundleVersion`。
- 修改 `Assets/Scripts/Runtime/Infrastructure/Lifecycle/`：最小测试依赖创建边界与 Host 接入。
- 修改 `Assets/Scripts/Runtime/Infrastructure/Persistence/`、`Settings/`：删除自动档与恢复默认设置合同。
- 修改 `Assets/Scripts/Runtime/Infrastructure/UI/`、`Assets/UI/ApplicationShell.*` 和 `Assets/Localization/Tables/ui*`：玩家入口、确认 / 反馈和三语言文本。
- 新增 / 修改 `Assets/Scripts/Tests/EditMode/`、`PlayMode/` 与测试夹具目录：覆盖清单、隔离环境、迁移样本、综合路径和故障演练。
- 新增 `Docs/docs/infrastructure/alpha-0.2-acceptance.md`；阶段完成时迁移长期基础设施契约并归档总计划 / 阶段计划。
- 修改 `Docs/docs/README.md`、`project.md`、`project-structure.md` 和相关基础设施模块文档；不修改 `Docs/design/`。

## 完成定义

- 两项封板缺口完成：所有项目 PlayMode 使用隔离用户数据根；玩家可删除自动档并恢复完整默认设置。
- `Application.version` 为 `0.2.7-alpha`，Schema / Content 版本保持独立且迁移样本通过。
- A 层基础设施全部形成正式闭环；B 层全部有真实消费者和自动测试。
- 端到端玩家路径、故障矩阵、3×3 UI 矩阵、键鼠 / 手柄导航、Addressables build 和 Windows Player build 全部通过。
- EditMode / PlayMode 零失败；除 Input System issue 1252825 的既有两项外无跳过项。
- 正常 Bootstrap / Player 路径 Console Error / Warning 为 `0`；资源、任务、事件和对象回到基线。
- 规范扫描零未登记违规；8 项现有例外已逐项删除或保留精确原因与未来移除条件。
- 测试未改变开发者真实 Settings / Save；临时目录、测试设置、字体图集和构建产物无残留。
- 综合验收记录与长期契约完成，总计划和阶段计划归档，项目索引把 `alpha 0.2` 标记为已完成。
