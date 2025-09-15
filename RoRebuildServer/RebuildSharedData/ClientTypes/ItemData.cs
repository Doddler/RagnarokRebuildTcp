using RebuildSharedData.Enum;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace RebuildSharedData.ClientTypes;

[Serializable]
public class ItemData
{
    public int Id;
    public string Code;
    public string Name;
    public int Weight;
    public int Price;
    public int SellPrice;
    public int Slots;
    public int ItemRank;
    public int SubType;
    public bool IsUnique;
    public bool IsRefinable;
    public ItemClass ItemClass;
    public ItemUseType UseType;
    public EquipPosition Position;
    public string Sprite;
}

[Serializable]
public class ItemDataList
{
    public List<ItemData> Items = null!;
}