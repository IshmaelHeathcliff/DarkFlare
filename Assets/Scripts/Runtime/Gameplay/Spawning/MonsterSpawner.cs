using System.Collections.Generic;
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
        LifecycleScope _spawnScope;
        IArchitecture _architecture;
        bool _isSpawning;
        uint _spawnVersion;

        public Collider2D WorldBounds => _worldBounds;

        public MonsterSpawnDefinition SpawnDefinition => _spawnDefinition;

        public bool IsSpawning => _isSpawning;

        public IArchitecture GetArchitecture()
        {
            return _architecture ?? GameArchitectureProvider.RequireCurrent();
        }

        public void SetWorldBounds(Collider2D worldBounds)
        {
            _worldBounds = worldBounds;
        }

        public void PrepareForInitialization(MonsterSpawnDefinition spawnDefinition)
        {
            StopSpawning();
            _spawnDefinition = spawnDefinition;

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public void BeginSpawning()
        {
            if (_isSpawning)
            {
                return;
            }

            if (_spawnDefinition == null)
            {
                throw new System.InvalidOperationException("MonsterSpawner 缺少生成定义");
            }

            if (!isActiveAndEnabled)
            {
                throw new System.InvalidOperationException("MonsterSpawner 必须先启用再开始生成");
            }

            _spawnScope = ComponentLifecycle.CreateScope(
                this,
                "spawn-loop",
                this.GetCancellationTokenOnDestroy());
            _architecture = GameArchitectureProvider.RequireCurrent();
            _isSpawning = true;
            uint version = ++_spawnVersion;
            IArchitecture architecture = _architecture;
            _spawnScope.Tasks.Run(
                "spawn-loop",
                token => SpawnLoopAsync(version, architecture, token),
                failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }

        public void StopSpawning()
        {
            _isSpawning = false;
            _spawnVersion++;
            _spawnScope?.BeginStop();
            _spawnScope = null;
        }

        void OnDisable()
        {
            StopSpawning();
        }

        async UniTask SpawnLoopAsync(
            uint version,
            IArchitecture architecture,
            System.Threading.CancellationToken token)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                while (!token.IsCancellationRequested)
                {
                    CleanupAliveList();

                    if (_aliveMonsters.Count < _spawnDefinition.MaxAliveCount)
                    {
                        SpawnOne(architecture);
                    }

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_spawnDefinition.SpawnInterval),
                        cancellationToken: token);
                }
            }
            catch (System.OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
                {
                    host.RequestCurrentSessionStop("monster-spawner-failure");
                }

                throw;
            }
            finally
            {
                if (version == _spawnVersion)
                {
                    _isSpawning = false;
                    _spawnScope = null;
                }
            }
        }

        void SpawnOne(IArchitecture architecture)
        {
            GameplayRandomSystem randomSystem = architecture.GetSystem<GameplayRandomSystem>();
            int positionSeed = randomSystem.NextSeed(GameplayRandomChannel.SpawnPosition);
            int monsterSeed = randomSystem.NextSeed(GameplayRandomChannel.MonsterInstance);
            MonsterController monster = architecture.SendCommand(
                new SpawnMonsterCommand(_spawnDefinition, monsterSeed, GetSpawnPosition(new System.Random(positionSeed))));

            if (monster != null)
            {
                _aliveMonsters.Add(monster);
            }
        }

        Vector3 GetSpawnPosition(System.Random random)
        {
            Transform target = GetTarget();
            Vector3 center = target != null ? target.position : transform.position;

            if (_worldBounds == null)
            {
                return center + (Vector3)(GetRandomDirection(random) * _spawnDefinition.SpawnRadius);
            }

            Bounds bounds = _worldBounds.bounds;

            for (int i = 0; i < MaxSpawnPositionAttempts; i++)
            {
                Vector2 offset = GetRandomDirection(random) * _spawnDefinition.SpawnRadius;
                Vector3 candidate = center + new Vector3(offset.x, offset.y, 0f);

                if (IsInsideBounds(candidate, bounds, _spawnBoundsPadding))
                {
                    return candidate;
                }
            }

            Vector2 fallbackOffset = GetRandomDirection(random) * _spawnDefinition.SpawnRadius;
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

        static Vector2 GetRandomDirection(System.Random random)
        {
            double angle = random.NextDouble() * System.Math.PI * 2d;
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
