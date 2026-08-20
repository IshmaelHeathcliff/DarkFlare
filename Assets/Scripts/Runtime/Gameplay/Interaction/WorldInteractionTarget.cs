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

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? GetDefaultDisplayName() : _displayName;

        public LocalizedContentReference LocalizedName => _localizedName;

        public GameMenuPage MenuPage => _menuPage;

        public GameMenuAccess AvailablePages => GameMenuAccess.Inventory | _menuPage.ToAccess();

        public bool CanInteract => isActiveAndEnabled && _menuPage != GameMenuPage.Inventory;

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.RequireCurrent();
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
