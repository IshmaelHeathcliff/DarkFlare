using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    public class MonsterSpawner : MonoBehaviour, IController
    {
        [SerializeField]
        MonsterSpawnDefinition _spawnDefinition;

        [SerializeField]
        Transform _target;

        readonly List<MonsterController> _aliveMonsters = new List<MonsterController>();
        CancellationTokenSource _spawnCancellation;
        System.Random _random;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        void Awake()
        {
            _random = new System.Random();
        }

        void OnEnable()
        {
            _spawnCancellation = new CancellationTokenSource();
            SpawnLoop(_spawnCancellation.Token).Forget();
        }

        void OnDisable()
        {
            _spawnCancellation?.Cancel();
            _spawnCancellation?.Dispose();
            _spawnCancellation = null;
        }

        async UniTaskVoid SpawnLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                CleanupAliveList();

                if (_aliveMonsters.Count < _spawnDefinition.MaxAliveCount)
                {
                    SpawnOne();
                }

                await UniTask.Delay(System.TimeSpan.FromSeconds(_spawnDefinition.SpawnInterval), cancellationToken: token);
            }
        }

        void SpawnOne()
        {
            MonsterController monster = this.SendCommand(new SpawnMonsterCommand(_spawnDefinition, _random, GetSpawnPosition()));

            if (monster != null)
            {
                _aliveMonsters.Add(monster);
            }
        }

        Vector3 GetSpawnPosition()
        {
            Transform target = GetTarget();
            Vector3 center = target != null ? target.position : transform.position;
            Vector2 offset = Random.insideUnitCircle.normalized * _spawnDefinition.SpawnRadius;
            return center + new Vector3(offset.x, offset.y, 0f);
        }

        Transform GetTarget()
        {
            if (_target != null)
            {
                return _target;
            }

            PlayerController player = FindFirstObjectByType<PlayerController>();

            if (player != null)
            {
                _target = player.transform;
            }

            return _target;
        }

        void CleanupAliveList()
        {
            for (int i = _aliveMonsters.Count - 1; i >= 0; i--)
            {
                MonsterController monster = _aliveMonsters[i];

                if (monster == null || monster.Actor == null || !monster.Actor.IsAlive)
                {
                    _aliveMonsters.RemoveAt(i);
                }
            }
        }
    }
}
