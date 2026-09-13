using System;
using System.Collections.Generic;
using DarkFlare;
using NUnit.Framework;
using UnityEditor;

namespace DarkFlare.Tests
{
    public sealed class Alpha015AffixCoverageTests
    {
        const string AffixRoot = "Assets/Data/Preset/Affixes";
        const string ItemRoot = "Assets/Data/Preset/Items";

        [Test]
        public void OfficialAffixes_CoverEveryStatWithConsumableModifier()
        {
            List<AffixDefinition> affixes = LoadAssets<AffixDefinition>(AffixRoot);
            HashSet<string> covered = new HashSet<string>(StringComparer.Ordinal);

            for (int affixIndex = 0; affixIndex < affixes.Count; affixIndex++)
            {
                AffixDefinition affix = affixes[affixIndex];

                for (int modifierIndex = 0; modifierIndex < affix.Modifiers.Count; modifierIndex++)
                {
                    StatModifierDefinition modifier = affix.Modifiers[modifierIndex];

                    if (modifier != null && modifier.Stat != null && IsConsumable(modifier))
                    {
                        covered.Add(modifier.Stat.Id);
                    }
                }
            }

            CollectionAssert.IsSubsetOf(StatIds.All, covered, "存在没有正式可消费词条的公开属性");
        }

        [Test]
        public void OfficialAffixes_HaveCompatibleItemAndReplayRolledValues()
        {
            List<AffixDefinition> affixes = LoadAssets<AffixDefinition>(AffixRoot);
            List<ItemBaseDefinition> items = LoadAssets<ItemBaseDefinition>(ItemRoot);

            for (int affixIndex = 0; affixIndex < affixes.Count; affixIndex++)
            {
                AffixDefinition affix = affixes[affixIndex];
                Assert.IsTrue(
                    items.Exists(item => affix.CanApplyTo(item.RuntimeTags, 1)),
                    $"{affix.Id} 没有兼容正式物品");
                AffixInstance first = affix.CreateInstance(new System.Random(9100 + affixIndex));
                AffixInstance replay = affix.CreateInstance(new System.Random(9100 + affixIndex));
                Assert.AreEqual(first.Modifiers.Count, replay.Modifiers.Count, affix.Id);

                for (int modifierIndex = 0; modifierIndex < first.Modifiers.Count; modifierIndex++)
                {
                    Assert.AreEqual(
                        first.Modifiers[modifierIndex].Value,
                        replay.Modifiers[modifierIndex].Value,
                        0.0001f,
                        $"{affix.Id} 修改器 {modifierIndex} 未按固定种子复现");
                }
            }
        }

        static bool IsConsumable(StatModifierDefinition modifier)
        {
            bool supportedOperation = modifier.Operation == ModifierOperation.Flat
                || modifier.Operation == ModifierOperation.Increase
                || modifier.Operation == ModifierOperation.More;

            if (CombatStatResolver.IsDamageStat(modifier.Stat.Id))
            {
                return supportedOperation
                    && (modifier.Scope == ModifierScope.LocalItem
                        || modifier.Scope == ModifierScope.GlobalActor
                        || modifier.Scope == ModifierScope.Skill);
            }

            return (supportedOperation || modifier.Operation == ModifierOperation.Override)
                && modifier.Scope == ModifierScope.GlobalActor;
        }

        static List<T> LoadAssets<T>(string root) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root });
            List<T> assets = new List<T>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));

                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }
    }
}
