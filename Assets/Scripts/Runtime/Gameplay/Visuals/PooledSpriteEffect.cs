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
        LifecycleScope _playScope;
        uint _playVersion;
        WorldSortParticipant _worldSort;
        WorldEffectContext _context;
        Transform _poolRoot;

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
            _playScope?.BeginStop();
            _playScope = null;
            _worldSort?.ReleaseRegistration();
            Action<PooledSpriteEffect> completed = _completed; _completed = null;
            _context = default;
            if (completed != null)
            {
                uint disabledVersion = _playVersion;
                PlayerLoopHelper.AddContinuation(PlayerLoopTiming.LastPostLateUpdate, () =>
                { if (this != null && disabledVersion == _playVersion) { completed(this); } });
            }
        }

        void OnDestroy()
        {
            if (_worldSort != null)
            {
                _worldSort.ReleaseRegistration();
                if (Application.isPlaying) { Destroy(_worldSort.gameObject); } else { DestroyImmediate(_worldSort.gameObject); }
            }
        }

        internal void DetachForRelease()
        {
            _completed = null; _playVersion++; _worldSort?.ReleaseRegistration();
            if (_poolRoot != null) { transform.SetParent(_poolRoot, true); }
            gameObject.SetActive(false);
        }

        internal void ResetWorldRoot()
        {
            if (_worldSort != null) { _worldSort.gameObject.SetActive(false); }
        }

        void OnValidate()
        {
            EnsureComponents();
            _duration = Mathf.Max(0.01f, _duration);
        }

        internal bool Play(Vector3 position, Color tint, Action<PooledSpriteEffect> completed, WorldEffectContext context)
        {
            EnsureComponents();
            _poolRoot ??= transform.parent;
            _context = context;
            if (context.IsAttached)
            {
                if (context.Host == null || !context.Host.isActiveAndEnabled) { return false; }
                transform.SetParent(context.Host.transform, true);
            }
            else
            {
                if (context.Sorting == null || string.IsNullOrWhiteSpace(context.Identity)) { return false; }
                if (_worldSort == null)
                {
                    var root = new GameObject("Pooled world effect sort"); root.SetActive(false);
                    root.transform.SetParent(_poolRoot, false);
                    _worldSort = root.AddComponent<WorldSortParticipant>();
                }
                _worldSort.transform.position = context.GroundPosition;
                _worldSort.ConfigureExplicit(WorldSortCategory.Effect, context.Identity, _worldSort.transform);
                if (!context.Sorting.Register(_worldSort)) { return false; }
                _worldSort.gameObject.SetActive(true); transform.SetParent(_worldSort.transform, true);
                context.Sorting.Tick();
            }
            _renderer.sortingLayerName = "WorldObject"; _renderer.sortingOrder = 10;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = Vector3.one;
            _renderer.color = tint;
            _completed = completed;
            gameObject.SetActive(true);
            _animator.Rebind();
            _animator.Update(0f);
            uint version = ++_playVersion;
            _playScope?.BeginStop();
            _playScope = ComponentLifecycle.CreateScope(
                this,
                "effect-playback",
                this.GetCancellationTokenOnDestroy());
            _playScope.Tasks.Run(
                "return-after-delay",
                token => ReturnAfterDelayAsync(version, token),
                externalToken: context.Token);
            return true;
        }

        async UniTask ReturnAfterDelayAsync(uint version, CancellationToken token)
        {
            try { await UniTask.Delay(TimeSpan.FromSeconds(_duration), cancellationToken: token); }
            catch (OperationCanceledException) { }
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
