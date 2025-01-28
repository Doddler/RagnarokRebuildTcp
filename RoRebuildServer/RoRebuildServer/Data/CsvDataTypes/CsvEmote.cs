namespace RoRebuildServer.Data.CsvDataTypes;

public class CsvEmote
{
    //id,sprite,frame,x,y,size,commands
    public required int Id { get; set; }
    public required int Sprite { get; set; }
    public required int Frame { get; set; }
    public required int X { get; set; }
    public required int Y { get; set; }
    public required float Size { get; set; }
    public required string? Commands { get; set; }
}