namespace RoRebuildServer.Data.CsvDataTypes;

//disable null string warning on all our csv stuff
#pragma warning disable CS8618


public class CsvEffects
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool ImportEffect { get; set; }
    public bool Billboard { get; set; }
    public string? StrFile { get; set; }
    public string? Sprite { get; set; }
    public string? SoundFile { get; set; }
    public float Offset { get; set; }
    public string? PrefabName { get; set; }
}