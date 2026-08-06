using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed class ConfigCenterWindow : OdinMenuEditorWindow
    {
        const string ConfigMenuPrefix = "DarkFlare/Data/";
        const string DefaultConfigRoot = "Assets/Data/Preset";

        [MenuItem("DarkFlare/配置中心")]
        public static void Open()
        {
            ConfigCenterWindow window = GetWindow<ConfigCenterWindow>();
            window.titleContent = new GUIContent("配置中心");
            window.Show();
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            OdinMenuTree tree = new OdinMenuTree
            {
                DefaultMenuStyle =
                {
                    IconSize = 18.00f
                }
            };

            List<ConfigTypeInfo> configTypes = FindConfigTypes();
            tree.Add("配置概览", new ConfigOverviewPage(configTypes));
            tree.Add("内容校验", new ContentValidationPage(OpenAssetForEdit));

            for (int i = 0; i < configTypes.Count; i++)
            {
                ConfigTypeInfo configType = configTypes[i];
                List<ScriptableObject> assets = FindAssets(configType.Type);
                ConfigTypePage typePage = new ConfigTypePage(configType, assets, ForceMenuTreeRebuild, OpenAssetForEdit);
                string typePath = $"按类型/{configType.DisplayName}";

                tree.Add(typePath, typePage);

                for (int j = 0; j < assets.Count; j++)
                {
                    ScriptableObject asset = assets[j];
                    tree.Add($"{typePath}/{asset.name}", asset);
                }
            }

            return tree;
        }

        void OpenAssetForEdit(ScriptableObject asset)
        {
            if (asset == null || MenuTree == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            OdinMenuItem menuItem = MenuTree
                .EnumerateTree(true)
                .FirstOrDefault(item => ReferenceEquals(item.Value, asset));

            if (menuItem == null)
            {
                return;
            }

            menuItem.Select(false);
            MenuTree.ScrollToMenuItem(menuItem, true);
            Repaint();
        }

        static List<ConfigTypeInfo> FindConfigTypes()
        {
            List<ConfigTypeInfo> result = new List<ConfigTypeInfo>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types = GetTypesSafely(assemblies[i]);

                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];

                    if (!IsCreatableConfigType(type))
                    {
                        continue;
                    }

                    CreateAssetMenuAttribute createMenu = type.GetCustomAttribute<CreateAssetMenuAttribute>();
                    result.Add(new ConfigTypeInfo(type, createMenu.menuName, createMenu.fileName));
                }
            }

            return result
                .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
                .ToList();
        }

        static Type[] GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null).ToArray();
            }
        }

        static bool IsCreatableConfigType(Type type)
        {
            if (type == null || type.IsAbstract || !typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return false;
            }

            CreateAssetMenuAttribute createMenu = type.GetCustomAttribute<CreateAssetMenuAttribute>();

            return createMenu != null
                && !string.IsNullOrWhiteSpace(createMenu.menuName)
                && createMenu.menuName.StartsWith(ConfigMenuPrefix, StringComparison.Ordinal);
        }

        static List<ScriptableObject> FindAssets(Type type)
        {
            string[] guids = AssetDatabase.FindAssets($"t:{type.Name}", new[] { "Assets" });
            List<ScriptableObject> result = new List<ScriptableObject>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;

                if (asset != null && asset.GetType() == type)
                {
                    result.Add(asset);
                }
            }

            return result
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .ToList();
        }

        static string GetDefaultFolder(ConfigTypeInfo configType, IReadOnlyList<ScriptableObject> assets)
        {
            if (assets.Count > 0)
            {
                string firstAssetPath = AssetDatabase.GetAssetPath(assets[0]);
                string directory = Path.GetDirectoryName(firstAssetPath);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    return NormalizePath(directory);
                }
            }

            string relativeMenuPath = configType.MenuName.Substring(ConfigMenuPrefix.Length);
            string category = relativeMenuPath.Split('/')[0];

            if (string.IsNullOrWhiteSpace(category))
            {
                return DefaultConfigRoot;
            }

            return $"{DefaultConfigRoot}/{category}";
        }

        static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        static void EnsureFolder(string folder)
        {
            string[] parts = NormalizePath(folder).Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        static string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string result = fileName;

            for (int i = 0; i < invalidChars.Length; i++)
            {
                result = result.Replace(invalidChars[i], '_');
            }

            return string.IsNullOrWhiteSpace(result) ? "NewConfig" : result;
        }

        sealed class ContentValidationPage
        {
            readonly Action<ScriptableObject> _openAsset;

            [ShowInInspector]
            [ReadOnly]
            [LabelText("校验状态")]
            public string Status => ErrorCount == 0 && WarningCount == 0
                ? "通过：未发现内容配置问题"
                : $"发现 {ErrorCount} 个错误，{WarningCount} 个警告";

            [ShowInInspector]
            [ReadOnly]
            [LabelText("错误")]
            public int ErrorCount => Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error);

            [ShowInInspector]
            [ReadOnly]
            [LabelText("警告")]
            public int WarningCount => Issues.Count(issue => issue.Severity == ContentValidationSeverity.Warning);

            [ShowInInspector]
            [TableList(AlwaysExpanded = true)]
            [LabelText("问题列表")]
            public List<ContentValidationEntry> Issues { get; private set; }

            public ContentValidationPage(Action<ScriptableObject> openAsset)
            {
                _openAsset = openAsset;
                Rescan();
            }

            [Button("重新扫描", ButtonSizes.Large)]
            public void Rescan()
            {
                List<ContentValidationIssue> issues = ContentConfigurationValidator.Scan();
                Issues = new List<ContentValidationEntry>(issues.Count);

                for (int i = 0; i < issues.Count; i++)
                {
                    Issues.Add(new ContentValidationEntry(issues[i], _openAsset));
                }
            }
        }

        [Serializable]
        [HideReferenceObjectPicker]
        sealed class ContentValidationEntry
        {
            [SerializeField]
            [HideInInspector]
            UnityEngine.Object _asset;

            readonly Action<ScriptableObject> _openAsset;

            [ShowInInspector]
            [ReadOnly]
            [LabelText("级别")]
            public ContentValidationSeverity Severity { get; }

            [ShowInInspector]
            [ReadOnly]
            [LabelText("资产")]
            public string AssetName => _asset != null ? _asset.name : "全局";

            [ShowInInspector]
            [ReadOnly]
            [LabelText("路径")]
            public string Path { get; }

            [ShowInInspector]
            [ReadOnly]
            [LabelText("原因")]
            public string Message { get; }

            public ContentValidationEntry(
                ContentValidationIssue issue,
                Action<ScriptableObject> openAsset)
            {
                _asset = issue.Asset;
                _openAsset = openAsset;
                Severity = issue.Severity;
                Path = issue.AssetPath;
                Message = issue.Message;
            }

            [HorizontalGroup("操作")]
            [Button("打开")]
            public void Open()
            {
                if (_asset is ScriptableObject scriptableObject)
                {
                    _openAsset?.Invoke(scriptableObject);
                    return;
                }

                Ping();
            }

            [HorizontalGroup("操作")]
            [Button("定位")]
            public void Ping()
            {
                if (_asset == null)
                {
                    return;
                }

                Selection.activeObject = _asset;
                EditorGUIUtility.PingObject(_asset);
            }
        }

        sealed class ConfigTypeInfo
        {
            public Type Type { get; }

            public string MenuName { get; }

            public string FileName { get; }

            public string DisplayName { get; }

            public ConfigTypeInfo(Type type, string menuName, string fileName)
            {
                Type = type;
                MenuName = menuName;
                FileName = fileName;
                DisplayName = ObjectNames.NicifyVariableName(type.Name);
            }
        }

        sealed class ConfigOverviewPage
        {
            readonly List<ConfigTypeInfo> _configTypes;

            [ShowInInspector]
            [ReadOnly]
            [LabelText("配置类型数量")]
            public int TypeCount => _configTypes.Count;

            [ShowInInspector]
            [ReadOnly]
            [TableList(AlwaysExpanded = true)]
            [LabelText("配置类型")]
            public List<ConfigTypeSummary> Types { get; }

            public ConfigOverviewPage(List<ConfigTypeInfo> configTypes)
            {
                _configTypes = configTypes;
                Types = new List<ConfigTypeSummary>();

                for (int i = 0; i < configTypes.Count; i++)
                {
                    ConfigTypeInfo configType = configTypes[i];
                    Types.Add(new ConfigTypeSummary(configType.DisplayName, configType.MenuName, FindAssets(configType.Type).Count));
                }
            }
        }

        sealed class ConfigTypeSummary
        {
            [ShowInInspector]
            [ReadOnly]
            [LabelText("类型")]
            public string DisplayName { get; }

            [ShowInInspector]
            [ReadOnly]
            [LabelText("菜单")]
            public string MenuName { get; }

            [ShowInInspector]
            [ReadOnly]
            [LabelText("资产数量")]
            public int AssetCount { get; }

            public ConfigTypeSummary(string displayName, string menuName, int assetCount)
            {
                DisplayName = displayName;
                MenuName = menuName;
                AssetCount = assetCount;
            }
        }

        sealed class ConfigTypePage
        {
            readonly ConfigTypeInfo _configType;
            readonly List<ScriptableObject> _assets;
            readonly Action _rebuildMenuTree;

            [ShowInInspector]
            [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = false, HideAddButton = true, HideRemoveButton = true)]
            [LabelText("已有资产")]
            List<ConfigAssetEntry> _assetEntries;

            [SerializeField]
            [LabelText("新建名称")]
            string _newAssetName;

            [ShowInInspector]
            [ReadOnly]
            [LabelText("配置类型")]
            public string DisplayName => _configType.DisplayName;

            [ShowInInspector]
            [ReadOnly]
            [LabelText("CreateAssetMenu")]
            public string MenuName => _configType.MenuName;

            [ShowInInspector]
            [ReadOnly]
            [LabelText("默认创建目录")]
            public string CreateFolder { get; }

            [ShowInInspector]
            [ReadOnly]
            [LabelText("已有资产数量")]
            public int AssetCount => _assets.Count;

            public ConfigTypePage(ConfigTypeInfo configType, List<ScriptableObject> assets, Action rebuildMenuTree, Action<ScriptableObject> openAsset)
            {
                _configType = configType;
                _assets = assets;
                _rebuildMenuTree = rebuildMenuTree;
                CreateFolder = GetDefaultFolder(configType, assets);
                _newAssetName = GetDefaultAssetName(configType);
                _assetEntries = new List<ConfigAssetEntry>();

                for (int i = 0; i < assets.Count; i++)
                {
                    ScriptableObject asset = assets[i];
                    _assetEntries.Add(new ConfigAssetEntry(asset, rebuildMenuTree, openAsset));
                }
            }

            [Button("创建配置", ButtonSizes.Large)]
            public void CreateConfig()
            {
                EnsureFolder(CreateFolder);

                string assetName = SanitizeFileName(_newAssetName);
                string path = AssetDatabase.GenerateUniqueAssetPath($"{CreateFolder}/{assetName}.asset");
                ScriptableObject asset = ScriptableObject.CreateInstance(_configType.Type);

                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                _rebuildMenuTree?.Invoke();
            }

            static string GetDefaultAssetName(ConfigTypeInfo configType)
            {
                if (!string.IsNullOrWhiteSpace(configType.FileName))
                {
                    return configType.FileName;
                }

                return $"New{configType.Type.Name}";
            }
        }

        [Serializable]
        [HideReferenceObjectPicker]
        sealed class ConfigAssetEntry
        {
            [SerializeField]
            [HideInInspector]
            ScriptableObject _asset;

            readonly Action _rebuildMenuTree;
            readonly Action<ScriptableObject> _openAsset;

            [SerializeField]
            [LabelText("新名称")]
            string _assetName;

            [ShowInInspector]
            [ReadOnly]
            [LabelText("名称")]
            public string Name => _asset != null ? _asset.name : string.Empty;

            [ShowInInspector]
            [ReadOnly]
            [LabelText("路径")]
            public string Path => _asset != null ? AssetDatabase.GetAssetPath(_asset) : string.Empty;

            public ConfigAssetEntry(ScriptableObject asset, Action rebuildMenuTree, Action<ScriptableObject> openAsset)
            {
                _asset = asset;
                _rebuildMenuTree = rebuildMenuTree;
                _openAsset = openAsset;
                _assetName = asset != null ? asset.name : string.Empty;
            }

            [HorizontalGroup("操作")]
            [Button("打开编辑")]
            public void OpenEdit()
            {
                if (_asset == null)
                {
                    return;
                }

                _openAsset?.Invoke(_asset);
            }

            [HorizontalGroup("操作")]
            [Button("定位")]
            public void Ping()
            {
                if (_asset == null)
                {
                    return;
                }

                Selection.activeObject = _asset;
                EditorGUIUtility.PingObject(_asset);
            }

            [HorizontalGroup("操作")]
            [Button("应用重命名")]
            public void Rename()
            {
                if (_asset == null)
                {
                    return;
                }

                string sanitizedName = SanitizeFileName(_assetName);

                if (sanitizedName == _asset.name)
                {
                    return;
                }

                string path = AssetDatabase.GetAssetPath(_asset);
                string error = AssetDatabase.RenameAsset(path, sanitizedName);

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"重命名配置失败：{error}");
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _assetName = _asset.name;
                _rebuildMenuTree?.Invoke();
            }
        }
    }
}
