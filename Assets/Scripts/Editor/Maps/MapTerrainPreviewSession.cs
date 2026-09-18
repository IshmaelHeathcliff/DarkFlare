using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DarkFlare.Editor
{
    public sealed class MapTerrainPreviewSession : IDisposable
    {
        readonly DiagnosticTileLibrary _library = new();
        CancellationTokenSource _cancellation;
        MapTerrainPreviewFrame _building;
        int _generation;
        bool _disposed;

        public MapTerrainPreviewFrame Current { get; private set; }
        public bool IsValid { get; private set; }
        public bool IsBuilding { get; private set; }
        public string Status { get; private set; } = "请选择地图或打开 Key 画廊。";
        public int ResourceCount => _library.Count;

        public async UniTask RebuildAsync(Func<(CompiledMapDocument Map, TerrainRuleSnapshot Rules)> capture = null, Func<TerrainTileCatalog> captureTiles = null,
            Func<CompiledMapDocument, MapGeometryInput> captureGeometry = null)
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(MapTerrainPreviewSession)); }
            Cancel();
            int generation = _generation;
            _cancellation = new CancellationTokenSource();
            CancellationToken token = _cancellation.Token;
            IsBuilding = true;
            Status = "构建诊断预览…旧画面已过期。";
            MapTerrainPreviewFrame pending = null;
            try
            {
                TerrainResolution resolution = null;
                TerrainTileCatalog formalTiles = null;
                MapGeometrySnapshot geometry = null;
                if (capture != null)
                {
                    (CompiledMapDocument map, TerrainRuleSnapshot rules) = capture();
                    if (captureTiles != null)
                    {
                        formalTiles = captureTiles();
                        if (formalTiles == null || formalTiles.RuleHash != rules.RuleHash || formalTiles.TerrainSetId != map.TerrainSetId.ToString()) { throw new InvalidOperationException("正式 Tile 与当前地图规则不匹配"); }
                        formalTiles.Validate();
                    }
                    TerrainResolution previous = Current?.Resolution;
                    MapGeometryInput geometryInput = captureGeometry?.Invoke(map);
                    // 明确回到 Unity 主线程后才能创建或释放 Editor 资源。
                    await UniTask.SwitchToThreadPool();
                    try
                    {
                        resolution = TerrainResolver.Resolve(map, rules, previous, token);
                        if (geometryInput != null) { geometry = MapGeometryCompiler.Compile(geometryInput, token); }
                    }
                    finally { await UniTask.SwitchToMainThread(); }
                }
                token.ThrowIfCancellationRequested();
                pending = new MapTerrainPreviewFrame(_library, resolution, formalTiles, geometry);
                _building = pending;
                await pending.PopulateAsync(token);
                token.ThrowIfCancellationRequested();
                if (_disposed || generation != _generation) { return; }
                MapTerrainPreviewFrame old = Current;
                Current = pending;
                pending = null;
                _building = null;
                old?.Dispose();
                IsValid = true;
                Status = resolution == null ? "诊断 Key 画廊：256 个独立 Key + 8 个纯地表变体示例。"
                    : $"{(formalTiles != null ? "正式材质" : "诊断地图")}：{resolution.Map.MapId}，{resolution.CellCount} 显示格，重算 {resolution.RebuiltCells.Count}；比较 {resolution.ComparisonMilliseconds:F2} ms / 求解 {resolution.ResolutionMilliseconds:F2} ms。";
                if (geometry != null) { Status += $" 几何 {geometry.GeometryHash.Substring(0, 8)}：{geometry.Width}×{geometry.Height} 节点，{geometry.Rectangles.Count} 阻挡矩形。"; }
            }
            catch (OperationCanceledException)
            {
                if (!_disposed && generation == _generation) { Status = "已取消，旧画面已过期。"; }
            }
            catch (Exception error)
            {
                if (!_disposed && generation == _generation) { Status = "预览失败，旧画面已过期：" + error.Message; }
            }
            finally
            {
                pending?.Dispose();
                if (generation == _generation)
                {
                    _building = null;
                    IsBuilding = false;
                    _cancellation?.Dispose();
                    _cancellation = null;
                }
            }
        }

        public void Cancel()
        {
            _generation++;
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _building?.Dispose();
            _building = null;
            IsBuilding = false;
            IsValid = false;
            Status = "已取消，旧画面已过期。";
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            Cancel();
            Current?.Dispose();
            Current = null;
            _library.Dispose();
        }
    }
}
