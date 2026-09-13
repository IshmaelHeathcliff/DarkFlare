# 状态配置参考

配置、纯规则核心、角色战斗及七异常抗性已接入；自动计时与状态图标尚待阶段 4。模块说明见[状态系统](../status-system.md)。正式七异常及装备守护样例位于 `Assets/Data/Preset/Statuses/`，阶段 3 已通过验收。

## StatusDefinition

类型：`DarkFlare.StatusDefinition`。创建菜单：`DarkFlare/Data/Statuses/Status Definition`。建议正式资产目录：`Assets/Data/Preset/Statuses/`；稳定内容身份为 `status:<id>`，通过 `ContentDefinitionRegistry` 登记，缺失策略为 `BlockLoad`。新增正式资产时须加入内容目录并设置 Addressables；当前 core 内容版本为 4，存档 Schema 仍为 2；状态实例保存恢复尚未交付。

| 字段 | 类型 | 默认 | 规则与消费者 |
| --- | --- | --- | --- |
| `_id` | string | 空 | 必填，小写 snake_case；CreateRules 冻结为 ContentId，空值拒绝 |
| `_localizedName` | LocalizedContentReference | statuses / 空键 | 正式内容须提供本地化名称；展示阶段读取，不参与规则比较 |
| `_localizedDescription` | LocalizedContentReference | statuses / 空键 | 正式内容须提供本地化说明；正式名称和说明来自 statuses 中英表 |
| `_icon` | AssetReferenceSprite | null | 展示阶段经 Session Addressables 加载；纯核心允许未配置，不加载资源 |
| `_category` | StatusCategory | Skill | 光环、特殊技能或异常；用于快照和驱散筛选 |
| `_ailment` | AilmentKind | None | 七异常显式身份；非 None 必须为异常分类、限时生命周期；决定对应抗性与合法效果组合 |
| `_tags` | List&lt;TagDefinition&gt; | 空列表 | 冻结为只读 TagSet；禁止空引用或非法 ID；筛选要求包含全部指定标签 |
| `_canDispel` | bool | true | 驱散只移除允许驱散的层，与消费资格独立 |
| `_canConsume` | bool | true | 禁止时，消费请求失败且不改变层 |
| `_repeat` | StatusRepeatMode | Uniform | 统一数值覆盖刷新、独立候选择强、独立层分别生效；数值模型与重复模型合并为三个合法选项 |
| `_clock` | StatusClockMode | PerLayer | Shared 仅支持 Uniform；共享指到期时间，不重置各层周期相位 |
| `_refreshExistingLayers` | bool | true | 仅 Uniform + PerLayer 可关闭；关闭时未满层添加保留旧层时间，但统一数值仍更新；满层继续遵循满层策略 |
| `_lifetime` | StatusLifetime | Timed | Timed、SourceOwned、Infinite；持续来源与无限层的剩余时间显式为无限 |
| `_overflow` | StatusOverflow | Default | Default：统一刷新、最强保留更强、独立替换最早到期；Reject：满层拒绝；RefreshTime：仅刷新时间，不适用于最强模型 |
| `_maxStacks` | int | 1 | 1–4096；目标所有状态合计也限 4096 层，超出容量失败 |
| `_duration` | double | 5 | 有限且 0–1,000,000,000 秒；限时必须大于 0；非限时不使用此数值计算到期 |
| `_interval` | double | 0 | 0 表示无周期；启用后为 0.000001–1,000,000,000 秒；每跳伤害非空时必须启用 |
| `_strength` | double | 0 | 有限非负的显式比较值；不把不同属性或伤害自动相加作为强度 |
| `_modifiers` | List&lt;StatModifierDefinition&gt; | 空列表 | 复用既有配置；允许 GlobalActor、Skill、TargetTaken，拒绝空元素、非法范围和尚未实现的 Chance / Trigger / Limit；非伤害 GlobalActor 状态条件只支持 SourceActor 域 |
| `_periodicDamage` | List&lt;DamageRollDefinition&gt; | 空列表 | 每跳基础伤害，范围有限、非负且下限不大于上限；CreateEffects 标记为 Base，角色战斗参与者在施加准备时冻结来源增伤 |
| `_blockedActions` | StatusActionBlock | None | Move、Attack、Cast、UseItem 独立位；角色适配已拦截移动、攻击与施法，UseItem 供后续使用物品入口消费 |

`CreateRules` 生成不可变规则；`CreateEffects(Random)` 使用调用方提供的随机流生成独立效果快照，不写回共享资产。相同参数和相同随机输入产生相同结果；列表顺序决定掷值顺序。只读效果快照复制列表，保留既有不可变 ModifierInstance / DamagePacket。空效果列表合法，可表示纯标记。

`ValidateConfiguration` 和 Odin“校验状态规则”按钮检查 ID、范围、效果与组合；正式配置中心同样调用此校验。随机范围必须在生成前校验，不能只依赖一次掷值恰好合法。传入请求时还检查持续时间覆盖、来源、总容量和统一叠层数值溢出。

本类型没有历史正式资产或兼容字段。运行中的同 ID 状态组拒绝不同规则混入；编辑配置不会修改已持有层。后续新增图标时补齐展示资源验收，不把缺省展示资源当成完整状态已交付。

SourceOwned 的新提供方需要实际层身份。Uniform 组满层时拒绝新的提供方，避免未登记的新来源改写旧来源效果；同提供方重复绑定幂等，参数变化走 UpdateSource。多个持续来源需要择强竞争时配置 Strongest。
