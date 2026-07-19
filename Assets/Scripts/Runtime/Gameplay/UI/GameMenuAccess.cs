using System;

namespace DarkFlare
{
    public enum GameMenuPage
    {
        Inventory,
        Shop,
        Crafting
    }

    [Flags]
    public enum GameMenuAccess
    {
        None = 0,
        Inventory = 1 << 0,
        Shop = 1 << 1,
        Crafting = 1 << 2
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

            return GameMenuAccess.Inventory;
        }

        public static bool Contains(this GameMenuAccess access, GameMenuPage page)
        {
            return (access & page.ToAccess()) != 0;
        }
    }
}
