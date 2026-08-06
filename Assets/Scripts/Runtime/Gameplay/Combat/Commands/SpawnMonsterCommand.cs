using UnityEngine;

namespace DarkFlare
{
    public class SpawnMonsterCommand : AbstractCommand<MonsterController>
    {
        readonly MonsterSpawnDefinition _spawnDefinition;
        readonly int _seed;
        readonly Vector3 _position;

        public SpawnMonsterCommand(MonsterSpawnDefinition spawnDefinition, int seed, Vector3 position)
        {
            _spawnDefinition = spawnDefinition;
            _seed = seed;
            _position = position;
        }

        protected override MonsterController OnExecute()
        {
            return this.GetSystem<SpawnSystem>().SpawnMonster(_spawnDefinition, _seed, _position);
        }
    }
}
