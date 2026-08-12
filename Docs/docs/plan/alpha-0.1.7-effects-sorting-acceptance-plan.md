# alpha 0.1.7 多帧特效、稳定世界层级与综合验收执行计划

> 状态：执行中；阶段 B 已完成，下一步进入阶段 C 运行时接入
> 建立日期：2026-08-12
> 最近更新：2026-08-13
> 所属版本：`alpha 0.1`
> 前置阶段：`alpha 0.1.6` 已完成
> 计划完成后：封板并归档 `alpha 0.1`

## 阶段目标

以独立多帧投射物、命中和受击表现替换当前静态投射物复用、动态缩放淡出和角色闪白；以统一、可复现的世界排序入口消除玩家、怪物、商人、工作台、掉落物和场景物件重叠时的层级闪烁；补齐全部配置字段文档，并完成 `alpha 0.1` 全版本综合验收。

完成后必须满足：

1. 投射物飞行、投射物命中和角色受击是三个独立特效家族，连续帧无裁切、漂移、比例跳变或首帧闪现。
2. 伤害结算仍只有一个权威入口；特效只消费已有结果，不能重复伤害、治疗、暴击或资源事件。
3. 所有正式世界对象拥有稳定排序身份、接地点锚点和唯一整体排序边界，同点重叠和交叉移动不闪烁、不拆层穿插。
4. 12 个顶层配置类型及 6 个可达嵌套结构的全部序列化字段都有可追踪参考，自动覆盖检查为零缺失。
5. `alpha 0.1.0–0.1.7` 的战斗、资源、词条、打造、UI、输入、视觉、配置与场景回归全部通过，提交基线保持 `EnterPlayModeOptions = 0`。

## 当前基线与必须解决的问题

### 特效基线

- `Projectile_Default.prefab` 当前使用单张 `projectile_arcane.png`：96×96、64 PPU、Center Pivot、Point、Uncompressed、Full Rect，主体约 56×22 px；Prefab 根 Scale 为 `0.25`，同时影响物理根与表现，不能在本阶段直接归一。
- `ProjectileController.TryHit` 在一次命中判定后销毁投射物；只有 `DamageResult.IsHit` 时才调用 `ProjectileImpactVisual`。
- `ProjectileImpactVisual` 动态创建 GameObject，复用投射物当前 Sprite，以 `0.35 → 1.1` 缩放和 0.25 秒淡出模拟命中爆发；没有独立帧、Prefab、对象池或 Sorting Layer。
- `ActorVisualFeedbackController` 在 `ActorDamagedEvent` 上闪白并生成伤害数字；未命中、闪避和无伤害只生成语义文字，尚无独立角色受击特效。
- 伤害数字已经区分敌我、伤害、暴击、治疗、未命中、闪避和无伤害。本阶段保留其语义，不重新设计伤害公式或治疗系统。

### 世界排序基线

- 项目当前只有 `Default` 一个 Sorting Layer；Main 的两层 Tilemap、场景物件、商人和工作台，以及玩家、怪物、掉落物、投射物 Prefab 都依赖零散绝对 Order。
- Main 当前可见世界 Renderer 没有 `SortingGroup`；玩家和三种怪物根 Sprite 为 10，商人 / 工作台的主体、描边、火焰、名牌和提示分散在 8–12，掉落物分散在 5–8，投射物为 20，世界信息为 70–80。
- 商人、工作台和怪物等多 Renderer 对象没有整体边界；两个对象重叠时，子 Renderer 可以交叉穿插。
- Main 场景现有站立物件根 Y 约位于 `-6.5..5.2`，地表 Tilemap 根位于 `-20`。当前 Order 不能表达统一的“接地点越靠屏幕下方越在前”规则。

### 配置文档基线

- 配置中心按 `CreateAssetMenu.menuName` 的 `DarkFlare/Data/` 前缀动态发现配置类型，当前共有 12 个顶层类型、123 个直属序列化字段。
- 从这些字段递归可达 6 个项目嵌套序列化结构、27 个字段；当前自动覆盖基线合计为 18 个结构、150 个字段。
- `AffixDefinition` 仍只有标签字段说明，`MonsterSpawnDefinition` 与 `TraderDefinition` 只有索引骨架；现有“完整”文档也存在将多个字段概括成一行、没有逐字段默认值 / 必填性 / 消费者的问题。
- 当前没有机器可验证的“配置类型—文档章节—序列化字段”映射，新增字段后可以在没有同步文档的情况下通过测试。

## 冻结的多帧特效合同

### 通用生产合同

所有 `alpha 0.1.7` 正式栅格特效统一遵守：

- 每帧一张独立 PNG；禁止生产 SpriteSheet、`Sprite Mode: Multiple`、人工 Sprite Editor 切图和运行时逐帧缩放补偿。
- 画布 96×96、RGBA / sRGB、64 PPU、Center Pivot、Point、Uncompressed、Full Rect、MipMap Off、Wrap Clamp、Alpha Is Transparency。
- 同一状态只允许一次家族级等比尺度，不按单帧 fit-to-canvas；视觉中心、主体边界、边距、覆盖率和相邻帧差异由静态 manifest 逐帧冻结。
- 文件序号固定两位补零；导入、Animation Clip、Prefab 引用和旧依赖清理全部自动验证。
- 先生成并审计 Pilot，再扩展家族。Pilot 一旦通过即成为不可变身份 / 光向 / 材质 / 轮廓参考；后续帧只允许参考 Pilot 和上一张已通过帧。
- 每张图生成后立即执行 `audit_sprite_assets.py`，失败即停止该家族；不通过放宽合同、修改 Pivot、修改 Transform Scale 或人工切图绕过失败。
- 当前内置图像生成器实际输出 1254×1254 源画布；四个特效家族在首张 Pilot 处理前共同冻结唯一一次整画布导出变换：以 Lanczos 等比缩放至 96×96，比例 `16/209`（约 `0.0765550239`），不得裁切、重排或按主体边界逐图缩放。生产目录只保留导出的 96×96 透明 PNG，源图用于 Pilot 复核与后续逐帧参考。

### 家族 A：奥术投射物飞行

| 项目 | 冻结值 |
| --- | --- |
| 路径 | `Assets/Art/Sprites/Effects/Projectile/Arcane/Flight/` |
| 命名 | `effect_projectile_arcane_flight_e_00..05.png` |
| 帧数 / 播放 | 6 帧、12 FPS、0.5 秒循环 |
| 默认方向 | 朝东；Prefab 继续使用 `transform.right` 旋转到实际方向 |
| 主体合同 | 宽 52–60 px、高 18–26 px；方向性 Alpha 中心 `x=52±2, y=48±2`；任一边留白不低于 12 px |
| 运行时尺度 | 保留 Projectile 物理根 Scale `0.25` 作为已登记历史例外；所有帧自身与视觉子节点 Scale 为 1 |

- 当前已验收静态投射物是身份、配色、轮廓和运行时大小参考；新 `00` 帧不得改变技能辨识度。
- 六帧只表现能量流动和尾迹相位，不改变碰撞半径、速度、生命周期或攻击快照。
- 新飞行 Clip 完成绑定并通过零依赖扫描后，旧 `projectile_arcane.png` 才能退出生产目录。

### 家族 B：奥术投射物命中

| 项目 | 冻结值 |
| --- | --- |
| 路径 | `Assets/Art/Sprites/Effects/Projectile/Arcane/Impact/` |
| 命名 | `effect_projectile_arcane_impact_00..05.png` |
| 帧数 / 播放 | 6 帧、24 FPS、0.25 秒单次播放 |
| 方向 | 径向，无方向变体 |
| 主体合同 | 几何中心与 Alpha 中心均为 `(48±2, 48±2)`；峰值帧宽高 56–64 px、任一边留白不低于 12 px |
| 尺度 | 独立特效根与视觉节点均为 Scale 1，不再复制投射物 Sprite 或使用缩放 Tween |

- manifest 为 00–05 分别冻结“扩张—峰值—消散”的主体范围，不允许所有帧只换透明度或把一张图机械缩放成动画。
- `DamageResult.IsHit` 为真时播放命中爆发；未命中和闪避不播放伪命中特效，投射物仍按现有一次接触规则消失。
- `HitOutcome.NoDamage` 可以播放接触爆发和“无伤害”文字，但不得播放角色受伤反馈。

### 家族 C：角色受击

| 状态 | 命名 | 帧数 / 播放 | 主体合同 |
| --- | --- | --- | --- |
| 普通受击 | `effect_hit_default_00..04.png` | 5 帧、20 FPS、0.25 秒单次 | 峰值宽高 48–60 px、中心 `(48±2,48±2)`、边距不低于 14 px |
| 暴击受击 | `effect_hit_critical_00..05.png` | 6 帧、24 FPS、0.25 秒单次 | 峰值宽高 56–68 px、中心 `(48±2,48±2)`、边距不低于 10 px |

- 受击家族使用中性亮度底图，由 `DamageResult.Breakdowns` 中最终伤害最大的类型决定整次播放的固定 Tint；同值按 `Physical → Fire → Cold → Lightning → Chaos` 稳定选择。
- 冻结 Tint：物理暖白、火焰橙红、冰霜青蓝、闪电金黄、混沌紫色；具体 RGBA 写入 manifest 和纯逻辑测试，禁止散落在多个 Controller。
- 暴击使用独立帧状态与既有更大暴击飘字，不通过放大普通受击帧伪造。
- 每个战斗 Actor Prefab 新增固定 `HitEffectAnchor`，受击效果以该锚点播放；不使用当前动画帧 Sprite bounds 作为每帧位置来源，避免漂移。
- 只有 `ActorDamagedEvent` 的 `DidDealDamage` 结果播放受击特效。未命中、闪避、无伤害和治疗只保留各自语义文字，不触发受伤闪白或受击帧。

## 冻结的特效运行时合同

正式事件链固定为：

```text
ApplyDamageCommand
└── CombatSystem.ApplyDamage（唯一玩法结算）
    ├── DamageResolvedEvent ──→ 未命中 / 闪避 / 无伤害文字
    └── ActorDamagedEvent ────→ 受击帧 + 伤害 / 暴击文字

ProjectileController.TryHit
└── 只读取 DamageResult 决定是否播放接触爆发，然后销毁投射物
```

- 新增通用帧序列播放器；飞行状态循环，单次状态在最后一帧完成后归还对象池。帧推进使用可取消的 UniTask 或现有动画资产，不建立无管理协程和每个实例的常驻 `Update`。
- 命中和受击 Prefab 由统一视觉特效池管理。首次按 Prefab 预热，完成、禁用、目标销毁、场景退出和架构重置时都能回收；重复回收必须幂等。
- Prefab 作为现有 Addressable 角色 / 投射物的显式依赖加载，不在每次命中时创建新的 Addressables 句柄。
- 单个池设置明确上限；超过上限时回收最早完成对象或丢弃纯视觉请求，绝不能改变玩法结算。
- `ProjectileImpactVisual` 的旧动态 Sprite 复用路径在新 Prefab 验证后删除，不长期保留双实现。
- `ActorVisualFeedbackController` 保留死亡淡出、生命条刷新和伤害数字；受伤闪白由多帧受击效果替代，避免两套反馈叠加。

## 冻结的世界排序合同

### Sorting Layer

正式层从后到前固定为：

| Sorting Layer | 用途 | 内部基线 |
| --- | --- | ---: |
| `Ground` | 基础与细节 Tilemap | Base `0`，Detail `10` |
| `WorldObject` | 场景物件、玩家、怪物、商人、工作台、掉落物 | 由统一世界排序系统分配整体 Order |
| `WorldEffect` | 投射物飞行、命中、受击与其他独立世界特效 | Flight `0`，Impact / Hit `10` |
| `WorldInfo` | 伤害 / 治疗 / 未命中 / 闪避 / 无伤害文字及独立世界信息 | Combat Text `0` |

- `Default` 保留给 Unity 默认与明确非生产对象；正式世界 Renderer 覆盖扫描不得继续落在 `Default`。
- Sorting Layer 是渲染合同，不得修改同名 Physics Layer、碰撞矩阵或 GameObject Layer。

### 单对象整体边界与内部 Order

- 每个正式逻辑世界对象根只允许一个 `SortingGroup`。玩家、三种怪物、商人、工作台、掉落物和当前场景站立物件全部纳入；Tilemap 与独立世界特效按专用层处理。
- 同一对象的 Renderer 全部位于该 `SortingGroup` 下，禁止嵌套第二个 Group 或把描边、火焰、血条、词条文字留在外部与其他对象穿插。
- 内部相对顺序固定为：投影 / 稀有度环 `-20`、描边 `-10`、主体 `0`、火焰 / 局部附属 `10`、血条背景 `20`、Fill `21`、名称 / 词条 / 交互提示 `30`。
- 只有对象确实拥有对应职责时才使用该槽位；新增任意 Renderer 必须声明用途和偏移，禁止临时选择未登记绝对 Order。

### 接地点排序与稳定 Tie Breaker

- 新增 `WorldSortParticipant`，显式保存类别、接地点 `SortAnchor` 和稳定排序 ID；高大物件按接地点，不按 Sprite 中心、Renderer bounds 或透明画布边缘。
- 单一 `WorldSortingSystem` 维护参与者注册表。Y 以 `1/64` 世界单位量化，只有注册、注销、量化格变化或稳定身份变化时重新排序；不得在多个 Controller 中复制 Y→Order 公式。
- 后到前排序键固定为：`量化 Y（高处先） → 类别优先级（低者先） → StableSortId 序号比较`，然后在 `WorldObject` 层内分配连续唯一 Order。
- 同一量化 Y 的类别从后到前固定为：`StaticProp → Interactable → Loot → Monster → Player`。该优先级只处理同档冲突，不覆盖正常 Y 关系。
- 稳定 ID 来源：玩家使用稳定 Actor ID；怪物使用 Definition ID + 实例 Seed；掉落物使用 `ItemInstance.InstanceId`；商人、工作台和场景物件使用迁移时写入的显式语义 ID。
- StableSortId 不能为空且同场景活动对象中必须唯一。冲突是配置错误，记录上下文并由测试 / 验证器阻止，不允许回退到运行时 Instance ID、Renderer 提交顺序或生成先后。
- 排序循环集中在单一场景 Runner，使用统一取消；稳态不得产生逐帧 GC，列表与比较器复用，只有实际排序键变化时写 Renderer。

### 覆盖与行为边界

必须覆盖：

- `Player.prefab`、`Monster_Basic.prefab`、`Monster_Swift.prefab`、`Monster_Heavy.prefab`；
- `Merchant.prefab`、`CraftingStation.prefab`、`LootPickup.prefab`；
- Main 当前帐篷、补给箱、火盆、旗帜、两棵枯树和两组岩石；
- 两层地表 Tilemap、投射物、命中 / 受击特效、伤害数字与怪物词条文字。

排序只改变 Sorting Layer、SortingGroup Order 和内部相对 Order，不改变 Transform Z、场景 XY、根 Scale、Collider、Rigidbody2D、Trigger、交互距离、寻路、攻击或掉落。水平翻转、动画切帧、死亡 / 复活、禁用 / 启用和对象池复用后，排序身份与锚点必须保持不变。

## 配置文档封板合同

### 自动覆盖基线

顶层 12 类：

`TagDefinition`、`StatDefinition`、`AffixDefinition`、`ItemBaseDefinition`、`CharacterDefinition`、`ProjectileSkillDefinition`、`MonsterDefinition`、`MonsterAffixDefinition`、`MonsterSpawnDefinition`、`LootTableDefinition`、`TraderDefinition`、`CraftingDefinition`。

嵌套 6 类：

`TagQueryDefinition`、`StatModifierDefinition`、`DamageRollDefinition`、`LootTableEntry`、`MonsterSpawnRule`、`TraderStockEntry`。

- 覆盖工具复用配置中心的 `DarkFlare/Data/` 类型发现规则，通过反射读取 Unity 实际序列化字段；不硬编码 12 或 150 为长期真值。
- 机器 manifest 只登记“完整类型名 → Markdown 文件 → 二级标题锚点”，字段集合始终从代码反射得到，避免再维护一份重复字段清单。
- 扫描器只读取 manifest 指定的配置参考 Markdown，不全盘扫描 `Assets`，明确跳过字体、贴图、音频、场景和其他无关大文件。
- 每个字段必须在所属类型章节中以独立表格行出现，不能依靠同名字段在其他类型章节中误判覆盖。
- 自动检查必须发现：新增未登记类型、可达未登记嵌套结构、新增未记录字段、过期字段行、重复类型映射、缺失文档、错误标题和孤立 manifest 项。

### 单个类型文档要求

每个顶层配置章节必须包含：

1. 用途、创建菜单、正式目录和至少一个示例资产；
2. 每个序列化字段的精确名称、类型、默认值、合法范围、是否必填、稳定 ID / 引用所有权；
3. 运行时读取者、随机种子与可复现性；
4. Inspector / 内容验证、常见错误和调试入口；
5. 新增字段对旧资产的默认行为、迁移方式和废弃字段状态。

嵌套结构同样逐字段记录，并说明由哪些顶层字段持有。`_allowedItemTags`、`_blockedItemTags`、`_requiredTags`、`_blockedTags` 等仍在序列化结构中的兼容字段必须明确标记“正式资产应为空”，不能因不推荐使用而从覆盖中排除。

文档按职责拆分为标签、属性与角色、物品 / 词条 / 掉落、技能、怪物 / 生成、交易 / 打造六个短文档；旧 `combat-content.md` 保留为兼容入口并转向新章节，避免已有链接失效。

## 执行阶段

### 阶段 A：冻结基线、失败测试与迁移边界

- 为当前伤害事件顺序、投射物一次结算、未命中 / 闪避 / 无伤害语义建立特征测试。
- 冻结三类特效的路径、帧数、帧率、Importer、视觉尺寸、Anchor 和旧投射物运行时大小。
- 新增世界 Renderer 覆盖清单，测试当前只有 `Default`、目标 Prefab 没有 SortingGroup 的已知失败基线。
- 提取可复用的 Editor-only 配置类型发现入口，冻结当前 12 顶层、6 嵌套、150 字段快照。
- 建立一次性迁移的 `Preflight → Apply → Validate` 合同；只处理明确 Prefab、Main、Sorting Layer 和地表创建工具，不扫描全项目二进制资产。

验收：每个待修问题都有可重复失败测试；没有实现混入、没有场景 YAML 手改、没有 ProjectSettings 测试配置差异。

#### 阶段 A 完成记录

- 提取 `ConfigurationTypeDiscovery` 作为配置中心与后续文档覆盖工具共用的 Editor-only 类型发现入口；当前快照为 12 个顶层类型、123 个直属字段、6 个嵌套结构、27 个嵌套字段，共 150 个字段。
- 新增只读 `Alpha017VisualMigrationPreflight`，精准覆盖 8 个 Prefab 与 Main 场景 10 个对象，不扫描字体、贴图集、音频等无关大文件；`Apply` 在阶段 A 明确拒绝执行。
- 预检冻结当前差距：缺少 4 个正式 Sorting Layer、15 个对象缺少唯一 `SortingGroup`、18 个目标仍有正式 Renderer 位于 `Default`、23 个新特效帧尚未生产。
- 建立飞行、命中、普通受击、暴击受击四份静态 manifest，共冻结 23 张 96×96、64 PPU、Single 的独立 PNG 路径；当前静态投射物精准审计为 0 errors / 0 warnings。
- 新增伤害事件顺序、投射物一次结算、未命中 / 闪避 / 无伤害语义、战斗文字敌我语义及预检快照测试；Unity EditMode 6/6、PlayMode 1/1 通过。
- `DarkFlare.Editor`、`DarkFlare.Tests.EditMode`、`DarkFlare.Tests.PlayMode` 串行构建均为 0 errors；提交基线已恢复 `EnterPlayModeOptions = 0`，且未修改场景 YAML。

### 阶段 B：Pilot、逐帧生产与自动审计

- 以当前投射物作为飞行身份参考，先完成飞行 `00`；分别生成命中峰值、普通受击峰值和暴击受击峰值 Pilot。
- 每个 Pilot 完成原生分辨率检查、透明背景、中心 / 边界审计和项目风格验收后才扩展其余帧。
- 按“Pilot + 上一张通过帧”单张生成并审计 6 + 6 + 5 + 6 共 23 张正式 PNG。
- 新增 alpha 0.1.7 静态 family / pilot manifest、审计入口和可复现报告；导入后验证 Single、PPU、Pivot、Mesh、Filter、Compression、Mipmap 和 Wrap。
- 创建并校验 Flight、Impact、Hit、Critical Hit Animation Clip；本阶段只建立资产和绑定候选，不切换正式玩法入口。

验收：23/23 PNG 和四个 Clip 合同通过，0 errors / 0 warnings；不存在 SpriteSheet、人工切图、逐帧缩放或未验收批量扩展。

#### 阶段 B 完成记录

- 用户确认飞行 00、命中峰值 03、普通受击峰值 02、暴击受击峰值 03 四张 Pilot 后，已按“不可变 Pilot + 上一张通过帧”逐张完成其余 19 帧；生成、退回和量化记录见[特效 Pilot 与逐帧生成记录](../assets/visual-assets/alpha-0.1.7/pilot-generation.md)。
- 四个家族共 23 张 96×96 RGBA 独立 PNG，完整 family audit 合计 23 assets / 0 errors / 0 warnings；原生并排检查确认飞行相位稳定、命中独立扩张收束、普通受击保持斩痕、暴击始终保持交叉重斩。
- 发现并退回飞行污染帧及其派生链；对几何、覆盖率、中心或连续性不合格的单帧逐项重生成，没有放宽 manifest、修改 Pivot、按主体缩放或使用 SpriteSheet 补救。
- 目录级自动 Importer 合同精准覆盖 23 张 Sprite；Unity 验证均为 Single、64 PPU、Center、Full Rect、Point、Uncompressed、MipMap Off、Clamp，预检缺失帧由 23 降为 0。
- 新增幂等候选 Clip 构建器并生成 Flight、Impact、Default Hit、Critical Hit 四个 Clip；帧序、FPS、Loop 与末帧保持合同通过。阶段 B 未修改 Prefab、场景或运行时入口。
- `DarkFlare.Tests.Alpha017BaselineTests` EditMode 定向测试更新为 8 项并 8/8 通过，覆盖 23 张 Sprite Importer、四个 Clip、四份 manifest 与预检零缺帧。

验收：阶段 B 通过；阶段 C 才接入 Prefab、对象池、Tint 和运行时生命周期。

### 阶段 C：接入多帧特效与生命周期

- 为 `Projectile_Default.prefab` 接入飞行循环；保持物理根 Scale、Collider、速度、寿命和攻击快照不变。
- 创建独立命中与受击 Prefab、通用帧播放器和统一对象池；为四个战斗 Actor Prefab 设置 `HitEffectAnchor`。
- `ProjectileController` 改为读取 `DamageResult` 后请求命中表现；`ActorVisualFeedbackController` 根据 `ActorDamagedEvent` 请求普通 / 暴击受击表现并移除闪白。
- 集中实现主伤害类型 Tint 解析；未命中、闪避、无伤害、治疗和暴击文字与受击状态严格匹配。
- 完成旧 `ProjectileImpactVisual`、旧静态投射物依赖和临时 Sprite 复用路径的零依赖扫描后清理旧实现。

验收：一次攻击只结算一次；五类伤害 Tint、普通 / 暴击、未命中 / 闪避 / 无伤害 / 治疗语义正确；高频播放后活动对象和句柄不增长，取消与场景退出无残留。

### 阶段 D：建立稳定世界层级

- 通过 Unity MCP / Editor API 创建四个 Sorting Layer，并更新 `GroundTilemapSetup` 的未来生成合同。
- 实现 `WorldSortingSystem`、Participant、Anchor、类别和稳定 ID；以集中 Runner 处理量化变化和唯一 Order 分配。
- 精准迁移玩家、三种怪物、商人、工作台、掉落物 Prefab 的 SortingGroup、Anchor、内部 Order 和稳定身份来源。
- 使用 Unity MCP / Editor API 迁移 Main 的两层 Tilemap及八个已使用场景物件；禁止直接编辑已打开的 Scene YAML。
- 将投射物、命中 / 受击特效、伤害文字和动态怪物信息迁入对应专用层；新增永久内容验证与覆盖测试。

验收：全部目标零 `Default` 遗留、每个逻辑对象唯一 Group 和 Participant；上下交叉、同点重叠、水平翻转、死亡复活、动态生成和对象池复用连续帧稳定。

### 阶段 E：配置文档逐字段封板

- 建立类型—章节 manifest 和精准覆盖扫描器；复用配置中心发现规则，不维护第二份类型白名单。
- 将 12 个顶层类型与 6 个嵌套结构拆入六份职责文档，150 个当前字段逐项补齐默认值、范围、必填性、消费者、随机和迁移说明。
- 将文档覆盖结果接入 `ContentConfigurationValidator` 与 EditMode 测试；配置中心能显示具体类型、字段和文档路径错误。
- 更新配置索引和旧 `combat-content.md` 兼容入口，检查所有 `Docs/docs` 内部链接。

验收：运行时反射集合与文档映射完全一致；18/18 结构、150/150 当前字段通过，且新增一个临时测试字段或类型时测试会准确失败。

### 阶段 F：alpha 0.1 综合验收、文档与归档

- 运行 alpha 0.1 全部精准 EditMode / PlayMode、全量 EditMode / PlayMode、相关程序集构建和 Main Console 检查。
- 在正式 Main 中完成：持续自动攻击、命中 / 暴击 / 未命中 / 闪避 / 无伤害、敌我伤害与治疗、怪物接触伤害、法力不足 / 恢复、掉落、装备、商店、随机打造和词条显示回归。
- 固定场景构造玩家、三种怪物、商人、工作台、掉落物和场景物件的上下交叉与同点重叠，连续采样 Sorting Layer / Order，确认零闪烁和零子 Renderer 穿插。
- 预热后进行特效和排序压力测试；新特效池有界，排序稳态 0 B/frame GC Alloc，场景退出后活动特效为零。
- 在 1280×720、1920×1080、2560×1440 验证 HUD、背包、装备、商店、打造和物品浮窗没有布局回归。
- 更新视觉资产、视觉规范、玩法循环、世界渲染和配置参考模块文档；将本计划归档，标记 `alpha 0.1` 完成。
- 检查并恢复 `EditorSettings.EnterPlayModeOptions = 0`，确认 `ProjectSettings/EditorSettings.asset` 无提交差异。

验收：所有自动化、Main 实机、视觉、输入、资源生命周期、配置文档和工程门禁通过；没有临时迁移入口、测试资产、旧特效依赖或无关序列化变动。

## 验收矩阵

| 层级 | 必须证明的结果 |
| --- | --- |
| PNG | 23 个新帧独立 Single、96×96 / 64 PPU，视觉中心、占比、边距和连续性符合 manifest |
| 动画 | Flight 6 帧循环；Impact 6 帧、Hit 5 帧、Critical Hit 6 帧单次播放；无首帧闪现 |
| 战斗 | 视觉不改变伤害次数、命中、暴击、资源、碰撞和投射物生命周期 |
| 语义 | 五类伤害 Tint 稳定；敌我伤害、治疗、暴击、未命中、闪避和无伤害可辨认 |
| 生命周期 | 特效池有界；完成、禁用、销毁、场景退出和架构重置后无活动残留或 Handle 增长 |
| 层级 | 四个正式 Sorting Layer 顺序正确；生产世界 Renderer 零 `Default` 遗留 |
| 对象 | 目标对象唯一 SortingGroup、Participant、Anchor 和 StableSortId；内部 Order 符合表格 |
| 稳定性 | 上下交叉、同点、翻转、死亡复活、生成销毁和池复用不闪烁、不拆层 |
| 性能 | 排序键未变时不重排、不重复写 Renderer，稳态无逐帧 GC |
| 配置文档 | 12 顶层 + 6 嵌套全部映射；当前 150 字段逐项覆盖，过期与新增均能失败 |
| UI / 输入 | 键鼠与手柄回归通过，三档分辨率无超框、遮挡、错误浮窗和滚动条回归 |
| 工程 | Unity Console 零新增错误，相关程序集和全量测试通过，Addressables 与 Prefab / Scene 引用完整 |
| 提交 | `EnterPlayModeOptions = 0`，ProjectSettings 无测试差异，不提交用户的 `Docs/design/` 变动 |

## 预计改动面

- 资产：23 张特效帧、四个 Animation Clip、命中 / 受击 Prefab、静态 manifest 与审计脚本。
- 战斗表现：`ProjectileController`、`ActorVisualFeedbackController`、通用帧播放、伤害类型视觉解析、特效池。
- 世界排序：Sorting Layer、`WorldSortingSystem`、Participant / Anchor / Runner、七个正式 Prefab、Main 场景与 `GroundTilemapSetup`。
- 世界信息：`DamageNumberVisual`、怪物词条 / 血条 / 交互信息的所属层和内部 Order。
- 配置文档：配置类型发现公共入口、文档 coverage manifest / scanner、配置中心验证、六份字段参考。
- 测试：特效资产与事件、生命周期、世界排序 EditMode / PlayMode、配置文档覆盖、alpha 0.1 综合回归。
- 文档：`visual-assets.md`、`visual-style.md`、`gameplay-loop.md`、新增世界渲染模块、配置参考与总计划。

## 提交门禁

- 实现前必须先完成阶段 A；任何视觉资产家族必须先通过 Pilot 门禁，再逐张扩展。
- 测试期间可以临时调整 Enter Play Mode Options，但完成、失败或中止后必须恢复 `m_EnterPlayModeOptions: 0`；提交前检查实际值与 Git diff。
- Sorting Layer、Prefab 和已打开场景只通过 Unity MCP / Editor API 修改，不直接编辑 YAML；一次性迁移复跑必须为零变更，完成后删除入口。
- 配置覆盖只做反射和 manifest 指向文档的精准扫描，禁止再次对字体、贴图、音频或其他无关大文件做全盘文本扫描。
- 旧投射物 Sprite、旧命中特效代码和旧 Sorting Order 只有在消费者为零且替代路径验证完成后才能删除。
- 不读取、修改或提交 `Docs/design/` 中的用户内容；保留当前用户未提交变动。
- 不提交生成中间图、失败 Pilot、临时截图、测试种子资产、性能捕获、一次性迁移器或 Unity 自动序列化噪声。

## 非目标

- 不修改伤害、命中、闪避、暴击、护甲、抗性、接触攻击、无敌帧或技能耗蓝公式。
- 不新增技能、远程怪物 AI、Boss、状态异常、屏幕震动、镜头后处理或复杂粒子 / VFX Graph 系统。
- 不为五类伤害分别生成完整独立受击图集；本阶段使用同一中性帧族和冻结 Tint。
- 不归一已有角色、NPC、投射物或世界物件根 Scale，不改 Collider 和场景布局。
- 不把世界信息迁移为新的 UGUI / UI Toolkit 世界空间系统；本阶段只修复现有 Renderer 层级。
- 不把文档覆盖扩大到非 `DarkFlare/Data/` 的普通组件序列化字段、第三方插件或 Unity 内部类型。
- 不提前定义 `alpha 0.2` 内容；`alpha 0.1` 封板后另行规划下一版本。

## 完成定义

- 三类特效家族及普通 / 暴击受击状态全部使用独立固定画布帧，静态审计、导入、动画、Prefab 和实机连续性通过。
- 当前临时 Sprite 复用、缩放命中爆发和受伤闪白退出正式路径，视觉生命周期不驱动玩法结算。
- 四层世界渲染合同、单对象 SortingGroup、接地点排序和稳定 Tie Breaker 覆盖全部正式世界对象。
- 12 个顶层配置、6 个嵌套结构及当前 150 个字段有完整参考，新增 / 过期内容可被自动检测。
- alpha 0.1 全量战斗、资源、词条、打造、UI、输入、视觉、场景、资源生命周期与文档验收通过。
- 本计划归档，`alpha-0.1-plan.md` 标记完成，提交基线中 `EnterPlayModeOptions = 0` 且无无关 ProjectSettings 或 `Docs/design/` 变动。
