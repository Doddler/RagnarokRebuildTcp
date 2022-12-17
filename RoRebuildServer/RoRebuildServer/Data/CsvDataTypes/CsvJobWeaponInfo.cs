using CsvHelper;

namespace RoRebuildServer.Data.CsvDataTypes;

public class CsvJobWeaponInfo
{ 
    public required string Job { get; set; }
    public required string Class { get; set; }
    public required int ItemId { get; set; }
    public required int AttackMale { get; set; }
    public required int AttackFemale { get; set; }
    public required string SpriteMale { get; set; }
    public required string EffectMale { get; set; }
    public required string SpriteFemale { get; set; }
    public required string EffectFemale { get; set; }
}