# 打造系统

## 模块职责

打造系统负责对玩家背包中的物品修改词缀，并在操作实际生效后扣除金币、发送领域事件。当前首版没有配方或材料 Model，配置由 `CraftingDefinition` 提供。

主要入口：

- `CraftingOperations`：四种纯逻辑词缀操作。
- `CraftingSystem`：配置、背包归属、金币成本和事务提交。
- `CraftItemCommand`：Controller 使用的写入入口。
- `GetCraftingCostQuery`：单项成本查询。
- `GetCraftingSnapshotQuery`：打造 UI 的完整只读快照。
- `CraftingPanelController`：打造页选择、Command 和事件刷新。

## 四种操作

| 操作 | 当前行为 | 默认成本 |
| --- | --- | ---: |
| 添加词缀 | 从可用词条池按权重选择一条仍有容量的词缀 | 20 |
| 重随全部 | 保持原前缀 / 后缀数量与类型，重新生成全部词缀 | 40 |
| 移除并重随 | 替换选中词缀，替换结果保持相同词缀类型 | 30 |
| 提升数值 | 重掷选中词缀，只有修改器数值总和严格增加才提交 | 25 |

成本来自 `Assets/Data/Preset/Crafting/基础打造配置.asset`，表中为当前资源值。

## 事务规则

`CraftingSystem.Craft` 按以下顺序处理：

1. 检查配置、物品引用及物品是否位于 `InventoryModel.Grid.Placements`。
2. 检查玩家金币是否足够。
3. 调用对应纯逻辑操作。
4. 只有操作成功才扣金币并发送 `ItemCraftedEvent`。

重随全部会先保存原词缀；任一前缀或后缀无法重建时恢复全部原词缀。移除重随无法生成同类型替代词缀时恢复目标词缀。提升数值未严格变大时直接返回失败，不替换词缀、不扣费。

## UI 数据与刷新

`GetCraftingSnapshotQuery` 返回：

- 配置是否就绪、当前金币和四项成本；
- 按背包格子顺序排列的物品；
- 名称、类型、稀有度、前后缀容量、物品价值和出售价；
- 每条词缀的类型、名称、修改器摘要与修改器数值总和。

`CraftingPanelController` 实现 `IController`，只发送 `CraftItemCommand`，并订阅 `InventoryChangedEvent`、`GoldChangedEvent`、`ItemCraftedEvent` 重新查询快照。它不直接读取或修改 Model。

## 价值联动边界

当前 `ItemValueCalculator` 只按基础价值、稀有度和词缀数量计算价值。因此添加词缀会改变价值与出售价；只改变词缀类型或数值的重随、移除重随和提升数值通常不会改变交易价值。词缀 tier 或具体数值进入价值公式留给后续经济系统迭代。

## 当前限制

- 词条池目前只有“基础伤害增加”前缀，适合验证机制，不代表最终内容量。
- 只允许打造背包内物品，不处理已装备物品。
- 没有配方、材料物品、锁定词缀、撤销或批量打造。
- 打造页位于共享营地菜单；世界中的打造台与 `Interact` 入口留到 8f。
