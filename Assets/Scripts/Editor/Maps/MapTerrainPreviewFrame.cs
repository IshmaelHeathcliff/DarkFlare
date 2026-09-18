using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace DarkFlare.Editor
{
    public sealed class MapTerrainPreviewFrame : IDisposable
    {
        readonly PreviewRenderUtility _preview;
        readonly DiagnosticTileLibrary _library;
        readonly Dictionary<(TerrainWangKey Key, string Variant), DiagnosticTileLibrary.Lease> _leases = new();
        readonly Dictionary<Vector2Int, (TerrainWangKey Key, string Variant)> _gallery = new();
        readonly Material _material;
        readonly TerrainTileCatalog _formalTiles;
        bool _disposed;
        GameObject _stageRoot;

        public Tilemap Tilemap { get; }
        public TilemapRenderer Renderer { get; }
        public TerrainResolution Resolution { get; }
        public MapGeometrySnapshot Geometry { get; }
        public MapGeometryProjection Projection { get; private set; }
        public bool IsGallery => Resolution == null;
        public Bounds Bounds { get; private set; }
        public int ResourceCount => _leases.Count;
        public Camera Camera => _preview.camera;
        public string DisplayRevision => _formalTiles != null ? _formalTiles.Revision : DiagnosticTileLibrary.DisplayRevision;
        public IReadOnlyDictionary<Vector2Int, (TerrainWangKey Key, string Variant)> Gallery => _gallery;
        public static IReadOnlyList<ContentId> GalleryTerrains { get; } = Array.AsReadOnly(new[]
        {
            ContentId.Parse("terrain:preview_dirt"), ContentId.Parse("terrain:preview_grass"),
            ContentId.Parse("terrain:preview_water"), ContentId.Parse("terrain:preview_road")
        });

        public MapTerrainPreviewFrame(DiagnosticTileLibrary library, TerrainResolution resolution, TerrainTileCatalog formalTiles = null, MapGeometrySnapshot geometry = null)
        {
            _library = library;
            Resolution = resolution;
            Geometry = geometry;
            _formalTiles = formalTiles;
            _preview = new PreviewRenderUtility();
            try
            {
                Camera.orthographic = true;
                Camera.nearClipPlane = 0.1f;
                Camera.farClipPlane = 100;
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = new Color(0.055f, 0.07f, 0.09f, 1);
                Camera.allowHDR = false;
                Camera.allowMSAA = false;
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null || !shader.isSupported) { throw new InvalidOperationException("URP 2D Unlit Shader 不可用"); }
                _material = new Material(shader) { name = "Map diagnostic unlit", hideFlags = HideFlags.HideAndDontSave };
                var root = new GameObject("Map diagnostic grid", typeof(Grid)) { hideFlags = HideFlags.HideAndDontSave };
                _preview.AddSingleGO(root);
                var tiles = new GameObject("Map diagnostic tilemap", typeof(Tilemap), typeof(TilemapRenderer)) { hideFlags = HideFlags.HideAndDontSave };
                // 先进入预览场景，再建立父子关系，避免活动场景的跨场景对象。
                _preview.AddSingleGO(tiles);
                tiles.transform.SetParent(root.transform, false);
                Tilemap = tiles.GetComponent<Tilemap>();
                Renderer = tiles.GetComponent<TilemapRenderer>();
                Renderer.mode = TilemapRenderer.Mode.Chunk;
                Renderer.sortingLayerName = "Ground";
                Renderer.sharedMaterial = _material;
                if (resolution != null)
                {
                    CompiledMapDocument map = resolution.Map;
                    root.transform.position = new Vector3((float)(map.OriginX + map.CellSize * 0.5), (float)(map.OriginY + map.CellSize * 0.5), 0);
                    root.transform.localScale = new Vector3((float)map.CellSize, (float)map.CellSize, 1);
                }
            }
            catch { Dispose(); throw; }
        }

        public async UniTask PopulateAsync(CancellationToken token)
        {
            var positions = new List<Vector3Int>(256);
            var tiles = new List<TileBase>(256);
            int count = IsGallery ? 264 : Resolution.CellCount;
            int created = 0;
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                Vector2Int coordinate;
                TerrainWangKey key;
                string variant;
                if (IsGallery)
                {
                    int encoded = Math.Min(i, 255);
                    key = new TerrainWangKey(GalleryTerrains[encoded & 3], GalleryTerrains[(encoded >> 2) & 3], GalleryTerrains[(encoded >> 4) & 3], GalleryTerrains[(encoded >> 6) & 3]);
                    coordinate = new Vector2Int(i % 16 * 2, 30 - i / 16 * 2);
                    variant = null;
                    if (i >= 256)
                    {
                        ContentId terrain = GalleryTerrains[(i - 256) / 2];
                        key = new TerrainWangKey(terrain, terrain, terrain, terrain);
                        variant = (i & 1) == 0 ? "diagnostic_a" : "diagnostic_b";
                    }
                    _gallery.Add(coordinate, (key, variant));
                }
                else
                {
                    int stride = Resolution.Map.Width + 1;
                    coordinate = new Vector2Int(i % stride - 1, i / stride - 1);
                    TerrainVariantSelection selection = Resolution.GetCell(coordinate.x, coordinate.y).Selection;
                    key = selection.Key;
                    variant = selection.VariantId;
                }
                TileBase tile;
                if (_formalTiles != null)
                {
                    tile = _formalTiles.GetTile(key, variant);
                }
                else
                {
                    if (!_leases.TryGetValue((key, variant), out DiagnosticTileLibrary.Lease lease))
                    {
                        lease = _library.Acquire(Resolution?.RuleHash ?? "diagnostic-gallery-v1", key, variant);
                        _leases.Add((key, variant), lease);
                        created++;
                    }
                    tile = lease.Tile;
                }
                positions.Add(new Vector3Int(coordinate.x, coordinate.y, 0));
                tiles.Add(tile);
                // 即使 Key 很多也分批让出主线程，资源只由当前构建帧持有。
                if (positions.Count == 1024 || created == 8 || i == count - 1)
                {
                    Tilemap.SetTiles(positions.ToArray(), tiles.ToArray());
                    positions.Clear();
                    tiles.Clear();
                    created = 0;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            token.ThrowIfCancellationRequested();
            Tilemap.CompressBounds();
            Tilemap.RefreshAllTiles();
            Bounds = Renderer.bounds;
            if (Geometry != null)
            {
                Projection = new MapGeometryProjection(Geometry, Tilemap.gameObject.scene);
                await Projection.PopulateAsync(token);
            }
        }

        public Texture Render(int width, int height, Vector2 center, float halfHeight)
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(MapTerrainPreviewFrame)); }
            Camera.transform.SetPositionAndRotation(new Vector3(center.x, center.y, -10), Quaternion.identity);
            Camera.orthographicSize = Mathf.Max(0.01f, halfHeight);
            Camera.aspect = (float)width / height;
            _preview.BeginPreview(new Rect(0, 0, width, height), GUIStyle.none);
            Texture texture;
            try { _preview.Render(true, false); }
            finally { texture = _preview.EndPreview(); }
            return texture;
        }

        // 已完成的帧转交编辑舞台显示，所有 Tile / 材质仍由本帧持有。
        public void MoveToScene(UnityEngine.SceneManagement.Scene scene)
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(MapTerrainPreviewFrame)); }
            _stageRoot = Tilemap.transform.root.gameObject;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_stageRoot, scene);
        }

        public bool TryInspect(Vector2 world, out Vector2Int coordinate, out TerrainWangKey key, out string variant)
        {
            Vector3Int cell = Tilemap.WorldToCell(world);
            coordinate = new Vector2Int(cell.x, cell.y);
            key = default;
            variant = null;
            if (IsGallery)
            {
                if (!_gallery.TryGetValue(coordinate, out var entry)) { return false; }
                key = entry.Key;
                variant = entry.Variant;
                return true;
            }
            if (!DualGridCoordinates.ContainsDisplay(Resolution.Map, cell.x, cell.y)) { return false; }
            TerrainVariantSelection selection = Resolution.GetCell(cell.x, cell.y).Selection;
            key = selection.Key;
            variant = selection.VariantId;
            return true;
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            Projection?.Dispose();
            if (_stageRoot != null) { Object.DestroyImmediate(_stageRoot); }
            _preview?.Cleanup();
            if (_material != null) { Object.DestroyImmediate(_material); }
            foreach (DiagnosticTileLibrary.Lease lease in _leases.Values) { lease.Dispose(); }
            _leases.Clear();
        }
    }
}
