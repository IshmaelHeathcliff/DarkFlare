# 消耗品综合验收与 alpha 0.4.2 交付

2026-09-15：完成。运行版本 `0.4.2-alpha`，core v5、Schema 3。统一使用核心、四种正式药水、商人内容和背包交互完成独立 Player 验收。模块说明见[使用系统](../../../item-use-system.md)与[消耗品](../../../consumables.md)。

## 全量回归

移除临时 Player 入口后，EditMode **563/563**、PlayMode **74/74** 通过，失败 / 跳过均为 0；见[当次结果](./tests.json)与[覆盖核对](./coverage.md)。没有新增长期测试或调整玩法规则；阶段 1、2 的事务、输入、存档保护继续保留。场景、资源租约释放和原装备 / 交易 / 打造合同由本轮全量回归验证。

## 独立 Player

验收包使用当前脚本、Bootstrap / Main 和正式 Addressables。临时编译定义仅提供受控场景入口和路径隔离，存档 / 设置分别写入 `Temp/ItemAcceptance/UserData/Saves`、`Settings`；日志也指定到验收目录，不访问真实玩家数据。

- [行为结果](./behavior-result.json)：四类药水各购买两份并合堆，实际总扣款 180；商人物品拒绝直接使用。键盘回血、手柄末件使用、鼠标眩晕净化均通过。回血 40、回蓝 60、满资源不扣除、复合效果部分有效时成功、七种异常清除而坚韧保留、打造上下文和 Shell 门禁正确。
- [自然到期](./expiry-result.json)：消耗末件坚韧药剂，恢复游戏时间并等待 31 秒，状态自然消失、生命上限恢复。验收期间将角色移离战斗，避免怪物干扰计时检查。
- [保存](./save-result.json)与[独立进程继续](./continue-result.json)：剩余法力药水 1 件、生命 36/120、法力 60/200，以及已经耗尽来源物品的坚韧 30 秒状态，均按[保存前快照](./expected.json)恢复；身份、位置、库存和状态周期不变，不重复回蓝或扣除。
- [Session 循环](./cycle-result.json)：返回前台再继续保持快照，旧请求拒绝；新游戏没有前一角色的药水和状态。
- [历史恢复](./history-result.json)：使用上一版 Player 实际产生的 core v4 / Schema 3 文件；恢复后重存为 core v5，背包和商店不补发药水。见[原文件](./history-core-v4.save.txt)、[重存文件](./history-resaved-v5.save.txt)与[对比](./history-comparison.json)。唯一 Payload 差异为刷怪剩余时间从 1.5 到 1.4999998807907104 秒的浮点舍入，物品、库存、角色资源与状态不变。

输入通过 Player 内 Input System 键盘、鼠标和手柄事件注入验证，不宣称实体手柄人工验收。Windows 网络权限提示出现时未进行操作，未把提示遮挡下的原生鼠标交互计为通过。相关日志见[首进程](./first-process.txt)与[重启进程](./restart-process.txt)。

## 显示

实际 Player 渲染覆盖中文 / 英文 / Pseudo × 1920×1080 / 1280×720。详情确实可见、菜单和浮窗边界正确，代表截图已目视核对；Pseudo 使用 Unity PseudoLocale 文本扩展，数字计数与输入 Glyph 保持原样。全部图像尺寸及非空检查见[截图清单](./screenshots.json)。

| 场景 | 1920×1080 | 1280×720 |
| --- | --- | --- |
| 中文详情 | [详情](./zh-Hans-1920-details.png) | [详情](./zh-Hans-1280-details.png) |
| 英文详情 | [详情](./en-1920-details.png) | [详情](./en-1280-details.png) |
| Pseudo 详情 | [详情](./pseudo-1920-details.png) | [详情](./pseudo-1280-details.png) |
| Pseudo 菜单 | [菜单](./pseudo-1920-menu.png) | [菜单](./pseudo-1280-menu.png) |
| 中文 HUD | [HUD](./zh-Hans-1920-hud.png) | [HUD](./zh-Hans-1280-hud.png) |

[状态详情](./status-detail.png)显示来源“消耗品”、剩余时间及效果；[地面物品](./world.png)检查四件丢弃物均生成，截图可见范围显示三件，其余不以截图单独证明。[重启恢复截图](./continued.png)确认状态仍显示。

## 最终构建

正式输出：`Builds/Alpha042/DarkFlare.exe`，普通 Windows x64 Mono 构建，包含正式 Addressables，不含临时验收入口和额外验收编译定义。[构建报告](./builds.json)和[校验值](./checksums.json)记录交付文件；[验收包构建](./acceptance-build.txt)另行留档，行为验收时版本仍为 0.4.1-alpha，收尾后再调整版本。

同代码、无临时入口的干净包使用独立产品名 `DarkFlare-ItemUse-Smoke` 隔离默认用户目录，实际启动显示 `0.4.2-alpha`，见[启动截图](./clean-startup.jpg)与[日志](./clean-startup.txt)。随后恢复正式产品名构建交付包；两个包的 DarkFlare.Runtime.dll SHA-256 相同。正式产品名输出未再次启动，避免访问真实玩家目录。

两个最终构建均无错误，有可选 Pipeline Runtime 配置缺省与既有 TMP Shader 调试指令弃用警告。环境音频设备不可用，本轮不作音频验收结论；图形驱动时间戳提示未影响所验收的渲染和游戏计时。

## 清理与验证边界

临时验收驱动、路径分支、采集代码和自动生成的 link.xml 已从 Assets 清理；恢复产品名、正式路径工厂和 Reload Scene Only 基线。字体、Prefab 和 PlayerSettings 的无关序列化噪声已清理，仅保留正式版本改动。没有修改怪物掉落资产。

验收期间修正的是临时脚本的 ContentId 命名空间比较、Unity 向量序列化和隐藏窗口截图问题；Shell 门禁检查后需退出该场景再独立开始显示验收。早期失败或黑图未作为最终成功证据保留。系统网络提示仍在干净启动截图中，未授予权限。计划已归档，本轮未执行 Git 提交。
