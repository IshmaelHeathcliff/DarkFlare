using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed class Alpha017EffectAssetPostprocessor : AssetPostprocessor
    {
        const string ProjectileRoot = "Assets/Art/Sprites/Effects/Projectile/Arcane/";
        const string HitRoot = "Assets/Art/Sprites/Effects/Hit/";
        const float PixelsPerUnit = 64f;

        static readonly string[] EffectRoots =
        {
            "Assets/Art/Sprites/Effects/Projectile/Arcane",
            "Assets/Art/Sprites/Effects/Hit",
        };

        void OnPreprocessTexture()
        {
            if (!IsEffectPath(assetPath))
            {
                return;
            }

            ApplyContract((TextureImporter)assetImporter);
        }

        [MenuItem("DarkFlare/Alpha 0.1.7/应用特效导入合同")]
        public static void ApplyToExistingAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", EffectRoots);
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (IsEffectPath(path))
                {
                    paths.Add(path);
                }
            }

            int changedCount = 0;

            foreach (string path in paths)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                ApplyContract(importer);
                importer.SaveAndReimport();
                changedCount++;
            }

            Debug.Log($"[Alpha017EffectAssetPostprocessor] 已精准应用 {changedCount} 张特效 Sprite 导入合同。");
        }

        static void ApplyContract(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.isReadable = false;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteBorder = Vector4.zero;
            settings.spritePixelsPerUnit = PixelsPerUnit;
            settings.spriteExtrude = 1;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }

        public static bool IsEffectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return path.StartsWith(ProjectileRoot, StringComparison.Ordinal)
                || path.StartsWith(HitRoot, StringComparison.Ordinal);
        }
    }
}
