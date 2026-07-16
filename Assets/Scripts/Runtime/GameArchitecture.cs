namespace DarkFlare
{
    public class GameArchitecture : Architecture<GameArchitecture>
    {
        protected override void Init()
        {
            this.RegisterUtility(new GameInput());
            this.RegisterModel(new CombatModel());
            this.RegisterModel(new EquipmentModel());
            this.RegisterModel(new InventoryModel());
            this.RegisterModel(new EconomyModel());
            this.RegisterSystem(new CombatSystem());
            this.RegisterSystem(new SpawnSystem());
            this.RegisterSystem(new LootSystem());
            this.RegisterSystem(new TradingSystem());
            this.RegisterSystem(new CraftingSystem());
            this.RegisterUtility(new PrefabAssetLoader());
        }

        protected override void OnDeinit()
        {
            this.GetUtility<GameInput>().Dispose();
        }
    }
}
