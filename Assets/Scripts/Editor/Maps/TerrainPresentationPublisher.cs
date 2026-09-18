using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DarkFlare.Editor
{
    public static class TerrainPresentationPublisher
    {
        public const string GroupName = "DarkFlare-Map-Terrain";

        public static void Publish(TerrainTextureSources sources, AddressableAssetSettings settings = null)
        {
            if (sources == null || sources.Presentation == null) { throw new ArgumentException("需要指定持久地表显示配置"); }
            TerrainTileCatalog compiled = sources.RequireCurrent();
            TerrainPresentationDefinition target = sources.Presentation;
            if (target.TerrainSet != sources.TerrainSet) { throw new InvalidOperationException("源与显示配置必须使用同一地表集"); }
            ValidateAddress(target, settings ?? AddressableAssetSettingsDefaultObject.Settings);
            TerrainTileCatalog previous = target.Tiles;
            var serialized = new SerializedObject(target);
            try
            {
                serialized.FindProperty("_tiles").objectReferenceValue = compiled;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                target.Validate();
                ValidateDependencies(target);
                AssetDatabase.SaveAssetIfDirty(target);
            }
            catch
            {
                serialized.Update(); serialized.FindProperty("_tiles").objectReferenceValue = previous;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(target);
                throw;
            }
        }

        public static void ValidatePublished(TerrainTextureSources sources, AddressableAssetSettings settings = null)
        {
            if (sources == null || sources.Presentation == null) { throw new ArgumentException("缺少源或显示配置"); }
            TerrainTileCatalog compiled = sources.RequireCurrent();
            TerrainPresentationDefinition target = sources.Presentation;
            if (target.TerrainSet != sources.TerrainSet || target.Tiles != compiled) { throw new InvalidOperationException("地表尚未发布当前编译修订"); }
            target.Validate();
            ValidateAddress(target, settings ?? AddressableAssetSettingsDefaultObject.Settings);
            ValidateDependencies(target);
        }

        public static List<ContentValidationIssue> Scan()
        {
            var issues = new List<ContentValidationIssue>();
            var owners = new HashSet<TerrainPresentationDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainTextureSources", new[] { "Assets" }))
            {
                var source = AssetDatabase.LoadAssetAtPath<TerrainTextureSources>(AssetDatabase.GUIDToAssetPath(guid));
                if (source.Presentation == null) { continue; }
                try
                {
                    if (!owners.Add(source.Presentation)) { throw new InvalidOperationException("同一地表显示配置只能有一个发布源"); }
                    ValidatePublished(source);
                }
                catch (Exception error) { issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, source, error.Message)); }
            }
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) { return issues; }
            foreach (AddressableAssetEntry entry in settings.groups.Where(group => group != null).SelectMany(group => group.entries))
            {
                var target = AssetDatabase.LoadAssetAtPath<TerrainPresentationDefinition>(entry.AssetPath);
                if (target != null && !owners.Contains(target)) { issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, target, "已登记 Addressables 的地表显示目录缺少发布源")); }
            }
            return issues;
        }

        static void ValidateAddress(TerrainPresentationDefinition target, AddressableAssetSettings settings)
        {
            string path = AssetDatabase.GetAssetPath(target);
            string guid = AssetDatabase.AssetPathToGUID(path);
            AddressableAssetEntry entry = settings != null ? settings.FindAssetEntry(guid) : null;
            if (entry == null || entry.parentGroup.Name != GroupName || entry.address != target.Address
                || !entry.labels.Contains("df.session") || !entry.labels.Contains("df.terrain"))
            {
                throw new InvalidOperationException("请登记地表显示配置的 Addressables 组 / 地址 / 标签：" + target.Address);
            }
            if (settings.groups.Where(group => group != null).SelectMany(group => group.entries).Count(item => item.address == target.Address) != 1)
            {
                throw new InvalidOperationException("地表资源地址重复：" + target.Address);
            }
        }

        static void ValidateDependencies(TerrainPresentationDefinition target)
        {
            // 尚未保存的 SO 修改不一定刷新 AssetDatabase 依赖图；显式检查待发布根。
            string[] roots = { AssetDatabase.GetAssetPath(target), AssetDatabase.GetAssetPath(target.Tiles), AssetDatabase.GetAssetPath(target.TerrainSet) };
            foreach (string path in AssetDatabase.GetDependencies(roots, true))
            {
                Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type == typeof(TerrainTextureSources) || path.Contains("/TerrainSources/") || path.Contains("/Development/"))
                {
                    throw new InvalidOperationException("运行时地表依赖包含编辑源或开发样例：" + path);
                }
            }
        }
    }
}
