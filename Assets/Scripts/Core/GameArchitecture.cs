namespace DarkFlare
{
    public class GameArchitecture : Architecture<GameArchitecture>
    {
        protected override void Init()
        {
            this.RegisterModel(new CombatModel());
            this.RegisterSystem(new CombatSystem());
            this.RegisterSystem(new SpawnSystem());
            this.RegisterSystem(new LootSystem());
            this.RegisterUtility(new PrefabAssetLoader());
        }
    }
}
