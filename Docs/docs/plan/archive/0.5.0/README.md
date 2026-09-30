# 0.5.0 计划与验收归档

按所属交付系列归档。阶段记录中的实际运行版本、环境、验收结果及未执行项保留当时含义。

## 计划

- [Alpha 0.5.0：首轮数据与导入合同](plans/alpha-0.5.0-map-foundation-contract.md)
- [Alpha 0.5.0：地图数据与地表基础](plans/alpha-0.5.0-map-foundation-plan.md)
- [Alpha 0.5.0 D：同源碰撞与静态导航几何](plans/alpha-0.5.0-map-geometry-plan.md)
- [Alpha 0.5.0 C：正式地表资产](plans/alpha-0.5.0-terrain-art-plan.md)
- [Alpha 0.5.0 B：Dual Grid / Wang 解析与预览](plans/alpha-0.5.0-wang-preview-plan.md)

## 验收

- [B4 地表样例与压力验收](acceptance/maps/b4/README.md)
- [泥土 Pilot 生成记录](acceptance/maps/c1/generation.md)
- [C1 地表尺度与泥土源 Pilot](acceptance/maps/c1/README.md)
- [C2 正式纹理编译验收](acceptance/maps/c2/README.md)
- [C3 四材质双变体与资源发布验收](acceptance/maps/c3/README.md)
- [一次性采集故障记录](acceptance/maps/c4/collection-incident.md)
- [C4 正式地表样例与压力验收](acceptance/maps/c4/README.md)
- [D：同源碰撞与静态几何验收](acceptance/maps/d/README.md)
- [原始结果、截图与日志](acceptance/)

## 历史完成记录

- [Alpha 0.5.0 地图数据与地表基础](plans/alpha-0.5.0-map-foundation-plan.md)：2026-09-18 完成 A–D，数据 / 地表 / 碰撞 / 静态通行完整闭环；版本 `0.5.0-alpha`，下一阶段地图编辑器。
- [Alpha 0.5.0 同源碰撞与静态导航几何](plans/alpha-0.5.0-map-geometry-plan.md)：2026-09-18 完成 D1–D4，EditMode 676/676、PlayMode 77/77；窄路保守拒绝与极端地图容量边界见验收。
- [Alpha 0.5.0 正式地表资产](plans/alpha-0.5.0-terrain-art-plan.md)：2026-09-18 完成 C1–C4，四材质双变体、完整 Wang 编译、资源发布、正式样例与压力验收；EditMode 662/662、PlayMode 76/76。下一步 D 同源几何。
- [Alpha 0.5.0 Wang 解析与预览](plans/alpha-0.5.0-wang-preview-plan.md)：2026-09-18 完成切片 B，确定性四角规则、分区与增量解析、隔离预览、持久样例及 256×256 压力验收；项目 EditMode 650/650、PlayMode 75/75。下一步为 C 正式地表家族合同与 Pilot。
- [Alpha 0.5.0 数据与导入合同](plans/alpha-0.5.0-map-foundation-contract.md)：2026-09-18 完成切片 A，`.dfmap`、规范校验 / 导出、只读导入与配置中心入口；EditMode 603/603。0.5.0 的 Wang 预览、正式地表和几何仍待完成。

[返回归档索引](../README.md)
