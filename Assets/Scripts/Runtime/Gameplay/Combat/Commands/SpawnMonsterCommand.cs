using UnityEngine;

namespace DarkFlare
{
    public class SpawnMonsterCommand : AbstractCommand<MonsterController>
    {
        readonly MonsterSpawnDefinition _spawnDefinition;
        readonly System.Random _random;
        readonly Vector3 _position;

        public SpawnMonsterCommand(MonsterSpawnDefinition spawnDefinition, System.Random random, Vector3 position)
        {
            _spawnDefinition = spawnDefinition;
            _random = random;
            _position = position;
        }

        protected override MonsterController OnExecute()
        {
            return this.GetSystem<SpawnSystem>().SpawnMonster(_spawnDefinition, _random, _position);
        }
    }
}
