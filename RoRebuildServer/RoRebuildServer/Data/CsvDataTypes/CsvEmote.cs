namespace RoRebuildServer.Data.CsvDataTypes;

#nullable disable

public class CsvEmote
{
    //id,frame,x,y,size,commands,alt,sequence,chance
    public int Id { get; set; }
    public int Frame { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Size { get; set; }
    public string Commands { get; set; }
    public string Alt { get; set; }
    public string Sequence { get; set; }
    public string Chance { get; set; }

}