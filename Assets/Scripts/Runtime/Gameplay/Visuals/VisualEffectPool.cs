using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct VisualEffectPoolStats
    {
        public int Total { get; }

        public int Active { get; }

        public int Available { get; }

        public VisualEffectPoolStats(int total, int active, int available)
        {
            Total = total;
            Active = active;
            Available = available;
        }
    }

    public sealed class VisualEffectPool : IUtility
    {
        public const int DefaultPrewarmCount = 4;
        public const int MaxInstancesPerPrefab = 24;

        readonly Dictionary<GameObject, PoolBucket> _buckets = new Dictionary<GameObject, PoolBucket>();

        Transform _root;

        public void Prewarm(GameObject prefab, int count = DefaultPrewarmCount)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            PoolBucket bucket = GetOrCreateBucket(prefab);
            bucket.Prewarm(Mathf.Min(count, MaxInstancesPerPrefab));
        }

        public bool TryPlay(GameObject prefab, Vector3 position, Color tint)
        {
            if (prefab == null)
            {
                return false;
            }

            return GetOrCreateBucket(prefab).TryPlay(position, tint);
        }

        public VisualEffectPoolStats GetStats(GameObject prefab)
        {
            if (prefab == null || !_buckets.TryGetValue(prefab, out PoolBucket bucket))
            {
                return default;
            }

            return bucket.GetStats();
        }

        public void ReleaseAll()
        {
            foreach (PoolBucket bucket in _buckets.Values)
            {
                bucket.ReleaseAll();
            }

            _buckets.Clear();

            if (_root != null)
            {
                DestroyObject(_root.gameObject);
                _root = null;
            }
        }

        PoolBucket GetOrCreateBucket(GameObject prefab)
        {
            EnsureRoot();

            if (!_buckets.TryGetValue(prefab, out PoolBucket bucket))
            {
                bucket = new PoolBucket(prefab, _root);
                _buckets.Add(prefab, bucket);
            }
            else
            {
                bucket.SetRoot(_root);
            }

            return bucket;
        }

        void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("[VisualEffectPool]");
            _root = rootObject.transform;
        }

        static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        sealed class PoolBucket
        {
            readonly GameObject _prefab;
            readonly Stack<PooledSpriteEffect> _available = new Stack<PooledSpriteEffect>();
            readonly HashSet<PooledSpriteEffect> _active = new HashSet<PooledSpriteEffect>();

            Transform _root;
            int _serial;

            public PoolBucket(GameObject prefab, Transform root)
            {
                _prefab = prefab;
                _root = root;
            }

            public void SetRoot(Transform root)
            {
                if (_root == root)
                {
                    return;
                }

                _root = root;
                RemoveDestroyedReferences();
            }

            public void Prewarm(int count)
            {
                RemoveDestroyedReferences();

                while (_available.Count + _active.Count < count)
                {
                    _available.Push(CreateInstance());
                }
            }

            public bool TryPlay(Vector3 position, Color tint)
            {
                RemoveDestroyedReferences();
                PooledSpriteEffect effect;

                if (_available.Count > 0)
                {
                    effect = _available.Pop();
                }
                else if (_active.Count < MaxInstancesPerPrefab)
                {
                    effect = CreateInstance();
                }
                else
                {
                    return false;
                }

                _active.Add(effect);
                effect.Play(position, tint, Return);
                return true;
            }

            public VisualEffectPoolStats GetStats()
            {
                RemoveDestroyedReferences();
                return new VisualEffectPoolStats(
                    _active.Count + _available.Count,
                    _active.Count,
                    _available.Count);
            }

            public void ReleaseAll()
            {
                foreach (PooledSpriteEffect effect in _active)
                {
                    DestroyObject(effect != null ? effect.gameObject : null);
                }

                foreach (PooledSpriteEffect effect in _available)
                {
                    DestroyObject(effect != null ? effect.gameObject : null);
                }

                _active.Clear();
                _available.Clear();
            }

            PooledSpriteEffect CreateInstance()
            {
                GameObject instance = Object.Instantiate(_prefab, _root);
                instance.name = $"{_prefab.name}_Pooled_{_serial++:00}";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                instance.SetActive(false);
                PooledSpriteEffect effect = instance.GetComponent<PooledSpriteEffect>();

                if (effect == null)
                {
                    DestroyObject(instance);
                    throw new MissingComponentException($"特效 Prefab 缺少 {nameof(PooledSpriteEffect)}：{_prefab.name}");
                }

                return effect;
            }

            void Return(PooledSpriteEffect effect)
            {
                if (effect == null || !_active.Remove(effect))
                {
                    return;
                }

                effect.gameObject.SetActive(false);

                if (_root != null)
                {
                    effect.transform.SetParent(_root, false);
                }

                _available.Push(effect);
            }

            void RemoveDestroyedReferences()
            {
                _active.RemoveWhere(effect => effect == null);

                if (_available.Count == 0)
                {
                    return;
                }

                PooledSpriteEffect[] effects = _available.ToArray();
                _available.Clear();

                for (int i = effects.Length - 1; i >= 0; i--)
                {
                    if (effects[i] != null)
                    {
                        _available.Push(effects[i]);
                    }
                }
            }
        }
    }
}
