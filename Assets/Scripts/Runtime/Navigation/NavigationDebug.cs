using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public sealed class GetNavigationDebugQuery : AbstractQuery<NavigationModel>
    {
        protected override NavigationModel OnDo() { return this.GetModel<NavigationModel>(); }
    }
    public sealed class GetNavigationMapQuery : AbstractQuery<NavigationMap>
    {
        protected override NavigationMap OnDo() { return this.GetSystem<NavigationSystem>().Active; }
    }
    public sealed class SpawnNavigationStressCommand : AbstractCommand<int>
    {
        readonly int _count;
        public SpawnNavigationStressCommand(int count) { _count = count; }
        protected override int OnExecute()
        {
            MapScope scope = this.GetUtility<WorldMapRuntime>().Active;
            if (scope?.Spawner?.SpawnDefinition == null || _count < 1 || _count > NavigationMap.Capacity) { return 0; }
            scope.Spawner.StopSpawning();
            int spawned = 0, existing = scope.Navigation.AgentCount;
            IReadOnlyList<MonsterDefinition> definitions = ApplicationHost.Current.ContentCatalog.GetAll<MonsterDefinition>();
            for (int i = existing; i < _count; i++)
            {
                MonsterDefinition definition = definitions[i % definitions.Count];
                var requested = new MapGeometryPoint(-24 + (i % 40) * 1.2, -15 + (i / 40) * 1.2);
                if (scope.Geometry.FindPoint(requested, new MapGroundProfile(.4), 2, 1024, out MapGeometryPoint position) != MapGeometryResult.Valid) { continue; }
                MonsterInstanceData instance = definition.CreateInstanceData(this.GetUtility<IRunInstanceIdGenerator>().NextMonsterId(), 71023 + i);
                MonsterController monster = this.GetSystem<SpawnSystem>().SpawnRestoredMonster(definition, instance,
                    new Vector3((float)position.X, (float)position.Y, 0), default, 0, true);
                if (monster == null) { continue; }
                int special = _count > 500 ? 50 : 20;
                monster.RestoreNavigation(new NavigationIntentDto { Mode = i >= _count - special ? NavigationMode.AStar : NavigationMode.FlowField });
                scope.Spawner.RegisterRestoredMonster(monster); spawned++;
            }
            scope.Navigation.ConfigureStressMix(_count > 500 ? 50 : 20);
            return spawned;
        }
    }
    public sealed partial class NavigationMap
    {
        internal void ConfigureStressMix(int special)
        {
            for (int i = 0; i < _agents.Count; i++)
            {
                NavigationMode mode = i >= _agents.Count - special ? NavigationMode.AStar : NavigationMode.FlowField;
                if (_agents[i].Monster.NavigationIntent.Mode != mode)
                { _agents[i].Monster.RestoreNavigation(new NavigationIntentDto { Mode = mode }); }
            }
        }
        // Explicit diagnostic capture; never called by the simulation or from Jobs.
        public Vector3[] CaptureRoute(MonsterController monster, int maximum = 256)
        {
            if (_disposed || !_lookup.TryGetValue(monster, out Agent agent) || agent.Profile < 0 || agent.Profile >= _grids.Length) { return System.Array.Empty<Vector3>(); }
            var points = new List<Vector3>();
            NavigationGridView grid = _grids[agent.Profile].View;
            if (!_grids[agent.Profile].IsReady) { return points.ToArray(); }
            _grids[agent.Profile].Complete();
            if (agent.Mode == NavigationMode.AStar)
            {
                for (int i = agent.PathIndex; i < agent.PathCount && points.Count < maximum; i++)
                { MapGeometryPoint p = grid.Point(agent.Path[i]); points.Add(new Vector3((float)p.X, (float)p.Y)); }
            }
            else if (agent.Field?.PublishedGoal >= 0)
            {
                Vector2 position = monster.Body.position;
                int node = grid.LegalNode(new Unity.Mathematics.float2(position.x, position.y));
                while (node >= 0 && points.Count < maximum)
                { MapGeometryPoint p = grid.Point(node); points.Add(new Vector3((float)p.X, (float)p.Y)); node = agent.Field.Parents[node]; }
            }
            return points.ToArray();
        }
    }
}
