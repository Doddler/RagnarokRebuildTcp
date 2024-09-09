namespace RoRebuildServer.Data.Player;

#nullable disable

public class EmoteInfo
{
    public string Name { get; set; }
    public int[] Emotes { get; set; }
    public int[] Weights { get; set; }
    public int TotalWeight { get; set; }
}