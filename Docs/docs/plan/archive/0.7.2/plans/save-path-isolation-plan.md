# 0.7.2-alpha.1 前置：普通 Play 存档路径修复计划

状态：2026-10-26，修复及步骤 A 验收完成，随阶段一归档。普通 Play 使用固定正式目录，自动化测试继续隔离；原正式 / 临时文件已恢复并核对哈希。详见[阶段验收](../acceptance/continuous-casting/README.md)。

## 已定位的问题

修复前，`Alpha027PlayModeDataEnvironment.Install` 带有 `AfterAssembliesLoaded` 运行时初始化标记，普通 Editor Play 也会执行：先 `ReleaseAndDelete`，再生成随机目录，并替换设置与存档路径工厂。测试程序集已经加载或 `UNITY_INCLUDE_TESTS` 成立，只能表示测试代码可用，不能证明当前正在执行自动化测试。

2026-10-25 排查观察到：

- Editor 的测试隔离静态状态指向 `Application.temporaryCachePath/DarkFlare/PlayModeTests/<随机 ID>`。
- 临时路径仍有当日 21:56 写出的存档，正式保存目录只发现 2026-09-19 的历史代际。以上为当次观察，不保证这些临时文件以后仍存在。
- FrontEnd 仅在 `PrepareContinueAsync` 成功后启用“继续”；路径每次变化会使前一轮保存不可见。
- 工厂的 `ResetForSubsystemRegistration` 会保留有效的测试安装，现有回归主要保护测试重启，尚未覆盖普通 Play 与测试运行的区别。

正式路径合同不变：`Application.persistentDataPath/DarkFlare/Saves/auto/`。正常进入 Play 后应使用这个固定位置；Settings 同样恢复自身固定目录。

## 修复目标

| 运行方式 | 路径与清理合同 |
| --- | --- |
| 普通 Play，含测试结束后手动 Play | 使用正式固定设置 / 存档路径；不新建或清理测试目录 |
| 自动化 PlayMode 测试 | 在 Bootstrap 任何存储服务创建前安装测试路径，整个测试运行保持独占；不读取 / 写入真实玩家目录 |
| 测试正常结束、取消或中止 | 解除本轮测试安装和标记；只清理本轮明确拥有且验证在允许父目录中的测试目录 |
| 关闭 Domain Reload 后再次 Play | 根据当前是否处于真实测试运行选择路径，不把上一轮安装永久带入手动试玩 |

## 实施方案

1. **保留现有文件再验证。** 不直接进入会清理临时目录的旧启动流程；先列明已有临时 / 正式文件，记录路径、时间与哈希。需要保存证据时只复制，不移动或覆盖原文件。不能把所有 `PlayModeTests` 子目录都认定为本次允许删除的产物。
2. **测试显式声明身份。** 使用当前 Test Framework 已有的程序集级 `PrebuildSetup` / `IPrebuildSetup` 在进入测试 Play 前设置本轮运行标记，按同轮身份管理测试 owner。当前包 `TaskList` 中该任务早于 `EnterPlayModeTask`，可覆盖 Bootstrap 初始化之前的窗口。
3. **限制运行时安装入口。** 保留必要的早期初始化时机，但只在有当前有效测试身份时安装隔离路径。普通 Play 直接退出该测试入口，工厂恢复正式 Provider；不以“Editor 正在 Play”或“测试程序集已加载”代替身份检查。
4. **保留测试内重装。** `Alpha027PlayModeDataEnvironmentFixture` 及需要重装的生命周期用例使用同一测试身份；重复安装仍有唯一 owner，旧句柄不能释放新安装。缺少有效身份时明确拒绝测试重装，不能回退到真实玩家路径继续跑测试。
5. **完整结束边界。** 使用测试清理钩子解除身份，并补齐取消 / 中止路径；若需 Editor 会话标记，记录本轮身份而非永久偏好，在下次进入普通 Play 前清除残留。Domain Reload、Scene Reload、停止后再次启动都检查 owner 是否仍属于当前测试运行。
6. **补证据与模块说明。** 先跑隔离定向回归，再验证两轮普通 Play 的保存继续，确认设置也未随机重置。只修复路径选择，不顺带改存档格式、退出系统或主菜单布局。

主要改动为 `Alpha027PlayModeDataEnvironment`、其程序集测试启动 / 清理入口、`Alpha027PlayModeDataEnvironmentFixture`，必要时收紧 `ApplicationDataPathProviderFactory` 的有效 owner 判定。优先使用现有测试程序集和包接口，不把 NUnit / Test Runner 依赖带入正式 Runtime。

## 已有存档处理边界

- 正式旧档原样保留，先做读取预检；测试隔离修复不以删除正式档来换取“继续”可用。
- 临时目录中的文件可能是测试生成数据，也可能来自玩家普通试玩，不能自动挑选最新文件覆盖正式档。
- 本阶段不自动迁移或认领临时档，不对其声称已经恢复。若后续需要找回进度，应先识别候选、复制备份、验证 Schema / 内容与代际，再确定恢复目标。
- 验收通过“明确保存完成 → 返回主菜单 → 停止 Play → 再进入 Play → 继续”证明持久化。直接点击 Unity Stop 时仍遵守当前应急关闭不保证等待异步保存的合同，不在本修复中承诺新的 Stop 自动保存能力。

## 验证与测试维护

| 验证 | 成功标准 |
| --- | --- |
| 无测试身份创建 Provider | 返回固定正式路径，不改变或清理任何测试目录 |
| 正式目录标记文件保护 | 自动化测试前后内容 / 哈希不变；测试不得访问这些数据作为夹具 |
| Bootstrap 提前启动 | 即使存储服务早于 SetUpFixture 创建，也已使用隔离根，无真实目录写入 |
| 同轮重装与旧句柄 | 旧测试目录按既有路径检查回收，新安装持续有效，无跨 owner 释放 |
| 测试结束 / 取消 / 中止后普通 Play | 重新使用正式路径，旧安装 / 标记不泄漏；不依赖手动刷新脚本域 |
| 两轮真实普通 Play | 明确保存的实例、物品、金币与冷却可继续；两轮根路径一致，不靠重新注入工厂模拟 |
| Reload Scene Only | 连续启动、测试后手动启动均成立，结束恢复项目基线 |

长期覆盖扩充 `ApplicationDataPathProviderFactoryTests`、`Alpha027ApplicationDataIsolationPlayModeTests`、`ApplicationLifecyclePlayModeTests`；原“替换隔离路径”测试应明确只验证测试运行，保留玩家数据隔离和句柄生命周期保护。普通启动负例应观察路径与真实行为，不用源码字符串断言替代。

普通试玩验证会写入正式存档，因此实施时先做无损备份和前后对照，验证完按约定恢复验证前状态，不替换玩家旧进度为测试角色。截图 / 路径报告 / 必要日志只保留本次验收证据，临时探针清理。

通过后更新[本地存档](../../../../infrastructure/local-save.md)、[Domain Reload 兼容](../../../../infrastructure/domain-reload-compatibility.md)和[测试维护规范](../../../../testing/test-maintenance.md)中的运行边界，并将真实结果随 0.7.2 阶段验收归档。
