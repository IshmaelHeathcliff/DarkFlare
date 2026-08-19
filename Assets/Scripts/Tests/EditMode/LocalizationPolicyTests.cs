using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace DarkFlare.Tests
{
    public sealed class LocalizationPolicyTests
    {
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

        static bool HasTextBinding(XElement element)
        {
            return element.Descendants().Any(binding =>
                binding.Name.LocalName == "LocalizedString"
                && binding.Attribute("property")?.Value == "text");
        }
    }
}
