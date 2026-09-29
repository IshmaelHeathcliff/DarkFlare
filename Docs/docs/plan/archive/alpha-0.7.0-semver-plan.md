# Alpha 0.7.0 SemVer 执行计划与验收

状态：2026-09-29，**两阶段完成并归档**，运行版本为 `0.7.0-alpha.2`。不可变 Core 对象与 `GameVersion` 解析接入均已完成；日常行为见[版本管理](../../version-management.md)，后续小版本由[0.7 总计划](../alpha-0.7-plan.md)接管。

## 基线与顺序

- 实施基线：`751225c`、`0.6.5-alpha`、core v10、存档 Schema 9、设置 Schema 1、地图格式 V4。
- 首个小版本系列为 `0.7.0-alpha`，先完成版本对象，再接入应用版本；阶段目标分别为 `0.7.0-alpha.1`、`0.7.0-alpha.2`。
- 后续小版本按 `0.7.1-alpha`、`0.7.2-alpha` 推进，功能范围另行补充；当前均预计为 alpha。
- 下一里程碑仍处于 alpha 时进入 `0.8.0-alpha`，明确进入 beta 时进入 `0.8.0-beta`。

## 首个事项：简易 SemVer 版本对象

目的：用一个明确的版本值替代调用方自行拆字符串或按字典序比较，正确处理阶段编号和先行版本优先级。标准依据为 [SemVer 2.0.0](https://semver.org/lang/zh-CN/)；通用对象支持合法的稳定版、先行版和构建元数据，项目当前只发布 alpha 的策略由版本管理文档约束。

实施前 `GameVersion` 位于 `Infrastructure/Persistence/VersionContracts.cs`，只检查非空、保存原字符串并做 Ordinal 相等比较。FrontEnd 和保存协调器读取 `Application.version`；现有存档校验只要求 `gameVersion` 非空，数据兼容由独立版本域判断。实施前核对项目源码与 Packages 声明，未发现可直接复用的 SemVer 类型。

### 对象与 API

- 新增不可变纯 C# `SemanticVersion`，放入既有 `DarkFlare.Core` 的 `Core/Versioning/`；不依赖 Unity、Runtime、存档或 QFramework 架构实例，不新增程序集。
- 提供只读 `Major`、`Minor`、`Patch`、`Prerelease`、`BuildMetadata` 信息，以及 `Parse`、`TryParse`、`ToString`。
- 实现 `IComparable<SemanticVersion>`、`IEquatable<SemanticVersion>`、`GetHashCode` 和常用比较运算符。`TryParse` 对非法输入返回 false，`Parse` 明确报告失败；构造成功后不允许修改内部标识集合。
- 首版只负责解析、表达、比较与身份判断；版本范围表达式、自动升版、Git / CI 自动发布和版本管理 UI 不属于本事项。

### 解析与比较合同

1. 严格读取 `major.minor.patch[-prerelease][+build]`，拒绝缺段、前缀 `v`、首尾空白、空标识、非法字符和数字段前导零；不自动截断、补零或修改大小写。
2. 先行标识仅允许 ASCII 字母、数字和连字符，按句点分段；纯数字先行标识不能有前导零。构建标识使用相同字符集，但纯数字构建标识允许前导零。
3. 主 / 次 / 修订号先按数值比较；同核心版本下，稳定版高于先行版。先行标识逐段比较：数字按数值、非数字按 Ordinal、数字低于非数字；共同部分相同时，标识段更多者优先级更高。
4. 数字比较不能溢出或回退到字典序；可使用规范十进制字符串按长度、再按 Ordinal 比较，避免为长数字标识引入额外依赖。
5. `CompareTo` 与大小运算符忽略构建元数据；`Equals`、`==` 和哈希按完整版本标识判断身份，包含构建元数据。必须明确区分“优先级相同”与“同一版本标识”，防止用排序结果识别具体构建。
6. `ToString` 保留完整先行及构建标识，合法输入往返不丢信息；比较结果不受当前文化设置影响。

### 应用版本接入

- `GameVersion` 通过 `TryGetSemanticVersion(out SemanticVersion version)` 提供显式解析入口，复用 Core 对象；`Current`、`Value`、原有完整字符串相等语义与展示来源保持兼容。
- 不收紧历史 `GameVersion` 构造和存档 Header 的非空字符串合同。遇到不可解析的历史字符串，解析入口返回 false，不据此拒绝原本可读取的存档。
- `bundleVersion` 仍是运行版本唯一来源，Header 的 `gameVersion` 仍是字符串；不把 `SemanticVersion` 对象直接写入存档 DTO。
- 不用应用版本排序代替内容目录、存档 / 设置 Schema 或地图版本的兼容判断。本事项没有数据格式变更，保持 core v10、Schema 9 / 1、地图 V4。

## 阶段与成功标准

| 阶段目标 | 交付 | 成功标准 | 状态 |
| --- | --- | --- | --- |
| `0.7.0-alpha.1` | Core 版本对象、解析 / 比较 API 与核心回归 | 合法输入往返、非法输入拒绝、数值与先行优先级、元数据身份、文化独立性和长数字边界通过；无 Runtime / Unity 依赖 | 完成，52/52 定向测试 |
| `0.7.0-alpha.2` | `GameVersion` 解析桥接、历史兼容与模块文档收尾 | 当前 / 阶段版本可通过正式入口解析比较；旧原始字符串保持可用；现有存档和独立版本域行为无回归 | 完成，32/32 定向测试及全量回归 |

阶段号是实际交付边界，不为每次代码提交增加序号；只有对应实现与验证完成后才更新运行版本。小版本收尾保留最后一个阶段号，不能从 `alpha.2` 退回 `alpha`。

## 验证与收尾

- 新增按模块命名的 `SemanticVersionTests`，放入现有 EditMode 程序集；这是独立、长期复用的核心规则，不建立 `Alpha070...` 发布验收套件。
- 覆盖 `alpha < alpha.1 < alpha.2 < alpha.10 < beta < 稳定版`、跨修订 / 次版本、数字和非数字标识、共同前缀、超出固定整数宽度的数字，以及比较反对称 / 传递性。
- 覆盖非法前导零、空标识、非法 ASCII / Unicode、空输入；同时验证构建标识 `+001` 合法，元数据不同的版本排序相同但完整身份不同，哈希与相等语义一致。
- `GameVersion` 接入覆盖放在已有 `MigrationPipelineTests` 的版本域用例；保存 / 恢复验证复用现有存档套件与真实历史夹具，不修改旧夹具的版本字符串。
- `.1` 运行核心定向 EditMode；`.2` 运行版本域和存档相关回归，小版本收尾运行项目完整 EditMode / PlayMode 并记录当次结果。仅在用户明确要求 Player 验收时构建 Player。
- 实施后更新[版本管理](../../version-management.md)中的实际 API / 行为，模块说明与计划分开维护；小版本完成后按[测试维护规范](../../testing/test-maintenance.md)审查覆盖、归档本版完成的执行记录。0.7 总计划在里程碑完成前持续维护。

## 实际结果

| 验证 | 当次结果 | 范围 |
| --- | --- | --- |
| `0.7.0-alpha.1` 核心定向 | 52/52 通过 | `SemanticVersionTests` |
| 应用与存档定向 | 32/32 通过 | `MigrationPipelineTests` 10、`SaveSerializerTests` 13、`SaveRestorePreparerTests` 9 |
| `0.7.0-alpha.2` 完整 EditMode | 882/882 通过，221.61 秒 | `DarkFlare.Tests.EditMode` |
| `0.7.0-alpha.2` 完整 PlayMode | 102/102 通过，287.73 秒 | `DarkFlare.Tests.PlayMode` |

各轮均为 0 失败、0 跳过、0 不确定结果。本轮按 assembly 筛选项目自有程序集，没有执行第三方包套件；0.6.5 的包内 2 项跳过仍属于历史记录，不计入这轮结果。[原始测试结果与源码哈希](alpha-0.7.0-semver-tests.json)保存本次结果；不新增读取此文件的运行测试。

实现为 `Core/Versioning/SemanticVersion.cs`，通用对象无 Unity / Runtime 依赖，数字段以规范十进制字符串表达。应用入口为 `GameVersion.TryGetSemanticVersion`。存档 Header 保持原字符串，包括不可解析的历史标识；现有真实迁移夹具未改写。内容、Schema、地图格式和 Revision 均未变更。

版本依次推进为 `0.7.0-alpha.1`、`0.7.0-alpha.2`。现场核对 `Application.version`、`GameVersion.Current.Value` 均为最终版本且解析成功；FrontEnd 仍读取 `Application.version`，`ApplicationHost` 仍将它传入保存协调器，Header 仍写字符串。测试后 Reload Scene Only 恢复为选项启用、数值 1，Schema 当前值为 9 / 1。

验收时源码身份为基线提交 `751225cee015ad20cfec316573b0a5d818ebda70` 加本次未提交改动；JSON 记录受影响代码 / 测试和最终 ProjectSettings 的 SHA-256，不把基线提交当作已包含本版实现的提交。工作区预先存在的字体资产改动保留。验收时尚未创建发布提交或标签。

收尾审查保留 52 项新核心行为用例，以及既有版本域套件中的 2 项接入、序列化套件中的 3 项 Header 参数用例，用于长期防止比较错误、历史字符串误拒绝和数据兼容域混用。没有新增发布编号断言、永久临时验证脚本或新历史夹具；没有删除独有回归覆盖。

本版未构建或验收 Player，真实设备、跨进程和当前版本性能矩阵未执行。两阶段计划的 Editor 成功标准已满足，下一小版本范围待明确。
