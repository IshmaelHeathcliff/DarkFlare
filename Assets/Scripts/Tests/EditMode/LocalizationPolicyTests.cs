using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class LocalizationPolicyTests
    {
        static readonly string[] MigratedRuntimeTextPaths =
        {
            "Assets/Scripts/Runtime/Gameplay/UI/GetHudSnapshotQuery.cs",
            "Assets/Scripts/Runtime/Gameplay/UI/GetInventorySnapshotQuery.cs",
            "Assets/Scripts/Runtime/Gameplay/UI/InteractionPromptController.cs",
            "Assets/Scripts/Runtime/Gameplay/UI/InventoryPanelController.cs",
            "Assets/Scripts/Runtime/Gameplay/UI/ItemDetailFormatter.cs",
            "Assets/Scripts/Runtime/Gameplay/UI/ItemDetailSnapshotFactory.cs",
            "Assets/Scripts/Runtime/Gameplay/UI/ItemDetailView.cs",
            "Assets/Scripts/Runtime/Gameplay/UI/ShopPanelController.cs",
            "Assets/Scripts/Runtime/Gameplay/UI/CraftingPanelController.cs",
            "Assets/Scripts/Runtime/Gameplay/Visuals/LootPickupVisual.cs",
            "Assets/Scripts/Runtime/Gameplay/Visuals/MonsterAffixVisual.cs",
            "Assets/Scripts/Runtime/Gameplay/Visuals/WorldInteractionVisual.cs",
        };

        static readonly Regex ChineseStringLiteralPattern = new Regex(
            "\"(?:\\\\.|[^\"\\\\])*[\\u4e00-\\u9fff](?:\\\\.|[^\"\\\\])*\"",
            RegexOptions.Compiled);

        static readonly HashSet<string> DynamicTextElements = new HashSet<string>(
            StringComparer.Ordinal)
        {
            "game-menu-save-status",
            "item-tooltip-context",
            "crafting-gold",
            "crafting-input-name",
            "crafting-selected-rarity",
            "crafting-selected-capacity",
            "crafting-selected-value",
            "crafting-slot-place",
            "crafting-upgrade-rarity",
            "crafting-reset-normal",
            "crafting-feedback",
            "crafting-reroll-affixes",
            "crafting-add-affix",
            "crafting-remove-affix",
            "crafting-reroll-values",
            "crafting-result",
            "skill-status-label",
            "gold-label",
            "interaction-prompt-label",
            "inventory-target-slot",
            "inventory-feedback",
            "inventory-slot-weapon",
            "inventory-slot-armor",
            "inventory-slot-ring-left",
            "inventory-slot-ring-right",
            "shop-gold",
            "shop-selected-source",
            "shop-selected-price",
            "shop-feedback",
            "item-detail-name",
            "item-detail-meta",
            "item-detail-base",
        };

        [Test]
        public void Uxml_PlayerTextIsBoundOrRegisteredDynamicAndHasNoLiteralFallback()
        {
            List<string> violations = new List<string>();
            string[] paths = Directory.GetFiles(
                "Assets/UI",
                "*.uxml",
                SearchOption.TopDirectoryOnly);

            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                string path = paths[pathIndex].Replace('\\', '/');
                XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);

                foreach (XElement element in document.Descendants())
                {
                    XAttribute text = element.Attribute("text");
                    XAttribute tooltip = element.Attribute("tooltip");

                    if (text != null && !string.IsNullOrEmpty(text.Value))
                    {
                        violations.Add($"{path}: text={text.Value}");
                    }

                    if (tooltip != null && !string.IsNullOrEmpty(tooltip.Value))
                    {
                        violations.Add($"{path}: tooltip={tooltip.Value}");
                    }

                    if (text == null || HasTextBinding(element))
                    {
                        continue;
                    }

                    string name = element.Attribute("name")?.Value ?? string.Empty;

                    if (!DynamicTextElements.Contains(name))
                    {
                        violations.Add($"{path}: 未登记动态文本元素 {name}/{element.Name.LocalName}");
                    }
                }
            }

            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void UiTable_AllBoundKeysAndTranslationsExistWithoutOrphans()
        {
            StringTableCollection collection = LocalizationEditorSettings
                .GetStringTableCollections()
                .Single(candidate => candidate.TableCollectionName == "ui");
            HashSet<string> keys = collection.SharedData.Entries
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal);
            List<string> violations = new List<string>();
            string[] paths = Directory.GetFiles(
                "Assets/UI",
                "*.uxml",
                SearchOption.TopDirectoryOnly);

            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                XDocument document = XDocument.Load(paths[pathIndex]);

                foreach (XElement binding in document.Descendants()
                    .Where(element => element.Name.LocalName == "LocalizedString"))
                {
                    string table = binding.Attribute("table")?.Value;
                    string key = binding.Attribute("entry")?.Value;

                    if (!string.Equals(table, "ui", StringComparison.Ordinal)
                        || string.IsNullOrWhiteSpace(key)
                        || !keys.Contains(key))
                    {
                        violations.Add($"{paths[pathIndex]}: {table}/{key}");
                    }
                }
            }

            foreach (SharedTableData.SharedTableEntry shared in collection.SharedData.Entries)
            {
                foreach (StringTable table in collection.StringTables)
                {
                    StringTableEntry entry = table.GetEntry(shared.Id);

                    if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                    {
                        violations.Add(
                            $"ui/{shared.Key}/{table.LocaleIdentifier.Code}: 缺失或空翻译");
                    }
                }
            }

            foreach (StringTable table in collection.StringTables)
            {
                foreach (StringTableEntry entry in table.Values)
                {
                    if (entry.SharedEntry == null)
                    {
                        violations.Add(
                            $"ui/{table.LocaleIdentifier.Code}/{entry.KeyId}: 孤儿条目");
                    }
                }
            }

            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void StatsTable_ExactlyMatchesSupportedStatsAndHasCompleteTranslations()
        {
            StringTableCollection collection = LocalizationEditorSettings
                .GetStringTableCollections()
                .Single(candidate => candidate.TableCollectionName == "stats");
            HashSet<string> expectedKeys = StatIds.All.ToHashSet(StringComparer.Ordinal);
            HashSet<string> actualKeys = collection.SharedData.Entries
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal);
            List<string> violations = new List<string>();

            foreach (string missingKey in expectedKeys.Except(actualKeys))
            {
                violations.Add($"stats/{missingKey}: 缺少共享键");
            }

            foreach (string unexpectedKey in actualKeys.Except(expectedKeys))
            {
                violations.Add($"stats/{unexpectedKey}: 未登记属性键");
            }

            foreach (SharedTableData.SharedTableEntry shared in collection.SharedData.Entries)
            {
                foreach (StringTable table in collection.StringTables)
                {
                    StringTableEntry entry = table.GetEntry(shared.Id);

                    if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                    {
                        violations.Add(
                            $"stats/{shared.Key}/{table.LocaleIdentifier.Code}: 缺失或空翻译");
                    }
                }
            }

            foreach (StringTable table in collection.StringTables)
            {
                foreach (StringTableEntry entry in table.Values)
                {
                    if (entry.SharedEntry == null)
                    {
                        violations.Add(
                            $"stats/{table.LocaleIdentifier.Code}/{entry.KeyId}: 孤儿条目");
                    }
                }
            }

            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void ContentTables_ExactlyMatchFormalAssetsAndHaveCompleteTranslations()
        {
            Dictionary<string, HashSet<string>> expectedKeys = new Dictionary<string, HashSet<string>>
            {
                ["items"] = new HashSet<string>(StringComparer.Ordinal),
                ["affixes"] = new HashSet<string>(StringComparer.Ordinal),
                ["monsters"] = new HashSet<string>(StringComparer.Ordinal),
                ["stats"] = new HashSet<string>(StringComparer.Ordinal),
            };
            List<string> violations = new List<string>();

            ValidateAssets<ItemBaseDefinition>(
                "Assets/Data/Preset/Items",
                "items",
                asset => $"item.{asset.Id}.name",
                asset => asset.LocalizedName,
                expectedKeys,
                violations);
            ValidateAssets<TagDefinition>(
                "Assets/Data/Preset/Tags",
                "items",
                asset => $"tag.{asset.Id}.name",
                asset => asset.LocalizedName,
                expectedKeys,
                violations);
            ValidateAssets<AffixDefinition>(
                "Assets/Data/Preset/Affixes",
                "affixes",
                asset => $"item_affix.{asset.Id}.name",
                asset => asset.LocalizedName,
                expectedKeys,
                violations);
            ValidateAssets<MonsterAffixDefinition>(
                "Assets/Data/Preset/MonsterAffixes",
                "affixes",
                asset => $"monster_affix.{asset.Id}.name",
                asset => asset.LocalizedName,
                expectedKeys,
                violations);
            ValidateAssets<MonsterDefinition>(
                "Assets/Data/Preset/Monsters",
                "monsters",
                asset => $"monster.{asset.Id}.name",
                asset => asset.LocalizedName,
                expectedKeys,
                violations);
            ValidateAssets<CharacterDefinition>(
                "Assets/Data/Preset/Actors",
                "monsters",
                asset => $"actor.{asset.Id}.name",
                asset => asset.LocalizedName,
                expectedKeys,
                violations);
            ValidateAssets<TraderDefinition>(
                "Assets/Data/Preset/Traders",
                "monsters",
                asset => $"trader.{asset.Id}.name",
                asset => asset.LocalizedName,
                expectedKeys,
                violations);
            ValidateAssets<StatDefinition>(
                "Assets/Data/Preset/Stats",
                "stats",
                asset => asset.Id,
                asset => asset.LocalizedName,
                expectedKeys,
                violations);
            ValidateInteractionPrefab(
                "Assets/Prefabs/World/Merchant.prefab",
                "interaction.merchant.name",
                expectedKeys,
                violations);
            ValidateInteractionPrefab(
                "Assets/Prefabs/World/CraftingStation.prefab",
                "interaction.crafting_station.name",
                expectedKeys,
                violations);

            ValidateTable("items", expectedKeys["items"], violations);
            ValidateTable("affixes", expectedKeys["affixes"], violations);
            ValidateTable("monsters", expectedKeys["monsters"], violations);
            ValidateTable("stats", expectedKeys["stats"], violations);

            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void FontChain_CoversChineseEnglishAndPseudoLocalizationCharacters()
        {
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Settings/UI/GamePanelSettings.asset");
            Assert.IsNotNull(panelSettings);
            TextSettings textSettings = panelSettings.textSettings;
            Assert.IsNotNull(textSettings, "GamePanelSettings 未绑定 TextSettings");
            FontAsset cjk = AssetDatabase.LoadAssetAtPath<FontAsset>(
                "Assets/Settings/UI/Fonts/GameCjkFont.asset");
            FontAsset latin = AssetDatabase.LoadAssetAtPath<FontAsset>(
                "Assets/Settings/UI/Fonts/GameLatinFont.asset");
            Assert.AreSame(cjk, textSettings.defaultFontAsset, "UI Toolkit 主字体不是秋水书体");
            CollectionAssert.Contains(textSettings.fallbackFontAssets, latin);
            Assert.AreEqual("QiushuiShotai", cjk.sourceFontFile.name);
            Assert.AreEqual("LiberationSans", latin.sourceFontFile.name);
            Assert.IsNotNull(cjk.material, "UI Toolkit 主字体缺少材质");
            Assert.IsNotNull(latin.material, "UI Toolkit 回退字体缺少材质");
            Assert.IsTrue(cjk.material.HasProperty("_TextureWidth"));
            Assert.IsTrue(latin.material.HasProperty("_TextureWidth"));

            StringBuilder characters = new StringBuilder();
            var pseudo = LocalizationEditorSettings.GetPseudoLocales()
                .Single(locale => locale.Identifier.Code == "qps-ploc");

            foreach (string tableName in new[]
                     {
                         "ui",
                         "system",
                         "items",
                         "stats",
                         "affixes",
                         "monsters",
                     })
            {
                StringTableCollection collection = LocalizationEditorSettings
                    .GetStringTableCollections()
                    .Single(candidate => candidate.TableCollectionName == tableName);

                foreach (StringTable table in collection.StringTables)
                {
                    foreach (StringTableEntry entry in table.Values)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.Value))
                        {
                            continue;
                        }

                        characters.Append(entry.Value);

                        if (table.LocaleIdentifier.Code == "en")
                        {
                            characters.Append(pseudo.GetPseudoString(entry.Value));
                        }
                    }
                }
            }

            string uniqueCharacters = new string(characters.ToString().Distinct().ToArray());
            bool covered = cjk.HasCharacters(
                uniqueCharacters,
                out uint[] missingCharacters,
                true,
                false);
            Assert.IsTrue(
                covered,
                    "字体链缺少字符：" + string.Join(
                        ", ",
                        (missingCharacters ?? Array.Empty<uint>())
                        .Select(character => $"U+{character:X4}")));

            TMP_FontAsset qiushui = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/QiushuiShotai SDF.asset");
            TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Assert.IsNotNull(qiushui);
            Assert.IsNotNull(liberation);
            CollectionAssert.Contains(qiushui.fallbackFontAssetTable, liberation);
            Assert.IsTrue(File.Exists("Assets/TextMesh Pro/Fonts/QiushuiShotai - OFL.txt"));
            Assert.IsTrue(File.Exists("Assets/TextMesh Pro/Fonts/QiushuiShotai - ATTRIBUTION.txt"));
        }

        [Test]
        public void MigratedRuntimeText_HasNoPlayerFacingChineseLiteralsOrHardcodedBindings()
        {
            List<string> violations = new List<string>();

            for (int pathIndex = 0; pathIndex < MigratedRuntimeTextPaths.Length; pathIndex++)
            {
                string path = MigratedRuntimeTextPaths[pathIndex];
                string[] lines = File.ReadAllLines(path);

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];

                    if (!line.Contains("Debug.", StringComparison.Ordinal)
                        && ChineseStringLiteralPattern.IsMatch(line))
                    {
                        violations.Add($"{path}:{lineIndex + 1}: {line.Trim()}");
                    }
                }
            }

            string promptController = File.ReadAllText(
                "Assets/Scripts/Runtime/Gameplay/UI/InteractionPromptController.cs");

            if (!promptController.Contains(
                "GetInteractBindingDisplayString()",
                StringComparison.Ordinal))
            {
                violations.Add("InteractionPromptController 未使用 Input System 绑定显示文本");
            }

            if (promptController.Contains("E / Y", StringComparison.Ordinal))
            {
                violations.Add("InteractionPromptController 仍包含硬编码 E / Y 提示");
            }

            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        static bool HasTextBinding(XElement element)
        {
            return element.Descendants().Any(binding =>
                binding.Name.LocalName == "LocalizedString"
                && binding.Attribute("property")?.Value == "text");
        }

        static void ValidateAssets<T>(
            string folder,
            string tableName,
            Func<T, string> expectedKey,
            Func<T, LocalizedContentReference> getReference,
            IReadOnlyDictionary<string, HashSet<string>> expectedKeys,
            ICollection<string> violations)
            where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                LocalizedContentReference reference = getReference(asset);
                string key = expectedKey(asset);
                ValidateReference(path, reference, tableName, key, violations);
                expectedKeys[tableName].Add(key);
            }
        }

        static void ValidateInteractionPrefab(
            string path,
            string expectedKey,
            IReadOnlyDictionary<string, HashSet<string>> expectedKeys,
            ICollection<string> violations)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            WorldInteractionTarget target = prefab != null
                ? prefab.GetComponentInChildren<WorldInteractionTarget>(true)
                : null;

            if (target == null)
            {
                violations.Add($"{path}: 缺少 WorldInteractionTarget");
                return;
            }

            ValidateReference(path, target.LocalizedName, "monsters", expectedKey, violations);
            expectedKeys["monsters"].Add(expectedKey);
        }

        static void ValidateReference(
            string path,
            LocalizedContentReference reference,
            string expectedTable,
            string expectedKey,
            ICollection<string> violations)
        {
            if (reference == null || !reference.IsValid)
            {
                violations.Add($"{path}: 本地化名称引用无效");
                return;
            }

            if (!string.Equals(reference.TableName, expectedTable, StringComparison.Ordinal)
                || !string.Equals(reference.EntryKey, expectedKey, StringComparison.Ordinal))
            {
                violations.Add(
                    $"{path}: 期望 {expectedTable}/{expectedKey}，实际 "
                    + $"{reference.TableName}/{reference.EntryKey}");
            }
        }

        static void ValidateTable(
            string tableName,
            IReadOnlyCollection<string> expectedKeys,
            ICollection<string> violations)
        {
            StringTableCollection collection = LocalizationEditorSettings
                .GetStringTableCollections()
                .Single(candidate => candidate.TableCollectionName == tableName);
            HashSet<string> actualKeys = collection.SharedData.Entries
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string missingKey in expectedKeys.Except(actualKeys))
            {
                violations.Add($"{tableName}/{missingKey}: 缺少共享键");
            }

            foreach (string unexpectedKey in actualKeys.Except(expectedKeys))
            {
                violations.Add($"{tableName}/{unexpectedKey}: 未被正式内容引用");
            }

            foreach (SharedTableData.SharedTableEntry shared in collection.SharedData.Entries)
            {
                foreach (StringTable table in collection.StringTables)
                {
                    StringTableEntry entry = table.GetEntry(shared.Id);

                    if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                    {
                        violations.Add(
                            $"{tableName}/{shared.Key}/{table.LocaleIdentifier.Code}: 缺失或空翻译");
                    }
                }
            }

            foreach (StringTable table in collection.StringTables)
            {
                foreach (StringTableEntry entry in table.Values)
                {
                    if (entry.SharedEntry == null)
                    {
                        violations.Add(
                            $"{tableName}/{table.LocaleIdentifier.Code}/{entry.KeyId}: 孤儿条目");
                    }
                }
            }
        }
    }
}
