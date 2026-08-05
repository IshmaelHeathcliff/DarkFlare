using System.Collections.Generic;

namespace DarkFlare
{
    public class EquipmentModel : AbstractModel
    {
        readonly Dictionary<CombatActor, EquipmentLoadout> _loadouts = new Dictionary<CombatActor, EquipmentLoadout>();

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            _loadouts.Clear();
        }

        public EquipmentLoadout GetOrCreateLoadout(CombatActor actor)
        {
            if (actor == null)
            {
                return null;
            }

            if (!_loadouts.TryGetValue(actor, out EquipmentLoadout loadout))
            {
                loadout = new EquipmentLoadout();
                _loadouts.Add(actor, loadout);
            }

            return loadout;
        }

        public EquipmentLoadout GetLoadout(CombatActor actor)
        {
            if (actor == null)
            {
                return null;
            }

            return _loadouts.TryGetValue(actor, out EquipmentLoadout loadout) ? loadout : null;
        }

        public ItemInstance GetItem(CombatActor actor, EquipmentSlot slot)
        {
            EquipmentLoadout loadout = GetLoadout(actor);
            return loadout != null ? loadout.Get(slot) : null;
        }

        public ItemInstance GetWeapon(CombatActor actor)
        {
            return GetItem(actor, EquipmentSlot.Weapon);
        }

        public bool Contains(CombatActor actor, ItemInstance item)
        {
            EquipmentLoadout loadout = GetLoadout(actor);
            return loadout != null && loadout.Contains(item);
        }

        public void RemoveActor(CombatActor actor)
        {
            if (actor != null)
            {
                _loadouts.Remove(actor);
            }
        }
    }
}
