# Editor 控制台重复图形错误恢复

2026-10-27，运行0.7.2-alpha.5，Unity6000.6.7f1，Windows / Direct3D12。

## 现象与核对

控制台持续出现`ReleaseDynamicVBOFrame: Expected frame number to increase`。原生日志没有C#脚本路径或行号；只打开Bootstrap、非Play时也会出现。关闭Pipeline每16ms的强制后台刷新、请求一次正常PlayerLoop、进入 / 退出Play均未消除错误。正常主界面可以达到Ready / FrontEnd，未启动游戏Session。

这些观察支持原生渲染会话状态异常的判断，不能据此确定具体引擎内部根因，也不能归咎于地图、存档或某段游戏脚本。Unity历史版本曾记录同一ReleaseDynamicVBOFrame系列的DX12队列问题，参见[官方历史发布说明](https://unity.com/releases/editor/beta/6000.0.0b12)；该历史记录不是当前6000.6.7f1问题的已确认修复结论。

## 处理与验证

- 确认退出Play，场景无未保存修改，通过原生OpenProject重新打开同一项目，继续使用6000.6.7f1 / Direct3D12。
- 显式关闭Pipeline后台强制刷新，并保存本次会话设置；操作完成后继续保持关闭，避免让自动化持续驱动空闲编辑器。
- 重启后的新Editor.log内该错误为0；空闲观察、正常Bootstrap启动到Ready / FrontEnd，再退出Play后均未复现。最终Console Error0 / Warning0，只剩工具启动信息。
- 最终仅打开Bootstrap、非Play，无编译失败；项目版本和图形API配置保持。没有新增游戏脚本或永久测试，也没有通过忽略日志或修改日志处理器来屏蔽错误。

原会话日志由Unity自动轮换为`Logs/Editor-prev.log`，保留排查信息。该操作是当前会话恢复，不承诺解决引擎内部所有同类问题；若后续再次复现，需独立记录触发步骤，再评估图形API或Unity补丁。

Unity在重启 / 正常主界面验证时重新序列化了已有字体资产。执行前字体SHA-256为`EBBA3E8B8D41456CB5DDD2E684811A5E0125B3D2856FEC713093A2172FDE43FB`，执行后为`1695BFB04EE7F776B5C3F981771203118DD7E5DE5E6D1EBD4D6E2503FB09F59E`；保留现有字体工作区，不使用HEAD版本覆盖它。阶段五验收中的字体Hash保留其采集时的历史含义。
