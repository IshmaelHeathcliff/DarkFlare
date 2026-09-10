using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace DarkFlare.Tests
{
    public class Alpha01TagMigrationCharacterizationTests
    {
        const string PresetRoot = "Assets/Data/Preset";

        static readonly Dictionary<string, ExpectedCandidates> CandidateMatrix =
            new Dictionary<string, ExpectedCandidates>
            {
                {
                    "great_sword",
                    new ExpectedCandidates(
                        new[]
                        {
                            "chaos_touched",
                            "deft",
                            "flame_touched",
                            "frost_touched",
                            "learned",
                            "mighty",
                            "sharp",
                            "storm_touched",
                            "tempered",
                        },
                        new[] { "accurate", "deadly", "of_power", "of_ruin" })
                },
                {
                    "war_axe",
                    new ExpectedCandidates(
                        new[]
                        {
                            "chaos_touched",
                            "deft",
                            "flame_touched",
                            "frost_touched",
                            "learned",
                            "mighty",
                            "sharp",
                            "storm_touched",
                            "tempered",
                        },
                        new[] { "accurate", "deadly", "of_power", "of_ruin" })
                },
                {
                    "leather_armor",
                    new ExpectedCandidates(
                        new[]
                        {
                            "arcane_reserve",
                            "deft",
                            "elusive",
                            "healthy",
                            "learned",
                            "meditative",
                            "mighty",
                            "regenerating",
                            "reinforced",
                        },
                        new[]
                        {
                            "of_chaos_guard",
                            "of_cold_guard",
                            "of_endurance",
                            "of_fire_guard",
                            "of_lightning_guard",
                            "of_swiftness",
                        })
                },
                {
                    "plate_armor",
                    new ExpectedCandidates(
                        new[]
                        {
                            "arcane_reserve",
                            "deft",
                            "elusive",
                            "healthy",
                            "learned",
                            "meditative",
                            "mighty",
                            "regenerating",
                            "reinforced",
                        },
                        new[]
                        {
                            "of_chaos_guard",
                            "of_cold_guard",
                            "of_endurance",
                            "of_fire_guard",
                            "of_lightning_guard",
                            "of_swiftness",
                        })
                },
                {
                    "iron_ring",
                    new ExpectedCandidates(
                        new[]
                        {
                            "arcane_reserve",
                            "chaos_touched",
                            "deft",
                            "elusive",
                            "flame_touched",
                            "frost_touched",
                            "healthy",
                            "learned",
                            "meditative",
                            "mighty",
                            "regenerating",
                            "storm_touched",
                        },
                        new[]
                        {
                            "accurate",
                            "deadly",
                            "of_chaos_guard",
                            "of_cold_guard",
                            "of_endurance",
                            "of_fire_guard",
                            "of_lightning_guard",
                            "of_power",
                            "of_ruin",
                            "of_swiftness",
                        })
                },
                {
                    "jade_ring",
                    new ExpectedCandidates(
                        new[]
                        {
                            "arcane_reserve",
                            "chaos_touched",
                            "deft",
                            "elusive",
                            "flame_touched",
                            "frost_touched",
                            "healthy",
                            "learned",
                            "meditative",
                            "mighty",
                            "regenerating",
                            "storm_touched",
                        },
                        new[]
                        {
                            "accurate",
                            "deadly",
                            "of_chaos_guard",
                            "of_cold_guard",
                            "of_endurance",
                            "of_fire_guard",
                            "of_lightning_guard",
                            "of_power",
                            "of_ruin",
                            "of_swiftness",
                        })
                },
                {
                    "obsidian_ring",
                    new ExpectedCandidates(
                        new[]
                        {
                            "arcane_reserve",
                            "chaos_touched",
                            "deft",
                            "elusive",
                            "flame_touched",
                            "frost_touched",
                            "healthy",
                            "learned",
                            "meditative",
                            "mighty",
                            "regenerating",
                            "storm_touched",
                        },
                        new[]
                        {
                            "accurate",
                            "deadly",
                            "of_chaos_guard",
                            "of_cold_guard",
                            "of_endurance",
                            "of_fire_guard",
                            "of_lightning_guard",
                            "of_power",
                            "of_ruin",
                            "of_swiftness",
                        })
                },
            };

        [Test]
        public void TagSet_NormalizesDeduplicatesAndUnionsWithoutMutation()
        {
            List<string> source = new List<string>
            {
                CombatTagIds.Weapon,
                string.Empty,
                " ",
                null,
                CombatTagIds.Weapon,
                CombatTagIds.Projectile,
            };
            TagSet left = new TagSet(source);
            TagSet right = new TagSet(new[] { CombatTagIds.Projectile, CombatTagIds.Physical });

            source.Add("late_mutation");
            TagSet union = left.Union(right);

            CollectionAssert.AreEquivalent(
                new[] { CombatTagIds.Weapon, CombatTagIds.Projectile },
                new List<string>(left.Ids));
            CollectionAssert.AreEquivalent(
                new[] { CombatTagIds.Projectile, CombatTagIds.Physical },
                new List<string>(right.Ids));
            CollectionAssert.AreEquivalent(
                new[] { CombatTagIds.Weapon, CombatTagIds.Projectile, CombatTagIds.Physical },
                new List<string>(union.Ids));
            Assert.IsFalse(left.Contains("late_mutation"));
            Assert.IsFalse(left.Contains(CombatTagIds.Physical));
            Assert.IsTrue(left.ContainsAll(null));
            Assert.IsFalse(left.ContainsAny(null));

            ICollection<string> emptyView = TagSet.Empty.Ids as ICollection<string>;
            Assert.IsNotNull(emptyView);
            Assert.IsTrue(emptyView.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => emptyView.Add("pollution"));
            Assert.IsFalse(TagSet.Empty.Contains("pollution"));
        }

        [Test]
        public void TagQuery_RequiredAllAnyBlockedAndScopesFollowTruthTable()
        {
            TagQuery query = new TagQuery(
                CombatTagScope.Skill | CombatTagScope.SourceItem | CombatTagScope.Damage,
                new TagSet(new[] { CombatTagIds.Projectile, CombatTagIds.Physical }),
                new TagSet(new[] { CombatTagIds.Weapon, CombatTagIds.Ring }),
                new TagSet(new[] { CombatTagIds.Monster }));

            CombatTagContext matching = new CombatTagContext(
                sourceActorTags: new TagSet(new[] { CombatTagIds.Monster }),
                skillTags: new TagSet(new[] { CombatTagIds.Projectile }),
                sourceItemTags: new TagSet(new[] { CombatTagIds.Weapon }),
                damageTags: new TagSet(new[] { CombatTagIds.Physical }));
            CombatTagContext missingRequiredAll = new CombatTagContext(
                skillTags: new TagSet(new[] { CombatTagIds.Projectile }),
                sourceItemTags: new TagSet(new[] { CombatTagIds.Weapon }));
            CombatTagContext missingRequiredAny = new CombatTagContext(
                skillTags: new TagSet(new[] { CombatTagIds.Projectile }),
                damageTags: new TagSet(new[] { CombatTagIds.Physical }));
            CombatTagContext blockedInScope = new CombatTagContext(
                skillTags: new TagSet(new[] { CombatTagIds.Projectile }),
                sourceItemTags: new TagSet(new[] { CombatTagIds.Weapon }),
                damageTags: new TagSet(new[] { CombatTagIds.Physical, CombatTagIds.Monster }));

            Assert.IsTrue(query.Matches(matching), "作用域外的 Actor.monster 不应阻止匹配");
            Assert.IsFalse(query.Matches(missingRequiredAll));
            Assert.IsFalse(query.Matches(missingRequiredAny));
            Assert.IsFalse(query.Matches(blockedInScope));
            Assert.IsFalse(query.TryMatch(missingRequiredAll, out string failureReason));
            StringAssert.Contains("Scope=", failureReason);
            StringAssert.Contains("RequiredAll=", failureReason);
            StringAssert.Contains("RequiredAny=", failureReason);
            StringAssert.Contains("BlockedAny=", failureReason);
            StringAssert.Contains("Context=", failureReason);

            TagQuery skillOnly = new TagQuery(
                CombatTagScope.Skill,
                new TagSet(new[] { "shared" }),
                TagSet.Empty,
                TagSet.Empty);
            Assert.IsFalse(skillOnly.Matches(new CombatTagContext(
                sourceItemTags: new TagSet(new[] { "shared" }))));
            Assert.IsTrue(skillOnly.Matches(new CombatTagContext(
                skillTags: new TagSet(new[] { "shared" }))));
        }

        [Test]
        public void CombatTagContext_FlattenKeepsEveryScopeIsolated()
        {
            CombatTagContext context = new CombatTagContext(
                sourceActorTags: new TagSet(new[] { "source_actor" }),
                targetActorTags: new TagSet(new[] { "target_actor" }),
                skillTags: new TagSet(new[] { "skill" }),
                sourceItemTags: new TagSet(new[] { "source_item" }),
                attackTags: new TagSet(new[] { "attack" }),
                damageTags: new TagSet(new[] { "damage" }));

            AssertScope(context, CombatTagScope.SourceActor, "source_actor");
            AssertScope(context, CombatTagScope.TargetActor, "target_actor");
            AssertScope(context, CombatTagScope.Skill, "skill");
            AssertScope(context, CombatTagScope.SourceItem, "source_item");
            AssertScope(context, CombatTagScope.Attack, "attack");
            AssertScope(context, CombatTagScope.Damage, "damage");

            CollectionAssert.AreEquivalent(
                new[] { "skill", "source_item" },
                new List<string>(context.Flatten(
                    CombatTagScope.Skill | CombatTagScope.SourceItem).Ids));
        }

        [Test]
        public void ReleasedItems_AffixCompatibilityMatchesFrozenCandidateMatrix()
        {
            List<AffixDefinition> affixes = LoadAssets<AffixDefinition>($"{PresetRoot}/Affixes");
            List<ItemBaseDefinition> items = LoadAssets<ItemBaseDefinition>($"{PresetRoot}/Items");

            items = items.Where(item => CandidateMatrix.ContainsKey(item.Id)).ToList();
            Assert.AreEqual(CandidateMatrix.Count, items.Count, "已发布迁移基线中的物品缺失");

            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                ItemBaseDefinition item = items[itemIndex];
                Assert.IsTrue(
                    CandidateMatrix.TryGetValue(item.Id, out ExpectedCandidates expected),
                    $"缺少 {item.Id} 的迁移候选矩阵");

                string[] prefixes = affixes
                    .Where(affix => affix.AffixType == AffixType.Prefix && affix.CanApplyTo(item.RuntimeTags, 1))
                    .Select(affix => affix.Id)
                    .OrderBy(id => id, System.StringComparer.Ordinal)
                    .ToArray();
                string[] suffixes = affixes
                    .Where(affix => affix.AffixType == AffixType.Suffix && affix.CanApplyTo(item.RuntimeTags, 1))
                    .Select(affix => affix.Id)
                    .OrderBy(id => id, System.StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(expected.PrefixIds, prefixes, $"{item.Id} 的前缀候选发生变化");
                CollectionAssert.AreEqual(expected.SuffixIds, suffixes, $"{item.Id} 的后缀候选发生变化");
            }
        }

        [Test]
        public void DamageConversion_PreservesPhysicalLineageAndCapsTotalAtOneHundredPercent()
        {
            DamagePacket source = new DamagePacket(
                DamageType.Physical,
                100f,
                new TagSet(new[] { "origin" }));
            ModifierInstance convertToFire = CreateModifier(
                string.Empty,
                ModifierOperation.Conversion,
                60f,
                DamageType.Physical,
                DamageType.Fire,
                TagQuery.Empty);
            ModifierInstance convertToCold = CreateModifier(
                string.Empty,
                ModifierOperation.Conversion,
                60f,
                DamageType.Physical,
                DamageType.Cold,
                TagQuery.Empty);
            ModifierInstance scaleConvertedFire = CreateModifier(
                StatIds.FireDamage,
                ModifierOperation.Increase,
                100f,
                DamageType.Physical,
                DamageType.Fire,
                CreateDamageLineageQuery(CombatTagIds.Physical, CombatTagIds.Fire));

            DamageResult result = Calculate(
                new[] { source },
                new[] { convertToFire, convertToCold, scaleConvertedFire });

            Assert.AreEqual(0f, result.DamageBeforeDefense[DamageType.Physical], 0.001f);
            Assert.AreEqual(100f, result.DamageBeforeDefense[DamageType.Fire], 0.001f);
            Assert.AreEqual(50f, result.DamageBeforeDefense[DamageType.Cold], 0.001f);

            DamagePacket converted = source.WithCurrentType(DamageType.Fire, 50f);
            TagSet resolvedTags = CombatTagResolver.ResolveDamageTags(converted);
            Assert.AreEqual(
                DamageTypeMask.Physical | DamageTypeMask.Fire,
                converted.ScalingTypes);
            CollectionAssert.AreEquivalent(
                new[] { "origin", CombatTagIds.Damage, CombatTagIds.Physical, CombatTagIds.Fire },
                new List<string>(resolvedTags.Ids));

            DamagePacket defaultPacket = default;
            Assert.IsTrue(CombatTagResolver.ResolveDamageTags(defaultPacket).Contains(CombatTagIds.Physical));
            Assert.AreEqual(
                DamageTypeMask.Physical | DamageTypeMask.Fire,
                defaultPacket.WithCurrentType(DamageType.Fire, 1f).ScalingTypes);
        }

        [Test]
        public void GainAsExtra_PreservesSourceAndConvertedDamageLineage()
        {
            DamagePacket source = new DamagePacket(DamageType.Physical, 100f, TagSet.Empty);
            ModifierInstance extraFire = CreateModifier(
                string.Empty,
                ModifierOperation.GainAsExtra,
                20f,
                DamageType.Physical,
                DamageType.Fire,
                TagQuery.Empty);
            ModifierInstance scaleExtraFire = CreateModifier(
                StatIds.FireDamage,
                ModifierOperation.Increase,
                100f,
                DamageType.Physical,
                DamageType.Fire,
                CreateDamageLineageQuery(CombatTagIds.Physical, CombatTagIds.Fire));

            DamageResult result = Calculate(new[] { source }, new[] { extraFire, scaleExtraFire });

            Assert.AreEqual(100f, result.DamageBeforeDefense[DamageType.Physical], 0.001f);
            Assert.AreEqual(40f, result.DamageBeforeDefense[DamageType.Fire], 0.001f);
        }

        [Test]
        public void DamagePipeline_KeepsLegacyIncreaseCriticalAndArmorBaselines()
        {
            DamagePacket source = new DamagePacket(DamageType.Physical, 100f, TagSet.Empty);
            ModifierInstance increasedDamage = CreateModifier(
                StatIds.Damage,
                ModifierOperation.Increase,
                50f,
                DamageType.Physical,
                DamageType.Physical,
                TagQuery.Empty);
            DamageResult increased = Calculate(new[] { source }, new[] { increasedDamage });
            Assert.AreEqual(150f, increased.TotalDamage, 0.001f);

            StatBlock criticalStats = new StatBlock();
            criticalStats.SetValue(StatIds.CriticalDamage, 50f);
            DamageResult critical = Calculate(
                new[] { source },
                new List<ModifierInstance>(),
                criticalStats,
                new StatBlock(),
                true);
            Assert.AreEqual(150f, critical.TotalDamage, 0.001f);

            StatBlock armoredDefender = new StatBlock();
            armoredDefender.SetValue(StatIds.Armor, 100f);
            DamageResult armored = Calculate(
                new[] { source },
                new List<ModifierInstance>(),
                new StatBlock(),
                armoredDefender);
            Assert.AreEqual(90.909f, armored.TotalDamage, 0.01f);
        }

        [Test]
        public void LegacyAndScopedModifiers_ReadOnlyTheirOriginalContexts()
        {
            DamagePacket physicalWithoutCustomTags = new DamagePacket(
                DamageType.Physical,
                100f,
                TagSet.Empty);
            ModifierInstance legacyPhysical = new ModifierInstance(
                StatIds.Damage,
                ModifierOperation.Increase,
                ModifierScope.GlobalActor,
                100f,
                DamageType.Physical,
                DamageType.Physical,
                new TagSet(new[] { CombatTagIds.Physical }),
                TagSet.Empty);
            CombatTagContext targetOnly = new CombatTagContext(
                targetActorTags: new TagSet(new[] { CombatTagIds.Physical }));
            DamageContext legacyContext = CreateContext(
                physicalWithoutCustomTags,
                targetOnly,
                new[] { legacyPhysical },
                new List<ModifierInstance>());

            Assert.AreEqual(100f, DamageCalculator.Calculate(legacyContext).TotalDamage, 0.001f);

            DamagePacket physicalWithCustomTag = new DamagePacket(
                DamageType.Physical,
                100f,
                new TagSet(new[] { CombatTagIds.Physical }));
            Assert.AreEqual(
                200f,
                DamageCalculator.Calculate(CreateContext(
                    physicalWithCustomTag,
                    CombatTagContext.Empty,
                    new[] { legacyPhysical },
                    new List<ModifierInstance>())).TotalDamage,
                0.001f);

            ModifierInstance scopedPhysical = CreateModifier(
                StatIds.Damage,
                ModifierOperation.Increase,
                100f,
                DamageType.Physical,
                DamageType.Physical,
                CreateDamageLineageQuery(CombatTagIds.Physical));
            Assert.AreEqual(
                200f,
                DamageCalculator.Calculate(CreateContext(
                    physicalWithoutCustomTags,
                    CombatTagContext.Empty,
                    new[] { scopedPhysical },
                    new List<ModifierInstance>())).TotalDamage,
                0.001f);

            ModifierInstance legacyTaken = new ModifierInstance(
                StatIds.Damage,
                ModifierOperation.More,
                ModifierScope.TargetTaken,
                100f,
                DamageType.Physical,
                DamageType.Physical,
                new TagSet(new[] { "packet_custom" }),
                TagSet.Empty);
            DamagePacket customPacket = new DamagePacket(
                DamageType.Physical,
                100f,
                new TagSet(new[] { "packet_custom" }));
            Assert.AreEqual(
                100f,
                DamageCalculator.Calculate(CreateContext(
                    customPacket,
                    CombatTagContext.Empty,
                    new List<ModifierInstance>(),
                    new[] { legacyTaken })).TotalDamage,
                0.001f);

            ModifierInstance scopedTaken = new ModifierInstance(
                StatIds.Damage,
                ModifierOperation.More,
                ModifierScope.TargetTaken,
                100f,
                DamageType.Physical,
                DamageType.Physical,
                new TagQuery(
                    CombatTagScope.Damage,
                    new TagSet(new[] { "packet_custom" }),
                    TagSet.Empty,
                    TagSet.Empty));
            Assert.AreEqual(
                200f,
                DamageCalculator.Calculate(CreateContext(
                    customPacket,
                    CombatTagContext.Empty,
                    new List<ModifierInstance>(),
                    new[] { scopedTaken })).TotalDamage,
                0.001f);
        }

        [Test]
        public void EquipmentComparison_DoesNotFlattenRequiredAnyModifier()
        {
            ModifierInstance conditional = new ModifierInstance(
                StatIds.Damage,
                ModifierOperation.Increase,
                ModifierScope.GlobalActor,
                10f,
                DamageType.Physical,
                DamageType.Physical,
                new TagQuery(
                    CombatTagScope.SourceItem,
                    TagSet.Empty,
                    new TagSet(new[] { CombatTagIds.Weapon }),
                    TagSet.Empty));
            ModifierDetailSnapshot modifierDetail = new ModifierDetailSnapshot(
                conditional,
                StatIds.Damage,
                false);
            ItemDetailSnapshot detail = new ItemDetailSnapshot(
                null,
                string.Empty,
                default,
                string.Empty,
                ItemType.Weapon,
                ItemRarity.Normal,
                1,
                UnityEngine.Vector2Int.one,
                0f,
                0,
                0,
                new List<DamageDetailSnapshot>(),
                new[] { modifierDetail },
                new List<AffixDetailSnapshot>(),
                new List<AffixDetailSnapshot>());
            MethodInfo collect = typeof(EquipmentComparisonFactory).GetMethod(
                "CollectComparableModifiers",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(collect);
            object result = collect.Invoke(null, new object[] { detail });
            PropertyInfo count = result.GetType().GetProperty("Count");
            Assert.IsNotNull(count);
            Assert.AreEqual(0, (int)count.GetValue(result));
        }

        [Test]
        public void AttackSnapshot_DeepCopiesAllTagScopesPacketsStatsAndModifierLists()
        {
            List<string> sourceActorIds = new List<string> { "source_actor" };
            List<string> targetActorIds = new List<string> { "target_actor" };
            List<string> skillIds = new List<string> { "skill" };
            List<string> sourceItemIds = new List<string> { "source_item" };
            List<string> attackIds = new List<string> { "attack" };
            List<string> damageIds = new List<string> { "damage" };
            List<string> legacyIds = new List<string> { "legacy" };
            List<string> packetIds = new List<string> { "packet_custom" };
            CombatTagContext context = new CombatTagContext(
                sourceActorTags: new TagSet(sourceActorIds),
                targetActorTags: new TagSet(targetActorIds),
                skillTags: new TagSet(skillIds),
                sourceItemTags: new TagSet(sourceItemIds),
                attackTags: new TagSet(attackIds),
                damageTags: new TagSet(damageIds),
                legacyTags: new TagSet(legacyIds));
            DamagePacket packet = new DamagePacket(
                DamageType.Fire,
                10f,
                DamageTypeMask.Physical | DamageTypeMask.Fire,
                new TagSet(packetIds));
            List<DamagePacket> packets = new List<DamagePacket> { packet };
            StatBlock stats = new StatBlock();
            stats.SetValue(StatIds.PhysicalDamage, 5f);
            List<ModifierInstance> modifiers = new List<ModifierInstance>
            {
                CreateModifier(
                    StatIds.Damage,
                    ModifierOperation.Increase,
                    20f,
                    DamageType.Physical,
                    DamageType.Physical,
                    TagQuery.Empty),
            };
            AttackSnapshot snapshot = new AttackSnapshot(
                "snapshot_attacker",
                ActorTeam.Player,
                "snapshot_skill",
                "snapshot_item",
                901,
                packets,
                context,
                stats,
                modifiers);

            sourceActorIds.Add("mutated_source_actor");
            targetActorIds.Add("mutated_target_actor");
            skillIds.Add("mutated_skill");
            sourceItemIds.Add("mutated_source_item");
            attackIds.Add("mutated_attack");
            damageIds.Add("mutated_damage");
            legacyIds.Add("mutated_legacy");
            packetIds.Add("mutated_packet");
            packets.Clear();
            modifiers.Clear();
            stats.SetValue(StatIds.PhysicalDamage, 500f);

            Assert.IsFalse(snapshot.TagContext.SourceActorTags.Contains("mutated_source_actor"));
            Assert.IsFalse(snapshot.TagContext.TargetActorTags.Contains("mutated_target_actor"));
            Assert.IsFalse(snapshot.TagContext.SkillTags.Contains("mutated_skill"));
            Assert.IsFalse(snapshot.TagContext.SourceItemTags.Contains("mutated_source_item"));
            Assert.IsFalse(snapshot.TagContext.AttackTags.Contains("mutated_attack"));
            Assert.IsFalse(snapshot.TagContext.DamageTags.Contains("mutated_damage"));
            Assert.AreEqual(1, snapshot.BaseDamages.Count);
            Assert.IsFalse(snapshot.BaseDamages[0].CustomTags.Contains("mutated_packet"));
            Assert.AreEqual(
                DamageTypeMask.Physical | DamageTypeMask.Fire,
                snapshot.BaseDamages[0].ScalingTypes);
            Assert.AreEqual(1, snapshot.AttackerModifiers.Count);
            Assert.AreEqual(5f, snapshot.AttackerStats.GetValue(StatIds.PhysicalDamage), 0.001f);
            CollectionAssert.AreEquivalent(
                new[] { "legacy" },
                new List<string>(snapshot.ContextTags.Ids));
        }

        static void AssertScope(CombatTagContext context, CombatTagScope scope, string expectedId)
        {
            TagSet tags = context.Flatten(scope);
            Assert.AreEqual(1, tags.Ids.Count, $"{scope} 发生标签泄漏");
            Assert.IsTrue(tags.Contains(expectedId), $"{scope} 缺少 {expectedId}");
        }

        static TagQuery CreateDamageLineageQuery(params string[] requiredAll)
        {
            return new TagQuery(
                CombatTagScope.Damage,
                new TagSet(requiredAll),
                TagSet.Empty,
                TagSet.Empty);
        }

        static ModifierInstance CreateModifier(
            string statId,
            ModifierOperation operation,
            float value,
            DamageType fromDamageType,
            DamageType toDamageType,
            TagQuery query)
        {
            return new ModifierInstance(
                statId,
                operation,
                ModifierScope.GlobalActor,
                value,
                fromDamageType,
                toDamageType,
                query);
        }

        static DamageResult Calculate(
            IEnumerable<DamagePacket> packets,
            IEnumerable<ModifierInstance> attackerModifiers,
            StatBlock attackerStats = null,
            StatBlock defenderStats = null,
            bool isCritical = false)
        {
            DamageContext context = new DamageContext(
                "attacker",
                "defender",
                "alpha_0_1_tag_characterization",
                string.Empty,
                901,
                packets,
                CombatTagContext.Empty,
                attackerStats ?? new StatBlock(),
                defenderStats ?? new StatBlock(),
                attackerModifiers,
                new List<ModifierInstance>(),
                true,
                isCritical);
            return DamageCalculator.Calculate(context);
        }

        static DamageContext CreateContext(
            DamagePacket packet,
            CombatTagContext tagContext,
            IEnumerable<ModifierInstance> attackerModifiers,
            IEnumerable<ModifierInstance> defenderModifiers)
        {
            return new DamageContext(
                "attacker",
                "defender",
                "legacy_compatibility",
                string.Empty,
                902,
                new[] { packet },
                tagContext,
                new StatBlock(),
                new StatBlock(),
                attackerModifiers,
                defenderModifiers);
        }

        static List<T> LoadAssets<T>(string root) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root });
            List<T> result = new List<T>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));

                if (asset != null)
                {
                    result.Add(asset);
                }
            }

            return result;
        }

        readonly struct ExpectedCandidates
        {
            public string[] PrefixIds { get; }

            public string[] SuffixIds { get; }

            public ExpectedCandidates(string[] prefixIds, string[] suffixIds)
            {
                PrefixIds = prefixIds;
                SuffixIds = suffixIds;
            }
        }
    }
}
