# 配置参考索引

## 封板状态

`alpha 0.1.7` 阶段 E 已建立配置发现、文档映射与字段覆盖闭环：

- 发现入口：所有 `CreateAssetMenu.menuName` 以 `DarkFlare/Data/` 开头的顶层配置，以及它们可达的 Unity 嵌套序列化类型；
- 当前基线：12 个顶层类型、6 个嵌套类型，共 18 类、153 个序列化字段；
- 映射真值：[coverage-manifest.json](./coverage-manifest.json) 只登记完整类型名、职责文档和 H2 锚点；
- 字段真值：Editor 反射实时读取 Unity 序列化字段，manifest 不复制字段列表；
- 文档要求：每个字段必须在对应类型 H2 章节中拥有独立表格行。

配置中心“内容校验”会同时运行覆盖扫描。新增配置类型、可达嵌套类型或序列化字段而未登记时，提交前会得到明确错误；删除或重命名字段后，旧文档行也会以 stale 错误阻止通过。

## 六份职责文档

| 职责 | 类型 | 字段数 | 字段参考 | 模块说明 |
| --- | --- | ---: | --- | --- |
| 标签与查询 | `TagDefinition`、`TagQueryDefinition` | 9 | [完整](./tags-and-queries.md) | [战斗标签系统](../combat-tag-system.md) |
| 属性与词缀 | `StatDefinition`、`AffixDefinition`、`StatModifierDefinition` | 28 | [完整](./stats-and-affixes.md) | [属性系统](../stat-system.md)、[伤害与词缀](../damage-affix-system.md) |
| 角色与怪物 | `CharacterDefinition`、`MonsterDefinition`、`MonsterAffixDefinition`、`MonsterSpawnDefinition`、`MonsterSpawnRule` | 59 | [完整](./actors-and-monsters.md) | [玩法循环](../gameplay-loop.md)、[首批内容池](../content-system.md) |
| 物品与掉落 | `ItemBaseDefinition`、`DamageRollDefinition`、`LootTableDefinition`、`LootTableEntry` | 23 | [完整](./items-and-loot.md) | [装备系统](../equipment-system.md)、[随机化与掉落](../randomization-system.md) |
| 技能 | `ProjectileSkillDefinition` | 12 | [完整](./skills.md) | [伤害与词缀](../damage-affix-system.md) |
| 交易与打造 | `TraderDefinition`、`TraderStockEntry`、`CraftingDefinition` | 19 | [完整](./trade-and-crafting.md) | [打造系统](../crafting-system.md)、[首批内容池](../content-system.md) |

旧 [标签配置参考](./combat-tags.md) 与 [角色、物品与攻击配置参考](./combat-content.md) 保留为阶段迁移摘要；新增或修改字段只维护上表六份封板合同。

## 每类配置的交付要求

每个类型章节必须记录：

- 资产用途、创建菜单、正式示例与所属目录；
- 每个序列化字段的精确名称、类型、CLR/显式默认值、范围与必填性；
- 稳定 ID、资产引用、单一数据所有者和实际运行时消费者；
- 随机流、根种子、列表顺序与可复现性边界；
- Inspector、内容校验、常见错误和失败行为；
- 旧资产默认、兼容字段、重命名和删除迁移。

兼容字段也必须单独成行，并明确“正式资产必须为空”；不能以兼容读取存在为由继续生产新数据。

## 变更流程

1. 用 `ConfigurationTypeDiscovery` 确认新增类型是否会被配置中心发现。
2. 顶层或嵌套类型新增时，在 `coverage-manifest.json` 登记唯一职责文档与 H2 锚点。
3. 为每个新增 `[SerializeField]` 字段增加独立表格行，写明默认、范围、所有权、消费者与迁移。
4. 运行 `ConfigurationDocumentationCoverageTests` 和配置中心内容校验。
5. 删除/改名字段时移除 stale 行；字段迁移完成前不得只修改文档或只修改代码。

覆盖扫描只读取 manifest 指定的 Markdown，不做全盘资源扫描，也不会读取字体、贴图或 `Docs/design`。

## 通用约定

- 稳定 ID 使用小写 ASCII `snake_case`；进入实例、存档、Addressables 或跨资产引用后不得随意修改。
- 正式配置放在 `Assets/Data/Preset` 对应分类目录；测试临时资产不得进入正式内容池。
- 随机结果由调用方提供种子；配置对象不得隐式使用全局随机状态或在运行时写回共享资产。
- 资源通过 Addressables 或显式资产引用持有；运行时不得使用 `Resources.Load`。
- 正式提交前运行配置中心“内容校验”、相关 EditMode/PlayMode 测试，并确认 `ProjectSettings/EditorSettings.asset` 无差异且 `EnterPlayModeOptions` 为 `0`。
