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
    public bool IsUnique;
    public bool IsUseable;
    public ItemClass ItemClass;
    public string Effect;
    public string Sprite;
}

[Serializable]
public class ItemDataList
{
    public List<ItemData> Items = null!;
}