namespace RoRebuildServer.Data.CsvDataTypes;

//disable null string warning on all our csv stuff
#pragma warning disable CS8618

public class CsvNpc
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string ClientSprite { get; set; }
    public float ClientOffset { get; set; }
    public float ClientShadow { get; set; }
    public float ClientSize { get; set; }
}