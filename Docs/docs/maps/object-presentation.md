# 地图对象表现与分段遮挡

当前 `0.5.5-alpha` 的正式 Player 已检查树木、帐篷及四段长障碍与角色的前后遮挡，见[当前截图与排序数据](../plan/archive/0.5.5/acceptance/maps/i/workflow.md)。

本模块消费地图文档 V2（兼容 V1 导入）的 ObjectId、DefinitionId、Position、Rotation 和 AppearanceVariant，不修改逻辑地表、footprint、区域或交互绑定。地图工作台和运行时共用表现目录、Prefab 校验及装配器；正式双地图与领域交互已由 0.5.3 接入，见[双地图生命周期](./world-map-lifecycle.md)。

## 配置与样例

独立“补齐对象表现样例”菜单已移除；打开世界排序样例时仍调用 `MapObjectPresentationSetup.Ensure()` 补齐资源。

- 创建菜单：`DarkFlare/Maps/Object Presentation Catalog`；配置中心“按类型 / Map Object Presentation Catalog”可编辑现有目录。表现配置不分配新的 ContentId，不属于 `DarkFlare/Data/` 内容身份目录。
- 正式样例目录：`Assets/Data/Maps/Presentation/map_object_presentations.asset`。外观以对象定义 + 变体 ID 精确匹配；空 ID 对应默认，未知 ID 拒绝，不能静默取默认。
- 样例 Prefab：`Assets/Prefabs/World/MapObjects/`，登记于 `DarkFlare-Map-Objects` Addressables 组。复用既有独立 Sprite；没有新增图片、改变原图 PPU / pivot 或修改碰撞尺寸。
- 菜单 `DarkFlare/地图/打开世界排序样例` 打开 `world_sorting.dfmap`。样例含树、岩石、帐篷及 0/90/180/270 度的分段石障；E 的城镇 / 野外草稿也可预览树木、商人和打造台。

字段合同见[地图配置参考](../config-reference/maps.md#mapobjectpresentationcatalog)。内容校验复用 ContentConfigurationValidator，并扫描表现目录和资源结构；纯布局保存不要求必须具备正式表现资源。

## 排序单元

每个可独立遮挡的单元带一个 `MapObjectVisualPart`、一个显式注册的 `WorldSortParticipant` 和一个根 `SortingGroup`。PartId 必须稳定且在该 Prefab 内唯一；SortAnchor 必须显式属于该单元。一个 Renderer 只归属一个单元，禁止嵌套 Group。纯表现资源不允许 Collider、Rigidbody、玩法 MonoBehaviour 或丢失的脚本；使用有效 Sprite 和材质。

多段对象的逻辑根不带 Group，子段互为兄弟。长石障由四块独立石头组成，不是重复绘制整张建筑。各段沿用原始 Sprite 画布，视觉中心相对接地点偏移；对象旋转同时作用于各段和锚点。全部分段仍对应一条源对象记录，碰撞只由 [D 几何投影](./map-geometry.md)生成一次。

静态表现身份由带长度分隔的 MapId / ObjectId / PartId 组成，不依赖资源加载顺序、数组下标或 Unity 对象编号。世界位置为地图 Origin + 对象局部世界坐标；对象 Position 不再次乘 CellSize。排序接地点来自旋转后的世界锚点，沿用 1/64 单位 Y 量化。

## 装配与资源所有权

`MapObjectPresentationBatch.PrepareAsync` 创建不可见候选：先校验目录、取得 Prefab lease、检查纯表现结构，再创建对象与身份。同批相同 Prefab 只申请一次；候选没有活动排序登记。运行时使用现有 `AddressableAssetService` 和独立 `AssetOwnerScope`；编辑器使用同一个入口，通过 AssetDatabase 提供临时 Prefab lease。

`Activate(sorting)` 再次检查取消及 owner 状态，预检全部身份和 Order 容量后统一注册、显示和排序。重复激活幂等；失败不留下半批登记。调用者在替换地图时仍需复核自己持有的 generation；此模块不取代 Scene Flow 或提供完整切图事务。

`DisposeAsync` 幂等并允许重复等待，先注销和隐藏对象，再销毁实例，最后释放 Prefab lease 和 owner。Player 必须等待帧末销毁完成；这段清理不可取消，避免取消流程让资源早于实例释放。准备期间异常、取消、空资源与部分加载失败均走相同清理路径。

使用示意：

```csharp
MapObjectPresentationBatch candidate = await MapObjectPresentationBatch.PrepareAsync(
    map, catalog, host.Resources, scope.Token);
try
{
    scope.Token.ThrowIfCancellationRequested();
    candidate.MoveToScene(scene);
    candidate.Activate(sorting);
    // 所有权转交给当前地图的表现持有者。
}
catch
{
    await candidate.DisposeAsync();
    throw;
}
```

## 工作台

“对象与区域”页可选择表现目录、切换正式对象 / 诊断轮廓。窗口拥有独立排序上下文及构建 generation；旧异步结果不能替换新图，预览不访问游戏架构。对象移动、旋转、外观、AI Patch、Undo / Redo 共享同一个源记录。

对象或区域独立变化时保留当前地表帧；地表、地图元数据及依赖变化才重新编译地表。对象重建先准备候选，成功后替换旧帧；失败明确标记旧画面过期。关闭、换舞台、进入 Play 和域重载会释放临时对象与引用。源选择和编辑继续依据 ObjectId 与 footprint，不直接修改派生 Transform。

商人 / 打造台样例是纯表现结构；没有复制交易系统、触发器或领域状态。其真实交互绑定由后续地图运行作用域消费。

## 验证

长期测试覆盖准备不注册、重复激活 / 释放、容量失败、取消后激活拒绝、部分加载失败、资源回收、坏 Prefab、默认 / 显式变体，以及工作台 AI 修改、Undo / Redo 与地表帧复用。阶段性能和独立 Player 的可视证据归档在[0.5.2 验收](../plan/archive/0.5.2/acceptance/maps/f/README.md)，不以读取历史验收文件代替当前测试。
