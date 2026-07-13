using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public class LootPickupController : MonoBehaviour, IController
    {
        SpriteRenderer _renderer;
        CircleCollider2D _collider;
        ItemInstance _item;

        public ItemInstance Item => _item;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void Init(ItemInstance item)
        {
            EnsureComponents();
            _item = item;

            if (_renderer != null)
            {
                _renderer.color = GetRarityColor(item != null ? item.Rarity : ItemRarity.Normal);
            }
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
                return;
            }

            if (_item.BaseDefinition != null && _item.BaseDefinition.ItemType == ItemType.Weapon)
            {
                this.SendCommand(new EquipItemCommand(actor, _item));
            }

            Destroy(gameObject);
        }

        void EnsureComponents()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<CircleCollider2D>();

            if (_collider != null)
            {
                _collider.isTrigger = true;
            }
        }

        static Color GetRarityColor(ItemRarity rarity)
        {
            if (rarity == ItemRarity.Magic)
            {
                return new Color(1f, 0.92f, 0.2f, 1f);
            }

            if (rarity == ItemRarity.Rare || rarity == ItemRarity.Unique)
            {
                return new Color(1f, 0.55f, 0.1f, 1f);
            }

            return Color.white;
        }
    }
}
