namespace DarkFlare
{
    public class GameArchitecture : Architecture<GameArchitecture>
    {
        protected override void Init()
        {
            this.RegisterUtility(new SessionObjectRegistry());
            this.RegisterUtility<IItemInstanceIdGenerator>(new UuidItemInstanceIdGenerator());
            this.RegisterUtility<IRunInstanceIdGenerator>(RunInstanceIdGenerator.Create());
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
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host)
                && host.Resources != null)
            {
                int generation = GameArchitectureProvider.Generation;
                this.RegisterUtility(new PrefabAssetLoader(
                    host.Resources,
                    host.Resources.CreateOwner($"session-{generation}-prefabs")));
                this.RegisterUtility(new SpriteAssetLoader(
                    host.Resources,
                    host.Resources.CreateOwner($"session-{generation}-sprites")));
            }
            else
            {
                this.RegisterUtility(new PrefabAssetLoader());
                this.RegisterUtility(new SpriteAssetLoader());
            }
            this.RegisterUtility(new VisualEffectPool());
            this.RegisterUtility(new WorldSortingSystem());
        }

        protected override void OnDeinit()
        {
            this.GetUtility<SessionObjectRegistry>().ReleaseAllImmediate();
            this.GetUtility<WorldSortingSystem>().ReleaseAll();
            this.GetUtility<VisualEffectPool>().ReleaseAll();
            this.GetUtility<SpriteAssetLoader>().Dispose();
            this.GetUtility<PrefabAssetLoader>().Dispose();
            this.GetUtility<GameInput>()?.Dispose();
        }
    }
}
