using System;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public class WorldInteractionTarget : MonoBehaviour, IController
    {
        [SerializeField]
        string _displayName = "交互目标";

        [SerializeField]
        LocalizedContentReference _localizedName = new LocalizedContentReference("monsters", string.Empty);

        [SerializeField]
        GameMenuPage _menuPage = GameMenuPage.Shop;

        [SerializeField]
        [Min(0.25f)]
        float _interactionRadius = 1.5f;

        [SerializeField]
        CircleCollider2D _collider;
        public MapInteractionBinding MapBinding { get; private set; }
        public int MapGeneration { get; private set; }

        public void ConfigureMap(MapInteractionBinding binding, int generation)
        {
            MapBinding = binding; MapGeneration = generation;
            _menuPage = binding.Kind == MapInteractionKind.Portal ? GameMenuPage.Inventory
                : binding.Kind == MapInteractionKind.Crafting ? GameMenuPage.Crafting : GameMenuPage.Shop;
            _displayName = binding.Kind == MapInteractionKind.Portal ? "地图出口" : binding.Kind == MapInteractionKind.Crafting ? "打造台" : "商人";
            _localizedName = new LocalizedContentReference("ui", binding.Kind == MapInteractionKind.Portal ? "world.map_exit" : binding.Kind == MapInteractionKind.Crafting ? "world.crafting" : "world.trader");
            EnsureComponents();
        }

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? GetDefaultDisplayName() : _displayName;

        public LocalizedContentReference LocalizedName => _localizedName;

        public GameMenuPage MenuPage => _menuPage;

        public GameMenuAccess AvailablePages => MapBinding?.Kind == MapInteractionKind.Portal
            ? GameMenuAccess.None : GameMenuAccess.Inventory | _menuPage.ToAccess();

        public bool CanInteract => isActiveAndEnabled && (MapBinding?.Kind == MapInteractionKind.Portal || _menuPage != GameMenuPage.Inventory);

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.RequireCurrent();
        }

        public event Action<WorldInteractionTarget> Unavailable;

        void OnDisable()
        {
            Unavailable?.Invoke(this);
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_collider == null)
            {
                _collider = GetComponent<CircleCollider2D>();
            }

            if (_collider == null && gameObject.scene.IsValid())
            {
                _collider = gameObject.AddComponent<CircleCollider2D>();
            }

            if (_collider != null)
            {
                _collider.isTrigger = true;
                _collider.radius = Mathf.Max(0.25f, _interactionRadius);
            }
        }

        string GetDefaultDisplayName()
        {
            return _menuPage == GameMenuPage.Crafting ? "打造台" : "商人";
        }
    }
}
