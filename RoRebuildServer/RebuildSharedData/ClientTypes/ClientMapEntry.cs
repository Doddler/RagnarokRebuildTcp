namespace RebuildSharedData.ClientTypes;

[Serializable]
public class ClientMapEntry
{
    public string Code = null!;
    public string Name = null!;
    public int MapMode;
    public bool CanMemo;
    public string Music = null!;
}