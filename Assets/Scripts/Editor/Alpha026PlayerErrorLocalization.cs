using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace DarkFlare.Editor
{
    public static class Alpha026PlayerErrorLocalization
    {
        public const string MenuPath =
            "DarkFlare/Infrastructure/Apply alpha 0.2.6 Player Error Localization";

        static readonly LocalizedEntry[] Entries =
        {
            new LocalizedEntry(
                "flow.error.unhandled_exception",
                "An unexpected fatal error occurred. Please quit the application.",
                "发生了未预期的致命错误，请退出应用。"),
            new LocalizedEntry(
                "resource.error.invalid_reference",
                "A required resource reference is invalid.",
                "必要资源引用无效。"),
            new LocalizedEntry(
                "resource.error.owner_closed",
                "The resource request is no longer active.",
                "资源请求已不再有效。"),
            new LocalizedEntry(
                "resource.error.cancelled",
                "Resource loading was cancelled.",
                "资源加载已取消。"),
            new LocalizedEntry(
                "resource.error.type_mismatch",
                "A resource has an unexpected type.",
                "资源类型不符合预期。"),
            new LocalizedEntry(
                "resource.error.load_failed",
                "A required resource could not be loaded.",
                "无法加载必要资源。"),
            new LocalizedEntry(
                "resource.error.service_closed",
                "The resource service is unavailable.",
                "资源服务当前不可用。"),
        };

        public static IReadOnlyList<string> RequiredKeys => Entries
            .Select(entry => entry.Key)
            .ToArray();

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            StringTableCollection collection = LocalizationEditorSettings
                .GetStringTableCollection("ui");

            if (collection == null)
            {
                throw new InvalidOperationException("ui String Table Collection 不存在");
            }

            foreach (LocalizedEntry localizedEntry in Entries)
            {
                SharedTableData.SharedTableEntry sharedEntry =
                    collection.SharedData.GetEntry(localizedEntry.Key)
                    ?? collection.SharedData.AddKey(localizedEntry.Key);

                SetValue(collection, "en", sharedEntry.Id, localizedEntry.English);
                SetValue(collection, "zh-Hans", sharedEntry.Id, localizedEntry.Chinese);
            }

            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();

            List<string> issues = Validate();

            if (issues.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", issues));
            }
        }

        public static List<string> Validate()
        {
            List<string> issues = new List<string>();
            StringTableCollection collection = LocalizationEditorSettings
                .GetStringTableCollection("ui");

            if (collection == null)
            {
                issues.Add("ui String Table Collection 不存在");
                return issues;
            }

            foreach (LocalizedEntry localizedEntry in Entries)
            {
                SharedTableData.SharedTableEntry sharedEntry =
                    collection.SharedData.GetEntry(localizedEntry.Key);

                if (sharedEntry == null)
                {
                    issues.Add($"缺少玩家错误本地化键：{localizedEntry.Key}");
                    continue;
                }

                ValidateValue(collection, "en", sharedEntry.Id, localizedEntry.Key, issues);
                ValidateValue(collection, "zh-Hans", sharedEntry.Id, localizedEntry.Key, issues);
            }

            if (!LocalizationEditorSettings.GetPseudoLocales()
                .Any(locale => locale.Identifier.Code == "qps-ploc"))
            {
                issues.Add("缺少 qps-ploc 伪本地化语言");
            }

            return issues;
        }

        static void SetValue(
            StringTableCollection collection,
            string localeCode,
            long entryId,
            string value)
        {
            StringTable table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;

            if (table == null)
            {
                throw new InvalidOperationException($"缺少 ui/{localeCode} String Table");
            }

            StringTableEntry entry = table.GetEntry(entryId) ?? table.AddEntry(entryId, value);
            entry.Value = value;
            EditorUtility.SetDirty(table);
        }

        static void ValidateValue(
            StringTableCollection collection,
            string localeCode,
            long entryId,
            string key,
            List<string> issues)
        {
            StringTable table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            StringTableEntry entry = table?.GetEntry(entryId);

            if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
            {
                issues.Add($"玩家错误本地化缺失：{localeCode}:{key}");
            }
        }

        readonly struct LocalizedEntry
        {
            public LocalizedEntry(string key, string english, string chinese)
            {
                Key = key;
                English = english;
                Chinese = chinese;
            }

            public string Key { get; }

            public string English { get; }

            public string Chinese { get; }
        }
    }
}
