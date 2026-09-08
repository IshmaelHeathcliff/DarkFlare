namespace DarkFlare
{
    public sealed class CanDiscardItemQuery : AbstractQuery<bool>
    {
        readonly ItemInstance _item;
        readonly CombatActor _player;

        public CanDiscardItemQuery(ItemInstance item, CombatActor player)
        {
            _item = item;
            _player = player;
        }

        protected override bool OnDo()
        {
            return this.GetSystem<LootSystem>().CanDiscardItem(_item, _player);
        }
    }
}
