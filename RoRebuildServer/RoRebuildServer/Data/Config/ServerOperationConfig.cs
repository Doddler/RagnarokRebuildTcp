namespace RoRebuildServer.Data.Config;

public class ServerOperationConfig
{
    public bool UseMultipleThreads { get; set; }
    public int MapChunkSize { get; set; }
    public int ClientTimeoutSeconds { get; set; }
    public bool UseAccurateSpawnZoneFormula { get; set; }
    public bool AllowAdminifyCommand { get; set; }
    public string? AdminifyPasscode { get; set; }
}