# 测试逐文件审查清单

本清单记录 2026-09-07 清理前 97 个 C# 文件的维护判断，包含 90 个测试文件和 7 个共享辅助文件。具体删除依据及运行结果见[审计报告](./test-suite-audit.md)。这是本次审计快照，不新增一个要求永久锁定测试数量的自动测试。

## EditMode

### 2026-09-10 十槽装备增量审查

继续使用已有装备、内容、存档和输入套件。装备快照与正式内容扫描改为遍历登记集合；删除固定四槽数量及戒指槽必须正方形的过时假设，保留最小命中范围、空间邻居与资源比例合同。新增两项存档行为回归，覆盖十槽唯一归属和 core v1 → v2 兼容；真实四槽历史夹具长期保留，旧标签候选真值表继续冻结原有基底。

商店压力场景并入既有购买测试，覆盖末行滚动、焦点恢复和跨窗导航。资产家族已有的 72–80 px 可见长轴合同继续保留，尺寸不合格的新图重新生成。截图采集、内容登记和独立 Player 验收入口为当次工具，完成后删除，不增加版本专用永久测试套件。

以下逐文件表仍为 2026-09-07 的历史审计快照。

| 文件 | 决定 | 长期覆盖 / 判断 |
| --- | --- | --- |
| [AbandonedGenerationIsolationTests.cs](../../../Assets/Scripts/Tests/EditMode/AbandonedGenerationIsolationTests.cs) | 保留 | 异常清理、迟到回调与代际隔离，不能用普通成功路径替代 |
| [AccessibilityAndPlatformLifecycleTests.cs](../../../Assets/Scripts/Tests/EditMode/AccessibilityAndPlatformLifecycleTests.cs) | 保留 | 真实服务消费、重叠挂起、提交失败回退与播放句柄生命周期 |
| [ActorAnimationStateTests.cs](../../../Assets/Scripts/Tests/EditMode/ActorAnimationStateTests.cs) | 保留 | 动画状态可达、攻击 / 受击 / 死亡 / 复活及朝向 |
| [Alpha012DamageResolutionTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha012DamageResolutionTests.cs) | 保留 | 命中、暴击、防御、伤害来源、随机种子不误消费及初始武器原子性 |
| [Alpha013CollisionSafetyTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha013CollisionSafetyTests.cs) | 保留 | 物理碰撞与世界障碍、接触攻击计时及移动配置 |
| [Alpha014ResourceSystemTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha014ResourceSystemTests.cs) | 保留 | 生命 / 法力比例、恢复、耗蓝事务及真实 HUD |
| [Alpha015AffixCoverageTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha015AffixCoverageTests.cs) | 精简后保留 | 保留可消费属性覆盖和确定性；相同全量 Scan 合并到 Phase4Content |
| [Alpha015MonsterAffixContentTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha015MonsterAffixContentTests.cs) | 精简后保留 | 保留正式词条效果、候选与可观察消费；相同全量 Scan 合并到 Phase4Content |
| [Alpha015MonsterAffixTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha015MonsterAffixTests.cs) | 保留 | 互斥、权重、实例效果与世界标记生命周期 |
| [Alpha015PrimaryAttributeTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha015PrimaryAttributeTests.cs) | 保留 | 主属性派生、聚合次序和反复换装不叠加 / 不恢复套利 |
| [Alpha017BaselineTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha017BaselineTests.cs) | 保留 | 现有特效资产家族与配置 / 战斗事件合同；审查后保留，不批量解除资产保护 |
| [Alpha017EffectRuntimeTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha017EffectRuntimeTests.cs) | 保留 | 命中表现语义、特效依赖、池预热 / 复用 / 释放 |
| [Alpha017WorldSortingTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha017WorldSortingTests.cs) | 保留 | 排序稳定性、移动 / 销毁 / 身份复用与正式世界层级 |
| [Alpha01TagMigrationCharacterizationTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha01TagMigrationCharacterizationTests.cs) | 保留 | 标签真值表、作用域隔离、转换 / 额外伤害血统与快照深拷贝仍是长期规则 |
| [Alpha024GameFlowContractTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha024GameFlowContractTests.cs) | 保留 | 场景状态语义、暂停所有者、场景请求仲裁与失败补偿 |
| [Alpha024SceneFlowFoundationTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha024SceneFlowFoundationTests.cs) | 保留 | 场景状态语义、暂停所有者、场景请求仲裁与失败补偿 |
| [Alpha025FoundationContractTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha025FoundationContractTests.cs) | 保留 | 输入 / 音频 / 平台值对象的合法性和独立标志；不按版本前缀删除 |
| [Alpha025ProductionAssetTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha025ProductionAssetTests.cs) | 保留 | 正式音频、设置控件、翻译与 Glyph 资源绑定；当前消费者仍存在 |
| [Alpha026AddressablesGovernanceTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha026AddressablesGovernanceTests.cs) | 保留 | 正式 Addressables 分组与错误消息配置治理 |
| [Alpha026DiagnosticsTests.cs](../../../Assets/Scripts/Tests/EditMode/Alpha026DiagnosticsTests.cs) | 保留 | 日志失效隔离、首故障与资源并发加载 / 取消 / 释放 |
| `Alpha027AcceptanceContractTests.cs` | 移除 | 历史发布核验或旧工作台源码锁定；对照见审计报告 |
| `Alpha027ReleaseAcceptanceTests.cs` | 移除 | 历史发布核验或旧工作台源码锁定；对照见审计报告 |
| [ApplicationDataPathProviderFactoryTests.cs](../../../Assets/Scripts/Tests/EditMode/ApplicationDataPathProviderFactoryTests.cs) | 保留 | 玩家数据路径隔离与测试运行清理，长期必须保留 |
| [ApplicationInputServiceTests.cs](../../../Assets/Scripts/Tests/EditMode/ApplicationInputServiceTests.cs) | 保留 | 唯一输入资产、重绑定 / 设备与 UI Context；保留实际入口与运行时隔离 |
| [ApplicationLifecycleTests.cs](../../../Assets/Scripts/Tests/EditMode/ApplicationLifecycleTests.cs) | 保留 | 作用域生命周期、取消 / 超时、真实场景启动与隔离夹具 |
| [ArchitectureExceptionSafetyTests.cs](../../../Assets/Scripts/Tests/EditMode/ArchitectureExceptionSafetyTests.cs) | 保留 | 异常清理、迟到回调与代际隔离，不能用普通成功路径替代 |
| [AudioServiceTests.cs](../../../Assets/Scripts/Tests/EditMode/AudioServiceTests.cs) | 保留 | 真实服务消费、重叠挂起、提交失败回退与播放句柄生命周期 |
| [ConfigurationDocumentationCoverageTests.cs](../../../Assets/Scripts/Tests/EditMode/ConfigurationDocumentationCoverageTests.cs) | 精简后保留 | 保留扫描完整性及负例；独立 Exists 并入既有 ScanOfficial 成功检查 |
| [ContentIdentityTests.cs](../../../Assets/Scripts/Tests/EditMode/ContentIdentityTests.cs) | 保留 | 稳定身份、配置登记、封闭对象图和错误归属拒绝 |
| [CraftingOperationsTests.cs](../../../Assets/Scripts/Tests/EditMode/CraftingOperationsTests.cs) | 保留 | 稀有度、容量、作用域、随机重放和失败原子性 |
| [DamageCalculatorTests.cs](../../../Assets/Scripts/Tests/EditMode/DamageCalculatorTests.cs) | 保留 | 命中、暴击、防御、伤害来源、随机种子不误消费及初始武器原子性 |
| [EquipmentPhase2Tests.cs](../../../Assets/Scripts/Tests/EditMode/EquipmentPhase2Tests.cs) | 保留 | 穿脱 / 替换、全量聚合、真实焦点、在途攻击快照；PlayMode 缩减分辨率参数 |
| [GameArchitectureTestFixture.cs](../../../Assets/Scripts/Tests/EditMode/GameArchitectureTestFixture.cs) | 保留辅助 | 作用域生命周期、取消 / 超时、真实场景启动与隔离夹具 |
| [GameInputTests.cs](../../../Assets/Scripts/Tests/EditMode/GameInputTests.cs) | 保留 | 唯一输入资产、重绑定 / 设备与 UI Context；保留实际入口与运行时隔离 |
| [GameplayUiFoundationTests.cs](../../../Assets/Scripts/Tests/EditMode/GameplayUiFoundationTests.cs) | 精简后保留 | 保留 Query / Command / 事件与事务最终态；移除一次性属性搬迁源码检查 |
| [GroundTilemapTests.cs](../../../Assets/Scripts/Tests/EditMode/GroundTilemapTests.cs) | 保留 | 已采用的双层地表、单图导入及正式场景填充 |
| [InfrastructurePolicyTests.cs](../../../Assets/Scripts/Tests/EditMode/InfrastructurePolicyTests.cs) | 保留 | 有负例的架构扫描与精确例外，保护长期服务边界 |
| [InstanceIdentityAndDtoTests.cs](../../../Assets/Scripts/Tests/EditMode/InstanceIdentityAndDtoTests.cs) | 保留 | 稳定身份、配置登记、封闭对象图和错误归属拒绝 |
| [InventoryDragTransactionTests.cs](../../../Assets/Scripts/Tests/EditMode/InventoryDragTransactionTests.cs) | 保留 | 真实占格、精确移动 / 交换 / 装备和失败不变 |
| [InventoryGridTests.cs](../../../Assets/Scripts/Tests/EditMode/InventoryGridTests.cs) | 保留 | 真实占格、精确移动 / 交换 / 装备和失败不变 |
| [ItemDetailSnapshotTests.cs](../../../Assets/Scripts/Tests/EditMode/ItemDetailSnapshotTests.cs) | 保留 | 语义详情在各 Query 一致、修改器格式化 |
| [ItemValueCalculatorTests.cs](../../../Assets/Scripts/Tests/EditMode/ItemValueCalculatorTests.cs) | 保留 | 价格与稀有度 / 词条关系及零值边界 |
| `ItemWorkbenchStructureTests.cs` | 移除 | 历史发布核验或旧工作台源码锁定；对照见审计报告 |
| [LocalSaveStorageTests.cs](../../../Assets/Scripts/Tests/EditMode/LocalSaveStorageTests.cs) | 保留 | 存档封闭快照、代际 IO、并发 / 故障 / 恢复与 Session 作用域 |
| [LocalSettingsStorageTests.cs](../../../Assets/Scripts/Tests/EditMode/LocalSettingsStorageTests.cs) | 保留 | 设置序列化、提交原子性、备份恢复、合法性与真实旧格式迁移 |
| [LocalizationAssetContractTests.cs](../../../Assets/Scripts/Tests/EditMode/LocalizationAssetContractTests.cs) | 保留 | 语言切换并发 / 回退、正式表与字体、可见文本治理；分别保护不同层级 |
| [LocalizationPolicyTests.cs](../../../Assets/Scripts/Tests/EditMode/LocalizationPolicyTests.cs) | 保留 | 语言切换并发 / 回退、正式表与字体、可见文本治理；分别保护不同层级 |
| [LocalizationServiceTests.cs](../../../Assets/Scripts/Tests/EditMode/LocalizationServiceTests.cs) | 保留 | 语言切换并发 / 回退、正式表与字体、可见文本治理；分别保护不同层级 |
| [LootTableDefinitionTests.cs](../../../Assets/Scripts/Tests/EditMode/LootTableDefinitionTests.cs) | 保留 | 加权选择、空池和零权重边界，保留不同消费者的入口 |
| [MerchantGridLayoutTests.cs](../../../Assets/Scripts/Tests/EditMode/MerchantGridLayoutTests.cs) | 保留 | 确定性布局、占格不重叠及交易后邻居选择 / 空列表回退 |
| [MigrationFixtureContractTests.cs](../../../Assets/Scripts/Tests/EditMode/MigrationFixtureContractTests.cs) | 保留 | 真实历史输入与迁移失败 / 不变性；夹具不是一次性工具 |
| [MigrationFixtureUtility.cs](../../../Assets/Scripts/Tests/EditMode/MigrationFixtureUtility.cs) | 保留辅助 | 真实历史输入与迁移失败 / 不变性；夹具不是一次性工具 |
| [MigrationPipelineTests.cs](../../../Assets/Scripts/Tests/EditMode/MigrationPipelineTests.cs) | 保留 | 真实历史输入与迁移失败 / 不变性；夹具不是一次性工具 |
| [MonsterSpawnDefinitionTests.cs](../../../Assets/Scripts/Tests/EditMode/MonsterSpawnDefinitionTests.cs) | 保留 | 加权选择、空池和零权重边界，保留不同消费者的入口 |
| [Phase4ContentTests.cs](../../../Assets/Scripts/Tests/EditMode/Phase4ContentTests.cs) | 保留 | 唯一正式全量内容校验入口及生成 / 属性消费 / 交易打造集成 |
| [Phase5VisualIntegrationTests.cs](../../../Assets/Scripts/Tests/EditMode/Phase5VisualIntegrationTests.cs) | 精简后保留 | 保留单图规格、正式引用和加载取消释放；仅移除旧工作台无背景 / 细边框约束 |
| [PrefabAssetLoaderTests.cs](../../../Assets/Scripts/Tests/EditMode/PrefabAssetLoaderTests.cs) | 保留 | 共享加载取消、失败重试和不同 owner 的句柄隔离 |
| [RandomizationPhase3Tests.cs](../../../Assets/Scripts/Tests/EditMode/RandomizationPhase3Tests.cs) | 保留 | 跨随机通道 / 怪物 / 伤害 / 掉落重放；保留并复核基线 PlayMode 采样失败 |
| [SaveCoordinatorTests.cs](../../../Assets/Scripts/Tests/EditMode/SaveCoordinatorTests.cs) | 保留 | 存档封闭快照、代际 IO、并发 / 故障 / 恢复与 Session 作用域 |
| [SaveDataContractTests.cs](../../../Assets/Scripts/Tests/EditMode/SaveDataContractTests.cs) | 保留 | 存档封闭快照、代际 IO、并发 / 故障 / 恢复与 Session 作用域 |
| [SaveRestorePreparerTests.cs](../../../Assets/Scripts/Tests/EditMode/SaveRestorePreparerTests.cs) | 保留 | 存档封闭快照、代际 IO、并发 / 故障 / 恢复与 Session 作用域 |
| [SaveSerializerTests.cs](../../../Assets/Scripts/Tests/EditMode/SaveSerializerTests.cs) | 保留 | 存档封闭快照、代际 IO、并发 / 故障 / 恢复与 Session 作用域 |
| [SettingsDataContractTests.cs](../../../Assets/Scripts/Tests/EditMode/SettingsDataContractTests.cs) | 保留 | 设置序列化、提交原子性、备份恢复、合法性与真实旧格式迁移 |
| [SettingsSerializerTests.cs](../../../Assets/Scripts/Tests/EditMode/SettingsSerializerTests.cs) | 保留 | 设置序列化、提交原子性、备份恢复、合法性与真实旧格式迁移 |
| [SettingsServiceTests.cs](../../../Assets/Scripts/Tests/EditMode/SettingsServiceTests.cs) | 保留 | 设置序列化、提交原子性、备份恢复、合法性与真实旧格式迁移 |
| [ShopViewStateTests.cs](../../../Assets/Scripts/Tests/EditMode/ShopViewStateTests.cs) | 保留 | 确定性布局、占格不重叠及交易后邻居选择 / 空列表回退 |
| [StatConfigurationTests.cs](../../../Assets/Scripts/Tests/EditMode/StatConfigurationTests.cs) | 保留 | 正式属性 ID 完整性、语义与非法配置诊断 |
| [VisualExperiencePhase05Tests.cs](../../../Assets/Scripts/Tests/EditMode/VisualExperiencePhase05Tests.cs) | 保留 | 相机 / 世界边界、刷怪位置与拾取表现的真实行为 |

## PlayMode

| 文件 | 决定 | 长期覆盖 / 判断 |
| --- | --- | --- |
| [ActorAnimationPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/ActorAnimationPlayModeTests.cs) | 保留 | 动画状态可达、攻击 / 受击 / 死亡 / 复活及朝向 |
| [Alpha012DamageResolutionPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha012DamageResolutionPlayModeTests.cs) | 保留 | 命中、暴击、防御、伤害来源、随机种子不误消费及初始武器原子性 |
| [Alpha013CollisionSafetyPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha013CollisionSafetyPlayModeTests.cs) | 保留 | 物理碰撞与世界障碍、接触攻击计时及移动配置 |
| [Alpha014ResourceSystemPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha014ResourceSystemPlayModeTests.cs) | 保留 | 生命 / 法力比例、恢复、耗蓝事务及真实 HUD |
| [Alpha015MonsterAffixPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha015MonsterAffixPlayModeTests.cs) | 保留 | 互斥、权重、实例效果与世界标记生命周期 |
| [Alpha017BaselinePlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha017BaselinePlayModeTests.cs) | 保留 | 现有特效资产家族与配置 / 战斗事件合同；审查后保留，不批量解除资产保护 |
| [Alpha017EffectRuntimePlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha017EffectRuntimePlayModeTests.cs) | 保留 | 命中表现语义、特效依赖、池预热 / 复用 / 释放 |
| [Alpha017WorldSortingPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha017WorldSortingPlayModeTests.cs) | 保留 | 排序稳定性、移动 / 销毁 / 身份复用与正式世界层级 |
| [Alpha024SceneFlowPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha024SceneFlowPlayModeTests.cs) | 保留 | 场景状态语义、暂停所有者、场景请求仲裁与失败补偿 |
| [Alpha025InputOwnershipPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha025InputOwnershipPlayModeTests.cs) | 保留 | 唯一输入资产、重绑定 / 设备与 UI Context；保留实际入口与运行时隔离 |
| [Alpha027ApplicationDataIsolationPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha027ApplicationDataIsolationPlayModeTests.cs) | 保留 | 玩家数据路径隔离与测试运行清理，长期必须保留 |
| [Alpha027IntegratedAcceptancePlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha027IntegratedAcceptancePlayModeTests.cs) | 保留 | 新建 / 保存 / 继续 / 删除 / 恢复默认 / 重启的集成流程，非历史记录检查 |
| [Alpha027PlayModeDataEnvironment.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha027PlayModeDataEnvironment.cs) | 保留辅助 | 玩家数据路径隔离与测试运行清理，长期必须保留 |
| [Alpha027PlayModeDataEnvironmentFixture.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha027PlayModeDataEnvironmentFixture.cs) | 保留辅助 | 玩家数据路径隔离与测试运行清理，长期必须保留 |
| [Alpha027ShellUiMatrixPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Alpha027ShellUiMatrixPlayModeTests.cs) | 精简后保留 | 三语言 Shell / 设置 / 错误框可见文字与边界；收敛为 1080p |
| [ApplicationHostSceneTransitionPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/ApplicationHostSceneTransitionPlayModeTests.cs) | 保留 | 异常清理、迟到回调与代际隔离，不能用普通成功路径替代 |
| [ApplicationLifecyclePlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/ApplicationLifecyclePlayModeTests.cs) | 保留 | 作用域生命周期、取消 / 超时、真实场景启动与隔离夹具 |
| [EquipmentPhase2PlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/EquipmentPhase2PlayModeTests.cs) | 精简后保留 | 穿脱 / 替换、全量聚合、真实焦点、在途攻击快照；PlayMode 缩减分辨率参数 |
| [GameArchitectureTestFixture.cs](../../../Assets/Scripts/Tests/PlayMode/GameArchitectureTestFixture.cs) | 保留辅助 | 作用域生命周期、取消 / 超时、真实场景启动与隔离夹具 |
| [InputTestFixtureGuard.cs](../../../Assets/Scripts/Tests/PlayMode/InputTestFixtureGuard.cs) | 保留辅助 | 唯一输入资产、重绑定 / 设备与 UI Context；保留实际入口与运行时隔离 |
| [Phase0ExperiencePlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Phase0ExperiencePlayModeTests.cs) | 保留 | 真实键鼠 / 手柄、点击拖动分离、精确移动 / 装备及详情恢复 |
| [Phase1UxPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Phase1UxPlayModeTests.cs) | 精简后保留 | 商店邻居焦点、右键出售、打造槽和三语言文本布局；收敛分辨率参数 |
| [Phase4ContentPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/Phase4ContentPlayModeTests.cs) | 保留 | 唯一正式全量内容校验入口及生成 / 属性消费 / 交易打造集成 |
| [RandomizationPhase3PlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/RandomizationPhase3PlayModeTests.cs) | 保留 | 跨随机通道 / 怪物 / 伤害 / 掉落重放；保留并复核基线 PlayMode 采样失败 |
| [SaveCaptureRestorePlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/SaveCaptureRestorePlayModeTests.cs) | 保留 | 存档封闭快照、代际 IO、并发 / 故障 / 恢复与 Session 作用域 |
| [SceneFlowPlayModeFixture.cs](../../../Assets/Scripts/Tests/PlayMode/SceneFlowPlayModeFixture.cs) | 保留辅助 | 作用域生命周期、取消 / 超时、真实场景启动与隔离夹具 |
| [SceneSessionComponentBindingPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/SceneSessionComponentBindingPlayModeTests.cs) | 保留 | 场景组件重绑和已捕获 Session 对象归属 |
| [SessionObjectRegistryOwnershipPlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/SessionObjectRegistryOwnershipPlayModeTests.cs) | 保留 | 场景组件重绑和已捕获 Session 对象归属 |
| [VisualExperiencePhase05PlayModeTests.cs](../../../Assets/Scripts/Tests/PlayMode/VisualExperiencePhase05PlayModeTests.cs) | 保留 | 相机 / 世界边界、刷怪位置与拾取表现的真实行为 |

两个测试 asmdef 保留，继续隔离 Editor / Player 与 NUnit 引用；Migration 目录的 manifest、save-v0-content-id、settings-v0-language 及对应 `.meta` 保留。发布 JSON 移入文档归档并校验 SHA-256 未变，删除其原 Assets `.meta`，不再参与运行测试。
