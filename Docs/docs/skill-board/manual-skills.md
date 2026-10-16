# 武器普通技能与六火焰槽

0.7.1-alpha.4 已接入手动技能、冷却、棋盘效果及保存。正式玩家棋盘和分配界面留待阶段五；当前可用 Odin 开发窗口分配火焰。范围与来源规则见[棋盘效果](effects.md)，当次验证见[阶段四验收](../plan/archive/0.7.1/acceptance/manual-skills/README.md)。

## 来源与分配

当前装备武器的 `ItemBaseDefinition.NormalSkill` 提供普通技能，使用独立 Attack，不需要柴薪、火焰或成长点。新角色沿用既有起始武器，六槽初始为空；卸下武器后普通技能不可用。场景 PlayerSkill 和旧 Configure 参数保留兼容 / 预热入口，不再决定玩家能力。

六个槽只保存已安装在大柴薪中心、且所属柴薪已点亮的火焰实例 ID。同一火焰再次分配会原子移槽，旧槽清空；同定义的不同火焰可独立分配。取回火焰时，在棋盘提交的最终态清除对应绑定，预览或失败不清槽。

| 操作 | 键鼠默认 | 手柄默认 |
| --- | --- | --- |
| 武器普通技能 | 鼠标左键 | 西键 |
| 火焰槽 1 / 2 | 1 / 2 | 东键 / 南键 |
| 火焰槽 3 / 4 | 3 / 4 | 左肩键 / 右肩键 |
| 火焰槽 5 / 6 | 5 / 6 | 左扳机 / 右扳机 |

七个入口均可在设置中重绑定，单次按下只请求一次；冷却结束、遇敌、进图、复活和读档不会自动施法。鼠标按指针方向，手柄按右摇杆方向；方向为空沿用本次角色上次有效方向，初始向右。移动和自动目标查询不覆盖瞄准。Glyph 偏好只决定显示。

## 运行态与提交

`PlayerSkillsModel` 保存六槽和普通技能通道冷却；`PlayerSkillStateRegistry` 验证来源、分配和施法。普通技能冷却在换装 / 卸下时继续保留；火焰冷却放在该道具的 `SkillBoardItemState`，换槽、取回、交易后仍随实例保留。两者使用玩法时间，暂停不推进。

Controller 捕获来源 ID、槽位、瞄准和输入世代，在帧末提交，允许 UI 消费同次点击。命令复核当前玩家 / Session、存活、输入、暂停、来源、冷却、法力及合法伤害来源；HUD 按钮点击不会同时普攻。UI、挂起、死亡、设备断开或 Session 退出使待处理请求失效，旧按住状态须释放再按。

沿用 `FireProjectileCommand` 和预热的 Addressables。生成成功后才扣法力、启动冷却并发出一次攻击通知；空槽、来源过期、无蓝或生成失败不产生这些变更。攻击种子沿用现有边界，生成失败不承诺已取得的种子回退。`PlayerSkillCastEvent` 携带冻结快照，供调试 / 行为测试观察。

Controller 通过 `AssignPlayerSkillCommand`、`CastPlayerSkillCommand` 与只读 `GetPlayerSkillsQuery` / `GetPlayerNormalSkillQuery` 接入。兼容读取器和底层 synthetic Actor 测试入口不参与正式 Player 的能力决策。技能 Prefab 在 NewGame / Restore 中按内容目录预热，取消和资源归属沿用 Session。

## 内容与持久化

正式大剑对应 `skill:great_sword_normal`（间隔 0.6 秒、耗蓝 1、速度 12、半径 0.16）；战斧对应 `skill:war_axe_normal`（0.85 秒、耗蓝 2、速度 9、半径 0.24）。两者使用已有投射物 Prefab 和装备武器伤害来源，数值为首轮可调参数，尚未扩展独立近战动作表现。火焰沿用实际实例保存的技能与词条。

core v13 登记这两个技能；Save Schema 12 保存 `Run.Player.Skills.FlameSlots`、普通技能剩余冷却及物品扩展中的火焰剩余冷却。11→12 将已发布 `skill:basic_projectile` 的旧等待时间迁入普通技能，按当前武器配置取能力，六槽为空；不补物品或点数。无武器时保留不可用状态及等待时间，重新装备后可用。未知旧技能缺少映射时明确拒绝。

恢复预检校验六槽长度、唯一激活火焰、所有权、合法冷却与内容。Detached 火焰在预热期间冻结保存的剩余时间，最终提交才启动时钟；恢复成长 / 全局效果后再恢复绝对资源。当前 Schema 缺少技能字段会失败，不按历史档补默认。Settings Schema 1、地图 V4 保持。

## 查看与验证

运行 Session，打开 `DarkFlare/Debug/柴薪安装与能量瓶`。安装并点亮大柴薪、嵌入火焰后，填写“道具实例 ID”和“火焰技能槽（1–6）”，点击分配；清槽使用相应按钮。右侧只读快照显示分配、冷却和局部来源，所有变更调用正式 Command。

HUD 显示独立普通技能和六槽的实际名称、火焰图标、绑定、耗蓝、状态及冷却。进入窗口 UI 时隐藏火焰条，保留资源区；属性详情分别解释每枚可用火焰的局部来源。长期回归集中在 `PlayerSkillsTests`、`ApplicationInputServiceTests`、保存测试和现有 PlayMode UI / 资源套件；临时截图采集只作为归档材料。
