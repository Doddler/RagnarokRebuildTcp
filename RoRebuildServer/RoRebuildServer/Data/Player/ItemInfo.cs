using RoRebuildServer.EntityComponents.Items;

namespace RoRebuildServer.Data.Player
{
    public class ItemInfo
    {
        public int Id;
        public string Code = null!;
        public int Weight;
        public int Price;
        public bool IsUseable;
        public int Effect;
        public ItemInteractionBase? Interaction = null!;
    }
}
