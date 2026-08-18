using UnityEngine;
using UnityEngine.Rendering;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SortingGroup))]
    public sealed class WorldSortParticipant : MonoBehaviour, IController
    {
        [SerializeField]
        WorldSortCategory _category;

        [SerializeField]
        string _stableSortId;

        [SerializeField]
        Transform _sortAnchor;

        [SerializeField]
        SortingGroup _sortingGroup;

        [SerializeField]
        bool _requiresRuntimeIdentity;

        WorldSortKey _sortKey;
        WorldSortingSystem _sortingSystem;
        ApplicationHost _host;
        bool _registered;
        bool _runtimeIdentityConfigured;

        public WorldSortCategory Category => _category;

        public string StableSortId => _stableSortId;

        public Transform SortAnchor => _sortAnchor;

        public SortingGroup SortingGroup => _sortingGroup;

        public WorldSortKey SortKey => _sortKey;

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.RequireCurrent();
        }

        public void ConfigureIdentity(WorldSortCategory category, string stableSortId)
        {
            WorldSortingSystem sortingSystem = ResolveSortingSystem();

            if (_registered)
            {
                sortingSystem.Unregister(this);
                _registered = false;
            }

            _category = category;
            _stableSortId = stableSortId;
            _runtimeIdentityConfigured = true;
            _registered = isActiveAndEnabled && sortingSystem.Register(this);
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            EnsureComponents();

            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                _host = host;
                _host.SessionRunning += OnSessionRunning;
            }

            if (_requiresRuntimeIdentity && !_runtimeIdentityConfigured)
            {
                return;
            }

            TryRegister();
        }

        void OnDisable()
        {
            if (_host != null)
            {
                _host.SessionRunning -= OnSessionRunning;
                _host = null;
            }

            if (_registered)
            {
                _sortingSystem?.Unregister(this);
            }

            _registered = false;
            _sortingSystem = null;
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        internal bool RefreshSortKey()
        {
            Transform anchor = _sortAnchor != null ? _sortAnchor : transform;
            WorldSortKey nextKey = WorldSortingSystem.CreateKey(anchor.position.y, _category, _stableSortId);
            bool changed = nextKey.QuantizedY != _sortKey.QuantizedY
                || nextKey.Category != _sortKey.Category
                || !string.Equals(nextKey.StableSortId, _sortKey.StableSortId, System.StringComparison.Ordinal);
            _sortKey = nextKey;
            return changed;
        }

        internal void ApplySortingOrder(int sortingOrder)
        {
            if (_sortingGroup == null)
            {
                return;
            }

            if (_sortingGroup.sortingLayerName != "WorldObject")
            {
                _sortingGroup.sortingLayerName = "WorldObject";
            }

            if (_sortingGroup.sortingOrder != sortingOrder)
            {
                _sortingGroup.sortingOrder = sortingOrder;
            }
        }

        void OnSessionRunning(GameSessionHost session)
        {
            if (session.IsBoundToScene(gameObject.scene))
            {
                TryRegister();
            }
        }

        void TryRegister()
        {
            if (_registered)
            {
                return;
            }

            if (_host != null
                && (_host.CurrentSession == null
                    || _host.CurrentSession.SceneScope.State != LifecycleScopeState.Active
                    || (_host.CurrentSession.HasBoundScene
                        && !_host.CurrentSession.IsBoundToScene(gameObject.scene))))
            {
                return;
            }

            _registered = ResolveSortingSystem().Register(this);
        }

        void EnsureComponents()
        {
            if (_sortingGroup == null)
            {
                _sortingGroup = GetComponent<SortingGroup>();
            }

            if (_sortAnchor == null)
            {
                Transform anchor = transform.Find("SortAnchor");
                _sortAnchor = anchor != null ? anchor : transform;
            }
        }

        WorldSortingSystem ResolveSortingSystem()
        {
            _sortingSystem ??= this.GetUtility<WorldSortingSystem>();
            return _sortingSystem;
        }
    }
}
