# I：地图制作与当前 Player 联验

2026-09-19，基于 `65429e0` 后的当前代码，Unity 6000.6.0f1、core v6、Schema 5。验收构建使用独立产品 `DarkFlare-IntegrationAudit-I`；新游戏主流程见[初始构建](./development-initial-build.json)，跨进程见[当前构建](./development-build.json)，长障碍近景见[补采构建](./save-audit-build.json)。构建的产品功能代码一致，仅临时采集器比较、预热与拍摄逻辑调整。

## 地图制作

[编辑结果](./editor/result.json)与[可导入 JSON](./editor/integration-map.json)来自从空白建立的隔离 64×64 地图。实际 SceneView 鼠标笔画经过保存、Ctrl+Z / Ctrl+Shift+Z；AI Preview 不修改副本，Apply 原子提交，旧修订 Conflict、后段非法操作完整回退。导出重导入保持规范 JSON 与 SHA256、稳定 ID、入口和 `dirt_00` 变体。

四种地表为泥土、草地、水和道路。8 个对象引用正式定义：6 棵树具有障碍与装饰能力，商人 / 打造台兼具障碍、装饰和交互；另有 Safe / Named 两个区域和 Arrival 入口。因此五类数据按能力组合验证，并非要求每个对象只能属于一个类别。水点阻挡、道路点可通行，正式对象预览同步 8 个对象。临时资产与工作副本已清理，原 Bootstrap 场景保持干净。

![SceneView 相机：编辑后样图](./editor/map-preview.png)

初次变体操作选在混合道路角，校验器正确拒绝；[诊断结果](./editor/diagnostic-variant-rejected.json)保留。改在纯泥土地块执行后通过，没有修改地表校验规则。

## Player 行为

[新游戏](./player/player-new.json)实际通过虚拟键盘 E 打开商店、虚拟手柄 North 打开打造台和传送入口，再调用正式交易 / 打造命令完成物品操作。UI 截图包含完整屏幕；相机截图仅包含世界。

![商店](./player/shop-ui.png)
![打造](./player/crafting-ui.png)

野外实际 Flow Field / A* 两只怪物绕过新增墙，130 个物理步观测无穿墙，结束位置分别约 `(13.01, 8.33)` / `(14.77, 10.14)`；同一边界额外 stamp 被 Capacity 拒绝。之后由玩家实际自动施法击杀并产生掉落。冻结怪物设置了独立 A* 固定点意图；回城实际经过约 30 秒，整个冻结地图 DTO 保持一致，玩家状态正常过期。回野外的生命、状态剩余时间和 tick 相位保持连续。

20 轮往返均收敛到 6 owner、46 lease、4 注册对象、37 个排序参与者、1 玩家 / 刷怪器 / 交互点。此小规模联验期间存在 Editor 采集器编译，耗时仅作运行记录，不作为独占机器性能基线；大规模独立测量见后续性能报告。

[野外继续](./player/player-wild.json)与[城镇继续](./player/player-town.json)分别在新进程中验证当前地图、怪物 / 掉落身份、已购买并打造物品、商店库存和导航意图。[历史迁移](./player/player-legacy.json)从真实 `save-v4-world-maps.json` 夹具恢复 2 只怪物，迁移为 Schema 5，原输入字节不变。

首次跨进程断言直接比较 JSON double 与 DTO float，错误报告导航变化；[误报](./player/diagnostic-float-comparison.json)保留。实际存档值为 float 的完整表示 `0.30000001192092896`，采集 JSON 为 `0.3`；将期望值还原为正式 DTO 后逐字段比较通过，未放宽游戏存档或导航规则。

## 遮挡与验证边界

[遮挡记录](./player/player-visual.json)在正式地图使用运行时排序更新，分别将角色放到对象锚点上下，校验 Y 与排序号方向。为制造可见重叠，拍照阶段临时关闭角色物理并靠近对象，结束恢复；没有手工设置 sortingOrder。这是视觉遮挡诊断，不代表可以走入实体碰撞体。

| 对象 | 角色在后 | 角色在前 |
| --- | --- | --- |
| 树 | ![树后](./player/sorting-tree_0-1.png) | ![树前](./player/sorting-tree_0--1.png) |
| 帐篷 | ![帐篷后](./player/sorting-tent-1.png) | ![帐篷前](./player/sorting-tent--1.png) |
| 四段长障碍 | ![石障后](./player/sorting-barrier_0-1.png) | ![石障前](./player/sorting-barrier_0--1.png) |

实际查看图像：树枝 / 树干遮挡后方角色，前方角色覆盖树干；后方角色被帐篷覆盖，前方角色显示在帐篷之前。正式野外长障碍的四段具有四个独立稳定 ID 和排序登记，后方角色被对应石段遮挡，前方角色覆盖石段；同 Y 分段顺序稳定。其他旋转与生命周期的完整行为由既有世界排序和地图表现回归保护。

本轮输入是 Input System 虚拟设备，交易 / 打造动作经正式命令执行；没有将此表述为实体手柄全按钮导航人工验收。未读取或覆盖正式玩家存档；所有自动建档与采集入口将在最终生产构建前删除。
