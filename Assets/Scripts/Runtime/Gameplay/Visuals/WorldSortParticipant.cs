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
        bool _registered;
        bool _runtimeIdentityConfigured;

        public WorldSortCategory Category => _category;

        public string StableSortId => _stableSortId;

        public Transform SortAnchor => _sortAnchor;

        public SortingGroup SortingGroup => _sortingGroup;

        public WorldSortKey SortKey => _sortKey;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void ConfigureIdentity(WorldSortCategory category, string stableSortId)
        {
            WorldSortingSystem sortingSystem = this.GetUtility<WorldSortingSystem>();

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

            if (_requiresRuntimeIdentity && !_runtimeIdentityConfigured)
            {
                return;
            }

            _registered = this.GetUtility<WorldSortingSystem>().Register(this);
        }

        void OnDisable()
        {
            if (!_registered)
            {
                return;
            }

            this.GetUtility<WorldSortingSystem>().Unregister(this);
            _registered = false;
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
    }
}
