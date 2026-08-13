using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    public sealed class PooledSpriteEffect : MonoBehaviour
    {
        [SerializeField]
        SpriteRenderer _renderer;

        [SerializeField]
        Animator _animator;

        [SerializeField]
        [Min(0.01f)]
        float _duration = 0.25f;

        Action<PooledSpriteEffect> _completed;
        uint _playVersion;

        public float Duration => _duration;

        public SpriteRenderer Renderer => _renderer;

        public Animator Animator => _animator;

        void Awake()
        {
            EnsureComponents();
        }

        void OnDisable()
        {
            _playVersion++;
            _completed = null;
        }

        void OnValidate()
        {
            EnsureComponents();
            _duration = Mathf.Max(0.01f, _duration);
        }

        internal void Play(Vector3 position, Color tint, Action<PooledSpriteEffect> completed)
        {
            EnsureComponents();
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = Vector3.one;
            _renderer.color = tint;
            _completed = completed;
            gameObject.SetActive(true);
            _animator.Rebind();
            _animator.Update(0f);
            uint version = ++_playVersion;
            ReturnAfterDelayAsync(version, this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid ReturnAfterDelayAsync(uint version, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_duration), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (this == null || version != _playVersion || !gameObject.activeSelf)
            {
                return;
            }

            Action<PooledSpriteEffect> completed = _completed;
            _completed = null;
            completed?.Invoke(this);
        }

        void EnsureComponents()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }
        }
    }
}
