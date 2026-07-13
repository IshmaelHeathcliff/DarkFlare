using System.Collections.Generic;

namespace DarkFlare
{
    public class EquipmentModel : AbstractModel
    {
        readonly Dictionary<CombatActor, ItemInstance> _weapons = new Dictionary<CombatActor, ItemInstance>();

        protected override void OnInit()
        {
        }

        public void SetWeapon(CombatActor actor, ItemInstance weapon)
        {
            if (actor == null)
            {
                return;
            }

            if (weapon == null)
            {
                _weapons.Remove(actor);
                return;
            }

            _weapons[actor] = weapon;
        }

        public ItemInstance GetWeapon(CombatActor actor)
        {
            if (actor == null)
            {
                return null;
            }

            return _weapons.TryGetValue(actor, out ItemInstance weapon) ? weapon : null;
        }
    }
}
