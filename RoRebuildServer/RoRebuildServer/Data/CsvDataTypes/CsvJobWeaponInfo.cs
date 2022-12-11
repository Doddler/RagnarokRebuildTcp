using CsvHelper;

namespace RoRebuildServer.Data.CsvDataTypes;

public class CsvJobWeaponInfo
{ 
    public string Job { get; set; }
    public string Class { get; set; }
    public int ItemId { get; set; }
    public int AttackAnimation { get; set; }
    public string SpriteMale { get; set; }
    public string SpriteFemale { get; set; }
}