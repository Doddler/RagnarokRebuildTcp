using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace RoRebuildServer.EntityComponents.Util;

public struct AttackRequest
{
    public int MinAtk;
    public int MaxAtk;
    public float AttackMultiplier;
    public int HitCount;
    public int AccuracyRatio;
    public AttackFlags Flags;
    public CharacterSkill SkillSource;
    public AttackElement Element;

    public AttackRequest()
    {
        AttackMultiplier = 1;
        HitCount = 1;
        AccuracyRatio = 100;
        Flags = AttackFlags.Neutral;
        SkillSource = CharacterSkill.None;
        Element = AttackElement.None;
    }

    public AttackRequest(CharacterSkill skill, float attackMultiplier, int hitCount, AttackFlags flags, AttackElement element)
    {
        SkillSource = skill;
        AttackMultiplier = attackMultiplier;
        HitCount = hitCount;
        Flags = flags;
        Element = element;
        AccuracyRatio = 100;
    }

    public AttackRequest(float attackMultiplier, int hitCount, AttackFlags flags, AttackElement element)
    {
        SkillSource = CharacterSkill.None;
        AttackMultiplier = attackMultiplier;
        HitCount = hitCount;
        Flags = flags;
        Element = element;
        AccuracyRatio = 100;
    }
}