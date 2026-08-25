using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed class ManagedAddressableContract
    {
        public ManagedAddressableContract(
            string guid,
            string group,
            string address,
            Type assetType,
            params string[] labels)
        {
            Guid = guid;
            Group = group;
            Address = address;
            AssetType = assetType;
            Labels = labels;
        }

        public string Guid { get; }

        public string Group { get; }

        public string Address { get; }

        public Type AssetType { get; }

        public IReadOnlyList<string> Labels { get; }
    }

    public static class Alpha026AddressablesGovernance
    {
        public const string ApplicationGroup = "DarkFlare-Application";
        public const string SessionPrefabsGroup = "DarkFlare-Session-Prefabs";
        public const string SessionSpritesGroup = "DarkFlare-Session-Sprites";
        public const string AudioGroup = "DarkFlare-Audio";

        const string MenuRoot = "DarkFlare/Infrastructure/";

        static readonly ManagedAddressableContract[] ManagedContracts =
        {
            new ManagedAddressableContract(
                "4fe1306a12212b747b1e41fb8c4aa53f",
                ApplicationGroup,
                "infrastructure/audio/configuration",
                typeof(AudioServiceConfiguration),
                "df.application",
                "df.configuration"),
            new ManagedAddressableContract(
                "942c099ccdd7eed428f0ac0b1694f6ff",
                SessionPrefabsGroup,
                "gameplay/prefabs/player",
                typeof(GameObject),
                "df.session",
                "df.prefab"),
            new ManagedAddressableContract(
                "67b8ae84734f42f4792a4190aad88049",
                SessionPrefabsGroup,
                "gameplay/prefabs/monsters/basic",
                typeof(GameObject),
                "df.session",
                "df.prefab"),
            new ManagedAddressableContract(
                "f4600000000000000000000000000002",
                SessionPrefabsGroup,
                "gameplay/prefabs/monsters/swift",
                typeof(GameObject),
                "df.session",
                "df.prefab"),
            new ManagedAddressableContract(
                "f4600000000000000000000000000003",
                SessionPrefabsGroup,
                "gameplay/prefabs/monsters/heavy",
                typeof(GameObject),
                "df.session",
                "df.prefab"),
            new ManagedAddressableContract(
                "3b6e21dfbc7f54e4f9e0312efd6c6af5",
                SessionPrefabsGroup,
                "gameplay/prefabs/projectiles/default",
                typeof(GameObject),
                "df.session",
                "df.prefab"),
            new ManagedAddressableContract(
                "07152fe41aa52d740b4d1df2049f9f3d",
                SessionPrefabsGroup,
                "gameplay/prefabs/loot/pickup",
                typeof(GameObject),
                "df.session",
                "df.prefab"),
            new ManagedAddressableContract(
                "1a1e12f2b0381f54c829d3ef4165a2de",
                SessionSpritesGroup,
                "ui/icons/items/great-sword",
                typeof(Sprite),
                "df.session",
                "df.sprite"),
            new ManagedAddressableContract(
                "7ea0a2ad891e6cd4891051d02b338cf0",
                SessionSpritesGroup,
                "ui/icons/items/leather-armor",
                typeof(Sprite),
                "df.session",
                "df.sprite"),
            new ManagedAddressableContract(
                "9b427dc6b96c7bc4ba30998360436d86",
                SessionSpritesGroup,
                "ui/icons/items/war-axe",
                typeof(Sprite),
                "df.session",
                "df.sprite"),
            new ManagedAddressableContract(
                "b25638dc709c3184cabc93ae9de79ee2",
                SessionSpritesGroup,
                "ui/icons/items/plate-armor",
                typeof(Sprite),
                "df.session",
                "df.sprite"),
            new ManagedAddressableContract(
                "bf07871323670d148819cd1d7e079297",
                SessionSpritesGroup,
                "ui/icons/items/obsidian-ring",
                typeof(Sprite),
                "df.session",
                "df.sprite"),
            new ManagedAddressableContract(
                "d8c5c86107920ab4d9e117e19d6ed11e",
                SessionSpritesGroup,
                "ui/icons/items/jade-ring",
                typeof(Sprite),
                "df.session",
                "df.sprite"),
            new ManagedAddressableContract(
                "f597b10c126e825418717edee921bd16",
                SessionSpritesGroup,
                "ui/icons/items/iron-ring",
                typeof(Sprite),
                "df.session",
                "df.sprite"),
            new ManagedAddressableContract(
                "39f88bd4147a82945b01aaac74795ba3",
                AudioGroup,
                "audio/ui/confirm",
                typeof(AudioClip),
                "df.application",
                "df.audio"),
        };

        public static IReadOnlyList<ManagedAddressableContract> Contracts => ManagedContracts;

        [MenuItem(MenuRoot + "Apply alpha 0.2.6 Addressables Governance")]
        public static void Apply()
        {
            AddressableAssetSettings settings = RequireSettings();
            Dictionary<string, AddressableAssetGroup> groups = new Dictionary<string, AddressableAssetGroup>(
                StringComparer.Ordinal);
            List<AddressableAssetGroupSchema> templateSchemas = settings.DefaultGroup != null
                ? settings.DefaultGroup.Schemas.ToList()
                : null;

            foreach (string groupName in ManagedContracts.Select(contract => contract.Group).Distinct())
            {
                AddressableAssetGroup group = settings.FindGroup(groupName);

                if (group == null)
                {
                    Type[] schemaTypes = templateSchemas == null
                        ? new[] { typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema) }
                        : Array.Empty<Type>();
                    group = settings.CreateGroup(
                        groupName,
                        false,
                        false,
                        false,
                        templateSchemas,
                        schemaTypes);
                }

                groups.Add(groupName, group);
            }

            foreach (ManagedAddressableContract contract in ManagedContracts)
            {
                string path = AssetDatabase.GUIDToAssetPath(contract.Guid);

                if (string.IsNullOrEmpty(path))
                {
                    throw new InvalidOperationException($"Addressables 受管资源 GUID 不存在：{contract.Guid}");
                }

                AddressableAssetEntry entry = settings.CreateOrMoveEntry(
                    contract.Guid,
                    groups[contract.Group],
                    false,
                    false);

                if (entry == null)
                {
                    throw new InvalidOperationException($"无法创建 Addressables 条目：{path}");
                }

                entry.SetAddress(contract.Address, false);

                foreach (string existingLabel in entry.labels.ToArray())
                {
                    entry.SetLabel(existingLabel, false, false, false);
                }

                for (int i = 0; i < contract.Labels.Count; i++)
                {
                    entry.SetLabel(contract.Labels[i], true, true, false);
                }

                EditorUtility.SetDirty(entry.parentGroup);
            }

            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.BatchModification,
                ManagedContracts,
                true,
                true);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            List<string> issues = Validate();

            if (issues.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", issues));
            }

            Debug.Log($"alpha 0.2.6 Addressables 治理完成：{ManagedContracts.Length} 个条目");
        }

        [MenuItem(MenuRoot + "Validate alpha 0.2.6 Addressables Governance")]
        public static void ValidateFromMenu()
        {
            List<string> issues = Validate();

            if (issues.Count > 0)
            {
                Debug.LogError(string.Join("\n", issues));
                return;
            }

            Debug.Log($"alpha 0.2.6 Addressables 验证通过：{ManagedContracts.Length} 个条目");
        }

        public static List<string> Validate()
        {
            List<string> issues = new List<string>();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                issues.Add("AddressableAssetSettings 不存在");
                return issues;
            }

            HashSet<string> managedGuids = new HashSet<string>(
                ManagedContracts.Select(contract => contract.Guid),
                StringComparer.Ordinal);

            if (settings.DefaultGroup == null)
            {
                issues.Add("Addressables 默认组不存在");
            }
            else if (settings.DefaultGroup.entries.Count > 0)
            {
                issues.Add("Default Local Group 必须为空");
            }

            foreach (ManagedAddressableContract contract in ManagedContracts)
            {
                AddressableAssetGroup group = settings.FindGroup(contract.Group);

                if (group == null)
                {
                    issues.Add($"缺少 Addressables 分组：{contract.Group}");
                    continue;
                }

                AddressableAssetEntry entry = settings.FindAssetEntry(contract.Guid);

                if (entry == null)
                {
                    issues.Add($"缺少受管条目：{contract.Guid}");
                    continue;
                }

                if (!ReferenceEquals(entry.parentGroup, group))
                {
                    issues.Add($"条目分组错误：{contract.Guid} -> {entry.parentGroup?.Name}");
                }

                if (!string.Equals(entry.address, contract.Address, StringComparison.Ordinal))
                {
                    issues.Add($"条目地址错误：{contract.Guid} -> {entry.address}");
                }

                if (!IsCanonicalAddress(entry.address))
                {
                    issues.Add($"条目地址格式非法：{entry.address}");
                }

                for (int i = 0; i < contract.Labels.Count; i++)
                {
                    if (!entry.labels.Contains(contract.Labels[i]))
                    {
                        issues.Add($"条目缺少 Label：{contract.Address} -> {contract.Labels[i]}");
                    }
                }

                string path = AssetDatabase.GUIDToAssetPath(contract.Guid);

                if (string.IsNullOrEmpty(path))
                {
                    issues.Add($"条目资源缺失：{contract.Guid}");
                }
                else if (AssetDatabase.LoadAssetAtPath(path, contract.AssetType) == null)
                {
                    issues.Add($"条目类型错误：{contract.Address} 需要 {contract.AssetType.Name}");
                }
            }

            Dictionary<string, string> addressOwners = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.guid))
                    {
                        issues.Add($"分组 {group.Name} 含空 GUID 条目");
                        continue;
                    }

                    if (!addressOwners.TryAdd(entry.address, entry.guid)
                        && !string.Equals(addressOwners[entry.address], entry.guid, StringComparison.Ordinal))
                    {
                        issues.Add($"Addressables 地址重复：{entry.address}");
                    }
                }
            }

            if (managedGuids.Count != ManagedContracts.Length)
            {
                issues.Add("受管 Addressables GUID 存在重复");
            }

            return issues;
        }

        static AddressableAssetSettings RequireSettings()
        {
            return AddressableAssetSettingsDefaultObject.Settings
                ?? throw new InvalidOperationException("AddressableAssetSettings 不存在");
        }

        static bool IsCanonicalAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)
                || address.StartsWith("/", StringComparison.Ordinal)
                || address.EndsWith("/", StringComparison.Ordinal)
                || address.Contains("//", StringComparison.Ordinal))
            {
                return false;
            }

            for (int i = 0; i < address.Length; i++)
            {
                char character = address[i];

                if (!(character >= 'a' && character <= 'z')
                    && !(character >= '0' && character <= '9')
                    && character != '-'
                    && character != '/')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
