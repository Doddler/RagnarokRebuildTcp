namespace RoRebuildServer.EntityComponents.Items;

public class VendingState
{

    public CharacterBag? CartInventory;
    public Dictionary<int, ItemReference> SellingItems = new();
    public Dictionary<int, int> SellingItemValues = new();

}