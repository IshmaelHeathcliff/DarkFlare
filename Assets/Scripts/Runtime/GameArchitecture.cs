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
            this.RegisterSystem(new GameplayRandomSystem());
            this.RegisterSystem(new EquipmentSystem());
            this.RegisterSystem(new CombatSystem());
            this.RegisterSystem(new ResourceRegenerationSystem());
            this.RegisterSystem(new SpawnSystem());
            this.RegisterSystem(new LootSystem());
            this.RegisterSystem(new TradingSystem());
            this.RegisterSystem(new CraftingSystem());
            this.RegisterSystem(new GameplayPauseSystem());
            this.RegisterUtility(new PrefabAssetLoader());
            this.RegisterUtility(new SpriteAssetLoader());
        }

        protected override void OnDeinit()
        {
            this.GetUtility<SpriteAssetLoader>().ReleaseAll();
            this.GetUtility<PrefabAssetLoader>().ReleaseAll();
            this.GetUtility<GameInput>().Dispose();
        }
    }
}
