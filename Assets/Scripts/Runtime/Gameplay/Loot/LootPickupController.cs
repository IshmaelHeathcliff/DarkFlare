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

        public ItemInstance Item => _item;

        public LootPickupVisual Visual => _visual;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void Init(ItemInstance item)
        {
            EnsureComponents();
            _item = item;
            _visual?.Bind(item);
        }

        void Awake()
        {
            EnsureComponents();
        }

        void OnValidate()
        {
            EnsureComponents();
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

            Destroy(gameObject);
        }

        void EnsureComponents()
        {
            _collider = GetComponent<CircleCollider2D>();

            if (_visual == null)
            {
                _visual = GetComponent<LootPickupVisual>();
            }

            if (_collider != null)
            {
                _collider.isTrigger = true;
            }
        }
    }
}
