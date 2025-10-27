namespace RoRebuildServer.Data.Player;

public class JobInfo
{
    public required int Id { get; set; }
    public required string Class { get; set; }
    public required int ExpChart { get; set; }
    public required int MaxJobLevel { get; set; }
    public required float[] WeaponTimings { get; set; }
}