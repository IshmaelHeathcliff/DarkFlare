using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    public class MonsterSpawner : MonoBehaviour, IController
    {
        const int MaxSpawnPositionAttempts = 8;

        [SerializeField]
        MonsterSpawnDefinition _spawnDefinition;

        [SerializeField]
        Transform _target;

        [SerializeField]
        Collider2D _worldBounds;

        [SerializeField]
        [Min(0f)]
        float _spawnBoundsPadding = 0.5f;

        readonly List<MonsterController> _aliveMonsters = new List<MonsterController>();
        CancellationTokenSource _spawnCancellation;
        System.Random _random;

        public Collider2D WorldBounds => _worldBounds;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void SetWorldBounds(Collider2D worldBounds)
        {
            _worldBounds = worldBounds;
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

            if (_worldBounds == null)
            {
                return center + (Vector3)(GetRandomDirection() * _spawnDefinition.SpawnRadius);
            }

            Bounds bounds = _worldBounds.bounds;

            for (int i = 0; i < MaxSpawnPositionAttempts; i++)
            {
                Vector2 offset = GetRandomDirection() * _spawnDefinition.SpawnRadius;
                Vector3 candidate = center + new Vector3(offset.x, offset.y, 0f);

                if (IsInsideBounds(candidate, bounds, _spawnBoundsPadding))
                {
                    return candidate;
                }
            }

            Vector2 fallbackOffset = GetRandomDirection() * _spawnDefinition.SpawnRadius;
            Vector3 fallback = center + new Vector3(fallbackOffset.x, fallbackOffset.y, 0f);
            return ClampToBounds(fallback, bounds, _spawnBoundsPadding);
        }

        Transform GetTarget()
        {
            if (_target != null)
            {
                return _target;
            }

            PlayerController player = FindAnyObjectByType<PlayerController>();

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

        Vector2 GetRandomDirection()
        {
            if (_random == null)
            {
                _random = new System.Random();
            }

            double angle = _random.NextDouble() * System.Math.PI * 2d;
            return new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
        }

        static bool IsInsideBounds(Vector3 point, Bounds bounds, float padding)
        {
            return point.x >= bounds.min.x + padding
                   && point.x <= bounds.max.x - padding
                   && point.y >= bounds.min.y + padding
                   && point.y <= bounds.max.y - padding;
        }

        static Vector3 ClampToBounds(Vector3 point, Bounds bounds, float padding)
        {
            float minimumX = bounds.min.x + padding;
            float maximumX = bounds.max.x - padding;
            float minimumY = bounds.min.y + padding;
            float maximumY = bounds.max.y - padding;

            if (minimumX > maximumX)
            {
                minimumX = bounds.center.x;
                maximumX = bounds.center.x;
            }

            if (minimumY > maximumY)
            {
                minimumY = bounds.center.y;
                maximumY = bounds.center.y;
            }

            point.x = Mathf.Clamp(point.x, minimumX, maximumX);
            point.y = Mathf.Clamp(point.y, minimumY, maximumY);
            return point;
        }
    }
}
