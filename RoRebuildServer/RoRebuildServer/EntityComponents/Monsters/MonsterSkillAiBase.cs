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
    NoInterrupt = 1 << 0,
    NeverInterrupt = 1 << 1,
    EasyInterrupt = 1 << 2,
    EventOnStartCast = 1 << 3,
    RandomTarget = 1 << 4,
    HideSkillName = 1 << 5,
    HideCastBar = 1 << 6,
    UnlimitedRange = 1 << 7,
    NoTarget = 1 << 8,
    IgnoreCooldown = 1 << 9,
    HiddenCast = 1 << 10,
    NoEffect = 1 << 11,
    SelfTarget = 1 << 12,
    TargetRudeAttacker = 1 << 13,
    IgnoreLineOfSight = 1 << 14,
}

public abstract class MonsterSkillAiBase
{
    public virtual void OnInit(MonsterSkillAiState skillState) {}
    public virtual void OnDie(MonsterSkillAiState skillState) { }
    public virtual void RunAiSkillUpdate(MonsterAiState aiState, MonsterSkillAiState skillState) {}
    public bool IsUnassignedAiType = false;
}
