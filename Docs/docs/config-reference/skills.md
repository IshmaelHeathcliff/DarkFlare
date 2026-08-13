# 技能配置参考

本文件封板当前投射物技能配置。正式示例位于 `Assets/Data/Preset/Skills`；创建入口为 `DarkFlare/Data/Skills/Projectile Skill Definition`。

## ProjectileSkillDefinition

用途：声明自动释放投射物的身份、Addressable Prefab、释放节奏、法力消耗、运动参数和伤害所有权。

| 字段 | 类型 / CLR 默认 | 必填、范围与稳定性 | 所有权、消费者、迁移 |
| --- | --- | --- | --- |
| `_id` | `string` / 空 | 正式资产必填；唯一小写 `snake_case`，发布后稳定 | 技能选择、攻击上下文、调试与潜在存档消费；改名须迁移 |
| `_displayName` | `string` / 空 | 正式资产必填；中文名称 | UI、Inspector 与调试消费 |
| `_projectilePrefab` | `AssetReferenceGameObject` / `null` | 正式技能必填且 GUID 有效 | 技能系统异步实例化并释放；Prefab 必须包含当前投射物控制器与碰撞合同 |
| `_cooldown` | `float` / `0.6` | 至少 `0.05` 秒 | 玩家自动攻击节奏；不是命中后冷却 |
| `_manaCost` | `float` / `0` | 不得小于 `0` | 释放前校验法力，投射物成功生成后才提交消耗；失败不取攻击种子 |
| `_targetRange` | `float` / `10` | 至少 `0.1` 世界单位 | 目标查询与释放条件消费 |
| `_projectileSpeed` | `float` / `12` | 至少 `0.1` | `ProjectileController` 位移消费 |
| `_projectileRadius` | `float` / `0.16` | 至少 `0.05` | 投射物命中空间查询消费；不由贴图尺寸隐式推导 |
| `_projectileLifetime` | `float` / `2` | 至少 `0.1` 秒 | 未命中时的自动回收/销毁消费 |
| `_tags` | `List<TagDefinition>` / 空 | 只允许非派生 Skill 标签；正式投射物技能当前为空 | `CombatTagResolver` 自动派生 `projectile`，不得重复手填 |
| `_damageSource` | `ProjectileDamageSource` / `Skill` | 必填；Skill 或 EquippedWeapon | `AttackSnapshotFactory` 决定伤害唯一所有者；切换必须同步 `_baseDamages` |
| `_baseDamages` | `List<DamageRollDefinition>` / 空 | Skill 必须非空；EquippedWeapon 必须为空 | Skill 来源按攻击伤害种子掷值；武器来源只读取 Weapon 槽物品基础伤害 |

`EquippedWeapon` 找不到有效 Weapon 槽来源时，攻击构建失败：不生成投射物、不发送攻击事件、不消耗法力或 PlayerAttack 根种子。法力不足遵循同一原子边界。两种来源都没有代码级固定伤害保护。

### 校验、随机与迁移

- 配置中心检查稳定 ID、Prefab、正数运动参数、非负法力、派生标签重复事实和伤害来源所有权。
- `CreateDamagePackets(seed)` 只为 Skill 自有伤害创建本地 `System.Random`；命中、暴击与闪避仍由攻击根种子的其他子种子负责。
- 从 Skill 迁到 EquippedWeapon 时先确保所有可用武器具有合法 `_baseDamages`，再清空技能伤害；反向迁移则必须补齐技能伤害。
- 调整半径、速度或寿命后需验证投射物 Prefab、视觉特效与命中空间，不得靠 Transform 缩放补偿配置错误。

