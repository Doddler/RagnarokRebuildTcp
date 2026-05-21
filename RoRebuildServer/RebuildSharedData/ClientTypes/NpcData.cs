namespace RebuildSharedData.ClientTypes;

#pragma warning disable CS8618 //disable warning for nullable fields

[Serializable]
public class NpcEntry
{
    public int Id;
    public string Map;
    public string Name;
    public string SpriteCode;
    public int X;
    public int Y;
    public string Facing;
    public bool IsTrader;
    public List<int> SellsItems;
}

[Serializable]
public class NpcDbFile
{
    public List<NpcEntry> Items;
}
