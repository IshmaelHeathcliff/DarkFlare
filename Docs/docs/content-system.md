# 首批内容池

## 当前范围

当前正式内容包含 14 个标签、25 个物品词条、10 个怪物词条、7 件装备和 3 种怪物，并统一接入刷怪、掉落、商店与打造。七件装备和三种怪物均使用独立正式视觉；带怪物词条的实例会在世界中显示最多两行短名称。

稳定本地 ID 均使用小写 `snake_case`，持久化时由类型命名空间组成 `<namespace>:<local_id>` 的 `ContentId`；唯一正式目录 `core` v1 收录当前 90 个配置。目录、缺失策略和扩展规则见[稳定身份、内容目录与迁移框架](./infrastructure/content-identity-migration.md)。运行时随机规则继续沿用独立通道：怪物生命倍率 `0.85–1.15`、初始大剑伤害 `20–40`，掉落概率由各怪物掉落表独立配置。空手武器技能没有伤害回退。

## 标签

当前标签目录仍包含 14 个稳定 ID，但按 Domain 和使用状态管理：

| Domain | Active | Reserved |
| --- | --- | --- |
| `ItemSpawn` | `weapon`、`armor`、`ring` | `sword`、`axe` |
| `Damage` | `physical` | `damage`、`fire`、`cold`、`lightning`、`chaos` |
| `Skill` | — | `projectile`、`melee` |
| `Actor` | — | `monster` |

`weapon`、`armor`、`ring` 由物品类型与装备槽派生；`projectile` 由投射物技能类型派生；`monster` 由角色阵营派生；伤害类型标签由 `DamagePacket` 的最终类型与缩放血统派生。正式资产不再重复手填这些事实。详细字段合同见[标签配置参考](./config-reference/combat-tags.md)。

## 词条池

| ID | 类型 | 允许装备 | 效果 | 权重 |
| --- | --- | --- | --- | ---: |
| `sharp` | 前缀 | 武器 | LocalItem 物理伤害 Flat `3–6` | 100 |
| `tempered` | 前缀 | 武器 | LocalItem 物理伤害 Increase `15–25%` | 80 |
| `flame_touched` | 前缀 | 武器、戒指 | 物理额外获得火焰 `8–12%`；火焰伤害 Increase `8–15%` | 60 |
| `frost_touched` | 前缀 | 武器、戒指 | 物理额外获得冰霜 `8–12%`；冰霜伤害 Increase `8–15%` | 60 |
| `healthy` | 前缀 | 护甲、戒指 | 最大生命 Flat `15–30` | 100 |
| `reinforced` | 前缀 | 护甲 | 护甲 Flat `15–30` | 100 |
| `of_power` | 后缀 | 武器、戒指 | 全局伤害 Increase `8–15%` | 80 |
| `of_endurance` | 后缀 | 护甲、戒指 | 最大生命 Increase `8–15%` | 80 |
| `of_fire_guard` | 后缀 | 护甲、戒指 | 火焰抗性 Flat `10–20` | 100 |
| `of_cold_guard` | 后缀 | 护甲、戒指 | 冰霜抗性 Flat `10–20` | 100 |
| `of_lightning_guard` | 后缀 | 护甲、戒指 | 闪电抗性 Flat `10–20` | 100 |
| `of_chaos_guard` | 后缀 | 护甲、戒指 | 混沌抗性 Flat `10–20` | 100 |
| `arcane_reserve` | 前缀 | 护甲、戒指 | 最大法力 Flat `15–30` | 100 |
| `regenerating` | 前缀 | 护甲、戒指 | 生命恢复 Flat `0.5–1.5` / 秒 | 80 |
| `meditative` | 前缀 | 护甲、戒指 | 法力恢复 Flat `1–3` / 秒 | 80 |
| `mighty` | 前缀 | 武器、护甲、戒指 | 力量 Flat `5–10` | 90 |
| `deft` | 前缀 | 武器、护甲、戒指 | 敏捷 Flat `5–10` | 90 |
| `learned` | 前缀 | 武器、护甲、戒指 | 智力 Flat `5–10` | 90 |
| `storm_touched` | 前缀 | 武器、戒指 | 物理额外获得闪电 `8–12%`；闪电伤害 Increase `8–15%` | 60 |
| `chaos_touched` | 前缀 | 武器、戒指 | 物理额外获得混沌 `8–12%`；混沌伤害 Increase `8–15%` | 45 |
| `deadly` | 后缀 | 武器、戒指 | 暴击率 Flat `2–5` | 75 |
| `of_ruin` | 后缀 | 武器、戒指 | 暴击伤害 Flat `10–20` | 75 |
| `accurate` | 后缀 | 武器、戒指 | 命中 Flat `10–25` | 90 |
| `elusive` | 前缀 | 护甲、戒指 | 闪避 Flat `8–20` | 90 |
| `of_swiftness` | 后缀 | 护甲、戒指 | 移动速度 Increase `5–10%` | 70 |

所有词条都有非空互斥组；四个元素额外伤害词条共用元素互斥组。掉落装备生成 1 前缀和 1 后缀，每件装备最多容纳 3 前缀和 3 后缀。25 个正式词条全部进入三张掉落表和打造池，且每条至少兼容一件正式装备。由 `StatIds.All` 驱动的覆盖校验保证 23 项公开属性都有当前运行时可消费的正式词条。

## 装备池

| 装备 | ID | 基础效果 | 价格 | 格子 |
| --- | --- | --- | ---: | --- |
| 大剑 | `great_sword` | 物理伤害 `20–40` | 30 | 2×3 |
| 战斧 | `war_axe` | 物理伤害 `26–34` | 32 | 2×2 |
| 皮甲 | `leather_armor` | 最大生命 Flat `20–35` | 24 | 2×3 |
| 板甲 | `plate_armor` | 护甲 Flat `25–40` | 36 | 2×3 |
| 铁指环 | `iron_ring` | 全局伤害 Increase `8–14%` | 20 | 1×1 |
| 翡翠戒指 | `jade_ring` | 最大生命 Flat `12–24` | 22 | 1×1 |
| 黑曜戒指 | `obsidian_ring` | 四种抗性各 Flat `5–10` | 28 | 1×1 |

商人库存包含七件普通装备各一件。打造配置引用全部 25 个物品词条；普通商品不会伪装成没有词条的魔法物品。

## 怪物、刷怪与掉落

| 怪物 | ID | 基础生命 | 移速 | 接触伤害 | 半径 / 间隔 | 掉落率 | 刷怪权重 |
| --- | --- | ---: | ---: | --- | --- | ---: | ---: |
| 荒原游魂 | `wasteland_wraith` | 52 | 2.6 | `6–10` | `0.75 / 0.75s` | 35% | 55 |
| 裂爪猎犬 | `razor_hound` | 36 | 3.6 | `4–7` | `0.75 / 0.55s` | 30% | 30 |
| 铁壳尸傀 | `iron_husk` | 96 | 1.7 | `10–14` | `0.75 / 1.0s` | 45% | 15 |

正式基础战斗属性：

| 怪物 | 命中 | 闪避 | 暴击率 | 暴击伤害 | 护甲 | 四抗 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 荒原游魂 | 90 | 15 | 5% | 50% | 0 | 0% |
| 裂爪猎犬 | 110 | 30 | 8% | 50% | 0 | 0% |
| 铁壳尸傀 | 80 | 5 | 3% | 50% | 40 | 0% |

三者使用独立 `MonsterDefinition`、掉落表、Addressable Prefab、Sprite 和 Animator Controller，并保持统一的移动、攻击、受伤与死亡参数契约。移动参数统一为停止距离 `0.6`、软分离半径 `0.8`、软分离权重 `0.65`；接触攻击按表中的独立间隔执行，没有玩家全局受伤冷却。三张掉落表都覆盖七件装备和 25 个物品词条，装备顺序统一为大剑、战斧、皮甲、板甲、铁指环、翡翠戒指、黑曜戒指：

| 掉落表 | 条目权重 |
| --- | --- |
| 荒原游魂 | `16 / 14 / 16 / 12 / 14 / 14 / 14` |
| 裂爪猎犬 | `14 / 22 / 18 / 8 / 14 / 14 / 10` |
| 铁壳尸傀 | `18 / 18 / 8 / 24 / 10 / 10 / 12` |

三种怪物都显式引用同一组 10 个 `MonsterAffixDefinition`，每个实例按根种子生成 `0–2` 条：

| 词条 | ID | 权重 | 效果 | 互斥组 |
| --- | --- | ---: | --- | --- |
| 燃烧 / 霜寒 / 风暴 | `monster_*_touched` | 各 70 | 物理额外获得对应元素 `15–25%` | `monster_elemental_damage` |
| 抗火 / 抗冰 / 抗电 | `monster_*_guarded` | 各 80 | 对应抗性 Flat `20–35` | `monster_elemental_guard` |
| 强韧 | `monster_robust` | 100 | 最大生命 More `25–40%` | `monster_health` |
| 装甲 | `monster_armored` | 100 | 护甲 Flat `30–60` | `monster_armor` |
| 敏捷 | `monster_elusive` | 90 | 闪避 Flat `20–40` | `monster_evasion` |
| 迅捷 | `monster_swift` | 80 | 移动速度 Increase `10–20%` | `monster_speed` |

怪物词条与物品词条是两个独立配置域：不使用前后缀容量或物品标签查询，只允许当前管线可消费的 `GlobalActor` 修改器。元素附伤进入接触攻击快照，其余修改器进入怪物最终属性。

## 配置入口与校验

配置中心新增“内容校验”页，可重新扫描、显示错误资产与原因，并直接打开或定位资产。`ContentConfigurationValidator` 检查：

- 预期数量、稳定 ID 格式与重复 ID。
- 标签 Domain、使用状态、引用位置、查询作用域与新旧字段混用。
- 物品类别、角色阵营、技能类型和伤害类型的重复手填标签。
- 中文名、词条组、权重、范围、Operation 与 Scope。
- 标签兼容、每件装备候选数量和池覆盖。
- 刷怪、掉落、商店和打造池的空项、权重与内容覆盖。
- 怪物 Prefab 的独立 GUID、Addressables 注册和运行组件。
- 10 个正式怪物词条的 ID、组、权重、颜色、范围与合法 Scope，以及三种怪物的完整池和 `0–2` 数量合同。
- 怪物移动范围、停止距离、软分离参数，以及 Actor Layer、Physics2D Matrix 和 `WorldBounds` 合同。
- 正式装备图标的非空、唯一、Addressables 注册、Sprite 类型、64 PPU、Point Filter 和无压缩导入规格。

`MonsterSpawnDefinition.Rules`、`LootTableDefinition.AffixPool` 与 `TraderDefinition.Stock` 提供只读检查入口，不改变运行时事务接口。阶段 4 的一次性安装与迁移脚本已在正式资产落地并通过验收后移除，后续内容维护统一通过配置中心直接编辑正式资产。

## 验证

- EditMode 覆盖正式内容数量与 ID、全量配置校验、25 个物品词条的属性覆盖与合法候选、10 个怪物词条的固定种子生成、可观察效果、互斥组和池覆盖。
- PlayMode 使用正式资产完成购买、打造、武器 / 护甲 / 双戒指装备、卸下与出售。
- 固定种子 `24681357` 连续两次运行 Main，前 12 个实例覆盖三种怪物，怪物类型、实例生命、攻击和掉落序列完全一致。
- 全量 EditMode 87/87 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游用例按原标记忽略、0 失败。
- Runtime、Editor、EditMode 与 PlayMode 四个程序集编译 0 警告、0 错误；三个怪物 Addressable Prefab 在 Main 预热中均成功加载，最终 Console 无错误。
- 阶段 5 全量 EditMode 92/92 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游用例按原标记跳过。七个图标、三种怪物 Prefab 与 Animator 契约、Addressables 图标预热、缓存和释放路径均通过专项检查。
- 阶段 6 全量 EditMode 98/98 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游既有用例跳过、0 失败。正式七件装备的交易 / 打造 / 四槽流程、三种怪物与十二词条池继续通过整合回归。
- alpha 0.1.3 全量 EditMode 148/148 通过；PlayMode 17 项中 15 项通过、2 项 Input System 上游既有用例跳过、0 失败。三种怪物的 Layer、独立接触间隔、停止与软分离配置均由永久校验和专项测试覆盖。
- alpha 0.1.5 正式内容专项 13/13、全量 EditMode 176/176 通过；PlayMode 20 项中 18 项通过、2 项 Input System 上游既有用例跳过、0 失败。25 个物品词条、10 个怪物词条、三份完整怪物池、三个世界标记 Prefab 与固定种子重放均通过整合回归。

随机种子与掉落判定见 [随机化与掉落规则](./randomization-system.md)，装备事务见 [装备系统](./equipment-system.md)，词条计算语义见 [伤害系统与词条系统设计](./damage-affix-system.md)。
