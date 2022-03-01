namespace RoRebuildServer.Data.CsvDataTypes;

//disable null string warning on all our csv stuff
#pragma warning disable CS8618

internal class CsvMonsterAI
{
    public string AiType { get; set; }
    public string State { get; set; }
    public string InputCheck { get; set; }
    public string OutputCheck { get; set; }
    public string EndState { get; set; }
}