using System.Collections.Generic;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEditor;

namespace DarkFlare.Tests
{
    public class Phase4ContentTests
    {
        const string PresetRoot = "Assets/Data/Preset";

        [Test]
        public void OfficialContent_HasExpectedCountsIdsAndNoValidationIssues()
        {
            Assert.AreEqual(14, LoadAssets<TagDefinition>($"{PresetRoot}/Tags").Count);
            Assert.AreEqual(12, LoadAssets<AffixDefinition>($"{PresetRoot}/Affixes").Count);
            Assert.AreEqual(7, LoadAssets<ItemBaseDefinition>($"{PresetRoot}/Items").Count);
            Assert.AreEqual(3, LoadAssets<MonsterDefinition>($"{PresetRoot}/Monsters").Count);

            List<ContentValidationIssue> issues = ContentConfigurationValidator.Scan();
            Assert.IsEmpty(issues, JoinIssues(issues));
        }

        [Test]
        public void OfficialItems_HaveCompatibleAffixes_AndGenerationReplaysSeed()
        {
            List<AffixDefinition> affixes = LoadAssets<AffixDefinition>($"{PresetRoot}/Affixes");
            List<ItemBaseDefinition> items = LoadAssets<ItemBaseDefinition>($"{PresetRoot}/Items");
            HashSet<AffixDefinition> coveredAffixes = new HashSet<AffixDefinition>();

            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                ItemBaseDefinition item = items[itemIndex];
                int candidateCount = 0;

                for (int affixIndex = 0; affixIndex < affixes.Count; affixIndex++)
                {
                    if (!affixes[affixIndex].CanApplyTo(item.RuntimeTags, 1))
                    {
                        continue;
                    }

                    candidateCount++;
                    coveredAffixes.Add(affixes[affixIndex]);
                }

                Assert.GreaterOrEqual(candidateCount, 4, $"{item.Id} 的兼容词条不足");

                ItemGenerationOptions options = new ItemGenerationOptions(
                    $"phase4_{item.Id}",
                    1,
                    4000 + itemIndex,
                    ItemRarity.Magic,
                    1,
                    1);
                ItemInstance first = ItemGenerator.Generate(item, affixes, options);
                ItemInstance replay = ItemGenerator.Generate(item, affixes, options);

                Assert.AreEqual(1, first.Prefixes.Count, $"{item.Id} 未生成前缀");
                Assert.AreEqual(1, first.Suffixes.Count, $"{item.Id} 未生成后缀");
                Assert.AreEqual(Describe(first), Describe(replay), $"{item.Id} 未按固定种子复现");
            }

            Assert.AreEqual(affixes.Count, coveredAffixes.Count, "存在没有兼容装备的词条");
        }

        [Test]
        public void ElementalExtra_UsesPhysicalDamagePacketTag_AndExclusiveGroupRejectsDuplicate()
        {
            AffixDefinition flame = LoadAffix("flame_touched");
            AffixDefinition frost = LoadAffix("frost_touched");
            ItemBaseDefinition ring = LoadItem("iron_ring");
            AffixInstance flameInstance = flame.CreateInstance(new System.Random(17));
            AffixInstance frostInstance = frost.CreateInstance(new System.Random(17));
            ItemInstance item = ring.CreateInstance("phase4_group_ring", 1, 17, ItemRarity.Magic);

            Assert.IsTrue(item.TryAddAffix(flameInstance));
            Assert.IsFalse(item.TryAddAffix(frostInstance), "同组元素额外伤害不应同时出现");

            DamageContext context = new DamageContext(
                "attacker",
                "defender",
                "projectile",
                string.Empty,
                17,
                new[]
                {
                    new DamagePacket(DamageType.Physical, 100f, new TagSet(new[] { "damage", "physical" })),
                },
                new TagSet(new[] { "projectile" }),
                new StatBlock(),
                new StatBlock(),
                flameInstance.Modifiers,
                new List<ModifierInstance>());
            DamageResult result = DamageCalculator.Calculate(context);

            Assert.IsTrue(result.DamageBeforeDefense.ContainsKey(DamageType.Fire));
            Assert.That(result.DamageBeforeDefense[DamageType.Fire], Is.InRange(8f, 12f));
            Assert.AreEqual(100f, result.DamageBeforeDefense[DamageType.Physical], 0.001f);
        }

        [Test]
        public void OfficialAffixes_AllProduceObservableDamageOrActorStatChanges()
        {
            List<AffixDefinition> affixes = LoadAssets<AffixDefinition>($"{PresetRoot}/Affixes");

            for (int i = 0; i < affixes.Count; i++)
            {
                AffixDefinition definition = affixes[i];
                AffixInstance instance = definition.CreateInstance(new System.Random(7000 + i));

                if (definition.Id == "sharp"
                    || definition.Id == "tempered"
                    || definition.Id == "of_power")
                {
                    DamageResult damage = CalculatePhysicalDamage(instance.Modifiers);
                    Assert.Greater(damage.DamageBeforeDefense[DamageType.Physical], 100f, definition.Id);
                }
                else if (definition.Id == "flame_touched" || definition.Id == "frost_touched")
                {
                    DamageResult damage = CalculatePhysicalDamage(instance.Modifiers);
                    DamageType extraType = definition.Id == "flame_touched" ? DamageType.Fire : DamageType.Cold;
                    Assert.Greater(damage.DamageBeforeDefense[extraType], 0f, definition.Id);
                }
                else
                {
                    AssertActorStatAffixChangesExpectedStat(definition.Id, instance.Modifiers);
                }
            }
        }

        [Test]
        public void OfficialPools_ExposeAllContentAndExpectedWeights()
        {
            MonsterSpawnDefinition spawn = AssetDatabase.LoadAssetAtPath<MonsterSpawnDefinition>(
                $"{PresetRoot}/Monsters/基础刷怪表.asset");
            Assert.IsNotNull(spawn);
            Assert.AreEqual(3, spawn.Rules.Count);
            Assert.AreEqual("wasteland_wraith", spawn.Rules[0].Monster.Id);
            Assert.AreEqual(55, spawn.Rules[0].Weight);
            Assert.AreEqual("razor_hound", spawn.Rules[1].Monster.Id);
            Assert.AreEqual(30, spawn.Rules[1].Weight);
            Assert.AreEqual("iron_husk", spawn.Rules[2].Monster.Id);
            Assert.AreEqual(15, spawn.Rules[2].Weight);

            List<LootTableDefinition> lootTables = LoadAssets<LootTableDefinition>($"{PresetRoot}/Loot");
            Assert.AreEqual(3, lootTables.Count);

            for (int i = 0; i < lootTables.Count; i++)
            {
                Assert.AreEqual(7, lootTables[i].Entries.Count);
                Assert.AreEqual(12, lootTables[i].AffixPool.Count);
            }

            TraderDefinition trader = AssetDatabase.LoadAssetAtPath<TraderDefinition>(
                $"{PresetRoot}/Traders/基础商人.asset");
            CraftingDefinition crafting = AssetDatabase.LoadAssetAtPath<CraftingDefinition>(
                $"{PresetRoot}/Crafting/基础打造配置.asset");
            Assert.IsNotNull(trader);
            Assert.IsNotNull(crafting);
            Assert.AreEqual(7, trader.Stock.Count);
            Assert.AreEqual(12, crafting.AffixPool.Count);
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

        static AffixDefinition LoadAffix(string id)
        {
            List<AffixDefinition> affixes = LoadAssets<AffixDefinition>($"{PresetRoot}/Affixes");

            for (int i = 0; i < affixes.Count; i++)
            {
                if (affixes[i].Id == id)
                {
                    return affixes[i];
                }
            }

            Assert.Fail($"找不到词条 {id}");
            return null;
        }

        static ItemBaseDefinition LoadItem(string id)
        {
            List<ItemBaseDefinition> items = LoadAssets<ItemBaseDefinition>($"{PresetRoot}/Items");

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id == id)
                {
                    return items[i];
                }
            }

            Assert.Fail($"找不到装备 {id}");
            return null;
        }

        static string Describe(ItemInstance item)
        {
            List<string> parts = new List<string>();

            for (int i = 0; i < item.Prefixes.Count; i++)
            {
                parts.Add(Describe(item.Prefixes[i]));
            }

            for (int i = 0; i < item.Suffixes.Count; i++)
            {
                parts.Add(Describe(item.Suffixes[i]));
            }

            return string.Join("|", parts);
        }

        static string Describe(AffixInstance affix)
        {
            List<string> values = new List<string>(affix.Modifiers.Count);

            for (int i = 0; i < affix.Modifiers.Count; i++)
            {
                values.Add(affix.Modifiers[i].Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }

            return $"{affix.Definition.Id}:{string.Join(",", values)}";
        }

        static DamageResult CalculatePhysicalDamage(IReadOnlyList<ModifierInstance> modifiers)
        {
            return DamageCalculator.Calculate(new DamageContext(
                "attacker",
                "defender",
                "projectile",
                string.Empty,
                1,
                new[]
                {
                    new DamagePacket(DamageType.Physical, 100f, new TagSet(new[] { "damage", "physical" })),
                },
                new TagSet(new[] { "projectile" }),
                new StatBlock(),
                new StatBlock(),
                modifiers,
                new List<ModifierInstance>()));
        }

        static void AssertActorStatAffixChangesExpectedStat(
            string affixId,
            IReadOnlyList<ModifierInstance> modifiers)
        {
            StatBlock baseStats = new StatBlock();
            baseStats.SetValue(StatIds.MaxHealth, 100f);
            StatBlock result = CombatStatResolver.Build(baseStats, modifiers);
            string expectedStatId;
            float baseline;

            switch (affixId)
            {
                case "healthy":
                case "of_endurance":
                    expectedStatId = StatIds.MaxHealth;
                    baseline = 100f;
                    break;
                case "reinforced":
                    expectedStatId = StatIds.Armor;
                    baseline = 0f;
                    break;
                case "of_fire_guard":
                    expectedStatId = StatIds.FireResistance;
                    baseline = 0f;
                    break;
                case "of_cold_guard":
                    expectedStatId = StatIds.ColdResistance;
                    baseline = 0f;
                    break;
                case "of_lightning_guard":
                    expectedStatId = StatIds.LightningResistance;
                    baseline = 0f;
                    break;
                case "of_chaos_guard":
                    expectedStatId = StatIds.ChaosResistance;
                    baseline = 0f;
                    break;
                default:
                    Assert.Fail($"未覆盖词条效果验证: {affixId}");
                    return;
            }

            Assert.Greater(result.GetValue(expectedStatId), baseline, affixId);
        }

        static string JoinIssues(IReadOnlyList<ContentValidationIssue> issues)
        {
            List<string> messages = new List<string>(issues.Count);

            for (int i = 0; i < issues.Count; i++)
            {
                messages.Add($"{issues[i].AssetPath}: {issues[i].Message}");
            }

            return string.Join("\n", messages);
        }
    }
}
