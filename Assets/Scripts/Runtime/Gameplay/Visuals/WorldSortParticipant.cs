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
        [SerializeField] bool _explicitRegistration;
        WorldSortRegistration _registration;
        bool _runtimeIdentityConfigured;
        int _appliedOrder = int.MinValue;
        int _appliedLayer = int.MinValue;

        public WorldSortCategory Category => _category;

        public string StableSortId => _stableSortId;

        public Transform SortAnchor => _sortAnchor;

        public SortingGroup SortingGroup => _sortingGroup;

        public WorldSortKey SortKey => _sortKey;
        public bool IsRegistered => _registration != null && _registration.IsActive;
        public bool ExplicitRegistration => _explicitRegistration;
        public WorldSortRegistration Registration => _registration;
        public bool HasValidAnchor
        {
            get
            {
                if (_sortingGroup == null || _sortAnchor == null) { return false; }
                Vector3 position = _sortAnchor.position;
                return float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z)
                    && System.Math.Abs((double)position.y * WorldSortingSystem.QuantizationUnitsPerWorldUnit) < int.MaxValue - 1d;
            }
        }

        public void ConfigureExplicit(WorldSortCategory category, string stableSortId, Transform anchor)
        {
            ReleaseRegistration(); _explicitRegistration = true;
            _category = category; _stableSortId = stableSortId; _sortAnchor = anchor;
            _runtimeIdentityConfigured = true; EnsureComponents();
        }

        internal void AttachRegistration(WorldSortRegistration registration) { _registration = registration; }
        public void ReleaseRegistration() { _registration?.Dispose(); _registration = null; }

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.RequireCurrent();
        }

        public void ConfigureIdentity(WorldSortCategory category, string stableSortId)
        {
            ReleaseRegistration();
            _category = category;
            _stableSortId = stableSortId;
            _runtimeIdentityConfigured = true;
            if (isActiveAndEnabled) { TryRegister(); }
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            EnsureComponents();
            if (_explicitRegistration) { return; }

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

            ReleaseRegistration();
            _sortingSystem = null;
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        internal bool RefreshSortKey()
        {
            if (!TryRefreshSortKey(out bool changed)) { throw new System.InvalidOperationException("排序锚点无效"); }
            return changed;
        }

        internal bool TryRefreshSortKey(out bool changed)
        {
            changed = false;
            Transform anchor = _sortAnchor != null ? _sortAnchor : transform;
            Vector3 position = anchor.position;
            if (_sortingGroup == null || !float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)
                || System.Math.Abs((double)position.y * WorldSortingSystem.QuantizationUnitsPerWorldUnit) >= int.MaxValue - 1d) { return false; }
            var nextKey = new WorldSortKey(Mathf.RoundToInt(position.y * WorldSortingSystem.QuantizationUnitsPerWorldUnit), _category, _stableSortId);
            changed = nextKey.QuantizedY != _sortKey.QuantizedY
                || nextKey.Category != _sortKey.Category
                || !string.Equals(nextKey.StableSortId, _sortKey.StableSortId, System.StringComparison.Ordinal);
            _sortKey = nextKey;
            return true;
        }

        internal bool ApplySortingOrder(int sortingOrder, int layer)
        {
            if (_sortingGroup == null)
            {
                return false;
            }

            bool changed = false;
            if (_appliedLayer != layer)
            {
                _sortingGroup.sortingLayerID = layer; _appliedLayer = layer; changed = true;
            }

            if (_appliedOrder != sortingOrder)
            {
                _sortingGroup.sortingOrder = sortingOrder; _appliedOrder = sortingOrder; changed = true;
            }
            return changed;
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
            if (IsRegistered || _explicitRegistration || (_requiresRuntimeIdentity && !_runtimeIdentityConfigured))
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

            ResolveSortingSystem().Register(this);
        }

        void EnsureComponents()
        {
            if (_sortingGroup == null)
            {
                _sortingGroup = GetComponent<SortingGroup>();
                _appliedLayer = int.MinValue; _appliedOrder = int.MinValue;
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
