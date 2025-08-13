namespace RoRebuildServer.EntityComponents.Monsters;

public class MonsterAiSkillHandlerAttribute(string monsterName) : Attribute
{
    public string MonsterName = monsterName;
}