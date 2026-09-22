# 世界渲染与稳定层级

## Sorting Layer 与接地点

Alpha 0.5.2 的世界对象与实体特效共用 Y 排序；地图配置、分段资源和装配入口见[地图对象表现](./maps/object-presentation.md)。Main 已由 0.5.3 接入正式城镇 / 野外，地图作用域统一管理对象表现与排序登记。

从后到前固定为：

| Layer | 用途 | 内部 Order |
| --- | --- | --- |
| `Ground` | 地表和明确贴地的表现 | 地图作用域创建的地表 Tilemap 为 `0`；贴地表现按配置排列 |
| `WorldObject` | 物件、交互物、掉落、怪物、玩家、投射物及实体特效 | 各独立 `SortingGroup` 由系统分配唯一 Order |
| `WorldEffect` | 保留的兼容层 | 普通飞行、命中、受击表现已迁出 |
| `WorldInfo` | 伤害、治疗及战斗语义文字 | Combat Text `0`，可读性例外 |

Main 的 Global Light 2D 覆盖 Default、Ground、WorldObject、WorldEffect、WorldInfo；世界 Sprite 使用现有 Sprite-Lit 材质。新增层时同步光照目标。渲染排序不修改 XY、Z、缩放、碰撞、物理层或交互范围。

每个独立单元有一个 `SortingGroup`、一个 `WorldSortParticipant` 和显式 `SortAnchor`。接地点表示脚底，不取图片透明边缘或视觉中心。单元内稀有度环 `-20`、描边 `-10`、主体 `0`、火焰 / 反馈 `10`、血条背景 `20`、Fill `21`、名称 / 词条 / 交互提示 `30`。长物件可含多个兄弟单元，逻辑根不带 Group，禁止嵌套 Group。

## 稳定身份与排序规则

后到前的键为：1/64 世界单位量化 Y 降序、类别升序、稳定 ID 的 Ordinal 比较。类别顺序固定为 `StaticProp → Interactable → Loot → Monster → Player → Effect`；类别只打破同 Y，不跨越 Y 遮挡。

| 对象 | 身份来源 |
| --- | --- |
| 玩家 | 运行时 Actor ID |
| 怪物 | `MonsterInstanceData.Id.Value` |
| 掉落 | `WorldDropId.Value`；旧物品入口通过 `FromLegacyItem` 适配 |
| Main 固定对象 | 显式语义 ID |
| 地图对象段 | `ComposeIdentity(MapId, ObjectId, PartId)`，各段带长度以避免分隔歧义 |
| 短期投射物 / 世界特效 | 来源身份和 VisualEffectPool 播放序号；不作为持久身份 |

空身份、重复身份、非有限锚点和超出量化范围的坐标拒绝注册；不回退到 Unity Instance ID。Order 使用 `0…32767`，最多 32768 个独立单元，分段和世界特效均计入。运行时锚点变为非法时注销该单元并禁用其 Group。

`Tick` 每帧读取接地点，量化键或参与者变化时才排序。比较器在进入排序前已排除失效对象，避免比较中的原生判空；类别按整数比较避免枚举装箱；Group 的最后应用 Layer / Order 缓存在参与者上，只有改变时写入 Unity。Group 的全局 Order 由系统独占，外部代码不能自行覆盖。静态对象仍被采样，合法移动不会被忽略。

## 注册所有权与生命周期

`Register` 保留旧 bool 入口；参与者的 `Registration` 返回可释放凭据。凭据绑定本次登记，重复释放和旧凭据不会移除重用身份后的新登记。`TryRegisterBatch` 预检整批身份、锚点和容量，失败不改变当前集合。

自动注册角色保留 SessionRunning / 运行身份门禁。地图装配和投射物使用 `ConfigureExplicit`：准备候选时不注册，所有者提交后才激活。禁用、销毁、重新配置、池回收和作用域退出均释放登记。

- Main 的 `WorldSortingRunner` 在带取消的 UniTask 中于 LastPostLateUpdate 统一 Tick。
- Session 的 GameArchitecture 持有唯一系统，退出 ReleaseAll。
- Editor 预览持有独立系统，不注册到游戏架构。
- 地图候选遵循 Prepare → Activate → DisposeAsync；实例先注销 / 销毁，再释放 Addressables 资源。

## 投射物与反馈池

`ProjectileController.Init` 给每次发射分配独立身份，飞行物使用 WorldObject Group；命中保留原伤害判定，落点效果使用独立世界上下文。

`WorldEffectContext` 提供两种播放方式：世界模式指定排序系统、播放身份、地面位置和取消令牌；附着模式指定宿主单元和令牌。世界模式的池实例有独立排序根，允许视觉中心高于地面锚点。附着受击效果是宿主 Group 内的 Renderer，没有新 Group，随宿主移动和遮挡。

池回收清除登记、父级、上下文与回调；宿主禁用后延迟至安全的帧阶段归还，播放版本防止旧延迟回收新实例。场景作用域关闭后拒绝新播放。原预热、容量、颜色、动画及伤害语义保持不变。

## 诊断与验证

Profiler 标记为 `DarkFlare.WorldSorting.Tick`。系统暴露 ParticipantCount、RejectedCount、SortCount、LastRefreshedKeys、LastOrderWrites，用于区分容量拒绝、键刷新和实际写入。

ContentConfigurationValidator 同时检查既有 Main / Prefab 排序合同和地图表现目录。长期测试覆盖同 Y / 跨量化边界、原子批量登记、旧凭据、容量、池复用 / 取消 / 宿主关闭、纯表现结构、装配失败释放和编辑器 Undo / Redo。

阶段 F 的真实 Player 画面、基线对照、完整回归与测量边界见[验收记录](./maps/acceptance/f/README.md)。500 / 1000 的本阶段预算只针对排序；完整怪物导航和战斗帧预算由 0.5.4 验收。
