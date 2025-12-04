using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.EntityComponents.Items;

public class VendingState
{
    public Entity VendProxy;
    public CharacterBag? CartInventory;
    public Dictionary<int, ItemReference> SellingItems = new();
    public Dictionary<int, int> SellingItemValues = new();
}