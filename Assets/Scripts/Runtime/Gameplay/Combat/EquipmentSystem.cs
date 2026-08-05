using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public class EquipmentSystem : AbstractSystem
    {
        IUnRegister _actorUnregistered;

        protected override void OnInit()
        {
            _actorUnregistered = this.RegisterEvent<ActorUnregisteredEvent>(OnActorUnregistered);
        }

        protected override void OnDeinit()
        {
            _actorUnregistered?.UnRegister();
            _actorUnregistered = null;
        }

        public bool Equip(CombatActor actor, ItemInstance item, EquipmentSlot slot)
        {
            if (actor == null
                || item == null
                || item.BaseDefinition == null
                || !item.BaseDefinition.CanEquipTo(slot))
            {
                return false;
            }

            EquipmentModel equipment = this.GetModel<EquipmentModel>();
            EquipmentLoadout existingLoadout = equipment.GetLoadout(actor);

            if (existingLoadout != null && existingLoadout.Contains(item))
            {
                return false;
            }

            ItemInstance previousItem = existingLoadout != null ? existingLoadout.Get(slot) : null;
            InventoryModel inventory = this.GetModel<InventoryModel>();

            if (!inventory.CanExchangeItem(item, previousItem))
            {
                return false;
            }

            EquipmentLoadout loadout = equipment.GetOrCreateLoadout(actor);

            if (!inventory.TryExchangeItemWithoutEvents(item, previousItem))
            {
                return false;
            }

            if (!loadout.TrySet(slot, item))
            {
                RollbackEquipInventory(inventory, item, previousItem);
                return false;
            }

            RebuildActorEffects(actor, loadout);
            inventory.NotifyItemChanged(item, InventoryChangeType.Removed);

            if (previousItem != null)
            {
                inventory.NotifyItemChanged(previousItem, InventoryChangeType.Added);
            }

            this.SendEvent(new EquipmentChangedEvent(actor, slot, previousItem, item));
            Debug.Log($"[EquipmentSystem] {actor.ActorId} 的{EquipmentSlots.GetDisplayName(slot)}已装备 {item.BaseDefinition.DisplayName}");
            return true;
        }

        public bool Unequip(CombatActor actor, EquipmentSlot slot)
        {
            if (actor == null)
            {
                return false;
            }

            EquipmentModel equipment = this.GetModel<EquipmentModel>();
            EquipmentLoadout loadout = equipment.GetLoadout(actor);
            ItemInstance previousItem = loadout != null ? loadout.Get(slot) : null;

            if (previousItem == null)
            {
                return false;
            }

            InventoryModel inventory = this.GetModel<InventoryModel>();

            if (!inventory.CanAddItem(previousItem) || !inventory.TryAddItemWithoutEvents(previousItem))
            {
                return false;
            }

            if (!loadout.TrySet(slot, null))
            {
                inventory.RemoveItemWithoutEvents(previousItem);
                return false;
            }

            RebuildActorEffects(actor, loadout);
            inventory.NotifyItemChanged(previousItem, InventoryChangeType.Added);
            this.SendEvent(new EquipmentChangedEvent(actor, slot, previousItem, null));
            Debug.Log($"[EquipmentSystem] {actor.ActorId} 卸下了{EquipmentSlots.GetDisplayName(slot)} {previousItem.BaseDefinition.DisplayName}");
            return true;
        }

        static void RebuildActorEffects(CombatActor actor, EquipmentLoadout loadout)
        {
            List<ModifierInstance> modifiers = EquipmentEffectResolver.CollectActorModifiers(loadout);
            actor.SetModifiers(modifiers);
        }

        static void RollbackEquipInventory(
            InventoryModel inventory,
            ItemInstance equippedItem,
            ItemInstance previousItem)
        {
            if (previousItem == null)
            {
                inventory.TryAddItemWithoutEvents(equippedItem);
                return;
            }

            inventory.TryExchangeItemWithoutEvents(previousItem, equippedItem);
        }

        void OnActorUnregistered(ActorUnregisteredEvent e)
        {
            this.GetModel<EquipmentModel>().RemoveActor(e.Actor);

            if (e.Actor != null)
            {
                e.Actor.SetModifiers(null);
            }
        }
    }
}
