using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkFlare
{
    public sealed class WorldMapCatalog
    {
        readonly Dictionary<string, CompiledMapDocument> _maps = new(StringComparer.Ordinal);
        public IMapDefinitionLookup Definitions { get; }
        public IReadOnlyDictionary<string, CompiledMapDocument> Maps => _maps;

        public static WorldMapCatalog FromContent(ContentCatalog content)
        {
            return new WorldMapCatalog(content.GetAll<MapDefinition>(), new CatalogLookup(content));
        }

        public void ValidateSave(RunSaveData run)
        {
            ValidateState(run.ActiveMapId, run.MapRevision, run.Player.Position, run.Monsters, run.WorldDrops, run.Spawner);
            foreach (MapStateDto map in run.FrozenMaps)
            { ValidateState(map.MapId, map.Revision, null, map.Monsters, map.WorldDrops, map.Spawner); }
        }

        void ValidateState(string id, int revision, Vector3Dto player, IEnumerable<MonsterRunStateDto> monsters,
            IEnumerable<WorldDropStateDto> drops, MonsterSpawnerStateDto spawner)
        {
            CompiledMapDocument map = Get(id);
            if (revision != 0 && revision != map.Revision) { throw new InvalidOperationException("地图修订不匹配：" + id); }
            MapDocument document = map.CreateEditableCopy();
            string spawn = document.Regions.Select(region => region.SpawnDefinitionId).FirstOrDefault(value => value != null);
            if (!string.Equals(spawn ?? "", spawner.SpawnContentId ?? "", StringComparison.Ordinal))
            { throw new InvalidOperationException("地图生成定义不匹配：" + id); }
            MapGeometrySnapshot geometry = MapGeometryCompiler.Compile(MapGeometryInput.Capture(map, Definitions, new MapGeometrySettings()));
            void Check(Vector3Dto point)
            {
                // 纯预检没有加载角色 Prefab；先拒绝阻挡内部，实际足迹在资源预热后校验。
                if (geometry.CheckPoint(new MapGeometryPoint(point.X, point.Y), new MapGroundProfile(0.001, 0)) != MapGeometryResult.Valid)
                { throw new InvalidOperationException("存档地图落点没有合法净空：" + id); }
            }
            if (player != null) { Check(player); }
            foreach (MonsterRunStateDto monster in monsters) { Check(monster.Position); }
            foreach (WorldDropStateDto drop in drops) { Check(drop.Position); }
        }

        public WorldMapCatalog(IEnumerable<MapDefinition> maps, IMapDefinitionLookup definitions)
        {
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            foreach (MapDefinition definition in maps)
            {
                CompiledMapDocument map = MapDocumentCompiler.Compile(MapDocumentJson.Read(definition.CanonicalJson), definitions);
                if (!_maps.TryAdd(map.MapId, map)) { throw new InvalidOperationException("地图身份重复：" + map.MapId); }
                MapDocument document = map.CreateEditableCopy();
                if (document.Entries.Count(entry => entry.Kind == MapEntryKind.Respawn) != 1)
                { throw new InvalidOperationException("正式地图必须有唯一复活入口：" + map.MapId); }
            }
            foreach (CompiledMapDocument map in _maps.Values)
            {
                foreach (MapObjectRecord obj in map.CreateEditableCopy().Objects)
                {
                    if (obj.Interaction?.Kind == MapInteractionKind.Portal)
                    { GetEntry(obj.Interaction.TargetMapId, obj.Interaction.TargetEntryId); }
                }
            }
        }

        public CompiledMapDocument Get(string mapId)
        {
            return _maps.TryGetValue(mapId, out CompiledMapDocument map) ? map : throw new InvalidOperationException("未知地图：" + mapId);
        }

        public MapEntryRecord GetEntry(string mapId, string entryId)
        {
            return Get(mapId).CreateEditableCopy().Entries.SingleOrDefault(entry => entry.EntryId == entryId)
                ?? throw new InvalidOperationException("未知入口：" + mapId + "/" + entryId);
        }

        public Vector3 EntryPosition(string mapId, string entryId)
        {
            CompiledMapDocument map = Get(mapId);
            MapEntryRecord entry = GetEntry(mapId, entryId);
            return new Vector3((float)(map.OriginX + entry.Position.X), (float)(map.OriginY + entry.Position.Y), 0);
        }

        sealed class CatalogLookup : IMapDefinitionLookup
        {
            readonly ContentCatalog _content;
            public CatalogLookup(ContentCatalog content) { _content = content; }
            public T Resolve<T>(ContentId id) where T : ScriptableObject, IContentDefinition
            { return _content.TryResolve(id, out T result) ? result : throw new InvalidOperationException("缺少地图内容：" + id); }
        }
    }
}
