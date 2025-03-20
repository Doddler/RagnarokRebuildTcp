namespace RoRebuildServer.EntityComponents.Items;

public class VendingState
{
    public CharacterBag? CartInventory;
    public Dictionary<int, int> SellingItemCounts = new();
    public Dictionary<int, int> SellingItemValues = new();

}