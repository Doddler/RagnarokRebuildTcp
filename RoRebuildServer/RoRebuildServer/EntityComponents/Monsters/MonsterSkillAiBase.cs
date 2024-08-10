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
    EasyInterrupt = 2,
    EventOnStartCast = 4,
    RandomTarget = 8,
    HideSkillName = 16,
    UnlimitedRange = 32
    
}

public abstract class MonsterSkillAiBase
{
    public virtual void OnDie(MonsterSkillAiState skillState) {}
    public virtual void RunAiSkillUpdate(MonsterAiState aiState, MonsterSkillAiState skillState) {}
}
