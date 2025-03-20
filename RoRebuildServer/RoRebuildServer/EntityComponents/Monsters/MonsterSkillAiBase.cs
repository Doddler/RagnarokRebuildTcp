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
    NeverInterrupt = 2,
    EasyInterrupt = 4,
    EventOnStartCast = 8,
    RandomTarget = 16,
    HideSkillName = 32,
    HideCastBar = 64,
    UnlimitedRange = 128,
    NoTarget = 256,
    IgnoreCooldown = 512
}

public abstract class MonsterSkillAiBase
{
    public virtual void OnInit(MonsterSkillAiState skillState) {}
    public virtual void OnDie(MonsterSkillAiState skillState) { }
    public virtual void RunAiSkillUpdate(MonsterAiState aiState, MonsterSkillAiState skillState) {}
    public bool IsUnassignedAiType = false;
}
