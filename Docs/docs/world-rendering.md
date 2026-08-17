# 世界渲染与稳定层级

## 当前状态

alpha 0.1.7 已完成统一世界渲染层级及综合验收。正式世界对象不再依赖零散绝对 Order；对象内部由单个 `SortingGroup` 保持整体边界，对象之间由 `WorldSortingSystem` 按显式接地点稳定排序。

## Sorting Layer 合同

从后到前固定为：

| Layer | 用途 | 内部 Order |
| --- | --- | --- |
| `Ground` | 双层 Tilemap | Base `0`，Detail `10` |
| `WorldObject` | 场景物件、交互物、掉落物、怪物、玩家 | 根 `SortingGroup` 由系统分配连续唯一 Order |
| `WorldEffect` | 投射物飞行、命中与受击特效 | Flight `0`，Impact / Hit `10` |
| `WorldInfo` | 伤害、治疗与战斗语义文字 | Combat Text `0` |

`Default` 只保留给 Unity 默认与明确非生产对象。永久验证入口会检查正式目标是否遗留 `Default` Renderer，以及四层的存在性和相对顺序。

Main 的 Global Light 2D 必须同时包含 `Default`、`Ground`、`WorldObject`、`WorldEffect`、`WorldInfo` 五个目标层。新增正式 Sorting Layer 时必须同步光照目标和自动验证，否则 Sprite-Lit 材质会在拆层后变黑。

## 世界对象边界

玩家、三种怪物、商人、工作台、掉落物，以及 Main 中帐篷、补给箱、火盆、旗帜、两棵枯树和两组岩石均满足：

- 根对象只有一个 `SortingGroup` 和一个 `WorldSortParticipant`；禁止嵌套第二个 Group。
- `SortAnchor` 显式表示接地点；高大物件不使用 Sprite 中心或透明画布边缘排序。
- 内部偏移为：稀有度环 `-20`、描边 `-10`、主体 `0`、火焰 `10`、血条背景 `20`、Fill `21`、名称 / 词条 / 交互提示 `30`。
- 排序只写 Sorting Layer 与 `SortingGroup.sortingOrder`，不修改 Transform Z、XY、Scale、Collider、Rigidbody2D、Physics Layer 或交互范围。

## 稳定排序规则

`WorldSortingSystem` 以 `1/64` 世界单位量化接地点 Y。只有参与者注册、注销、身份变化或量化 Y 变化时才重新排序；稳态不重复写 Renderer。

后到前的键固定为：量化 Y 高处先、类别低者先、`StableSortId` 序号比较。类别顺序为 `StaticProp → Interactable → Loot → Monster → Player`。

稳定 ID 来源：

- 玩家：运行时 Actor ID。
- 怪物：Definition ID 与实例 Seed。
- 掉落物：`ItemInstance.InstanceId`。
- 商人、工作台和场景物件：Prefab / Main 中显式语义 ID。

玩家、怪物和掉落 Prefab 使用运行时身份门禁，在 Controller 完成配置前不会用 Prefab 占位 ID 注册，避免同类动态实例产生短暂冲突。重复或空 ID 会记录警告并拒绝注册，不回退到 Instance ID 或生成顺序。

## 生命周期与入口

- Main 只有一个 `WorldSortingRunner`，在 `LastPostLateUpdate` 集中推进排序，并在禁用时统一取消 UniTask。
- `GameArchitecture` 注册唯一 `WorldSortingSystem`，退出时清空参与者和稳定 ID。
- `PlayerController`、`MonsterController` 和 `LootPickupController` 在运行时配置完成后设置稳定身份。
- `GroundTilemapSetup` 的未来创建路径固定生成 `Ground` Layer 的 Base `0` / Detail `10`。
- 怪物词条文字固定为 `WorldObject / 30`；伤害文字固定为 `WorldInfo / 0`。

## 验证与扩展规则

永久验证入口为 `DarkFlare/Alpha 0.1.7/阶段 D/验证世界排序合同`，同时接入 `ContentConfigurationValidator`。当前精准覆盖 8 个 Prefab 与 Main 的 10 个对象，验证结果为 18 targets / 0 issues。

综合验收时 Main 中 27 个活动参与者具有 27 个唯一 StableSortId 与 27 个唯一 Order，非法 Group 为 0。排序键不变时连续 1024 次 Tick 的 managed allocation 为 0 B；Global Light 2D 的五层目标也由 EditMode 合同永久验证。

新增世界 Renderer 时必须先声明所属 Layer。新增可与其他对象重叠的逻辑世界对象时，还必须声明唯一 Group、类别、接地点、稳定 ID 来源和内部偏移；不得临时选择未登记的绝对 Order。
