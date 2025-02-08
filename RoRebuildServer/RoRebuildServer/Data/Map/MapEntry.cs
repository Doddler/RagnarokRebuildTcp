namespace RoRebuildServer.Data.Map;

public class MapEntry
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public required string WalkData { get; set; }
    public required string MapMode { get; set; }
    public required bool CanMemo { get; set; }
    public required string Music { get; set; }
}