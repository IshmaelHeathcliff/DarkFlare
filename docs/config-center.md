# 配置中心

## 入口

Unity 菜单：

```text
DarkFlare/配置中心
```

配置中心基于 Odin `OdinMenuEditorWindow` 实现，用于集中浏览、创建和编辑项目配置。

## 类型筛选

配置中心只显示满足以下条件的类型：

- 继承 `ScriptableObject`
- 非抽象类型
- 标记了 `CreateAssetMenu`
- `menuName` 以 `DarkFlare/Data/` 开头

这样可以避免第三方插件、TextMesh Pro 示例资产或 Unity 内置 ScriptableObject 混入配置中心。

## 菜单结构

```text
配置概览
按类型/
  Tag Definition/
    已有标签资产
  Stat Definition/
    已有属性资产
  Affix Definition/
    已有词条资产
  Item Base Definition/
    已有物品基底资产
```

类型页提供：

- 当前类型信息
- 默认创建目录
- 已有资产列表
- 新建配置按钮
- 每个资产条目支持打开编辑、定位和重命名

资产节点直接挂载真实 `ScriptableObject` 资源，因此可以在配置中心内直接编辑资产字段。类型页的资产条目不显示配置详情，但可以通过打开编辑切换到对应资产节点。

## 编辑与重命名

在类型页的已有资产列表中，每个资产条目包含：

- 名称
- 路径
- 新名称输入框
- 打开编辑按钮
- 定位按钮
- 应用重命名按钮

打开编辑会切换到左侧菜单中的具体资产节点。重命名会通过 `AssetDatabase.RenameAsset` 修改资产文件名，并刷新配置中心菜单树。

## 创建路径

创建新配置时：

- 如果该类型已有资产，默认使用第一个已有资产所在目录
- 如果该类型没有资产，则根据 `CreateAssetMenu.menuName` 回退到 `Assets/Data/Preset/{分类}`

例如：

- `DarkFlare/Data/Tags/Tag Definition` -> `Assets/Data/Preset/Tags`
- `DarkFlare/Data/Stats/Stat Definition` -> `Assets/Data/Preset/Stats`
- `DarkFlare/Data/Affixes/Affix Definition` -> `Assets/Data/Preset/Affixes`
- `DarkFlare/Data/Items/Item Base Definition` -> `Assets/Data/Preset/Items`
- `DarkFlare/Data/Actors/Character Definition` -> `Assets/Data/Preset/Actors`
- `DarkFlare/Data/Skills/Projectile Skill Definition` -> `Assets/Data/Preset/Skills`
- `DarkFlare/Data/Monsters/Monster Definition` -> `Assets/Data/Preset/Monsters`
- `DarkFlare/Data/Monsters/Monster Spawn Definition` -> `Assets/Data/Preset/Monsters`

## 扩展约定

后续新增配置类型时，优先使用：

```csharp
[CreateAssetMenu(menuName = "DarkFlare/Data/分类/类型名", fileName = "类型名")]
```

只要满足该规则，新类型会自动出现在配置中心。
