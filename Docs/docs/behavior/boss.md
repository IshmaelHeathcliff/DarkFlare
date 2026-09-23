# 守卫 Boss

Alpha 0.6.2 在正式野外地图加入守卫者。怪物定义 `monster:guardian` 引用独立 `BossBehaviorDefinition`、Prefab 与中英文名称。Boss 不进入普通加权刷怪池；其稳定点位由地图 V3 的 `bossSpawns` 持有，当前每图最多一个。

当前代码与自动回归已完成，独立 Windows Player 验收尚受系统签名策略阻断；交付状态见[0.6.2 验收进度](acceptance/0.6.2/README.md)。

## 行为与结算

`BossBehaviorRuntime` 位于 Runtime，复用 Core 中独立的 HFSM 和行为树。HFSM 管普通、强化与死亡阶段；行为树按阶段转换、冲锋、挥击、接敌的优先级选择动作。未感知到玩家时等待；看到玩家后使用原怪物感知距离、视锥、遮挡和记忆规则。怪物行为、地图 Tick、导航与 Animator 的绑定由现有 `MonsterBehaviorMap` 和 `BehaviorSystem` 持有，Core 不包含 Boss 类型。

挥击锁定方向，经过预警、一次命中窗口与恢复；预警扇区和命中半径 / 角度共用配置。冲锋在预警时锁向，导航系统是唯一速度提交者，按圆足迹扫掠直线并在阻挡处停止；沿移动段只对玩家结算一次。每次命中都经过 `ResolveBossHitCommand` 重验目标、动作 ID、许可、遮挡和几何，再复用战斗伤害快照与 MonsterAttack 随机流。动作取消、目标失效或死亡不会补发迟到命中。生命比例达到阈值后执行一次可见转换，强化阶段缩短之后技能的冷却，已开始的冷却不重置。

`BossTelegraphRenderer` 读取行为状态绘制挥击扇区边线和冲锋矩形边线。预警采用固定几何，不依赖动画事件决定伤害；降低动态效果设置无需另开动画路径。Development Player 的行为面板和 Editor 图使用已有诊断句柄，可查看阶段、动作、剩余时间、冷却及节点状态。配置字段见[怪物配置参考](../config-reference/actors-and-monsters.md#bossbehaviordefinition)。

## 地图与保存

新图的 Boss 名额为 `Ready`，首次装配时变为 `Alive` 并分配稳定实例 ID。离场时冻结同一实例的生命、状态、位置、技能前摇 / 冲锋剩余距离、锁向、动作 ID、已命中标记和冷却；回图及跨进程继续都从剩余进度恢复。击杀后名额变为 `Defeated`，返回地图不自动复活。普通怪物补满规则暂不变；0.6.3 才接入普通怪物固定总量，0.6.4 才接入手动重置。

Schema 6→7 将旧版已访问图标记为 `LegacyAbsent`，不会为旧档补生成 Boss。野外地图历史 r1→当前 r4 仅在旧布局去掉新增 Boss 点后与登记基线一致时迁移；其它未知修订继续拒绝。活动图与冻结图名额须与 Boss 怪物 DTO 一一对应。地图和 Session 切换时旧行为句柄被撤销，候选失败回退原名额与战斗进度。相关地图合同见[双地图生命周期](../maps/world-map-lifecycle.md)，存档合同见[本地存档](../infrastructure/local-save.md)。
