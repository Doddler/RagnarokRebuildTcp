namespace RoRebuildServer.Data.CsvDataTypes;

//disable null string warning on all our csv stuff
#pragma warning disable CS8618

public class CsvServerConfig
{
    public string Key { get; set; }
    public string Value { get; set; }
}