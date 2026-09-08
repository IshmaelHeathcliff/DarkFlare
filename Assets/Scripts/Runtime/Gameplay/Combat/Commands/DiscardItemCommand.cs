namespace DarkFlare
{
    public sealed class DiscardItemCommand : AbstractCommand<bool>
    {
        readonly ItemInstance _item;
        readonly CombatActor _player;

        public DiscardItemCommand(ItemInstance item, CombatActor player)
        {
            _item = item;
            _player = player;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<LootSystem>().DiscardItem(_item, _player);
        }
    }
}
