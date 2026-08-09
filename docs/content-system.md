# 首批内容池

## 当前范围

阶段 4 已把首版正式内容扩充为 14 个标签、12 个可生效词条、7 件装备和 3 种怪物，并统一接入刷怪、掉落、商店与打造。阶段 5 已补齐七件装备图标，以及裂爪猎犬、铁壳尸傀的独立外观与动画。

稳定 ID 均使用小写 `snake_case`。运行时随机规则继续沿用阶段 3：怪物生命倍率 `0.85–1.15`、空手伤害 `10–14`，大剑伤害 `20–40`，掉落概率由各怪物掉落表独立配置。

## 标签

当前标签共 14 个：

| 类别 | 稳定 ID |
| --- | --- |
| 通用 | `damage` |
| 装备 | `weapon`、`armor`、`ring` |
| 武器子类 | `sword`、`axe` |
| 伤害类型 | `physical`、`fire`、`cold`、`lightning`、`chaos` |
| 技能来源 | `projectile`、`melee` |
| 角色 | `monster` |

技能上下文提供 `projectile`，基础物理伤害包提供 `damage` 与 `physical`。伤害词条匹配时会合并技能上下文标签和当前伤害包标签，因此要求 `physical` 的元素额外伤害能够正确作用于物理伤害包。

## 词条池

| ID | 类型 | 允许装备 | 效果 | 权重 |
| --- | --- | --- | --- | ---: |
| `sharp` | 前缀 | 武器 | LocalItem 物理伤害 Flat `3–6` | 100 |
| `tempered` | 前缀 | 武器 | LocalItem 物理伤害 Increase `15–25%` | 80 |
| `flame_touched` | 前缀 | 武器、戒指 | 物理伤害额外获得火焰 `8–12%` | 60 |
| `frost_touched` | 前缀 | 武器、戒指 | 物理伤害额外获得冰霜 `8–12%` | 60 |
| `healthy` | 前缀 | 护甲、戒指 | 最大生命 Flat `15–30` | 100 |
| `reinforced` | 前缀 | 护甲 | 护甲 Flat `15–30` | 100 |
| `of_power` | 后缀 | 武器、戒指 | 全局伤害 Increase `8–15%` | 80 |
| `of_endurance` | 后缀 | 护甲、戒指 | 最大生命 Increase `8–15%` | 80 |
| `of_fire_guard` | 后缀 | 护甲、戒指 | 火焰抗性 Flat `10–20` | 100 |
| `of_cold_guard` | 后缀 | 护甲、戒指 | 冰霜抗性 Flat `10–20` | 100 |
| `of_lightning_guard` | 后缀 | 护甲、戒指 | 闪电抗性 Flat `10–20` | 100 |
| `of_chaos_guard` | 后缀 | 护甲、戒指 | 混沌抗性 Flat `10–20` | 100 |

所有词条都有非空互斥组；`flame_touched` 与 `frost_touched` 共用元素额外伤害组。掉落装备生成 1 前缀和 1 后缀，每件装备最多容纳 3 前缀和 3 后缀。每件装备至少有 4 个兼容候选，全部词条至少兼容一件装备。

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

商人库存包含七件普通装备各一件。打造配置引用全部 12 个词条；普通商品不会伪装成没有词条的魔法物品。

## 怪物、刷怪与掉落

| 怪物 | ID | 基础生命 | 移速 | 接触伤害 | 间隔 | 掉落率 | 刷怪权重 |
| --- | --- | ---: | ---: | --- | ---: | ---: | ---: |
| 荒原游魂 | `wasteland_wraith` | 52 | 2.6 | `6–10` | 0.75s | 35% | 55 |
| 裂爪猎犬 | `razor_hound` | 36 | 3.6 | `4–7` | 0.55s | 30% | 30 |
| 铁壳尸傀 | `iron_husk` | 96 | 1.7 | `10–14` | 1.0s | 45% | 15 |

三者使用独立 `MonsterDefinition`、掉落表、Addressable Prefab、Sprite 和 Animator Controller，并保持统一的移动、攻击、受伤与死亡参数契约。三张掉落表都覆盖七件装备和十二词条，装备顺序统一为大剑、战斧、皮甲、板甲、铁指环、翡翠戒指、黑曜戒指：

| 掉落表 | 条目权重 |
| --- | --- |
| 荒原游魂 | `16 / 14 / 16 / 12 / 14 / 14 / 14` |
| 裂爪猎犬 | `14 / 22 / 18 / 8 / 14 / 14 / 10` |
| 铁壳尸傀 | `18 / 18 / 8 / 24 / 10 / 10 / 12` |

## 配置入口与校验

配置中心新增“内容校验”页，可重新扫描、显示错误资产与原因，并直接打开或定位资产。`ContentConfigurationValidator` 检查：

- 预期数量、稳定 ID 格式与重复 ID。
- 中文名、词条组、权重、范围、Operation 与 Scope。
- 标签兼容、每件装备候选数量和池覆盖。
- 刷怪、掉落、商店和打造池的空项、权重与内容覆盖。
- 怪物 Prefab 的独立 GUID、Addressables 注册和运行组件。
- 正式装备图标的非空、唯一、Addressables 注册、Sprite 类型、64 PPU、Point Filter 和无压缩导入规格。

`MonsterSpawnDefinition.Rules`、`LootTableDefinition.AffixPool` 与 `TraderDefinition.Stock` 提供只读检查入口，不改变运行时事务接口。阶段 4 的一次性安装与迁移脚本已在正式资产落地并通过验收后移除，后续内容维护统一通过配置中心直接编辑正式资产。

## 验证

- EditMode 覆盖正式内容数量与 ID、全量配置校验、词条兼容与固定种子生成、12 个词条的可观察效果、互斥组和池覆盖。
- PlayMode 使用正式资产完成购买、打造、武器 / 护甲 / 双戒指装备、卸下与出售。
- 固定种子 `24681357` 连续两次运行 Main，前 12 个实例覆盖三种怪物，怪物类型、实例生命、攻击和掉落序列完全一致。
- 全量 EditMode 87/87 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游用例按原标记忽略、0 失败。
- Runtime、Editor、EditMode 与 PlayMode 四个程序集编译 0 警告、0 错误；三个怪物 Addressable Prefab 在 Main 预热中均成功加载，最终 Console 无错误。
- 阶段 5 全量 EditMode 92/92 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游用例按原标记跳过。七个图标、三种怪物 Prefab 与 Animator 契约、Addressables 图标预热、缓存和释放路径均通过专项检查。
- 阶段 6 全量 EditMode 98/98 通过；PlayMode 12 项中 10 项通过、2 项 Input System 上游既有用例跳过、0 失败。正式七件装备的交易 / 打造 / 四槽流程、三种怪物与十二词条池继续通过整合回归。

随机种子与掉落判定见 [随机化与掉落规则](./randomization-system.md)，装备事务见 [装备系统](./equipment-system.md)，词条计算语义见 [伤害系统与词条系统设计](./damage-affix-system.md)。
