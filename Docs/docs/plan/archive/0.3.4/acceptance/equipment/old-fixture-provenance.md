# 四槽旧档夹具来源

`Assets/Scripts/Tests/Fixtures/Migration/save-v1-core-v1-four-slots.json` 于 2026-09-10 在隔离 PlayMode Session 中采集，运行版本 `0.3.3-alpha`，正式内容目录仍为 `core` v1。采集时新槽位登记已追加，但正式基底、掉落、商店及词条尚未扩充；只使用已发布的 Weapon / Armor / RingLeft / RingRight 四个稳定槽位。

通过真实 Main 初始化、购买、打造和装备命令形成四槽状态，再使用 `SessionSnapshotSource` 和正式序列化器生成文件。包含 8 个实际物品实例、四槽穿戴、四件剩余商品、已掷词条、角色资源、随机通道和 Session 身份。时间和版本来自采集环境，不冒充早期历史运行记录，也不使用玩家正式存档。

采集测试 1/1 通过，见 `old-fixture-capture-results.xml`；临时采集代码及 meta 已删除。此 JSON 是长期兼容性输入，后续测试不得重新生成或重掷其内容。`SaveRestorePreparerTests` 验证四槽身份及完整 Payload 精确保留、追加槽为空、未来版本拒绝和失败不改源 DTO；既有 v0 结构迁移夹具继续保留。
