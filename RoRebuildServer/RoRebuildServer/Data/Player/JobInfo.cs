namespace RoRebuildServer.Data.Player;

public class JobInfo
{
    public int Id { get; set; }
    public string Class { get; set; }
    public float HP { get; set; }
    public float SP { get; set; }
    public float[] WeaponTimings { get; set; }
}