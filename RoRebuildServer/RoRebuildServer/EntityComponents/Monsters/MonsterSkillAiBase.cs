using RebuildSharedData.Enum;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Logging;

namespace RoRebuildServer.EntityComponents.Monsters;

public interface IMonsterLoader
{
    public void Load();
}

[Flags]
public enum MonsterSkillAiFlags
{
    None = 0,
    NoInterrupt = 1,
    EventOnStartCast = 2,
    RandomTarget = 4,
    HideSkillName = 8,
}

public abstract class MonsterSkillAiBase
{
    public virtual void OnDie(MonsterSkillAiState skillState) {}
    public virtual void RunAiSkillUpdate(MonsterAiState aiState, MonsterSkillAiState skillState) {}
}
