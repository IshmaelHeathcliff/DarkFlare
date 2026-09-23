using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public static class FiniteSpawnPlan
    {
        public static MonsterSpawnerStateDto Create(CompiledMapDocument map, IMapDefinitionLookup definitions, int seed)
        {
            if (map == null || definitions == null) { throw new ArgumentNullException(map == null ? nameof(map) : nameof(definitions)); }
            MapDocument document = map.CreateEditableCopy();
            var state = new MonsterSpawnerStateDto { Mode = FiniteSpawnMode.Finite, PlanSeed = seed };
            var regions = document.Regions.ToDictionary(region => region.RegionId, StringComparer.Ordinal);
            var random = new Random(seed);
            foreach (MapSpawnBatchRecord batch in document.SpawnBatches.OrderBy(item => item.BatchId, StringComparer.Ordinal))
            {
                MonsterSpawnDefinition pool = definitions.Resolve<MonsterSpawnDefinition>(
                    ContentId.Parse(regions[batch.RegionId].SpawnDefinitionId));
                state.Batches.Add(new SpawnBatchStateDto
                {
                    BatchId = batch.BatchId,
                    IsActivated = batch.Activation == MapSpawnActivation.OnEnter
                });
                for (int index = 0; index < batch.Count; index++)
                {
                    MonsterDefinition monster = pool.PickMonster(random);
                    if (monster == null || monster.BossBehavior != null)
                    { throw new InvalidOperationException("批次没有可生成的普通怪物：" + batch.BatchId); }
                    state.Slots.Add(new SpawnSlotStateDto
                    {
                        SlotId = batch.BatchId + "_" + index,
                        BatchId = batch.BatchId,
                        MonsterDefinitionId = "monster:" + monster.Id,
                        MonsterSeed = random.Next(int.MinValue, int.MaxValue),
                        PositionSeed = random.Next(int.MinValue, int.MaxValue)
                    });
                }
            }
            return state;
        }
    }
}
