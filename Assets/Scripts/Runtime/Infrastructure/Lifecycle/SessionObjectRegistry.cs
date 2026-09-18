using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    public sealed class SessionObjectRegistry : IUtility
    {
        readonly HashSet<GameObject> _objects = new HashSet<GameObject>();
        readonly Dictionary<GameObject, object> _owners = new Dictionary<GameObject, object>();

        public object MapOwner { get; set; }

        public void RegisterMapObject(GameObject instance)
        {
            Register(instance);
            if (instance != null && MapOwner != null) { _owners[instance] = MapOwner; }
        }

        public IReadOnlyList<GameObject> CaptureOwner(object owner)
        {
            var result = new List<GameObject>();
            foreach (var pair in _owners)
            {
                if (ReferenceEquals(pair.Value, owner) && pair.Key != null) { result.Add(pair.Key); }
            }
            return result;
        }

        public async UniTask ReleaseOwnerAsync(object owner)
        {
            foreach (GameObject instance in CaptureOwner(owner)) { Release(instance); }
            if (Application.isPlaying) { await UniTask.NextFrame(); }
        }

        public int Count => _objects.Count;

        public IReadOnlyList<GameObject> CaptureObjects()
        {
            List<GameObject> result = new List<GameObject>(_objects.Count);

            foreach (GameObject instance in _objects)
            {
                if (instance != null)
                {
                    result.Add(instance);
                }
            }

            return result.AsReadOnly();
        }

        public void Register(GameObject instance)
        {
            if (instance != null)
            {
                _objects.Add(instance);
            }
        }

        public void Unregister(GameObject instance)
        {
            if (instance != null)
            {
                _objects.Remove(instance);
                _owners.Remove(instance);
            }
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            _objects.Remove(instance);
            _owners.Remove(instance);
            DisableAndDestroy(instance);
        }

        public async UniTask ReleaseAllAsync()
        {
            if (_objects.Count == 0)
            {
                return;
            }

            GameObject[] instances = new GameObject[_objects.Count];
            _objects.CopyTo(instances);
            _objects.Clear();
            _owners.Clear();

            for (int i = instances.Length - 1; i >= 0; i--)
            {
                DisableAndDestroy(instances[i]);
            }

            if (Application.isPlaying)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            }
        }

        public void ReleaseAllImmediate()
        {
            GameObject[] instances = new GameObject[_objects.Count];
            _objects.CopyTo(instances);
            _objects.Clear();
            _owners.Clear();

            for (int i = instances.Length - 1; i >= 0; i--)
            {
                DisableAndDestroy(instances[i]);
            }
        }

        static void DisableAndDestroy(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.SetActive(false);

            if (Application.isPlaying)
            {
                Object.Destroy(instance);
            }
            else
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
