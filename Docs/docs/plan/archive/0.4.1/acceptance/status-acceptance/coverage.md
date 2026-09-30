# 状态综合验收覆盖核对

2026-09-14，基线 `a418188`；本表记录现有长期覆盖与本阶段补充范围，执行结果另见同目录验收记录。

| 合同 | 现有覆盖与代码依据 | 本阶段补充 |
| --- | --- | --- |
| 叠层、刷新、择强、到期、消耗、驱散 | StatusSystemTests；StatusMutationResolver、StatusClock | 独立 Player 消耗、自然到期、驱散 |
| 准备 / 提交 / 发布 / 回退 | StatusStore.Prepare / Commit，StatusCommit.Publish / Rollback；DeferredCommit_RollbackAndStaleResourcesNeverLeakEffects | 核对供物品组合的接口，无需重复实现 |
| 装备与状态共存 | SourcesAndResources_CommitOnceAndSurviveOtherSourceRemoval、EquipmentStatus_FailedReplacementPreservesLoadoutInventoryAndLayers | 保持原来源规则，不启用未配置的常规战斗样例 |
| 七异常与抗性 | AilmentResistance_ScalesOnlyItsEffectAndFreezes、AilmentResistance_ClampsAtApplicationBoundary | Player 控制与周期伤害可见链路 |
| 周期伤害与来源归属 | PeriodicPhysical_SkipsArmorHitRandomAndImpact、SourceDeath_ReleasesAuraButPreservesFrozenTimedDamageAndAttribution | Player 实际生命减少 |
| 暂停、取消、死亡、禁用 | StatusCombatPlayModeTests；DeadPlayer_RestoresWithoutStatusProjectionAndRebindsOnRevive | Player 暂停 / 恢复与新 Session 隔离 |
| 存档、相位与旧档 | SaveRestore_PreservesIndependentPhasesAndNewGeneration、SaveRestore_MultipleRoundTripsPreservePhaseExactly、SaveCaptureRestorePlayModeTests；真实 Schema 2 夹具 | Player 跨进程继续与前台往返 |
| 三档、层数、时间、焦点 | Visibility_OnlyOptionalStatusesRespondToHideSwitch、StatusVisibility_FiltersHudAndMenuWithoutRemovingGameplayLayers、StatusDetails_GamepadScrollBackAndDispelKeepFocusReachable | 最终 Player HUD / 窗口、多语言和小分辨率 |
| 图标与资源 | 八图标既有逐图审计；Session SpriteAssetLoader 与 StatusPanelController 作用域释放 | Player Addressables 构建与实际图标加载 |

状态查询在未发布事务期间读取提交前数据；Publish 验证资源版本，Rollback 不通过反向施加补偿。后续物品系统仍需组织背包与资源的完整复合事务，不能把当前状态接口等同于物品功能已实现。
