# 行为管理

Alpha 0.6.0 已提供独立 HFSM、行为树、Runtime 组合与生命周期、Animator 语义桥接、Editor 实时图及 Development Player 观察面板。两个核心模块均位于现有 `DarkFlare.Core`，各自独立，不新建程序集。

- [HFSM 使用与执行语义](hfsm.md)
- [行为树使用与执行语义](behavior-tree.md)
- [接入、样例、Animator 与调试](integration-debugging.md)
- [普通怪物行为、感知与冻结恢复](monsters.md)
- [守卫 Boss 行为、技能与名额](boss.md)
- [有限刷怪与地图名额](finite-spawning.md)
- [刷怪地图手动重置](map-reset.md)
- [0.6.5 综合交付验收](../plan/archive/0.6.5/acceptance/behavior/README.md)
- [0.6.4 手动重置验收](../plan/archive/0.6.4/acceptance/behavior/README.md)
- [0.6.3 有限刷怪验收](../plan/archive/0.6.3/acceptance/behavior/README.md)
- [0.6.2 验收与 Windows Player 豁免](../plan/archive/0.6.2/acceptance/behavior/README.md)
- [0.6.1 验收与证据](../plan/archive/0.6.1/acceptance/behavior/README.md)
- [0.6.0 验收与证据](../plan/archive/0.6.0/acceptance/behavior/README.md)
- [F1 HFSM 早期验收](../plan/archive/0.6.0/acceptance/behavior/f1-hfsm.md)

核心通过显式上下文和 Tick 驱动，不依赖 Unity 对象、QFramework 全局架构、角色类型或场景。玩法能力、调度、取消与表现由 Runtime 负责。正式普通怪物已接入四种基础行为；守卫 Boss 复用核心并使用独立 Runtime。有限刷怪与手动重置分别由 0.6.3、0.6.4 接入。0.6.5 已完成 Editor 综合回归；Player 与当前版本性能实测未执行，详见[验收](../plan/archive/0.6.5/acceptance/behavior/README.md)。
