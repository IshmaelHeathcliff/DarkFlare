namespace DarkFlare
{
    public sealed class GrantStartingWeaponCommand : AbstractCommand<bool>
    {
        readonly CombatActor _actor;
        readonly ItemBaseDefinition _weaponDefinition;

        public GrantStartingWeaponCommand(
            CombatActor actor,
            ItemBaseDefinition weaponDefinition)
        {
            _actor = actor;
            _weaponDefinition = weaponDefinition;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<EquipmentSystem>().GrantAndEquipStartingWeapon(
                _actor,
                _weaponDefinition);
        }
    }
}
