namespace RoRebuildServer.Data.CsvDataTypes;

public class CsvSkillData
{
    public int SkillId { get; set; }
    public string ShortName { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public int MaxLevel { get; set; }
    public bool CanAdjustLevel { get; set; }
    public string? OnHitEffect { get; set; }
}