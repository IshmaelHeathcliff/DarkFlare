using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace DarkFlare
{
    // 一次地图装配的所有权；候选不参与任何活动系统。
    public sealed class MapScope
    {
        readonly AssetOwnerScope _assets;
        readonly List<IDisposable> _leases = new();
        readonly LifecycleScope _lifetime;
        MapObjectPresentationBatch _objects;
        MapGeometryProjection _projection;
        Material _material;
        UniTask? _disposal;

        public CompiledMapDocument Map { get; }
        public int Generation { get; }
        public GameObject Root { get; }
        public MapGeometrySnapshot Geometry { get; private set; }
        public MonsterSpawner Spawner { get; private set; }
        public NavigationMap Navigation { get; private set; }
        public CancellationToken Token => _lifetime.Token;

        MapScope(CompiledMapDocument map, int generation, LifecycleScope parent, AddressableAssetService resources, Scene scene)
        {
            Map = map; Generation = generation;
            _lifetime = parent.CreateChild("map:" + map.MapId + ":" + generation);
            _assets = resources.CreateOwner("map:" + map.MapId + ":" + generation);
            Root = new GameObject("Map " + map.MapId);
            Root.SetActive(false);
            SceneManager.MoveGameObjectToScene(Root, scene);
        }

        public static async UniTask<MapScope> PrepareAsync(CompiledMapDocument map, int generation, WorldMapConfiguration configuration,
            IMapDefinitionLookup definitions, LifecycleScope parent, AddressableAssetService resources, Scene scene, CancellationToken token)
        {
            var scope = new MapScope(map, generation, parent, resources, scene);
            LifecycleScope preparation = scope._lifetime.CreateChild("prepare", token);
            token = preparation.Token;
            try
            {
                ResourceLoadResult<TerrainPresentationDefinition> loaded = await resources.AcquireAsync<TerrainPresentationDefinition>(
                    scope._assets, configuration.TerrainPresentation.RuntimeKey, "map-terrain", token);
                if (!loaded.Succeeded) { throw new InvalidOperationException("地表资源加载失败", loaded.Exception); }
                scope._leases.Add(loaded.Lease);
                TerrainPresentationDefinition presentation = loaded.Lease.Asset;
                presentation.Validate();
                if (new ContentId(ContentNamespaces.TerrainSet, presentation.TerrainSet.Id) != map.TerrainSetId)
                { throw new InvalidOperationException("地图与地表表现资源不匹配"); }
                // 场景目录与 Addressables 包可能各有 SO 实例；语义规则始终取权威内容目录。
                TerrainRuleSnapshot rules = TerrainRuleSnapshot.Compile(definitions.Resolve<TerrainSetDefinition>(map.TerrainSetId), definitions);
                MapGeometryInput input = MapGeometryInput.Capture(map, definitions, new MapGeometrySettings());
                TerrainResolution resolution = await UniTask.RunOnThreadPool(() => TerrainResolver.Resolve(map, rules, cancellationToken: token), cancellationToken: token);
                scope.Geometry = await UniTask.RunOnThreadPool(() => MapGeometryCompiler.Compile(input, token), cancellationToken: token);
                token.ThrowIfCancellationRequested();
                scope.Navigation = new NavigationMap(scope.Geometry, map.MapId.ToString(), generation, scope._lifetime, GameArchitectureProvider.RequireCurrent());
                scope.Navigation.ObstacleParent = scope.Root.transform;
                await scope.Navigation.PrepareAsync(token);
                await scope.PopulateTerrainAsync(resolution, presentation.Tiles, token);
                scope._projection = new MapGeometryProjection(scope.Geometry, scene);
                scope._projection.Root.transform.SetParent(scope.Root.transform, true);
                await scope._projection.PopulateAsync(token, false);
                scope._objects = await MapObjectPresentationBatch.PrepareAsync(map, configuration.Objects, resources, scope.Token);
                token.ThrowIfCancellationRequested();
                scope._objects.MoveToScene(scene);
                scope._objects.Root.transform.SetParent(scope.Root.transform, true);
                foreach (MapObjectRecord record in map.CreateEditableCopy().Objects)
                {
                    if (record.Interaction == null) { continue; }
                    var interaction = new GameObject("Interaction " + record.ObjectId);
                    interaction.transform.SetParent(scope.Root.transform, false);
                    interaction.transform.position = scope._objects.Objects[record.ObjectId].position;
                    interaction.AddComponent<WorldInteractionTarget>().ConfigureMap(record.Interaction, generation);
                }
                var spawnRoot = new GameObject("Map spawner");
                spawnRoot.transform.SetParent(scope.Root.transform, false);
                scope.Spawner = spawnRoot.AddComponent<MonsterSpawner>();
                scope.Spawner.ConfigureMap(scope.Geometry, definitions);
                return scope;
            }
            catch { await scope.DisposeAsync(); throw; }
            finally { await preparation.StopAsync(); }
        }

        async UniTask PopulateTerrainAsync(TerrainResolution resolution, TerrainTileCatalog catalog, CancellationToken token)
        {
            var grid = new GameObject("Terrain grid", typeof(Grid));
            grid.transform.SetParent(Root.transform, false);
            grid.transform.position = new Vector3((float)(Map.OriginX + Map.CellSize / 2), (float)(Map.OriginY + Map.CellSize / 2), 0);
            grid.transform.localScale = new Vector3((float)Map.CellSize, (float)Map.CellSize, 1);
            var tiles = new GameObject("Terrain", typeof(Tilemap), typeof(TilemapRenderer));
            tiles.transform.SetParent(grid.transform, false);
            Tilemap tilemap = tiles.GetComponent<Tilemap>();
            TilemapRenderer renderer = tiles.GetComponent<TilemapRenderer>();
            renderer.sortingLayerName = "Ground";
            renderer.mode = TilemapRenderer.Mode.Chunk;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (shader == null) { throw new InvalidOperationException("缺少地表 Shader"); }
            _material = new Material(shader);
            renderer.sharedMaterial = _material;
            var positions = new List<Vector3Int>(1024);
            var batch = new List<TileBase>(1024);
            for (int i = 0; i < resolution.CellCount; i++)
            {
                token.ThrowIfCancellationRequested();
                int x = i % (Map.Width + 1) - 1, y = i / (Map.Width + 1) - 1;
                TerrainVariantSelection selection = resolution.GetCell(x, y).Selection;
                positions.Add(new Vector3Int(x, y, 0)); batch.Add(catalog.GetTile(selection.Key, selection.VariantId));
                if (batch.Count == 1024 || i == resolution.CellCount - 1)
                {
                    tilemap.SetTiles(positions.ToArray(), batch.ToArray()); positions.Clear(); batch.Clear();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            tilemap.CompressBounds();
        }

        public void Activate(WorldSortingSystem sorting)
        {
            Token.ThrowIfCancellationRequested();
            _objects.Activate(sorting);
            _projection.Root.SetActive(true);
            Root.SetActive(true);
            Navigation.Activate();
            // Manual Composite 在禁用祖先下无法生成，安装活动根后同步生成，仍不跨物理帧。
            _projection.Collider.GenerateGeometry();
            if (_projection.Collider.errorState != ColliderErrorState2D.None || _projection.Collider.pathCount == 0)
            { throw new InvalidOperationException("活动地图碰撞生成失败"); }
        }

        public void Deactivate()
        {
            Navigation?.Suspend();
            if (Spawner != null) { Spawner.StopSpawning(); }
            if (Root != null) { Root.SetActive(false); }
            _objects?.Deactivate();
        }

        public UniTask DisposeAsync()
        {
            _disposal ??= DisposeCoreAsync().Preserve();
            return _disposal.Value;
        }

        async UniTask DisposeCoreAsync()
        {
            Deactivate();
            await _lifetime.StopAsync();
            Navigation?.Dispose();
            if (_objects != null) { await _objects.DisposeAsync(); }
            _projection?.Dispose();
            UnityEngine.Object.Destroy(Root);
            if (_material != null) { UnityEngine.Object.Destroy(_material); }
            await UniTask.NextFrame();
            foreach (IDisposable lease in _leases) { lease.Dispose(); }
            _assets.Dispose();
        }
    }
}
