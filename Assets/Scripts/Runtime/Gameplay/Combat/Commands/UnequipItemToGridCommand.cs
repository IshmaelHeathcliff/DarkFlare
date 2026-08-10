using UnityEngine;

namespace DarkFlare
{
    public class UnequipItemToGridCommand : AbstractCommand<bool>
    {
        readonly CombatActor _actor;
        readonly EquipmentSlot _slot;
        readonly Vector2Int _targetOrigin;

        public UnequipItemToGridCommand(CombatActor actor, EquipmentSlot slot, Vector2Int targetOrigin)
        {
            _actor = actor;
            _slot = slot;
            _targetOrigin = targetOrigin;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<EquipmentSystem>().UnequipAt(_actor, _slot, _targetOrigin);
        }
    }
}
