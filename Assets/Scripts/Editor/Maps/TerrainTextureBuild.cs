using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace DarkFlare.Editor
{
    public static class TerrainTextureBuild
    {
        public sealed class Input
        {
            internal readonly Dictionary<ContentId, byte[]> Materials = new();
            internal readonly Dictionary<string, Dictionary<ContentId, byte[]>> Variants = new(StringComparer.Ordinal);
            internal readonly List<(TerrainWangKey Key, string Variant)> Tiles = new();
            public TerrainRuleSnapshot Rules { get; internal set; }
            public string Revision { get; internal set; }
        }

        public static Input Capture(TerrainTextureSources sources)
        {
            if (sources == null || sources.TerrainSet == null) { throw new ArgumentException("需要正式材质源配置和地表集"); }
            var input = new Input { Rules = TerrainRuleSnapshot.Compile(sources.TerrainSet, MapEditorDefinitionIndex.Capture()) };
            ContentId[] ids = sources.TerrainSet.Terrains.Select(terrain => new ContentId(ContentNamespaces.Terrain, terrain.Id)).OrderBy(id => id).ToArray();
            if (ids.Length > 4) { throw new ArgumentException("首期正式纹理编译最多四材质；不限制地图语义解析器"); }
            ReadMaterials(sources.Materials, input.Materials);
            if (!new HashSet<ContentId>(ids).SetEquals(input.Materials.Keys)) { throw new ArgumentException("基准源必须完整且恰好覆盖地表集"); }
            foreach (ContentId nw in ids)
            foreach (ContentId ne in ids)
            foreach (ContentId se in ids)
            foreach (ContentId sw in ids)
            {
                var key = new TerrainWangKey(nw, ne, se, sw);
                input.Tiles.Add((key, null));
                foreach (string variant in input.Rules.GetCandidates(key)) { input.Tiles.Add((key, variant)); }
            }
            foreach (TerrainVariantSource variant in sources.Variants)
            {
                if (variant == null || string.IsNullOrWhiteSpace(variant.VariantId) || input.Variants.ContainsKey(variant.VariantId)) { throw new ArgumentException("变体源 ID 为空或重复"); }
                var bindings = new Dictionary<ContentId, byte[]>();
                ReadMaterials(variant.Materials, bindings);
                var tile = input.Tiles.FirstOrDefault(item => item.Variant == variant.VariantId);
                if (!tile.Key.IsValid || bindings.Count == 0 || bindings.Keys.Any(id => tile.Key.GetMask(id) == 0)) { throw new ArgumentException("变体绑定必须属于该规则 Key，且至少覆盖一种材质：" + variant.VariantId); }
                input.Variants.Add(variant.VariantId, bindings);
            }
            if (input.Tiles.Any(tile => tile.Variant != null && !input.Variants.ContainsKey(tile.Variant))) { throw new ArgumentException("规则变体缺少正式源绑定"); }
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(TerrainTextureCompiler.Revision); writer.Write(TerrainPartition.Revision); writer.Write(input.Rules.RuleHash);
            WriteMaterials(writer, input.Materials);
            writer.Write(input.Variants.Count);
            foreach (var variant in input.Variants.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.Write(variant.Key); WriteMaterials(writer, variant.Value);
            }
            writer.Flush(); input.Revision = Hash(stream.ToArray());
            return input;
        }

        public static TerrainTileCatalog Compile(TerrainTextureSources sources, CancellationToken token = default, bool showProgress = true, IProgress<int> progress = null)
        {
            Input input = Capture(sources);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sources));
            if (string.IsNullOrEmpty(guid)) { throw new ArgumentException("请先保存材质源配置资产"); }
            string parent = "Assets/Art/Tiles/Environment/WangCompiled/" + guid;
            string output = parent + "/" + input.Revision;
            string catalogPath = output + "/catalog.asset";
            bool created = false;
            try
            {
                token.ThrowIfCancellationRequested();
                TerrainTileCatalog catalog;
                if (Directory.Exists(output))
                {
                    catalog = AssetDatabase.LoadAssetAtPath<TerrainTileCatalog>(catalogPath);
                    ValidateArtifact(input, catalog);
                }
                else
                {
                    // 在任何落盘前完成所有源像素、尺寸、Alpha 校验与固定采样。
                    var baseline = input.Materials.ToDictionary(pair => pair.Key, pair => Prepare(pair.Value, null, token));
                    var variants = input.Variants.ToDictionary(pair => pair.Key, pair => pair.Value.ToDictionary(material => material.Key, material => Prepare(material.Value, baseline[material.Key], token)));
                    EnsureFolder(parent);
                    AssetDatabase.CreateFolder(parent, input.Revision); created = true;
                    var entries = new List<TerrainTileEntry>();
                    for (int i = 0; i < input.Tiles.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        if (showProgress && EditorUtility.DisplayCancelableProgressBar("编译正式地表", $"{i + 1}/{input.Tiles.Count}", (float)i / input.Tiles.Count)) { throw new OperationCanceledException(); }
                        var target = input.Tiles[i];
                        var palette = new Dictionary<ContentId, Color32[]>(baseline);
                        if (target.Variant != null) { foreach (var material in variants[target.Variant]) { palette[material.Key] = material.Value; } }
                        Color32[] pixels = TerrainTextureCompiler.Compose(target.Key, palette, token);
                        entries.Add(WriteTile(output + "/tile_" + i.ToString("D4"), target.Key, target.Variant, pixels));
                        progress?.Report(i + 1);
                    }
                    token.ThrowIfCancellationRequested();
                    catalog = ScriptableObject.CreateInstance<TerrainTileCatalog>();
                    catalog.Initialize(input.Rules.TerrainSetId.ToString(), input.Rules.RuleHash, input.Revision, entries.ToArray());
                    AssetDatabase.CreateAsset(catalog, catalogPath);
                    ValidateArtifact(input, catalog);
                }
                token.ThrowIfCancellationRequested();
                // 发布前再次核对源；不把构建中修改的配置标记为最新。
                if (Capture(sources).Revision != input.Revision) { throw new InvalidOperationException("编译期间源已改变，请重新编译"); }
                Publish(sources, catalog);
                return catalog;
            }
            catch
            {
                if (created) { AssetDatabase.DeleteAsset(output); }
                throw;
            }
            finally { if (showProgress) { EditorUtility.ClearProgressBar(); } }
        }

        public static void ValidateArtifact(Input input, TerrainTileCatalog catalog)
        {
            if (catalog == null || catalog.Revision != input.Revision || catalog.RuleHash != input.Rules.RuleHash || catalog.TerrainSetId != input.Rules.TerrainSetId.ToString()) { throw new InvalidOperationException("正式 Tile 未编译或源图 / 规则已过期，请重新编译"); }
            catalog.Validate();
            if (catalog.Entries.Count != input.Tiles.Count) { throw new InvalidOperationException("正式 Tile 目录不完整"); }
            foreach (var target in input.Tiles) { catalog.GetTile(target.Key, target.Variant); }
            foreach (TerrainTileEntry entry in catalog.Entries)
            {
                Sprite sprite = entry.Tile.sprite;
                string path = AssetDatabase.GetAssetPath(sprite);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var settings = new TextureImporterSettings();
                if (importer != null) { importer.ReadTextureSettings(settings); }
                if (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != entry.TextureHash || importer == null
                    || importer.spriteImportMode != SpriteImportMode.Single || importer.filterMode != FilterMode.Point
                    || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled || !importer.sRGBTexture
                    || importer.wrapMode != TextureWrapMode.Clamp || settings.spriteMeshType != SpriteMeshType.FullRect
                    || sprite.rect.size != new Vector2(64, 64) || sprite.pixelsPerUnit != 64 || sprite.pivot != new Vector2(32, 32)
                    || entry.Tile.color != Color.white || entry.Tile.transform != Matrix4x4.identity || entry.Tile.colliderType != Tile.ColliderType.None)
                {
                    throw new InvalidOperationException("编译产物已损坏或被手工修改：" + path);
                }
            }
        }

        static void Publish(TerrainTextureSources sources, TerrainTileCatalog catalog)
        {
            TerrainTileCatalog previous = sources.Compiled;
            var serialized = new SerializedObject(sources);
            try
            {
                serialized.FindProperty("_compiled").objectReferenceValue = catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(sources);
            }
            catch
            {
                serialized.Update(); serialized.FindProperty("_compiled").objectReferenceValue = previous;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(sources);
                throw;
            }
        }

        static TerrainTileEntry WriteTile(string path, TerrainWangKey key, string variant, Color32[] pixels)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            byte[] png;
            try { texture.SetPixels32(pixels); texture.Apply(); png = texture.EncodeToPNG(); }
            finally { Object.DestroyImmediate(texture); }
            string pngPath = path + ".png";
            File.WriteAllBytes(pngPath, png);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
            importer.sRGBTexture = true; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 64;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f); importer.SetTextureSettings(settings); importer.SaveAndReimport();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath); tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, path + ".asset");
            return new TerrainTileEntry(key, variant, tile, Hash(png));
        }

        static Color32[] Prepare(byte[] bytes, Color32[] baseline, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(bytes) || texture.width != 1254 || texture.height != 1254) { throw new ArgumentException("正式材质源必须遵守 1254×1254 家族画布"); }
                return TerrainTextureCompiler.Prepare(texture.GetPixels32(), texture.width, baseline, token);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        static void ReadMaterials(IEnumerable<TerrainMaterialSource> sources, Dictionary<ContentId, byte[]> output)
        {
            foreach (TerrainMaterialSource source in sources)
            {
                if (source == null || source.Terrain == null || source.Source == null) { throw new ArgumentException("材质引用不完整"); }
                var id = new ContentId(ContentNamespaces.Terrain, source.Terrain.Id);
                string path = AssetDatabase.GetAssetPath(source.Source);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || output.ContainsKey(id)) { throw new ArgumentException("源必须是独立 PNG，地表不能重复：" + id); }
                output.Add(id, File.ReadAllBytes(path));
            }
        }

        static void WriteMaterials(BinaryWriter writer, Dictionary<ContentId, byte[]> materials)
        {
            writer.Write(materials.Count);
            foreach (var material in materials.OrderBy(pair => pair.Key))
            {
                writer.Write(material.Key.ToString()); writer.Write(Hash(material.Value));
            }
        }

        static string Hash(byte[] bytes)
        {
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
