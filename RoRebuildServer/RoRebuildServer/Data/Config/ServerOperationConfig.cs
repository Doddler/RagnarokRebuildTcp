namespace RoRebuildServer.Data.Config;

public class ServerOperationConfig
{
    public string? KeyPersistencePath { get; set; }
    public bool UseMultipleThreads { get; set; }
    public int MapChunkSize { get; set; }
    public int ClientTimeoutSeconds { get; set; }
    public bool UseAccurateSpawnZoneFormula { get; set; }
    public bool AllowAdminifyCommand { get; set; }
    public string? AdminifyPasscode { get; set; }
    public bool RemapDropRates { get; set; }
    public bool GuaranteeMvpDrops { get; set; } = true;
    public bool FliersIgnoreTraps { get; set; } = true;
    public float EtcItemValueMultiplier { get; set; }
    public List<string> ActiveEvents { get; set; } = new();
}