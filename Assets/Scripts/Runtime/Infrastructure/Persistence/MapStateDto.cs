using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkFlare
{
    public sealed class MapStateDto : IPersistenceDto
    {
        public string MapId { get; set; }
        public int Revision { get; set; }
        public List<MonsterRunStateDto> Monsters { get; set; } = new();
        public List<WorldDropStateDto> WorldDrops { get; set; } = new();
        public MonsterSpawnerStateDto Spawner { get; set; } = new();
        public BossEncounterStateDto Boss { get; set; } = new();
        public GameplayRandomStateDto Random { get; set; } = new();
        public StatusSaveDto Statuses { get; set; } = new();
        // 内存冻结图的物品子集；磁盘仍使用 Payload.Items 唯一总表。
        [JsonIgnore] public List<ItemInstanceDto> Items { get; set; } = new();
    }

    public static class MapStateData
    {
        public static T Copy<T>(T value)
        {
            return JToken.FromObject(value).ToObject<T>();
        }

        public static MapStateDto CopyState(MapStateDto state)
        {
            MapStateDto copy = Copy(state);
            copy.Items = Copy(state.Items);
            return copy;
        }

        public static StatusSaveDto AtTime(StatusSaveDto source, double time)
        {
            if (!double.IsFinite(time) || time < source.Time) { throw new ArgumentOutOfRangeException(nameof(time)); }
            StatusSaveDto result = Copy(source);
            double shift = time - source.Time;
            result.Time = time;
            foreach (StatusActorDto actor in result.Actors)
            foreach (StatusLayerDto layer in actor.Layers)
            {
                layer.AppliedAt += shift;
                layer.ExpiresAt += shift;
                layer.NextTickAt += shift;
            }
            return result;
        }

        internal static SavePayloadDto Flatten(SavePayloadDto payload)
        {
            if (payload?.Run?.FrozenMaps == null || payload.Run.FrozenMaps.Count == 0) { return payload; }
            SavePayloadDto copy = Copy(payload);
            foreach (MapStateDto map in copy.Run.FrozenMaps)
            {
                if (map == null) { continue; }
                if (map.Monsters != null && copy.Run.Monsters != null) { copy.Run.Monsters.AddRange(map.Monsters); }
                if (map.WorldDrops != null && copy.Run.WorldDrops != null) { copy.Run.WorldDrops.AddRange(map.WorldDrops); }
            }
            copy.Run.FrozenMaps.Clear();
            return copy;
        }

        internal static void LoadItems(MapStateDto map, IReadOnlyDictionary<string, ItemInstanceDto> items)
        {
            map.Items = map.WorldDrops.Select(drop => items[drop.ItemInstanceId]).ToList();
        }
    }
}
