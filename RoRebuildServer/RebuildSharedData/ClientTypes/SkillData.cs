using RebuildSharedData.Enum;

namespace RebuildSharedData.ClientTypes;

#nullable disable

public enum CastInterruptionMode : byte
{
    Default,
    InterruptOnDamage,
    InterruptOnSkill, //this is actually the default
    InterruptOnKnockback,
    NeverInterrupt,
    NoInterrupt = InterruptOnKnockback,
}

[Flags]
public enum SkillCastFlags : byte
{
    None,
    HideSkillName = 1,
    HideCastBar = 2,
    NoEffect = 4,
    EventOnHit = 8,
}


[Serializable]
public class SkillData
{
    public CharacterSkill SkillId;
    public string Icon;
    public string Name;
    public SkillTarget Target;
    public SkillClass Type = SkillClass.None;
    public int MaxLevel;
    public int[] SpCost;
    public bool AdjustableLevel;
    public string Description;
    public CastInterruptionMode InterruptMode;
}

