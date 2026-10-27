# 版本管理

2026-09-26 起，Alpha 0.7 及后续版本使用 [SemVer 2.0.0](https://semver.org/lang/zh-CN/) 的格式与排序规则，并采用下述初始开发期迭代约定。0.1–0.6 的版本名称、归档和历史存档保留原意，不追溯重命名。

## 版本层级

| 层级 | 示例 | 推进规则 |
| --- | --- | --- |
| 主版本 | `0` | 当前处于初始开发期，公共合同尚未承诺稳定 |
| 次版本 / 里程碑 | `0.7` | 本轮持续在 0.7 系列推进；下一里程碑或明确进入 beta 时转入 0.8，修订号归零 |
| 小版本迭代 | `0.7.0-alpha`、`0.7.1-alpha` | 同一里程碑内逐次递增第三段；每个小版本定义独立范围与验收目标 |
| 小版本内阶段 | `0.7.0-alpha.1`、`0.7.0-alpha.2` | 阶段序号从 1 开始、按数值递增；进入下一个小版本时重新从 1 开始 |

第三段在 SemVer 中称为修订号；本项目当前 `0.x` 初始开发期用它管理同一里程碑内的小版本迭代，可包含功能新增、改进或修复。此约定不意味着已经承诺稳定 API，也不改变各数据域的显式兼容规则。

目前规划均为 **alpha**，不因完成一个阶段、积累一定数量的小版本或单次测试通过而自动进入 beta。达到下一里程碑但仍处于 alpha 时使用 `0.8.0-alpha`；明确进入 beta 时使用 `0.8.0-beta`，其阶段可继续使用 `0.8.0-beta.1`、`0.8.0-beta.2`。

## 名称与排序

- 标准版本字符串必须包含三段非负整数；主版本、次版本、修订号和数字阶段标识都不补前导零。
- 当前先行标识统一为小写 `alpha`，阶段号为正整数；`0.7.0-alpha.01` 不合法。
- 文档中的 `0.7` 是里程碑简称，`0.7.0-alpha` 可作为小版本系列名称；记录实际交付、运行版本与验收时使用完整版本，不能省略已存在的阶段号。
- SemVer 按数字比较阶段号，例如 `alpha.2 < alpha.10`；禁止按整个字符串的字典序排序。
- 同一版本核心下，`alpha < alpha.1 < alpha.2`。无阶段号的 `alpha` 是该小版本的起始基线，阶段收尾保留最后一个 `alpha.N`，不退回无阶段号的 `alpha`。

推进示例：

```text
0.7.0-alpha
  → 0.7.0-alpha.1
  → 0.7.0-alpha.2
  → 0.7.1-alpha
  → 0.7.1-alpha.1
  → 0.7.1-alpha.2
  → 0.7.2-alpha
```

以上只示范编号，不表示每个小版本固定两个阶段。阶段按可独立验证的交付边界划分；计划中的工作项可以共用一个阶段，不为每次提交递增版本。

可选构建元数据示例为 `0.7.0-alpha.2+sha.751225c`；`+` 后的信息不参与 SemVer 优先级比较。Git SHA、构建哈希和平台也可单独记录，不能用构建元数据替代需要递增的阶段号。

## 当前状态与版本来源

2026-10-27，当前运行`0.7.2-alpha.5`，五阶段实现及验证完成，见[地图验收](plan/archive/0.7.2/acceptance/compact-maps/README.md)。core15、Save12 / Settings1 / 地图V4；城镇r3、野外r11。EditMode993/993、PlayMode119/119，无失败 / 跳过；验证后推进运行版本。0.7.2计划已归档，0.7总计划继续活动，未构建Player。

2026-09-29 的 `0.7.0-alpha.2` 历史验收为核心定向 52/52、版本域及存档定向 32/32、完整 EditMode 882/882、PlayMode 102/102，均为 0 失败 / 跳过。见[0.7.0 执行与验收](plan/archive/0.7.0/plans/alpha-0.7.0-semver-plan.md)；该版 Player、真实设备、跨进程及性能矩阵未执行。

运行版本的唯一配置来源是 `ProjectSettings/ProjectSettings.asset` 的 `bundleVersion`：

- `Application.version` 读取该值，FrontEnd 展示同一字符串。
- `GameVersion.Current` 包装 `Application.version`，保存协调器将应用版本写入存档 Header 的 `gameVersion`。
- 项目概览、计划、验收和发布标签同步记录实际版本，不另设第二份运行版本常量。

`GameVersion` 仍保存非空原字符串并做 Ordinal 相等比较，通过 `TryGetSemanticVersion(out SemanticVersion version)` 显式解析。不可解析的历史字符串或默认值返回 false，不收紧原构造 / Header 合同，也不拒绝原本可读取的存档。

## SemVer 对象与 API

`DarkFlare.SemanticVersion` 位于 `Assets/Scripts/Runtime/Core/Versioning/SemanticVersion.cs`，属于既有 `DarkFlare.Core`。它是不可变纯 C# 对象，无 Unity、Runtime、存档或架构实例依赖；没有新增包或程序集。

| API | 合同 |
| --- | --- |
| `Major`、`Minor`、`Patch` | 只读规范十进制字符串，无固定整数宽度限制 |
| `Prerelease`、`BuildMetadata` | 只读完整标识，不含 `-` / `+` 前缀；缺省为空字符串 |
| `Parse(string)` | 严格 SemVer 解析；非法输入抛出 `FormatException` |
| `TryParse(string, out SemanticVersion)` | 非法 / 空输入返回 false，输出 null；不补零、不去空白、不修改大小写 |
| `ToString()` | 返回完整原版本标识，合法输入往返不丢信息 |
| `CompareTo`、`<`、`>`、`<=`、`>=` | 按 SemVer 优先级排序，忽略构建元数据；null 低于有效对象 |
| `Equals`、`==`、`!=`、`GetHashCode` | 按完整 Ordinal 标识判断身份，包含构建元数据 |

主 / 次 / 修订号严格为 ASCII 数字，除单独 `0` 外禁止前导零。先行与构建标识按句点分段，每段非空且仅含 ASCII 字母、数字和连字符；纯数字先行标识禁止前导零，构建标识允许 `001`。

比较先按核心数值，再按先行标识逐段进行：数字按长度和 Ordinal 比较以避免溢出，数字低于非数字，非数字使用 Ordinal；共同部分相同则更长的标识序列优先级更高。稳定版高于同核心的先行版。解析、排序和身份判断均不受当前文化影响。

```csharp
SemanticVersion earlier = SemanticVersion.Parse("0.7.0-alpha.2+build.001");
SemanticVersion later = SemanticVersion.Parse("0.7.0-alpha.10");
bool precedes = earlier < later;
bool samePrecedence = earlier.CompareTo(SemanticVersion.Parse("0.7.0-alpha.2+build.002")) == 0;
bool sameIdentity = earlier == SemanticVersion.Parse("0.7.0-alpha.2+build.002");
bool currentParsed = GameVersion.Current.TryGetSemanticVersion(out SemanticVersion current);
```

上述 `precedes`、`samePrecedence` 为 true，`sameIdentity` 为 false。用完整相等判断具体构建；`HashSet` 按该身份区分不同构建，默认排序集合按优先级比较会将它们视为同一排序键。

首版仅提供解析、表达、排序与身份判断，不包含版本范围、自动升版、CI 发布或管理 UI。核心回归位于 `SemanticVersionTests`；接入和历史 Header 覆盖复用 `MigrationPipelineTests`、`SaveSerializerTests`，历史迁移与恢复继续使用既有套件和夹具。

## 数据兼容域

| 版本域 | 当前基线 | 更新条件 |
| --- | --- | --- |
| GameVersion | `0.7.2-alpha.4` | 已验证的应用基线、小版本或阶段交付 |
| ContentVersion | core v14 | 正式内容目录的语义变化；评估内容迁移 |
| SaveSchemaVersion | 12 | 存档 DTO 结构与迁移合同变化 |
| SettingsSchemaVersion | 1 | 用户设置结构与迁移合同变化 |
| 地图 FormatVersion | V4 | 地图交换格式变化 |
| 地图 Revision | 城镇 r1、野外 r9 | 单张地图内容修订；评估旧档映射 |

上述版本分别管理。仅增加 `alpha.N`、进入下一个小版本、转入 0.8 或更改发布阶段，不自动递增内容目录、Schema 或地图格式。地图实例 `InstanceEpoch` 表示玩家手动重置的代次，内存 `Generation` 表示生命周期身份，二者不作为应用版本号。

存档读取按文件格式、Schema、内容目录及地图合同显式验证和迁移，不靠 `gameVersion` 字符串的大小推断兼容性。即使应用仍在同一个 0.7 系列，也不能跳过兼容评估；旧 `gameVersion` 不因本规则被改写。相关合同见[内容与迁移](./infrastructure/content-identity-migration.md)、[本地存档](./infrastructure/local-save.md)和[地图生命周期](./maps/world-map-lifecycle.md)。

## 计划、验收与交付

1. 里程碑总计划明确小版本顺序与范围，例如 `0.7.0-alpha`、`0.7.1-alpha`；各小版本计划为阶段列出完整目标版本、交付物、依赖和成功标准。
2. 小版本起始基线如需作为实际交付，先完成与变更相称的验证，再使用无阶段号的 `alpha`；否则它只作为系列名称，首次阶段交付使用 `alpha.1`。不为补齐编号生成空交付。
3. 每个阶段实现后先完成相关行为回归；确认交付时更新 `bundleVersion` 为对应 `alpha.N`，并核对版本展示 / 存档 Header 使用的配置来源。阶段最终证据记录实际版本与源码提交；未执行项目单独标记。
4. 同步模块文档、项目状态和阶段进度；整个小版本收尾时将执行计划和验收材料统一归档到 `Docs/docs/plan/archive/<小版本系列号>/`，分别放入 `plans/`、`acceptance/` 并更新版本目录与总归档索引。例如 `0.7.0-alpha.2` 随 `0.7.0/` 系列归档，正文保留完整实际版本。实际交付版本保持最后一个阶段号，然后推进下一小版本。
5. 已发布版本的内容和标签保持不变。同一小版本中的后续修复若再次交付，使用新的阶段号；未交付的开发提交由 Git SHA 区分，不冒充新的已验收阶段。
6. 需要发布标签时使用 `v<完整版本>`，例如 `v0.7.0-alpha.2`，指向已验收交付提交。发布记录至少包含版本、提交、范围、验证结果、兼容影响及未执行项；构建 Player 时另记构建身份和哈希。

阶段计划中的目标版本与源码实际版本必须区分。阶段失败、功能未完成或只有文档更新时，不把目标版本写成已交付；规则确定后也不改写旧验收数字。测试遵守[测试维护规范](./testing/test-maintenance.md)，不为每个版本建立永久验收测试套件。只有用户针对当前任务明确要求 Player 验收时才构建 Player。
