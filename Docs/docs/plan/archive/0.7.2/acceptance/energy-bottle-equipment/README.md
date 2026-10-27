# 0.7.2-alpha.2 能量瓶装备入口迁移验收

状态：2026-10-26，**阶段二完成，当前运行 `0.7.2-alpha.2`**。EditMode 984/984、PlayMode 114/114，均无失败 / 跳过；普通试玩与三语言 9 状态矩阵通过。实施基线为 `main` / `479f901` 加本轮改动；Unity `6000.6.7f1`，core v14、Save / Settings Schema 12 / 1、地图 V4 保持。见[已归档实施计划](../../plans/phase-2-execution.md)及[源码身份](source-identity.json)。

## 实际交付

- 背包装备区左上空位增加独立 2×1 能量瓶栏。只打开背包即可通过鼠标拖放、右键、动作菜单、键盘 / 手柄拿起放置进行装备、换瓶与卸下；不扩展十个战斗装备槽。
- 换瓶继续将旧瓶精确回填新瓶原格。快捷卸下自动找位，拖回背包只使用指定格；被占、越界、空间不足、来源或 Revision 变化整笔失败。
- 复用共享 `ItemActionRouter / ItemInteractionSession`，识别已装备瓶来源；成功后焦点留在背包，商店默认出售保留，Shell / 设备 / Session 中断取消未提交操作。
- 天赋页移除装备与卸下按钮、装备投放目标，保留只读摘要、选择装备瓶 / 背包瓶、拖到中心并确认整瓶充能。
- 复用正式瓶图标和 Addressables。空栏显示标题，装备后显示实际瓶图标；能量 / 容量与满瓶提示分行，中文、英文和伪本地化可读。装备区保持原高度与格子尺度。
- 保存继续沿用 `EnergyBottleInstanceId` 及实例能量字段；未更改数据格式、击杀收益、地图或其余技能规则。

## 验证材料

| 检查 | 结果与材料 |
| --- | --- |
| 项目 EditMode 全量 | 984/984，0 失败 / 跳过；[实际结果](editmode-full.json) |
| 项目 PlayMode 全量 | 114/114，0 失败 / 跳过；[实际结果](playmode-full.json) |
| 三语言画面 | 1920×1080 × 空栏 / 321 能量 / 满瓶，9/9 无瓶栏文本裁切；[数据](visual-matrix.json) |
| 键鼠 / 手柄与中心充能 | 已补入现有 UX 套件；最终全量同时覆盖原交易、真实击杀、成长、取消与设备移除 |
| 保存继续绑定 | 第一轮保存满瓶，第二轮继续后同实例、1000 能量且图标正确；第二轮保存部分瓶，第三轮继续后同实例、321 能量；见下述普通 Play 记录 |
| 正式路径 | 普通 Play 使用固定正式根，测试标记为空；未安装临时路径工厂替代普通启动 |
| 原文件保护 | 两个原存档 SHA-256 均恢复一致；本轮新增存档与设置已清除，见[恢复结果](original-files-restored.json) |
| Editor 与版本收尾 | 版本已写入项目设置、core / Schema 有效；Reload Scene Only、已停止、仅 Bootstrap、无测试标记，见[检查](editor-final.json) |

自动化和普通试玩先在 `0.7.2-alpha.1` 加本阶段实现的工作区完成，随后只推进 `bundleVersion` 至 `0.7.2-alpha.2`；其余数据版本保持。阶段二生产逻辑已参与最终完整 PlayMode，原文件恢复后仍是验收前字节内容。项目设置忽略 Unity 原生行尾空白后仅版本号改变。Localization 两处 Unity 原生空字段尾空格保留，源码 / UXML / USS / Markdown 的空白检查通过。汇总见[实际统计](test-summary.json)。

普通 Play 使用验收前已有角色继续，不把验收瓶或文件保留给玩家。图标预热、临时瓶准备和读取片段见[采集脚本](ordinary-visual-check.cs.txt)；原文件恢复以[原生 PowerShell 片段](restore-player-files.ps1.txt)和实际哈希结果为准。动态脚本中未生效的 JSON 清单不作为恢复依据。

- [备份来源与哈希](ordinary-backup.json)
- [第一轮继续](ordinary-first-continue.json)、[第一轮保存与返回](ordinary-first-save.json)
- [第二轮继续](ordinary-second-continue.json)、[第二轮保存与返回](ordinary-second-save.json)
- [第三轮继续](ordinary-third-continue.json)、[最终画面采集继续](ordinary-final-continue.json)、[最终保存与返回](ordinary-final-save.json)

## 最终画面

| 状态 | 中文 | 英文 | 伪本地化 |
| --- | --- | --- | --- |
| 空栏 | [截图](empty-zh-Hans.png) | [截图](empty-en.png) | [截图](empty-qps-ploc.png) |
| 部分能量 | [截图](partial-zh-Hans.png) | [截图](partial-en.png) | [截图](partial-qps-ploc.png) |
| 满瓶 | [截图](full-zh-Hans.png) | [截图](full-en.png) | [截图](full-qps-ploc.png) |

截图来自实际 Game View。伪本地化沿用项目已有变换工具，从英文真实文本生成扩展文字；不是第三份正式翻译表。图片复制到归档后逐个核对哈希，再通过 Unity API 删除临时导入目录。

## 排查与修正

- 最初增加一行瓶栏导致背包格越出视口，全量 111/114。改为利用装备区左上空位，保留原有完整背包断言；[修复前](playmode-before-layout-fix.json)、[布局定向修复后](layout-after-fix.json)。
- 新增保存后的 UI 绑定检查最初插入在精确时间断言前，推进了游戏时间。将 UI 检查移至原保存、资源、随机与切图断言之后，未放宽原有时间精度。
- 伪本地化扩展标题曾被默认 Label 边距和高度限制裁切；满瓶数字与状态合并后也过长。空栏标题改用整个槽面并清除默认边距，数值与满瓶提示分行，装备后隐藏空栏标题。中间结果见[修正前全量](playmode-before-pseudo-fix.json)；最终 9 状态矩阵另行实际采集。
- 早期输入夹具假设背包左上格为空、进入天赋前只打开属性窗、拿起后最近背包格可直接放置。改为使用实际空位、恢复原窗口前置、按可见有效投放反馈移动；保留失败回退与窗口恢复断言。
- 临时验收瓶最初未走图标预热，出现占位图；修正验收准备，正式图标已在最终画面确认。生产 UI 刷新继续从 Session 加载器读取图标，不以实例相同为由跳过绑定。
- 截图初次归档误删了临时图片，已重新采集最终 9 张并验证复制。动态脚本的 JSON 备份清单为空，但两个原始二进制备份与独立调用结果中的路径 / 哈希完整；恢复前再次核验备份，再用原生 PowerShell 恢复，全部原文件哈希一致。

长期测试仅补充现有事务、物品 / 棋盘和保存套件：指定格卸瓶与失败回退、图标 / 空栏标题状态、真实输入、旧来源失效和恢复后新实例绑定。未新建按本版本命名的永久套件，未删除旧存档、成长或资源 / Session 保护。临时执行脚本和导入截图目录在收尾清理，证据留在本目录。

最终源码 / 配置 21 个文件哈希一致，文档链接检查无缺失；原有字体文件保留，Game View 已恢复为进入验收前的 640×480，Editor 留在 Bootstrap 非 Play 状态。临时采集代码和备份副本已清理，见[收尾检查](cleanup-and-checks.json)。

## 未执行项

本阶段按项目默认仅做 Editor 验证，未构建 Windows Player，未执行真实手柄、跨进程或 Player 性能验收。阶段三天赋精简 / 方位、阶段四 HUD、阶段五地图与 300 名额仍由[活动总计划](../../plans/alpha-0.7.2-optimization-plan.md)推进。
