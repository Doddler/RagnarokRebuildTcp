using System;
using System.Collections.Generic;
using System.Text;
using RebuildSharedData.Enum;

namespace RebuildSharedData.ClientTypes;

[Serializable]
public class ItemData
{
    public int Id;
    public string Code;
    public string Name;
    public int Weight;
    public int Price;
    public int Slots;
    public int ItemRank;
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