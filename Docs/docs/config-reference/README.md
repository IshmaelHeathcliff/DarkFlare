# 配置参考索引

## 覆盖范围

本目录登记所有 `CreateAssetMenu.menuName` 以 `DarkFlare/Data/` 开头的配置类型。新增配置类型或嵌套序列化结构时，必须同步更新本索引和对应字段参考。

| 配置类型 | 当前示例目录 | 字段参考状态 | 现有模块文档 |
| --- | --- | --- | --- |
| `TagDefinition` | `Assets/Data/Preset/Tags` | [完整](./combat-tags.md) | [战斗标签系统](../combat-tag-system.md) |
| `StatDefinition` | `Assets/Data/Preset/Stats` | 骨架已登记 | [属性系统](../stat-system.md) |
| `AffixDefinition` | `Assets/Data/Preset/Affixes` | 标签相关字段已覆盖，其余待补全 | [伤害与词条](../damage-affix-system.md) |
| `ItemBaseDefinition` | `Assets/Data/Preset/Items` | 骨架已登记 | [装备系统](../equipment-system.md) |
| `CharacterDefinition` | `Assets/Data/Preset/Actors` | 骨架已登记 | [玩法循环](../gameplay-loop.md) |
| `ProjectileSkillDefinition` | `Assets/Data/Preset/Skills` | 标签相关字段已覆盖，其余待补全 | [伤害与词条](../damage-affix-system.md) |
| `MonsterDefinition` | `Assets/Data/Preset/Monsters` | 标签相关字段已覆盖，其余待补全 | [首批内容池](../content-system.md) |
| `MonsterSpawnDefinition` | `Assets/Data/Preset/Monsters` | 骨架已登记 | [首批内容池](../content-system.md) |
| `LootTableDefinition` | `Assets/Data/Preset/Loot` | 骨架已登记 | [随机化与掉落](../randomization-system.md) |
| `TraderDefinition` | `Assets/Data/Preset/Traders` | 骨架已登记 | [首批内容池](../content-system.md) |
| `CraftingDefinition` | `Assets/Data/Preset/Crafting` | 骨架已登记 | [打造系统](../crafting-system.md) |

## 每类配置的交付要求

完整字段参考至少记录：

- 资产用途、创建菜单和正式示例；
- 每个序列化字段的类型、默认值、合法范围和必填性；
- 稳定 ID、资产引用和资源所有权；
- 运行时读取者、随机种子和可复现性；
- Inspector、内容校验与常见错误；
- 字段迁移和旧资产默认行为。

`alpha 0.1.1` 建立索引和标签字段参考。后续每个改变配置结构的阶段负责补全受影响类型；`alpha 0.1.6` 增加自动覆盖检查并完成封板。

## 通用约定

- 稳定 ID 使用小写 ASCII `snake_case`，一旦进入存档、Addressables 或跨资产引用便不得随意修改。
- 正式配置放在 `Assets/Data/Preset` 对应分类目录；测试临时资产不得进入正式内容池。
- 随机结果必须由调用方提供种子，禁止在配置对象内部隐式使用全局随机状态。
- 配置资产由 Addressables 或显式资产引用持有；运行时不得使用 `Resources.Load`。
- 正式提交前运行配置中心“内容校验”和相关 EditMode 测试。
