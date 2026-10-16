# 棋盘道具与安装事务

阶段二交付正式道具、安装、拆卸、交易和保存。几何规则见[几何模块](geometry.md)，能量见[能量瓶](energy-bottles.md)，配置见[配置参考](../config-reference/skill-board.md)。阶段三已接入[充能与点亮](growth.md)，阶段四已交付[效果投影](effects.md)和[手动六槽技能](manual-skills.md)，正式玩家棋盘继续按[总计划](../plan/alpha-0.7.1-skill-board-plan.md)推进。

## 实例与配置

`ItemBaseDefinition` 追加 Firewood / Flame / Ember / EnergyBottle，保留原枚举编号；`SkillBoardItemConfiguration` 采用组合字段。柴薪、火焰、余烬背包占 1×1，能量瓶占 2×1，均为独立不可堆叠实例，没有战斗装备槽或普通主动使用能力。

创建入口仍为基底工厂和既有掉落 / 商店工厂。`SkillBoardItemState` 一次保存实际柴薪布局、合同版本、节点 ID、显式边、端口、天赋已掷值与范围，或火焰技能内容引用、余烬范围、瓶内能量。布局和节点效果使用物品种子的独立子流；移动、旋转、交易与恢复均不重新生成。

首轮正式资产在 `Assets/Data/Preset/Items/SkillBoard/`：大 / 中 / 小柴薪、基础投射物火焰、增伤余烬和三档瓶。柴薪节点候选为伤害 Increase 1–4、暴击率 Increase 1–3；大柴薪全局、中小范围 3。火焰隐式伤害 Increase 1–5，余烬 5–12、范围 3。这些是暂定内容；阶段四起按点亮和路径规则应用战斗效果。火焰 / 余烬可接收满足 SpawnQuery 的显式词条，当前正式货池为普通品质的隐式变体；柴薪和瓶不接收显式词条，打造仍只接受装备。

三个普通怪物掉落表各追加八项，单项权重 12、数量 1；守卫 Boss 复用其既有掉落表。商人候选池追加八项、权重各 100，仍按原 16 件随机库存生成。四类都有渠道，单批库存不保证四类齐全。八张独立图标使用既有 Addressables 物品组，见[图标合同](item-icons.md)。

## 所有权与提交

`SkillBoardModel` 持有当前 Profile PlayerId、不可变棋盘状态和 Revision。`SkillBoardSystem` 负责候选最终态；Controller 用 `PreviewSkillBoardQuery` 和 `CommitSkillBoardCommand`。预览捕获 System / Session 身份、状态和背包引用、位置、物品 Revision、品质与耐久；任何源变化使旧预览失效，已消费预览不能重入。

背包、棋盘柴薪、嵌入插槽与专栏之间转移同一 ItemInstance。先验证最终几何、嵌入、连根与最终背包空间，再整体替换背包及棋盘，最后发送 InventoryChanged / SkillBoardChanged；失败不发成功事件、不部分转移。销毁 Session 与停止接收场景工作后拒绝操作。

大柴薪只能安装在六根；中小必须经合法裂缝连根。棋盘无固定外边界，完整主体与未连接外伸格参与碰撞。裂缝双向直连、每端口一条连接；候选需显式选择。只有大柴薪中心接受火焰，中柴薪中心留白；余烬只接受未连接裂缝，连接前必须先取出。

拆卸先手动逐件取出火焰 / 余烬，再单件断连并取回。当前件的连接与返回背包是一次事务；其余连接保留。拆后所有剩余柴薪须至少连一个剩余根，允许成环和跨根，最后一件可取回。背包满、恢复外伸碰撞或拆桥导致断根均失败。安装件需先取回背包才能出售；出售沿现有规则发交易事件，不提供回购库存。

取回火焰在正式提交时同步清空相应技能槽；火焰冷却随道具实例保留，背包移动、重新安装和交易不能刷新冷却。棋盘 / 成长预检准备最终全局效果，失败不清绑定或部分替换效果。

## 保存与身份

棋盘所有权使用 PlayerController.Id，战斗来源校验使用 CombatActor.ActorId / CombatIdentity；两者不能混作同一身份。所有实际物品仅在统一 Items 表序列化一次，Profile.SkillBoard 保存坐标、旋转、连接、嵌入与专栏引用。Mapper、Validator 和 Restore Preparer 同时拒绝重复所有者、未知内容、角色 / 插槽错误、断根、碰撞、损坏布局与坐标溢出。

阶段二 core v11 / Save Schema 10 接入追加内容兼容与 9→10 迁移；阶段三 core v12 / Save Schema 11 另保存 Profile.Growth，点亮 / 取回与成长同一次提交。旧档初始化空棋盘 / 专栏、新物品扩展为空，保留原技能和冷却，不赠送物品。恢复只读实际结果，切图保留角色资产；见[本地保存](../infrastructure/local-save.md)。

## 开发验证入口

运行有效游戏 Session 后打开 `DarkFlare/Debug/柴薪安装与能量瓶`。选择基底 / 种子生成到背包；填写实例 ID、操作、锚点和 0–5 旋转，先预检再提交。火焰中心节点填写 `center`，余烬填写右侧列出的 Crack 节点 ID。连接可列出候选，再明确采用并重新预检；取消预览不改资产。

右侧显示玩家、实例 ID、锚点、节点、配对、嵌入、Revision 和能量。此入口复用正式 Command，不代表正式玩家界面交付。长期回归复用实例 / 保存套件，独立 `SkillBoardTransactionsTests` 保护安装、失败回退、所有权和网络规则。
