namespace RoRebuildServer.Data.CsvDataTypes;

public class CsvWeaponClass
{
    public required int Id { get; set; }
    public required string WeaponClass { get; set; }
    public required string FullName { get; set;}
    public required string HitSound { get; set; }
}