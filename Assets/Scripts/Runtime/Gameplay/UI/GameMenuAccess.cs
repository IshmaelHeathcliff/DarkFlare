using System;

namespace DarkFlare
{
    public enum GameMenuPage
    {
        Inventory,
        Shop,
        Crafting,
        Attributes
    }

    [Flags]
    public enum GameMenuAccess
    {
        None = 0,
        Inventory = 1 << 0,
        Shop = 1 << 1,
        Crafting = 1 << 2,
        Attributes = 1 << 3
    }

    public static class GameMenuAccessExtensions
    {
        public static GameMenuAccess ToAccess(this GameMenuPage page)
        {
            if (page == GameMenuPage.Shop)
            {
                return GameMenuAccess.Shop;
            }

            if (page == GameMenuPage.Crafting)
            {
                return GameMenuAccess.Crafting;
            }

            if (page == GameMenuPage.Attributes) { return GameMenuAccess.Attributes; }
            return page == GameMenuPage.Inventory ? GameMenuAccess.Inventory : GameMenuAccess.None;
        }

        public static bool Contains(this GameMenuAccess access, GameMenuPage page)
        {
            return (access & page.ToAccess()) != 0;
        }
    }
}
