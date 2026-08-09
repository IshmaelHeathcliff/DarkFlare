using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DarkFlare.Editor
{
    public static class VisualAssetSingleSpriteMigration
    {
        const string MenuRoot = "DarkFlare/美术资产迁移/";
        const string ManifestRelativePath = "tools/visual_slice/asset_pipeline_manifest.json";
        const int SupportedSchemaVersion = 1;
        const float FloatTolerance = 0.0001f;

        [MenuItem(MenuRoot + "1. 仅校验", false, 100)]
        public static void ValidateOnly()
        {
            ExecuteSafely("美术资产迁移校验", () =>
            {
                MigrationReport report = new MigrationReport();
                MigrationContext context = BuildContext(report);

                ValidateEditorState(report, false);

                if (context != null)
                {
                    ValidateManifestAssets(context, report, true);
                    FindLegacyDependencies(context, report);
                }

                report.Log("美术资产迁移校验");
            });
        }

        [MenuItem(MenuRoot + "2. 应用导入与引用迁移", false, 101)]
        public static void ApplyMigration()
        {
            ExecuteSafely("应用美术资产迁移", () =>
            {
                MigrationReport report = new MigrationReport();
                MigrationContext context = BuildContext(report);

                ValidateEditorState(report, true);

                if (context != null)
                {
                    ValidateManifestAssets(context, report, false);
                }

                if (report.HasErrors || context == null)
                {
                    report.Log("应用美术资产迁移：预检失败");
                    return;
                }

                ConfigureImporters(context, report);

                if (report.HasErrors)
                {
                    report.Log("应用美术资产迁移：导入失败");
                    return;
                }

                Dictionary<SpriteAssetKey, Sprite> replacements = LoadReplacements(context, report);

                if (report.HasErrors)
                {
                    report.Log("应用美术资产迁移：目标 Sprite 加载失败");
                    return;
                }

                ReplaceAnimationClipReferences(replacements, report);
                ReplaceScriptableObjectReferences(context, replacements, report);
                ReplacePrefabReferences(context, replacements, report);
                ReplaceSceneReferences(context, replacements, report);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ValidateManifestAssets(context, report, true);
                FindLegacyDependencies(context, report);
                report.Log("应用美术资产迁移");
            });
        }

        [MenuItem(MenuRoot + "3. 校验并删除旧图集", false, 102)]
        public static void VerifyAndDeleteLegacySheets()
        {
            ExecuteSafely("校验并删除旧图集", () =>
            {
                MigrationReport report = new MigrationReport();
                MigrationContext context = BuildContext(report);

                ValidateEditorState(report, true);

                if (context != null)
                {
                    ValidateManifestAssets(context, report, true);
                    FindLegacyDependencies(context, report);
                }

                if (report.HasErrors || context == null)
                {
                    report.Log("删除旧图集：校验失败，未删除任何资产");
                    return;
                }

                for (int i = 0; i < context.Manifest.LegacySheets.Length; i++)
                {
                    LegacySheetDto sheet = context.Manifest.LegacySheets[i];
                    string currentGuid = AssetDatabase.AssetPathToGUID(
                        sheet.AssetPath,
                        AssetPathToGUIDOptions.OnlyExistingAssets);

                    if (!string.Equals(currentGuid, sheet.Guid, StringComparison.Ordinal))
                    {
                        report.AddError($"删除前 GUID 复核失败：{sheet.AssetPath}");
                        break;
                    }
                }

                if (report.HasErrors)
                {
                    report.Log("删除旧图集：最终复核失败，未删除任何资产");
                    return;
                }

                string[] deletePaths = new string[context.Manifest.LegacySheets.Length];

                for (int i = 0; i < context.Manifest.LegacySheets.Length; i++)
                {
                    deletePaths[i] = context.Manifest.LegacySheets[i].AssetPath;
                }

                List<string> failedPaths = new List<string>();
                bool deleted = AssetDatabase.DeleteAssets(deletePaths, failedPaths);
                report.DeletedAssetCount = deletePaths.Length - failedPaths.Count;

                if (!deleted || failedPaths.Count > 0)
                {
                    report.AddError($"旧图集批量删除未完整成功：{string.Join(", ", failedPaths)}");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                for (int i = 0; i < context.Manifest.LegacySheets.Length; i++)
                {
                    LegacySheetDto sheet = context.Manifest.LegacySheets[i];

                    if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(
                        sheet.AssetPath,
                        AssetPathToGUIDOptions.OnlyExistingAssets)))
                    {
                        report.AddError($"删除后仍可解析旧图集：{sheet.AssetPath}");
                    }
                }

                report.Log("校验并删除旧图集");
            });
        }

        static void ExecuteSafely(string operationName, Action operation)
        {
            try
            {
                operation();
            }
            catch (Exception exception)
            {
                Debug.LogError($"{operationName}发生未处理异常，操作已停止。\n{exception}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static MigrationContext BuildContext(MigrationReport report)
        {
            string manifestPath = GetAbsoluteProjectPath(ManifestRelativePath);

            if (!File.Exists(manifestPath))
            {
                report.AddError($"找不到迁移清单：{ManifestRelativePath}");
                return null;
            }

            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
            MigrationManifestDto manifest = JsonUtility.FromJson<MigrationManifestDto>(json);

            if (manifest == null)
            {
                report.AddError("迁移清单无法反序列化");
                return null;
            }

            if (manifest.SchemaVersion != SupportedSchemaVersion)
            {
                report.AddError($"不支持的迁移清单版本：{manifest.SchemaVersion}");
            }

            if (manifest.Families.Length == 0)
            {
                report.AddError("迁移清单未定义任何美术家族合同");
            }

            if (manifest.LegacySheets.Length == 0)
            {
                report.AddError("迁移清单未定义旧图集白名单");
            }

            if (manifest.Mappings.Length != manifest.ExpectedMappingCount)
            {
                report.AddError(
                    $"映射数量不符合清单声明：声明 {manifest.ExpectedMappingCount}，实际 {manifest.Mappings.Length}");
            }

            Dictionary<string, FamilyContractDto> families = new Dictionary<string, FamilyContractDto>(StringComparer.Ordinal);

            for (int i = 0; i < manifest.Families.Length; i++)
            {
                FamilyContractDto family = manifest.Families[i];

                if (family == null || string.IsNullOrWhiteSpace(family.Id))
                {
                    report.AddError($"家族合同 {i} 缺少 id");
                    continue;
                }

                if (!families.TryAdd(family.Id, family))
                {
                    report.AddError($"家族合同 id 重复：{family.Id}");
                }

                ValidateContractValues(
                    family.Id,
                    family.CanvasWidth,
                    family.CanvasHeight,
                    family.PixelsPerUnit,
                    family.PivotX,
                    family.PivotY,
                    family.BorderLeft,
                    family.BorderBottom,
                    family.BorderRight,
                    family.BorderTop,
                    report);
            }

            Dictionary<string, LegacySheetDto> sheets = new Dictionary<string, LegacySheetDto>(StringComparer.Ordinal);
            HashSet<string> sheetPaths = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> sheetGuids = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < manifest.LegacySheets.Length; i++)
            {
                LegacySheetDto sheet = manifest.LegacySheets[i];

                if (sheet == null
                    || string.IsNullOrWhiteSpace(sheet.Id)
                    || string.IsNullOrWhiteSpace(sheet.AssetPath)
                    || string.IsNullOrWhiteSpace(sheet.Guid))
                {
                    report.AddError($"旧图集定义 {i} 不完整");
                    continue;
                }

                if (!sheets.TryAdd(sheet.Id, sheet))
                {
                    report.AddError($"旧图集 id 重复：{sheet.Id}");
                }

                if (!sheetPaths.Add(sheet.AssetPath))
                {
                    report.AddError($"旧图集路径重复：{sheet.AssetPath}");
                }

                if (!sheetGuids.Add(sheet.Guid))
                {
                    report.AddError($"旧图集 GUID 重复：{sheet.Guid}");
                }

                if (!IsAssetPngPath(sheet.AssetPath))
                {
                    report.AddError($"旧图集必须是 Assets 下的 PNG：{sheet.AssetPath}");
                }
            }

            Dictionary<SpriteAssetKey, SpriteMappingDto> mappings =
                new Dictionary<SpriteAssetKey, SpriteMappingDto>();
            List<VisualAssetImportContract> importContracts = new List<VisualAssetImportContract>();
            HashSet<string> targetPaths = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, int> mappingCountsBySheet = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < manifest.Mappings.Length; i++)
            {
                SpriteMappingDto mapping = manifest.Mappings[i];

                if (mapping == null)
                {
                    report.AddError($"Sprite 映射 {i} 为空");
                    continue;
                }

                if (!families.TryGetValue(mapping.FamilyId, out FamilyContractDto family))
                {
                    report.AddError($"Sprite 映射引用未知家族：{mapping.FamilyId}");
                    continue;
                }

                if (!sheets.TryGetValue(mapping.SheetId, out LegacySheetDto sheet))
                {
                    report.AddError($"Sprite 映射引用未知旧图集：{mapping.SheetId}");
                    continue;
                }

                if (!string.Equals(mapping.LegacySheetGuid, sheet.Guid, StringComparison.Ordinal))
                {
                    report.AddError($"Sprite 映射 GUID 与旧图集不一致：{mapping.LegacySpriteName}");
                }

                if (!IsAssetPngPath(mapping.TargetPath))
                {
                    report.AddError($"目标必须是 Assets 下的 PNG：{mapping.TargetPath}");
                    continue;
                }

                SpriteAssetKey key = new SpriteAssetKey(mapping.LegacySheetGuid, mapping.LegacyLocalId);

                if (!mappings.TryAdd(key, mapping))
                {
                    report.AddError($"旧 Sprite 键重复：{mapping.LegacySheetGuid}:{mapping.LegacyLocalId}");
                }

                if (!targetPaths.Add(mapping.TargetPath))
                {
                    report.AddError($"目标 PNG 路径重复：{mapping.TargetPath}");
                }
                else
                {
                    importContracts.Add(VisualAssetImportContract.FromFamily(mapping.TargetPath, family));
                }

                mappingCountsBySheet.TryGetValue(mapping.SheetId, out int mappingCount);
                mappingCountsBySheet[mapping.SheetId] = mappingCount + 1;
            }

            for (int i = 0; i < manifest.LegacySheets.Length; i++)
            {
                LegacySheetDto sheet = manifest.LegacySheets[i];

                if (sheet == null || string.IsNullOrWhiteSpace(sheet.Id))
                {
                    continue;
                }

                mappingCountsBySheet.TryGetValue(sheet.Id, out int mappingCount);

                if (mappingCount != sheet.ExpectedSpriteCount)
                {
                    report.AddError(
                        $"旧图集 {sheet.Id} 的映射数应为 {sheet.ExpectedSpriteCount}，实际为 {mappingCount}");
                }
            }

            for (int i = 0; i < manifest.StandaloneAssets.Length; i++)
            {
                StandaloneAssetDto standalone = manifest.StandaloneAssets[i];

                if (standalone == null || !IsAssetPngPath(standalone.Path))
                {
                    report.AddError($"独立资产定义 {i} 缺少有效 Assets PNG 路径");
                    continue;
                }

                ValidateContractValues(
                    standalone.Path,
                    standalone.CanvasWidth,
                    standalone.CanvasHeight,
                    standalone.PixelsPerUnit,
                    standalone.PivotX,
                    standalone.PivotY,
                    standalone.BorderLeft,
                    standalone.BorderBottom,
                    standalone.BorderRight,
                    standalone.BorderTop,
                    report);

                if (!TryParseWrapMode(standalone.WrapMode, out TextureWrapMode wrapMode))
                {
                    report.AddError($"独立资产 wrapMode 只支持 Clamp 或 Repeat：{standalone.Path}");
                    continue;
                }

                if (!targetPaths.Add(standalone.Path))
                {
                    report.AddError($"独立资产路径与其他合同重复：{standalone.Path}");
                    continue;
                }

                importContracts.Add(new VisualAssetImportContract(
                    standalone.Path,
                    standalone.CanvasWidth,
                    standalone.CanvasHeight,
                    standalone.PixelsPerUnit,
                    new Vector2(standalone.PivotX, standalone.PivotY),
                    new Vector4(
                        standalone.BorderLeft,
                        standalone.BorderBottom,
                        standalone.BorderRight,
                        standalone.BorderTop),
                    wrapMode,
                    standalone.AlphaRequired));
            }

            if (manifest.StandaloneAssets.Length == 0)
            {
                report.AddWarning("清单未定义 standaloneAssets；仅处理映射生成的新 Single Sprite");
            }

            return new MigrationContext(manifest, mappings, importContracts);
        }

        static void ValidateContractValues(
            string id,
            int width,
            int height,
            float pixelsPerUnit,
            float pivotX,
            float pivotY,
            float borderLeft,
            float borderBottom,
            float borderRight,
            float borderTop,
            MigrationReport report)
        {
            if (width <= 0 || height <= 0 || pixelsPerUnit <= 0f)
            {
                report.AddError($"导入合同尺寸或 PPU 无效：{id}");
            }

            if (pivotX < 0f || pivotX > 1f || pivotY < 0f || pivotY > 1f)
            {
                report.AddError($"导入合同 Pivot 超出 0..1：{id}");
            }

            if (borderLeft < 0f
                || borderBottom < 0f
                || borderRight < 0f
                || borderTop < 0f
                || borderLeft + borderRight > width
                || borderBottom + borderTop > height)
            {
                report.AddError($"导入合同 Border 无效：{id}");
            }
        }

        static void ValidateEditorState(MigrationReport report, bool requireCleanScenes)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.AddError("播放模式或播放模式切换期间不能执行迁移");
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                report.AddError("脚本编译或资产刷新期间不能执行迁移");
            }

            if (!requireCleanScenes)
            {
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                report.AddError("Prefab Stage 打开时不能执行迁移，请先关闭 Prefab Stage");
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.IsValid() && scene.isLoaded && string.IsNullOrEmpty(scene.path))
                {
                    report.AddError("存在未保存到 Assets 的临时场景，迁移已拒绝执行");
                }

                if (scene.isDirty)
                {
                    report.AddError($"场景存在未保存改动，迁移已拒绝执行：{scene.path}");
                }
            }
        }

        static void ValidateManifestAssets(
            MigrationContext context,
            MigrationReport report,
            bool requireImporterContract)
        {
            Dictionary<SpriteAssetKey, Sprite> legacySprites = new Dictionary<SpriteAssetKey, Sprite>();

            for (int i = 0; i < context.Manifest.LegacySheets.Length; i++)
            {
                LegacySheetDto sheet = context.Manifest.LegacySheets[i];
                string currentGuid = AssetDatabase.AssetPathToGUID(
                    sheet.AssetPath,
                    AssetPathToGUIDOptions.OnlyExistingAssets);

                if (!string.Equals(currentGuid, sheet.Guid, StringComparison.Ordinal))
                {
                    report.AddError($"旧图集不存在或 GUID 不匹配：{sheet.AssetPath}");
                    continue;
                }

                if (AssetImporter.GetAtPath(sheet.AssetPath) is not TextureImporter importer
                    || importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Multiple)
                {
                    report.AddError($"旧图集不再是 Multiple Sprite：{sheet.AssetPath}");
                }

                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sheet.AssetPath);
                int spriteCount = 0;

                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    if (assets[assetIndex] is not Sprite sprite)
                    {
                        continue;
                    }

                    spriteCount++;

                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                            sprite,
                            out string guid,
                            out long localId))
                    {
                        report.AddError($"无法取得旧 Sprite 文件标识：{sheet.AssetPath}/{sprite.name}");
                        continue;
                    }

                    legacySprites[new SpriteAssetKey(guid, localId)] = sprite;
                }

                if (spriteCount != sheet.ExpectedSpriteCount)
                {
                    report.AddError(
                        $"旧图集 Sprite 数量变化：{sheet.AssetPath}，期望 {sheet.ExpectedSpriteCount}，实际 {spriteCount}");
                }
            }

            foreach (KeyValuePair<SpriteAssetKey, SpriteMappingDto> entry in context.Mappings)
            {
                if (!legacySprites.TryGetValue(entry.Key, out Sprite sprite))
                {
                    report.AddError(
                        $"旧 Sprite 映射无法解析：{entry.Value.LegacySpriteName} ({entry.Key})");
                    continue;
                }

                if (!string.Equals(sprite.name, entry.Value.LegacySpriteName, StringComparison.Ordinal))
                {
                    report.AddError(
                        $"旧 Sprite 名称变化：清单 {entry.Value.LegacySpriteName}，实际 {sprite.name}");
                }
            }

            for (int i = 0; i < context.ImportContracts.Count; i++)
            {
                ValidateTargetAsset(context.ImportContracts[i], report, requireImporterContract);
            }
        }

        static void ValidateTargetAsset(
            VisualAssetImportContract contract,
            MigrationReport report,
            bool requireImporterContract)
        {
            if (!File.Exists(GetAbsoluteProjectPath(contract.Path)))
            {
                report.AddError($"目标 PNG 不存在：{contract.Path}");
                return;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(contract.Path);

            if (texture == null)
            {
                report.AddError($"目标 PNG 尚未被 Unity 导入：{contract.Path}");
                return;
            }

            if (texture.width != contract.CanvasWidth || texture.height != contract.CanvasHeight)
            {
                report.AddError(
                    $"目标画布尺寸错误：{contract.Path}，期望 {contract.CanvasWidth}x{contract.CanvasHeight}，实际 {texture.width}x{texture.height}");
            }

            if (AssetImporter.GetAtPath(contract.Path) is not TextureImporter importer)
            {
                report.AddError($"目标 PNG 缺少 TextureImporter：{contract.Path}");
                return;
            }

            if (requireImporterContract && !ImporterMatchesContract(importer, contract, out string mismatch))
            {
                report.AddError($"目标导入合同不一致：{contract.Path}（{mismatch}）");
            }

            if (requireImporterContract && AssetDatabase.LoadAssetAtPath<Sprite>(contract.Path) == null)
            {
                report.AddError($"目标 PNG 无法作为 Single Sprite 加载：{contract.Path}");
            }
        }

        static void ConfigureImporters(MigrationContext context, MigrationReport report)
        {
            for (int i = 0; i < context.ImportContracts.Count; i++)
            {
                VisualAssetImportContract contract = context.ImportContracts[i];

                if (AssetImporter.GetAtPath(contract.Path) is not TextureImporter importer)
                {
                    report.AddError($"无法配置目标 TextureImporter：{contract.Path}");
                    continue;
                }

                if (ImporterMatchesContract(importer, contract, out _))
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = contract.PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = contract.WrapMode;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = contract.AlphaRequired;
                importer.sRGBTexture = true;
                importer.isReadable = false;

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = contract.Pivot;
                settings.spriteBorder = contract.Border;
                settings.spritePixelsPerUnit = contract.PixelsPerUnit;
                settings.spriteExtrude = 1;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);

                AssetDatabase.WriteImportSettingsIfDirty(contract.Path);
                AssetDatabase.ImportAsset(
                    contract.Path,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                report.ImporterChangeCount++;

                TextureImporter reloadedImporter = AssetImporter.GetAtPath(contract.Path) as TextureImporter;

                if (reloadedImporter == null)
                {
                    report.AddError($"导入后无法重新加载 TextureImporter：{contract.Path}");
                }
                else if (!ImporterMatchesContract(reloadedImporter, contract, out string mismatch))
                {
                    report.AddError($"导入后合同仍不一致：{contract.Path}（{mismatch}）");
                }
            }
        }

        static bool ImporterMatchesContract(
            TextureImporter importer,
            VisualAssetImportContract contract,
            out string mismatch)
        {
            if (importer.textureType != TextureImporterType.Sprite)
            {
                mismatch = "Texture Type 不是 Sprite";
                return false;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                mismatch = "Sprite Mode 不是 Single";
                return false;
            }

            if (!Approximately(importer.spritePixelsPerUnit, contract.PixelsPerUnit))
            {
                mismatch = $"PPU 为 {importer.spritePixelsPerUnit}，应为 {contract.PixelsPerUnit}";
                return false;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                mismatch = "Filter Mode 不是 Point";
                return false;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.crunchedCompression)
            {
                mismatch = "纹理未保持无压缩";
                return false;
            }

            if (importer.mipmapEnabled)
            {
                mismatch = "Mip Map 未关闭";
                return false;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                mismatch = "NPOT 缩放未关闭";
                return false;
            }

            if (importer.wrapMode != contract.WrapMode)
            {
                mismatch = $"Wrap Mode 为 {importer.wrapMode}，应为 {contract.WrapMode}";
                return false;
            }

            if (importer.alphaSource != TextureImporterAlphaSource.FromInput
                || importer.alphaIsTransparency != contract.AlphaRequired)
            {
                mismatch = "Alpha 设置不符合合同";
                return false;
            }

            if (!importer.sRGBTexture || importer.isReadable)
            {
                mismatch = "sRGB 或 Read/Write 设置不符合合同";
                return false;
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                mismatch = "Sprite Mesh Type 不是 Full Rect";
                return false;
            }

            if (settings.spriteAlignment != (int)SpriteAlignment.Custom
                || !Approximately(settings.spritePivot, contract.Pivot))
            {
                mismatch = $"Pivot 为 {settings.spritePivot}，应为 {contract.Pivot}";
                return false;
            }

            if (!Approximately(settings.spriteBorder, contract.Border))
            {
                mismatch = $"Border 为 {settings.spriteBorder}，应为 {contract.Border}";
                return false;
            }

            if (settings.spriteGenerateFallbackPhysicsShape)
            {
                mismatch = "仍会生成 Fallback Physics Shape";
                return false;
            }

            mismatch = string.Empty;
            return true;
        }

        static Dictionary<SpriteAssetKey, Sprite> LoadReplacements(
            MigrationContext context,
            MigrationReport report)
        {
            Dictionary<SpriteAssetKey, Sprite> replacements = new Dictionary<SpriteAssetKey, Sprite>();

            foreach (KeyValuePair<SpriteAssetKey, SpriteMappingDto> entry in context.Mappings)
            {
                Sprite target = AssetDatabase.LoadAssetAtPath<Sprite>(entry.Value.TargetPath);

                if (target == null)
                {
                    report.AddError($"无法加载迁移目标 Sprite：{entry.Value.TargetPath}");
                    continue;
                }

                replacements.Add(entry.Key, target);
            }

            return replacements;
        }

        static void ReplaceAnimationClipReferences(
            IReadOnlyDictionary<SpriteAssetKey, Sprite> replacements,
            MigrationReport report)
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" });
            HashSet<AnimationClip> visited = new HashSet<AnimationClip>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    if (assets[assetIndex] is not AnimationClip clip || !visited.Add(clip))
                    {
                        continue;
                    }

                    EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                    bool clipChanged = false;

                    for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                    {
                        EditorCurveBinding binding = bindings[bindingIndex];
                        ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                        bool curveChanged = false;

                        for (int keyIndex = 0; keyIndex < keyframes.Length; keyIndex++)
                        {
                            if (!TryGetReplacement(keyframes[keyIndex].value, replacements, out Sprite replacement))
                            {
                                continue;
                            }

                            ObjectReferenceKeyframe keyframe = keyframes[keyIndex];
                            keyframe.value = replacement;
                            keyframes[keyIndex] = keyframe;
                            curveChanged = true;
                            clipChanged = true;
                            report.ReferenceChangeCount++;
                        }

                        if (curveChanged)
                        {
                            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
                        }
                    }

                    if (clipChanged)
                    {
                        EditorUtility.SetDirty(clip);
                        report.ChangedAssetCount++;
                    }
                }
            }
        }

        static void ReplaceScriptableObjectReferences(
            MigrationContext context,
            IReadOnlyDictionary<SpriteAssetKey, Sprite> replacements,
            MigrationReport report)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });
            HashSet<Object> visited = new HashSet<Object>();
            HashSet<string> legacyGuids = BuildLegacyGuidSet(context);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (!SerializedAssetContainsLegacyGuid(path, legacyGuids, report))
                {
                    continue;
                }

                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    if (assets[assetIndex] is not ScriptableObject asset
                        || !visited.Add(asset))
                    {
                        continue;
                    }

                    int changed = ReplaceSerializedObjectReferences(asset, replacements, report);

                    if (changed > 0)
                    {
                        EditorUtility.SetDirty(asset);
                        report.ChangedAssetCount++;
                    }
                }
            }
        }

        static void ReplacePrefabReferences(
            MigrationContext context,
            IReadOnlyDictionary<SpriteAssetKey, Sprite> replacements,
            MigrationReport report)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            HashSet<string> legacyGuids = BuildLegacyGuidSet(context);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (!SerializedAssetContainsLegacyGuid(path, legacyGuids, report))
                {
                    continue;
                }

                GameObject root = null;

                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    int changed = ReplaceHierarchyReferences(root, replacements, report, path);

                    if (changed > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);

                        if (!saved)
                        {
                            report.AddError($"Prefab 保存失败：{path}");
                            continue;
                        }

                        report.ChangedAssetCount++;
                    }
                }
                catch (Exception exception)
                {
                    report.AddError($"Prefab 迁移失败：{path}\n{exception.Message}");
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
        }

        static void ReplaceSceneReferences(
            MigrationContext context,
            IReadOnlyDictionary<SpriteAssetKey, Sprite> replacements,
            MigrationReport report)
        {
            SceneSetup[] initialSetup = EditorSceneManager.GetSceneManagerSetup();
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            HashSet<string> legacyGuids = BuildLegacyGuidSet(context);

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                    if (!SerializedAssetContainsLegacyGuid(path, legacyGuids, report))
                    {
                        continue;
                    }

                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    GameObject[] roots = scene.GetRootGameObjects();
                    int changed = 0;

                    for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    {
                        changed += ReplaceHierarchyReferences(roots[rootIndex], replacements, report, path);
                    }

                    if (changed <= 0)
                    {
                        continue;
                    }

                    EditorSceneManager.MarkSceneDirty(scene);

                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        report.AddError($"场景保存失败：{path}");
                        continue;
                    }

                    report.ChangedAssetCount++;
                }
            }
            catch (Exception exception)
            {
                report.AddError($"场景迁移失败：{exception.Message}");
            }
            finally
            {
                try
                {
                    EditorSceneManager.RestoreSceneManagerSetup(initialSetup);
                }
                catch (Exception exception)
                {
                    report.AddError($"迁移后无法恢复原场景布局：{exception.Message}");
                }
            }
        }

        static int ReplaceHierarchyReferences(
            GameObject root,
            IReadOnlyDictionary<SpriteAssetKey, Sprite> replacements,
            MigrationReport report,
            string assetPath)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            int changed = 0;

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];

                if (component == null)
                {
                    report.AddError($"发现 Missing Script，无法证明引用扫描完整：{assetPath}/{root.name}");
                    continue;
                }

                changed += ReplaceSerializedObjectReferences(component, replacements, report);
            }

            return changed;
        }

        static int ReplaceSerializedObjectReferences(
            Object target,
            IReadOnlyDictionary<SpriteAssetKey, Sprite> replacements,
            MigrationReport report)
        {
            try
            {
                SerializedObject serializedObject = new SerializedObject(target);
                SerializedProperty property = serializedObject.GetIterator();
                bool enterChildren = true;
                int changed = 0;
                bool isPrefabInstance = target is Component component
                    && PrefabUtility.IsPartOfPrefabInstance(component.gameObject);

                while (property.Next(enterChildren))
                {
                    enterChildren = true;

                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    if (isPrefabInstance && !property.prefabOverride)
                    {
                        continue;
                    }

                    if (!TryGetReplacement(property.objectReferenceValue, replacements, out Sprite replacement))
                    {
                        continue;
                    }

                    property.objectReferenceValue = replacement;
                    changed++;
                    report.ReferenceChangeCount++;
                }

                if (changed > 0)
                {
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }

                return changed;
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"序列化引用迁移失败：{AssetDatabase.GetAssetPath(target)}/{target.name}\n{exception.Message}");
                return 0;
            }
        }

        static bool TryGetReplacement(
            Object value,
            IReadOnlyDictionary<SpriteAssetKey, Sprite> replacements,
            out Sprite replacement)
        {
            replacement = null;

            if (value is not Sprite sprite
                || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    sprite,
                    out string guid,
                    out long localId))
            {
                return false;
            }

            return replacements.TryGetValue(new SpriteAssetKey(guid, localId), out replacement);
        }

        static HashSet<string> BuildLegacyGuidSet(MigrationContext context)
        {
            HashSet<string> guids = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < context.Manifest.LegacySheets.Length; i++)
            {
                guids.Add(context.Manifest.LegacySheets[i].Guid);
            }

            return guids;
        }

        static bool SerializedAssetContainsLegacyGuid(
            string assetPath,
            IReadOnlyCollection<string> legacyGuids,
            MigrationReport report)
        {
            string absolutePath = GetAbsoluteProjectPath(assetPath);

            try
            {
                using StreamReader reader = new StreamReader(absolutePath, Encoding.UTF8, true);
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    foreach (string guid in legacyGuids)
                    {
                        if (line.Contains(guid, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                report.AddWarning($"无法预筛选序列化资产，将执行完整引用扫描：{assetPath}（{exception.Message}）");
                return true;
            }

            return false;
        }

        static void FindLegacyDependencies(MigrationContext context, MigrationReport report)
        {
            HashSet<string> legacyPaths = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> legacyGuids = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < context.Manifest.LegacySheets.Length; i++)
            {
                legacyPaths.Add(context.Manifest.LegacySheets[i].AssetPath);
                legacyGuids.Add(context.Manifest.LegacySheets[i].Guid);
            }

            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];

                if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                    || legacyPaths.Contains(assetPath)
                    || AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);

                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    string dependency = dependencies[dependencyIndex];

                    if (!legacyPaths.Contains(dependency))
                    {
                        continue;
                    }

                    string key = $"{assetPath}|{dependency}";

                    if (reported.Add(key))
                    {
                        report.AddError($"仍依赖旧图集：{assetPath} -> {dependency}");
                    }
                }
            }

            FindSerializedGuidTextReferences(legacyGuids, report);
        }

        static void FindSerializedGuidTextReferences(
            IReadOnlyCollection<string> legacyGuids,
            MigrationReport report)
        {
            string[] serializedExtensions =
            {
                ".anim",
                ".asset",
                ".controller",
                ".mat",
                ".overridecontroller",
                ".playable",
                ".prefab",
                ".spriteatlas",
                ".unity",
                ".uss",
                ".uxml",
            };
            HashSet<string> extensions = new HashSet<string>(serializedExtensions, StringComparer.OrdinalIgnoreCase);
            string[] files = Directory.GetFiles(Application.dataPath, "*", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]);

                if (!extensions.Contains(extension))
                {
                    continue;
                }

                string content;

                try
                {
                    content = File.ReadAllText(files[i], Encoding.UTF8);
                }
                catch (Exception exception)
                {
                    report.AddWarning($"无法读取序列化资产进行 GUID 复核：{files[i]}（{exception.Message}）");
                    continue;
                }

                foreach (string guid in legacyGuids)
                {
                    if (!content.Contains(guid, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string relativePath = $"Assets{files[i].Substring(Application.dataPath.Length).Replace('\\', '/')}";
                    report.AddError($"序列化文本仍包含旧图集 GUID：{relativePath} -> {guid}");
                }
            }
        }

        static bool TryParseWrapMode(string value, out TextureWrapMode wrapMode)
        {
            if (string.IsNullOrWhiteSpace(value)
                || string.Equals(value, "Clamp", StringComparison.OrdinalIgnoreCase))
            {
                wrapMode = TextureWrapMode.Clamp;
                return true;
            }

            if (string.Equals(value, "Repeat", StringComparison.OrdinalIgnoreCase))
            {
                wrapMode = TextureWrapMode.Repeat;
                return true;
            }

            wrapMode = TextureWrapMode.Clamp;
            return false;
        }

        static bool IsAssetPngPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.StartsWith("Assets/", StringComparison.Ordinal)
                && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("..", StringComparison.Ordinal);
        }

        static string GetAbsoluteProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("无法解析 Unity 项目根目录");
            }

            return Path.GetFullPath(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        static bool Approximately(float left, float right)
        {
            return Math.Abs(left - right) <= FloatTolerance;
        }

        static bool Approximately(Vector2 left, Vector2 right)
        {
            return Approximately(left.x, right.x) && Approximately(left.y, right.y);
        }

        static bool Approximately(Vector4 left, Vector4 right)
        {
            return Approximately(left.x, right.x)
                && Approximately(left.y, right.y)
                && Approximately(left.z, right.z)
                && Approximately(left.w, right.w);
        }

        readonly struct SpriteAssetKey : IEquatable<SpriteAssetKey>
        {
            readonly string _guid;
            readonly long _localId;

            public SpriteAssetKey(string guid, long localId)
            {
                _guid = guid;
                _localId = localId;
            }

            public bool Equals(SpriteAssetKey other)
            {
                return _localId == other._localId
                    && string.Equals(_guid, other._guid, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is SpriteAssetKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_guid != null ? StringComparer.Ordinal.GetHashCode(_guid) : 0) * 397)
                        ^ _localId.GetHashCode();
                }
            }

            public override string ToString()
            {
                return $"{_guid}:{_localId}";
            }
        }

        sealed class VisualAssetImportContract
        {
            public string Path { get; }

            public int CanvasWidth { get; }

            public int CanvasHeight { get; }

            public float PixelsPerUnit { get; }

            public Vector2 Pivot { get; }

            public Vector4 Border { get; }

            public TextureWrapMode WrapMode { get; }

            public bool AlphaRequired { get; }

            public VisualAssetImportContract(
                string path,
                int canvasWidth,
                int canvasHeight,
                float pixelsPerUnit,
                Vector2 pivot,
                Vector4 border,
                TextureWrapMode wrapMode,
                bool alphaRequired)
            {
                Path = path;
                CanvasWidth = canvasWidth;
                CanvasHeight = canvasHeight;
                PixelsPerUnit = pixelsPerUnit;
                Pivot = pivot;
                Border = border;
                WrapMode = wrapMode;
                AlphaRequired = alphaRequired;
            }

            public static VisualAssetImportContract FromFamily(string path, FamilyContractDto family)
            {
                return new VisualAssetImportContract(
                    path,
                    family.CanvasWidth,
                    family.CanvasHeight,
                    family.PixelsPerUnit,
                    new Vector2(family.PivotX, family.PivotY),
                    new Vector4(
                        family.BorderLeft,
                        family.BorderBottom,
                        family.BorderRight,
                        family.BorderTop),
                    TextureWrapMode.Clamp,
                    true);
            }
        }

        sealed class MigrationContext
        {
            public MigrationManifestDto Manifest { get; }

            public IReadOnlyDictionary<SpriteAssetKey, SpriteMappingDto> Mappings { get; }

            public IReadOnlyList<VisualAssetImportContract> ImportContracts { get; }

            public MigrationContext(
                MigrationManifestDto manifest,
                Dictionary<SpriteAssetKey, SpriteMappingDto> mappings,
                List<VisualAssetImportContract> importContracts)
            {
                Manifest = manifest;
                Mappings = mappings;
                ImportContracts = importContracts;
            }
        }

        sealed class MigrationReport
        {
            const int DetailLimit = 80;

            readonly List<string> _errors = new List<string>();
            readonly List<string> _warnings = new List<string>();

            public bool HasErrors => _errors.Count > 0;

            public int ImporterChangeCount { get; set; }

            public int ReferenceChangeCount { get; set; }

            public int ChangedAssetCount { get; set; }

            public int DeletedAssetCount { get; set; }

            public void AddError(string message)
            {
                _errors.Add(message);
            }

            public void AddWarning(string message)
            {
                _warnings.Add(message);
            }

            public void Log(string title)
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine(title);
                builder.AppendLine(
                    $"结果：错误 {_errors.Count}，警告 {_warnings.Count}，Importer 更新 {ImporterChangeCount}，引用替换 {ReferenceChangeCount}，变更资产 {ChangedAssetCount}，删除资产 {DeletedAssetCount}");
                AppendDetails(builder, "错误", _errors);
                AppendDetails(builder, "警告", _warnings);

                if (HasErrors)
                {
                    Debug.LogError(builder.ToString());
                }
                else
                {
                    Debug.Log(builder.ToString());
                }
            }

            static void AppendDetails(StringBuilder builder, string label, IReadOnlyList<string> details)
            {
                int count = Math.Min(details.Count, DetailLimit);

                for (int i = 0; i < count; i++)
                {
                    builder.AppendLine($"{label} {i + 1}：{details[i]}");
                }

                if (details.Count > DetailLimit)
                {
                    builder.AppendLine($"{label}其余 {details.Count - DetailLimit} 项已省略");
                }
            }
        }

        [Serializable]
        sealed class MigrationManifestDto
        {
            // JSON wire fields retain the manifest's lowerCamelCase names.
            [SerializeField]
            int schemaVersion;

            [SerializeField]
            int expectedMappingCount;

            [SerializeField]
            FamilyContractDto[] families;

            [SerializeField]
            LegacySheetDto[] legacySheets;

            [SerializeField]
            SpriteMappingDto[] mappings;

            [SerializeField]
            StandaloneAssetDto[] standaloneAssets;

            public int SchemaVersion => schemaVersion;

            public int ExpectedMappingCount => expectedMappingCount;

            public FamilyContractDto[] Families => families ?? Array.Empty<FamilyContractDto>();

            public LegacySheetDto[] LegacySheets => legacySheets ?? Array.Empty<LegacySheetDto>();

            public SpriteMappingDto[] Mappings => mappings ?? Array.Empty<SpriteMappingDto>();

            public StandaloneAssetDto[] StandaloneAssets => standaloneAssets ?? Array.Empty<StandaloneAssetDto>();
        }

        [Serializable]
        sealed class FamilyContractDto
        {
            [SerializeField]
            string id;

            [SerializeField]
            int canvasWidth;

            [SerializeField]
            int canvasHeight;

            [SerializeField]
            float pixelsPerUnit;

            [SerializeField]
            float pivotX;

            [SerializeField]
            float pivotY;

            [SerializeField]
            float borderLeft;

            [SerializeField]
            float borderBottom;

            [SerializeField]
            float borderRight;

            [SerializeField]
            float borderTop;

            public string Id => id;

            public int CanvasWidth => canvasWidth;

            public int CanvasHeight => canvasHeight;

            public float PixelsPerUnit => pixelsPerUnit;

            public float PivotX => pivotX;

            public float PivotY => pivotY;

            public float BorderLeft => borderLeft;

            public float BorderBottom => borderBottom;

            public float BorderRight => borderRight;

            public float BorderTop => borderTop;
        }

        [Serializable]
        sealed class LegacySheetDto
        {
            [SerializeField]
            string id;

            [SerializeField]
            string assetPath;

            [SerializeField]
            string guid;

            [SerializeField]
            int expectedSpriteCount;

            public string Id => id;

            public string AssetPath => assetPath;

            public string Guid => guid;

            public int ExpectedSpriteCount => expectedSpriteCount;
        }

        [Serializable]
        sealed class SpriteMappingDto
        {
            [SerializeField]
            string sheetId;

            [SerializeField]
            string familyId;

            [SerializeField]
            string legacySheetGuid;

            [SerializeField]
            long legacyLocalId;

            [SerializeField]
            string legacySpriteName;

            [SerializeField]
            string targetPath;

            [SerializeField]
            bool allowUnreferenced;

            public string SheetId => sheetId;

            public string FamilyId => familyId;

            public string LegacySheetGuid => legacySheetGuid;

            public long LegacyLocalId => legacyLocalId;

            public string LegacySpriteName => legacySpriteName;

            public string TargetPath => targetPath;

            public bool AllowUnreferenced => allowUnreferenced;
        }

        [Serializable]
        sealed class StandaloneAssetDto
        {
            [SerializeField]
            string path;

            [SerializeField]
            int canvasWidth;

            [SerializeField]
            int canvasHeight;

            [SerializeField]
            float pixelsPerUnit;

            [SerializeField]
            float pivotX;

            [SerializeField]
            float pivotY;

            [SerializeField]
            float borderLeft;

            [SerializeField]
            float borderBottom;

            [SerializeField]
            float borderRight;

            [SerializeField]
            float borderTop;

            [SerializeField]
            string wrapMode;

            [SerializeField]
            bool alphaRequired;

            public string Path => path;

            public int CanvasWidth => canvasWidth;

            public int CanvasHeight => canvasHeight;

            public float PixelsPerUnit => pixelsPerUnit;

            public float PivotX => pivotX;

            public float PivotY => pivotY;

            public float BorderLeft => borderLeft;

            public float BorderBottom => borderBottom;

            public float BorderRight => borderRight;

            public float BorderTop => borderTop;

            public string WrapMode => wrapMode;

            public bool AlphaRequired => alphaRequired;
        }
    }
}
