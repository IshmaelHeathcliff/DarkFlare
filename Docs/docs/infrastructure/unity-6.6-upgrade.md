# Unity 6.6 升级兼容

2026-09-11，编辑器由 `6000.4.3f1` 升级为 `6000.6.0f1`；游戏版本仍为 `0.3.4-alpha`。本次处理升级后的 Console，不修改历史 alpha 0.3.4 验收环境记录。

## 修改范围

| 原因 | 处理 |
| --- | --- |
| `GetInstanceID()` 在 6.6 成为编译错误 | 生命周期任务名称与输入资产身份测试改用 `GetEntityId()`；测试使用 `EntityId`，不截断为整数 |
| 带排序参数的 `FindObjectsByType` 弃用 | 运行时音频监听器与已有测试改用无排序参数重载，保留 Include / Exclude 规则 |
| `AppDomain.GetAssemblies()` 的 UAC0005 警告 | 配置中心使用 `TypeCache.GetTypesDerivedFrom<ScriptableObject>()`，保留配置过滤和稳定排序，删除不再需要的反射异常辅助方法 |
| Odin `Expanded` 弃用 | 使用 `ShowFoldout = true`，保留原 API 实际控制折叠标题的语义 |
| USS 切片比例缺少单位 | Components / Hud / Theme 的 `0.125` 改为 `0.125px`，保留比例与四边切片数值 |
| 伪本地化 HUD 两行文字需要 41px，原区域仅 38px | 详情区域最小高度调整为 44px，中区上下留白减为 5px；保留外层 136px 高度与原有裁切断言 |
| TMP LiberationSans 字体导入器版本过旧 | 通过 Unity `ForceReserializeAssets` 仅重写导入元数据，升级到版本 4，保留 GUID |
| `TextSettings.defaultFontAsset` 测试弃用 | EditMode 继续校验字体资产、回退和字符覆盖；已有三语言 PlayMode 矩阵改为验证真实按钮的 resolved font，避免依赖失效的默认值 |
| Addressables 旧配置转换器找不到程序集 | 确认包已清理转换失败的 `Library/AddressablesConfig.dat`，通过新版 `ProjectConfigData` 接口保存，文件头为 `ACD` 版本 1；不修改正式 Addressables 内容或存档 |

Input System 1.20 自动重新生成 `InputSystem_Actions.cs`，生成文件随输入包版本保留，不手改生成器输出。其余包升级与项目设置沿用用户完成的升级结果。

Unity 的 [USS 属性语法](https://docs.unity3d.com/cn/6000.0/Manual/UIE-USS-SupportedProperties.html)要求切片比例使用长度形式；[官方兼容问题记录](https://issuetracker.unity.com/issues/23693/objectgetinstanceid-is-obsolete-errors-thrown-in-the-console-after-importing-happy-harvest-2d-sample-project)说明 6.6 中旧实例 ID API 的编译错误。

## 验证与维护

2026-09-12 已进一步执行[Domain Reload 兼容检查](./domain-reload-compatibility.md)，修复测试隔离重复安装与启动取消误判，并补充两项生命周期回归。以下 434/63 为首次 Console 修复时的历史计数，最新结果见该检查报告。

本轮复用项目 EditMode / PlayMode 套件，不增加升级专用长期测试。临时验证输出放在 `Temp/unity66-*`，最终结果见下方收尾记录。历史 Windows Player 构建基于 6.4，不据此声称 6.6 Player 已验收。

2026-09-11 收尾结果：

- 项目编译成功；字体与三份 USS 定向重新导入后，Console Warning / Error 均为 0。
- EditMode 434/434，0 失败、0 跳过，22.45 秒。
- 首轮 PlayMode 发现两项 HUD 伪本地化裁切失败；修复实际布局后全量 63/63，0 失败、0 跳过，75.73 秒。三语言实际字体绑定验证通过。
- 独立正常启动 Bootstrap，宿主达到 `Ready`、场景流达到 `FrontEnd`；重新加载新版 Addressables 配置成功。启动及停止 Play Mode 后 Console Warning / Error 均为 0。
- 原始 NUnit 结果保留为 `Temp/unity66-edit-results.xml`、`Temp/unity66-play-results.xml`。故障注入测试产生的预期异常与正常启动日志分别检查，不通过放宽断言或全局忽略日志解决问题。
- 字体动态数据与测试 Editor 设置恢复到本轮执行前备份；Editor 留在 Bootstrap 非 Play 状态，无新增临时测试脚本。

本轮未重建 Windows Player；旧验收中的 Pipeline 构建警告和 2D Animation 进程退出提示不能由 Editor Console 清零推断已修复。
