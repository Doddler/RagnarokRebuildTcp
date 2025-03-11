namespace RoRebuildServer.EntityComponents.Monsters;

public class MonsterDropData
{
    public record MonsterDropEntry(int Id, int Chance, int CountMin = 1, int CountMax = 1);

    public List<MonsterDropEntry> DropChances = new();
}