namespace DarkFlare
{
    public class PickupLootCommand : AbstractCommand<bool>
    {
        readonly LootPickupController _pickup;
        readonly CombatActor _collector;

        public PickupLootCommand(LootPickupController pickup, CombatActor collector)
        {
            _pickup = pickup;
            _collector = collector;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<LootSystem>().CollectLoot(_pickup, _collector);
        }
    }
}
