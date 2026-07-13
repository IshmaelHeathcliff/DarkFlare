namespace DarkFlare
{
    public class InventoryModel : AbstractModel
    {
        // 背包尺寸暂硬编码，配置化留给背包 UI/角色成长那一步
        const int DefaultWidth = 10;
        const int DefaultHeight = 6;

        public InventoryGrid Grid { get; private set; }

        protected override void OnInit()
        {
            Grid = new InventoryGrid(DefaultWidth, DefaultHeight);
        }

        public bool TryAddItem(ItemInstance item)
        {
            return Grid.TryAdd(item);
        }

        public bool RemoveItem(ItemInstance item)
        {
            return Grid.Remove(item);
        }
    }
}
