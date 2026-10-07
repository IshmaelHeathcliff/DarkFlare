# 柴薪生成配置参考

柴薪模板负责几何生成，由正式道具组合配置引用；模板自身不另登记内容身份。模块见[几何与生成](../skill-board/geometry.md)与[道具安装](../skill-board/items-installation.md)。

## FirewoodGenerationDefinition

用途：选择固定柴薪尺寸和小柴薪三个顶点的外伸模板。创建菜单 `DarkFlare/技能面板/柴薪生成配置`；样例位于 `Assets/Data/Preset/SkillBoard/`，大 / 中 / 小各一份，不进入正式物品内容目录。沿用非内容配置的显式类型发现入口，可从配置中心浏览、创建和编辑。

| 字段 | 类型 / 默认 | 范围与必填 | 所有权、消费者与兼容 |
| --- | --- | --- | --- |
| `_size` | `FirewoodSize` / `Large`（0） | 仅 Large、Medium、Small | `Generate` 决定主体、数量范围和对称群；配置中心、预览与内容校验消费。不是可变实例状态，无内容 ID / 存档字段。 |
| `_smallPortDirections` | `HexDirectionMask[]` / `[West \| NorthWest, East \| NorthEast, SouthEast \| SouthWest]`（24、33、6） | Small 时必须恰有三项，各项非零且为对应默认掩码的子集；大 / 中不消费该字段 | 按局部顶点 `(0,0)`、`(1,0)`、`(0,1)` 顺序指定允许方向；裂缝候选枚举只选这些向外方向。只读消费，生成、旋转与预览不写回。无旧正式资产迁移。 |

大主体半径 2、中主体半径 1，小为两两相邻三角形。大 / 中裂缝沿径向，小裂缝从模板所选方向外伸一格。方向序号 0–5 为东、东南、西南、西、西北、东北，与棋盘轴坐标一致。

### 数量与随机合同

数量是已确认规则，未复制为另一套可越界配置：大 N 9–15、C 1–6 且不超半数、内圈 L 1–3；中 N 4–6、C 1–3 且不超半数；小 N 2–3、C 1–2，可超半数。调用方可指定数量，0 表示从合法组合抽取；非法请求返回 `counts`，不裁减或重试。大 N 15 只能 L 3。

`FirewoodGenerator.ContractVersion` 是生成协议身份，当前 1；生成器显式接收种子，以稳定混合派生数量、裂缝旋转类 / 朝向、外圈普通节点和内圈位置子流。候选先按对称评分取最优，再按规范坐标序列归并旋转类；N、给定 N 的 C、给定 N/C 的 L 分层等概率抽取，端口旋转类及其可用姿态分别抽取，普通节点按固定环槽无放回选择。旋转类只针对端口布局；节点词条由道具组合配置的独立子流生成。

候选列表按固定槽序 / 方向序构造，类键按 Ordinal 排序；大 / 中使用六重旋转，小使用绕三角形中心的三重旋转重定位到固定主体。反射用于评分而不合并左右手变体，不增加翻面操作。无全局 Unity 随机或 GameplayRandom 通道消费；相同配置内容、种子与合同版本重放相同节点 / 连线。后续道具保存实际生成结果，不能因算法升级重掷旧实例。

### 校验与错误

`OnValidate` 输出带资产上下文的中文 Warning。配置中心“内容校验”扫描 `Assets/Data/Preset/SkillBoard` 的模板配置并验证代表性生成；字段覆盖沿用 `ConfigurationDocumentationCoverageTests`。非法尺寸、方向数组缺失 / 数量错误、向内或未知方向给出可定位错误；生成配置不自动修复。配置修改立即影响下一次生成，无跨配置缓存；已生成布局保持独立副本。

## SkillBoardItemConfiguration

用途：嵌套于 ItemBaseDefinition；正式样例在 `Assets/Data/Preset/Items/SkillBoard/`。创建时按道具类型生成实际结果，保存到 ItemInstance.SkillBoardState。不同角色只消费对应字段。

| 字段 | 类型 / 默认 | 范围与必填 | 所有权、消费者与兼容 |
| --- | --- | --- | --- |
| `_firewood` | `FirewoodGenerationDefinition` / 空 | 柴薪必填，必须通过生成校验 | 独立布局子种子生成一次；读档保存布局须与基底尺寸一致，不重新生成 |
| `_talentModifiers` | `List<StatModifierDefinition>` / 空 | 柴薪必填，合法 Stat / Scope / Operation，有限非倒置值域 | 每个天赋抽一项并固定实际值；节点 ID、范围与数值进入实例存档 |
| `_talentRadius` | `int` / `0` | 非负，0 全局，正数为六边格距离 | 首轮大柴薪 0、中小 3；阶段二仅保存，后续激活与效果计算消费 |
| `_flameSkill` | `ProjectileSkillDefinition` / 空 | 火焰必填，技能必须已登记 | 固定技能内容身份随实例保存；本阶段保留原玩家技能运行 |
| `_emberRadius` | `int` / `3` | 余烬大于 0，且基底有非空隐式强化修改器 | 保存实际范围与实例已掷修改器；阶段二不应用战斗效果 |
| `_energyCapacity` | `int` / `1000` | 能量瓶大于 0；三档 1000 / 2000 / 4000 | EnergyBottleSystem 限容收集；恢复拒绝负值和越容量；改容量须兼容已有能量 |

新角色必须不可堆叠、无战斗装备槽 / 主动使用 / 维持状态。柴薪、火焰、余烬背包 1×1，能量瓶 2×1。火焰与余烬允许合法显式词条，普通掉落与货池当前使用已掷隐式变体；柴薪和瓶子不允许显式词条。配置中心与 ContentCatalog.Build 共同拒绝非法组合。
