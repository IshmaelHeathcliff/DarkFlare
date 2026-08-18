using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    public sealed class SessionObjectRegistry : IUtility
    {
        readonly HashSet<GameObject> _objects = new HashSet<GameObject>();

        public int Count => _objects.Count;

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
            }
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            _objects.Remove(instance);
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
