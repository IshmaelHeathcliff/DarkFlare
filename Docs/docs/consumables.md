# 正式消耗品与使用交互

2026-09-15：阶段 2 已完成，EditMode 563/563、PlayMode 74/74 全部通过。使用核心见[统一使用系统](./item-use-system.md)。综合验收后运行版本为 `0.4.2-alpha`；正式目录增加到 core v5，存档结构保持 Schema 3。

## 正式内容

| ID | 名称 | 每次效果 | 基础单价 |
| --- | --- | --- | ---: |
| health_potion | 生命药水 | 恢复 40 生命 | 10 |
| mana_potion | 法力药水 | 恢复 60 法力 | 10 |
| fortitude_potion | 坚韧药剂 | 获得 30 秒坚韧：最大生命 Increase +20% | 20 |
| cleansing_potion | 净化药剂 | 先恢复 20 生命，再驱散全部可驱散异常，最多 4096 层 | 20 |

四类均普通品质、1×1、堆叠上限 10，每次消耗 1 件。坚韧为统一效果、上限 1 层、覆盖刷新、始终显示；最大生命变化沿用生命比例规则，药剂不是额外隐藏治疗。末件耗尽不撤销状态。所有效果均无变化时不消费；眩晕不封锁 UseItem，因此允许净化。

配置位于 `Assets/Data/Preset/Items/Consumables`、`Assets/Data/Preset/ItemUses` 和 `Assets/Data/Preset/Statuses/坚韧.asset`。四类药水进入商人随机候选池，每次抽中生成数量 1 的独立商品；购买沿用合堆与价格倍率，出售仍按整堆。旧档原样恢复商人库存，不补发新商品。core v4 → v5 只更新副本的内容版本，既有 v1–v3 迁移链继续适用，不重复治疗、扣除或重置状态。

怪物掉落池不增加药水；装备权重 100、金币 2000、矿石 500 保持不变。

## 操作与详情

`ItemActionRouter` 的 Primary / Validate / Execute 共用 Use 动作与核心 Command / Query。普通背包右键快捷使用；Enter / 手柄南键打开动作菜单，再确认“使用”。商店玩家物品右键默认出售，操作菜单额外提供使用；商人商品只可购买。打造仅接受装备，背包药水仍能从操作菜单使用。单击选择、拖放和手柄拿起放置沿原合同。

详情通过 ItemDetailSnapshot / Formatter / View 显示配置顺序、用量、状态说明；玩家拥有的物品额外查询使用后的生命、法力和消费数。商人物品仅显示静态效果。药水隐藏无意义的空前后缀区。失败原因使用本地化条目，满资源、无异常、层数上限、状态冲突、行动封锁和失效来源可区分。

预览请求按当前实例缓存，打开操作菜单重新建立；成功或失败后清除，背包变化与窗口挂起也清除。刷新详情不会每帧创建新操作号。过期失败不自动重试；通知异常后重新查询真实状态。余量保留原实例和选择；末件删除后关闭菜单、清除浮窗，并恢复到邻近物品或窗口关闭入口。Shell 接管和 Session 结束沿既有订阅清理及核心所有权检查。

## 视觉与验证

四个独立透明药水图标和一个坚韧图标均为 96×96、Sprite Single、64 PPU、Point、无压缩、中心 Pivot，已登记到 Session Sprite Addressables 分组。四药水有效图形宽 60、高 76–77 像素，视觉中心相差不足 0.1 像素；生成、导出和验收记录见[资产合同](./assets/acceptance/alpha-0.4-consumables-ui/asset-contract.md)。

持续回归复用 `Phase1UxPlayModeTests` 的真实设备事件、`SaveCaptureRestorePlayModeTests` 的真实 Session 保存恢复和 `SaveRestorePreparerTests` 的历史内容兼容，不保留截图采集代码。实际测试、截图与范围说明见[阶段 2 验收](./assets/acceptance/alpha-0.4-consumables-ui/README.md)。

2026-09-15：独立 Windows Player 的使用、自然到期、输入、显示、跨进程恢复及历史存档验收完成；以 `0.4.2-alpha` 交付。全量回归 563/563、74/74 通过，见[综合验收](./assets/acceptance/alpha-0.4-item-use-acceptance/README.md)。
