using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public class LootPickupController : MonoBehaviour, IController
    {
        [SerializeField]
        LootPickupVisual _visual;

        CircleCollider2D _collider;
        ItemInstance _item;
        WorldDropId _id;
        WorldSortParticipant _sortParticipant;
        IArchitecture _architecture;
        SessionObjectRegistry _sessionObjects;

        public ItemInstance Item => _item;

        public WorldDropId Id => _id;

        public LootPickupVisual Visual => _visual;

        public IArchitecture GetArchitecture()
        {
            return _architecture ?? GameArchitectureProvider.RequireCurrent();
        }

        public void Init(ItemInstance item)
        {
            Init(WorldDropId.FromLegacyItem(item.Id), item);
        }

        public void Init(WorldDropId id, ItemInstance item)
        {
            EnsureComponents();
            _id = id;
            _item = item;
            _visual?.Bind(item);

            if (_sortParticipant != null && item != null)
            {
                _sortParticipant.ConfigureIdentity(WorldSortCategory.Loot, id.Value);
            }
        }

        void Awake()
        {
            EnsureComponents();
            _architecture = GameArchitectureProvider.RequireCurrent();
            _sessionObjects = this.GetUtility<SessionObjectRegistry>();
        }

        internal void ClearItem()
        {
            _item = null;
            if (_collider != null) { _collider.enabled = false; }
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void OnDestroy()
        {
            _sessionObjects?.Unregister(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_item == null)
            {
                return;
            }

            CombatActor actor = other.GetComponentInParent<CombatActor>();

            if (actor == null || actor.Team != ActorTeam.Player)
            {
                return;
            }

            bool collected = this.SendCommand(new PickupLootCommand(this, actor));

            if (!collected)
            {
                // 背包已满，拾取物留在地上等待清理背包后再来
                return;
            }

        }

        void EnsureComponents()
        {
            _collider = GetComponent<CircleCollider2D>();

            if (_visual == null)
            {
                _visual = GetComponent<LootPickupVisual>();
            }

            if (_sortParticipant == null)
            {
                _sortParticipant = GetComponent<WorldSortParticipant>();
            }

            if (_collider != null)
            {
                _collider.isTrigger = true;
            }
        }
    }
}
